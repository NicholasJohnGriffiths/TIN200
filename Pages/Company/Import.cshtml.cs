using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using TINWeb.Models;

namespace TINWeb.Pages.Company
{
    [Authorize(Policy = "AdminOnly")]
    [IgnoreAntiforgeryToken]
    public class ImportModel : PageModel
    {
        private readonly IConfiguration _config;

        public ImportModel(IConfiguration config)
        {
            _config = config;
        }

        [BindProperty]
        public string? ResultMessage { get; set; }

        public List<string>? Errors { get; set; }

        public IActionResult OnGet()
        {
            // Keep the old route for backward compatibility but route users to the menu-driven flow.
            return RedirectToPage("/Company/Index", new { showImportTool = "import" });
        }

        public async Task<JsonResult> OnPostPreviewAsync(IFormFile? UploadFile, int? SurveyYear)
        {
            if (UploadFile == null || UploadFile.Length == 0)
            {
                return new JsonResult(new { success = false, message = "No file uploaded." });
            }

            if (!SurveyYear.HasValue || SurveyYear.Value <= 0)
            {
                return new JsonResult(new { success = false, message = "Survey Year is required." });
            }

            var connString = _config.GetConnectionString("DefaultConnection");
            await using (var conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();
                await using var surveyCmd = conn.CreateCommand();
                surveyCmd.CommandText = "SELECT TOP (1) [Id] FROM [Survey] WHERE [FinancialYear] = @financialYear ORDER BY [CurrentSurvey] DESC, [Id] DESC";
                surveyCmd.Parameters.AddWithValue("@financialYear", SurveyYear.Value);
                var surveyResult = await surveyCmd.ExecuteScalarAsync();
                if (surveyResult == null || surveyResult == DBNull.Value)
                {
                    return new JsonResult(new { success = false, message = $"No Survey exists for year {SurveyYear.Value}. Please create it first." });
                }
            }

            var previewRows = new List<object>();
            var errors = new List<string>();
            using (var stream = UploadFile.OpenReadStream())
            using (var reader = new StreamReader(stream))
            {
                string? line = await reader.ReadLineAsync();
                bool hasHeader = false;
                if (line != null && line.Contains('\t') && line.ToLower().Contains("ceo first")) hasHeader = true;
                var headers = hasHeader ? line!.Split('\t').Select(h => h.Trim()).ToArray() : Array.Empty<string>();
                if (!hasHeader)
                {
                    reader.BaseStream.Position = 0;
                    reader.DiscardBufferedData();
                }

                var row = 0;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    row++;
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var cols = line.Split('\t');
                    if (cols.Length < 9)
                    {
                        errors.Add($"Row {row}: not enough columns ({cols.Length}).");
                        continue;
                    }

                    var ceoFirst = cols[0]?.Trim();
                    var ceoLast = cols.Length > 1 ? cols[1]?.Trim() : null;
                    var email = cols.Length > 2 ? cols[2]?.Trim() : null;
                    var externalId = cols.Length > 3 ? cols[3]?.Trim() : null;
                    var companyName = cols.Length > 4 ? cols[4]?.Trim() : null;
                    var companyDesc = cols.Length > 5 ? cols[5]?.Trim() : null;
                    var externalIdImportColumnName = GetHeaderName(headers, 3);
                    var companyNameImportColumnName = GetHeaderName(headers, 4);
                    var companyDescriptionImportColumnName = GetHeaderName(headers, 5);
                    var fye2025Raw = cols.Length > 6 ? cols[6]?.Trim() : null;
                    var fye2024Raw = cols.Length > 7 ? cols[7]?.Trim() : null;
                    var fye2023Raw = cols.Length > 8 ? cols[8]?.Trim() : null;
                    var tin200Raw = cols.Length > 9 ? cols[9]?.Trim() : null;

                    var fye2025 = ParseDecimalNullable(fye2025Raw);
                    var fye2024 = ParseDecimalNullable(fye2024Raw);
                    var fye2023 = ParseDecimalNullable(fye2023Raw);
                    TinStatusHelper.TryParseLegacyTin200(tin200Raw, out var tinStatus);

                    // Validate email
                    var emailValid = true;
                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        var attr = new System.ComponentModel.DataAnnotations.EmailAddressAttribute();
                        emailValid = attr.IsValid(email);
                        if (!emailValid) errors.Add($"Row {row}: invalid email '{email}'.");
                    }

                    previewRows.Add(new
                    {
                        Row = row,
                        CeoFirst = ceoFirst,
                        CeoLast = ceoLast,
                        Email = email,
                        ExternalId = externalId,
                        MissingExternalId = string.IsNullOrWhiteSpace(externalId),
                        CompanyName = companyName,
                        CompanyDescription = companyDesc,
                        ExternalIdImportColumnName = externalIdImportColumnName,
                        CompanyNameImportColumnName = companyNameImportColumnName,
                        CompanyDescriptionImportColumnName = companyDescriptionImportColumnName,
                        Fye2025 = fye2025,
                        Fye2024 = fye2024,
                        Fye2023 = fye2023,
                        Tin200 = tin200Raw,
                        TinStatus = tinStatus
                    });
                }
            }

            return new JsonResult(new { success = true, preview = previewRows, total = previewRows.Count, errors });
        }

        public async Task<JsonResult> OnPostConfirmAsync(IFormFile? UploadFile, int? SurveyYear)
        {
            if (UploadFile == null || UploadFile.Length == 0)
            {
                return new JsonResult(new { success = false, message = "No file uploaded." });
            }

            if (!SurveyYear.HasValue || SurveyYear.Value <= 0)
            {
                return new JsonResult(new { success = false, message = "Survey Year is required." });
            }

            var connString = _config.GetConnectionString("DefaultConnection");
            var inserted = 0;
            var updated = 0;
            var companySurveyCreated = 0;
            var errors = new List<string>();
            var touchedCompanyIds = new HashSet<int>();

            using (var stream = UploadFile.OpenReadStream())
            using (var reader = new StreamReader(stream))
            using (var conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();

                int surveyId;
                using (var surveyLookup = conn.CreateCommand())
                {
                    surveyLookup.CommandText = "SELECT TOP (1) [Id] FROM [Survey] WHERE [FinancialYear] = @financialYear ORDER BY [CurrentSurvey] DESC, [Id] DESC";
                    surveyLookup.Parameters.AddWithValue("@financialYear", SurveyYear.Value);
                    var surveyResult = await surveyLookup.ExecuteScalarAsync();
                    if (surveyResult == null || surveyResult == DBNull.Value)
                    {
                        return new JsonResult(new { success = false, message = $"No Survey exists for year {SurveyYear.Value}. Please create it first." });
                    }

                    surveyId = Convert.ToInt32(surveyResult);
                }

                string? line = await reader.ReadLineAsync();
                bool hasHeader = false;
                if (line != null && line.Contains('\t') && line.ToLower().Contains("ceo first")) hasHeader = true;
                var headers = hasHeader ? line!.Split('\t').Select(h => h.Trim()).ToArray() : Array.Empty<string>();
                if (!hasHeader)
                {
                    reader.BaseStream.Position = 0;
                    reader.DiscardBufferedData();
                }

                var row = 0;
                using var tran = conn.BeginTransaction();
                try
                {
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        row++;
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var cols = line.Split('\t');
                        if (cols.Length < 9)
                        {
                            errors.Add($"Row {row}: not enough columns ({cols.Length}).");
                            continue;
                        }

                        var ceoFirst = cols[0]?.Trim();
                        var ceoLast = cols.Length > 1 ? cols[1]?.Trim() : null;
                        var email = cols.Length > 2 ? cols[2]?.Trim() : null;
                        var externalId = cols.Length > 3 ? cols[3]?.Trim() : null;
                        var companyName = cols.Length > 4 ? cols[4]?.Trim() : null;
                        var companyDesc = cols.Length > 5 ? cols[5]?.Trim() : null;
                        var externalIdImportColumnName = GetHeaderName(headers, 3);
                        var companyNameImportColumnName = GetHeaderName(headers, 4);
                        var companyDescriptionImportColumnName = GetHeaderName(headers, 5);
                        var fye2025Raw = cols.Length > 6 ? cols[6]?.Trim() : null;
                        var fye2024Raw = cols.Length > 7 ? cols[7]?.Trim() : null;
                        var fye2023Raw = cols.Length > 8 ? cols[8]?.Trim() : null;
                        var tin200Raw = cols.Length > 9 ? cols[9]?.Trim() : null;

                        var fye2025 = ParseDecimalNullable(fye2025Raw);
                        var fye2024 = ParseDecimalNullable(fye2024Raw);
                        var fye2023 = ParseDecimalNullable(fye2023Raw);
                        TinStatusHelper.TryParseLegacyTin200(tin200Raw, out var tinStatus);

                        try
                        {
                            int? existingId = null;

                            if (!string.IsNullOrWhiteSpace(externalId))
                            {
                                using var lookup = conn.CreateCommand();
                                lookup.Transaction = tran;
                                lookup.CommandText = "SELECT TOP (1) [Id] FROM [TIN200] WHERE [ExternalID] = @externalId";
                                lookup.Parameters.AddWithValue("@externalId", (object?)externalId ?? DBNull.Value);
                                var r = await lookup.ExecuteScalarAsync();
                                if (r != null && r != DBNull.Value) existingId = Convert.ToInt32(r);
                            }

                            if (existingId == null && !string.IsNullOrWhiteSpace(companyName))
                            {
                                using var lookup2 = conn.CreateCommand();
                                lookup2.Transaction = tran;
                                lookup2.CommandText = "SELECT TOP (1) [Id] FROM [TIN200] WHERE [CompanyName] = @companyName";
                                lookup2.Parameters.AddWithValue("@companyName", (object?)companyName ?? DBNull.Value);
                                var r2 = await lookup2.ExecuteScalarAsync();
                                if (r2 != null && r2 != DBNull.Value) existingId = Convert.ToInt32(r2);
                            }

                            if (existingId != null)
                            {
                                using var cmd = conn.CreateCommand();
                                cmd.Transaction = tran;
                                cmd.CommandText = @"
UPDATE [TIN200]
SET [CEOFirstName] = @ceoFirst,
    [CEOLastName] = @ceoLast,
    [Email] = @email,
    [ExternalID] = @externalId,
    [CompanyName] = @companyName,
    [CompanyDescription] = @companyDesc,
    [ExternalId_ImportColumnName] = @externalIdImportColumnName,
    [CompanyName_ImportColumnName] = @companyNameImportColumnName,
    [CompanyDescription_ImportColumnName] = @companyDescriptionImportColumnName,
    [FYE2025] = @fye2025,
    [FYE2024] = @fye2024,
    [FYE2023] = @fye2023,
    [TINStatus] = @tinStatus
WHERE [Id] = @id";
                                cmd.Parameters.AddWithValue("@ceoFirst", (object?)ceoFirst ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@ceoLast", (object?)ceoLast ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@email", (object?)email ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@externalId", (object?)externalId ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@companyName", (object?)companyName ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@companyDesc", (object?)companyDesc ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@externalIdImportColumnName", (object?)externalIdImportColumnName ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@companyNameImportColumnName", (object?)companyNameImportColumnName ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@companyDescriptionImportColumnName", (object?)companyDescriptionImportColumnName ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@fye2025", (object?)fye2025 ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@fye2024", (object?)fye2024 ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@fye2023", (object?)fye2023 ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@tinStatus", (object?)tinStatus ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@id", existingId.Value);

                                var affected = await cmd.ExecuteNonQueryAsync();
                                if (affected > 0)
                                {
                                    updated++;
                                    touchedCompanyIds.Add(existingId.Value);
                                }
                            }
                            else
                            {
                                using var cmd = conn.CreateCommand();
                                cmd.Transaction = tran;
                                cmd.CommandText = @"
INSERT INTO [TIN200] ([CEOFirstName], [CEOLastName], [Email], [ExternalID], [CompanyName], [CompanyDescription], [ExternalId_ImportColumnName], [CompanyName_ImportColumnName], [CompanyDescription_ImportColumnName], [FYE2025], [FYE2024], [FYE2023], [TINStatus])
OUTPUT INSERTED.[Id]
VALUES (@ceoFirst, @ceoLast, @email, @externalId, @companyName, @companyDesc, @externalIdImportColumnName, @companyNameImportColumnName, @companyDescriptionImportColumnName, @fye2025, @fye2024, @fye2023, @tinStatus)";
                                cmd.Parameters.AddWithValue("@ceoFirst", (object?)ceoFirst ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@ceoLast", (object?)ceoLast ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@email", (object?)email ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@externalId", (object?)externalId ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@companyName", (object?)companyName ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@companyDesc", (object?)companyDesc ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@externalIdImportColumnName", (object?)externalIdImportColumnName ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@companyNameImportColumnName", (object?)companyNameImportColumnName ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@companyDescriptionImportColumnName", (object?)companyDescriptionImportColumnName ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@fye2025", (object?)fye2025 ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@fye2024", (object?)fye2024 ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@fye2023", (object?)fye2023 ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@tinStatus", (object?)tinStatus ?? DBNull.Value);

                                var insertedIdObj = await cmd.ExecuteScalarAsync();
                                if (insertedIdObj != null && insertedIdObj != DBNull.Value)
                                {
                                    touchedCompanyIds.Add(Convert.ToInt32(insertedIdObj));
                                }

                                inserted++;
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Row {row}: error inserting/updating - {ex.Message}");
                        }
                    }

                    if (errors.Count == 0)
                    {
                        foreach (var companyId in touchedCompanyIds)
                        {
                            using var csExistsCmd = conn.CreateCommand();
                            csExistsCmd.Transaction = tran;
                            csExistsCmd.CommandText = "SELECT TOP (1) [Id] FROM [CompanySurvey] WHERE [CompanyId] = @companyId AND [SurveyId] = @surveyId";
                            csExistsCmd.Parameters.AddWithValue("@companyId", companyId);
                            csExistsCmd.Parameters.AddWithValue("@surveyId", surveyId);

                            var csExistsObj = await csExistsCmd.ExecuteScalarAsync();
                            if (csExistsObj != null && csExistsObj != DBNull.Value)
                            {
                                continue;
                            }

                            using var csInsertCmd = conn.CreateCommand();
                            csInsertCmd.Transaction = tran;
                            csInsertCmd.CommandText = @"
INSERT INTO [CompanySurvey]
([CompanyId], [SurveyId], [Saved], [Submitted], [Requested], [Locked], [Estimate], [SavedDate], [SubmittedDate], [RequestedDate], [SurveyEmailSent], [SurveyEmailSentLastDate], [SurveyReminderEmailSent], [SurveyReminderEmailSentLastDate], [Unsubscribed], [UnsubscribedDate], [SurveyLink])
VALUES
(@companyId, @surveyId, 0, 0, 0, 0, 0, NULL, NULL, NULL, 0, NULL, 0, NULL, 0, NULL, NULL)";
                            csInsertCmd.Parameters.AddWithValue("@companyId", companyId);
                            csInsertCmd.Parameters.AddWithValue("@surveyId", surveyId);
                            await csInsertCmd.ExecuteNonQueryAsync();
                            companySurveyCreated++;
                        }
                    }

                    if (errors.Count == 0) tran.Commit();
                    else tran.Rollback();
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    return new JsonResult(new { success = false, message = ex.Message });
                }
            }

            return new JsonResult(new { success = errors.Count == 0, inserted, updated, total = inserted + updated, companySurveyCreated, errors });
        }

        private decimal? ParseDecimalNullable(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (decimal.TryParse(s, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.InvariantCulture, out var v)) return v;
            var cleaned = s.Replace(",", "");
            if (decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.InvariantCulture, out v)) return v;
            return null;
        }

        private static string? GetHeaderName(string[] headers, int index)
        {
            if (headers.Length <= index)
            {
                return null;
            }

            var header = headers[index];
            return string.IsNullOrWhiteSpace(header) ? null : header;
        }
    }
}

