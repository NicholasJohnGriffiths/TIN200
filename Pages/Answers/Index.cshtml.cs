using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Services;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace TINWeb.Pages.Answers
{
    public class IndexModel : PageModel
    {
        private readonly AnswerService _answerService;
        private static readonly ConcurrentDictionary<string, PendingAnswerImport> PendingImports = new();

        public List<AnswerService.AnswerListRow> Rows { get; set; } = new();
        public List<int> FinancialYears { get; set; } = new();
        public List<AnswerService.CompanySurveyOption> CompanySurveyOptions { get; set; } = new();
        public int? SelectedFinancialYear { get; set; }
        public int? SelectedCompanySurveyId { get; set; }
        public int QuestionCount { get; set; }
        public int AnsweredCount { get; set; }
        public AnswerService.AnswerImportPreviewResult? ImportPreview { get; set; }
        public AnswerService.AnswerImportPreviewResult? GlobalImportPreview { get; set; }
        public string? PendingImportToken { get; set; }
        public string? PendingGlobalImportToken { get; set; }

        [TempData]
        public string? StatusMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public IndexModel(AnswerService answerService)
        {
            _answerService = answerService;
        }

        public async Task OnGetAsync(int? financialYear, int? companySurveyId)
        {
            var hasExplicitFilters = Request.Query.ContainsKey("financialYear") || Request.Query.ContainsKey("companySurveyId");
            await LoadPageDataAsync(financialYear, companySurveyId, hasExplicitFilters);
        }

        public async Task<IActionResult> OnGetExportAsync(int? financialYear)
        {
            var effectiveYear = financialYear ?? await _answerService.GetCurrentSurveyFinancialYearAsync();
            var rows = await _answerService.GetAnswerExportRowsAsync(effectiveYear);

            var questionColumns = rows
                .GroupBy(r => r.QuestionId)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var title = g
                        .Select(x => x.QuestionTitle)
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

                    return new
                    {
                        QuestionId = g.Key,
                        Header = string.IsNullOrWhiteSpace(title) ? $"Question {g.Key}" : title!.Trim()
                    };
                })
                .ToList();

            var csv = new StringBuilder();
            var headerColumns = new List<string>
            {
                "ExternalId",
                "CompanyName",
                "Email"
            };

            headerColumns.AddRange(questionColumns.Select(x => x.Header));

            csv.AppendLine(string.Join(',', headerColumns.Select(EscapeCsv)));

            var companyGroups = rows
                .GroupBy(r => new { r.CompanyId, r.CompanyExternalId, r.CompanyName, r.CompanyEmail })
                .OrderBy(g => g.Key.CompanyName)
                .ThenBy(g => g.Key.CompanyId);

            foreach (var companyGroup in companyGroups)
            {
                var values = new List<string>
                {
                    EscapeCsv(companyGroup.Key.CompanyExternalId),
                    EscapeCsv(companyGroup.Key.CompanyName),
                    EscapeCsv(companyGroup.Key.CompanyEmail)
                };

                var latestByQuestion = companyGroup
                    .GroupBy(x => x.QuestionId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.AnswerId).First());

                foreach (var question in questionColumns)
                {
                    if (latestByQuestion.TryGetValue(question.QuestionId, out var answer))
                    {
                        values.Add(EscapeCsv(GetAnswerValue(answer)));
                    }
                    else
                    {
                        values.Add(string.Empty);
                    }
                }

                csv.AppendLine(string.Join(',', values));
            }

            var yearLabel = effectiveYear?.ToString() ?? "all";
            var fileName = $"answers-export-fy-{yearLabel}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", fileName);
        }

        private static string GetAnswerValue(AnswerService.AnswerExportRow answer)
        {
            if (!string.IsNullOrWhiteSpace(answer.AnswerText))
            {
                return answer.AnswerText;
            }

            if (answer.AnswerCurrency.HasValue)
            {
                return answer.AnswerCurrency.Value.ToString(CultureInfo.InvariantCulture);
            }

            if (answer.AnswerNumber.HasValue)
            {
                return answer.AnswerNumber.Value.ToString(CultureInfo.InvariantCulture);
            }

            return string.Empty;
        }

        private async Task LoadPageDataAsync(int? financialYear, int? companySurveyId, bool preserveExplicitFilters = false)
        {
            QuestionCount = await _answerService.GetQuestionCountAsync();
            FinancialYears = await _answerService.GetAvailableFinancialYearsAsync();

            if (!financialYear.HasValue && companySurveyId.HasValue)
            {
                var selectedCompanySurvey = (await _answerService.GetCompanySurveyOptionsAsync(null))
                    .FirstOrDefault(x => x.CompanySurveyId == companySurveyId.Value);
                financialYear = selectedCompanySurvey?.FinancialYear;
            }

            if (preserveExplicitFilters)
            {
                SelectedFinancialYear = financialYear;
            }
            else
            {
                SelectedFinancialYear = financialYear ?? await _answerService.GetCurrentSurveyFinancialYearAsync();

                if (!SelectedFinancialYear.HasValue && FinancialYears.Any())
                {
                    SelectedFinancialYear = FinancialYears.First();
                }
            }

            CompanySurveyOptions = await _answerService.GetCompanySurveyOptionsAsync(SelectedFinancialYear);
            SelectedCompanySurveyId = companySurveyId;

            if (SelectedCompanySurveyId.HasValue && !CompanySurveyOptions.Any(x => x.CompanySurveyId == SelectedCompanySurveyId.Value))
            {
                var allOptions = await _answerService.GetCompanySurveyOptionsAsync(null);
                var previouslySelected = allOptions.FirstOrDefault(x => x.CompanySurveyId == SelectedCompanySurveyId.Value);

                if (previouslySelected != null)
                {
                    var sameCompanyInSelectedYear = CompanySurveyOptions
                        .Where(x => x.CompanyId == previouslySelected.CompanyId)
                        .OrderByDescending(x => x.AnswerCount)
                        .ThenByDescending(x => x.CompanySurveyId)
                        .FirstOrDefault();

                    SelectedCompanySurveyId = sameCompanyInSelectedYear?.CompanySurveyId;
                }
                else
                {
                    SelectedCompanySurveyId = null;
                }
            }

            if (!SelectedCompanySurveyId.HasValue && !preserveExplicitFilters)
            {
                SelectedCompanySurveyId = CompanySurveyOptions
                    .OrderByDescending(x => x.AnswerCount)
                    .ThenBy(x => x.CompanyName)
                    .Select(x => (int?)x.CompanySurveyId)
                    .FirstOrDefault();
            }

            if (!SelectedCompanySurveyId.HasValue)
            {
                Rows = new List<AnswerService.AnswerListRow>();
                AnsweredCount = 0;
                return;
            }

            Rows = await _answerService.GetAnswerRowsAsync(SelectedCompanySurveyId.Value);
            AnsweredCount = Rows.Count(x => x.Id > 0);
        }

        public async Task<IActionResult> OnPostDeleteAllAnswersAsync(string? deleteConfirmation)
        {
            if (!string.Equals(deleteConfirmation?.Trim(), "delete", StringComparison.Ordinal))
            {
                ErrorMessage = "Delete cancelled. You must type 'delete' exactly to confirm.";
                return RedirectToPage();
            }

            try
            {
                await _answerService.RecreateAnswerTableAsync();
                StatusMessage = "All answers were deleted and the Answer table was recreated.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Delete all answers failed: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostCreateMissingAnswersAsync(int? financialYear)
        {
            var effectiveYear = financialYear ?? await _answerService.GetCurrentSurveyFinancialYearAsync();
            if (!effectiveYear.HasValue)
            {
                ErrorMessage = "Create missing answers failed: no financial year is selected and no current survey year is configured.";
                return RedirectToPage();
            }

            var createdCount = await _answerService.CreateMissingAnswersForYearAsync(effectiveYear.Value);
            StatusMessage = $"Create Missing Answers complete for FY {effectiveYear.Value}. New answer rows created: {createdCount}.";
            return RedirectToPage(new { financialYear = effectiveYear.Value });
        }

        public async Task<IActionResult> OnPostPreviewImportAsync(IFormFile? importFile, int? financialYear)
        {
            if (importFile == null || importFile.Length == 0)
            {
                ErrorMessage = "Import failed: please select an Excel file.";
                return RedirectToPage(new { financialYear });
            }

            var fileName = importFile.FileName ?? string.Empty;
            if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = "Import failed: only .xlsx Excel files are supported.";
                return RedirectToPage(new { financialYear });
            }

            var effectiveYear = financialYear ?? await _answerService.GetCurrentSurveyFinancialYearAsync();
            if (!effectiveYear.HasValue)
            {
                ErrorMessage = "Import failed: no financial year is selected and no current survey year is configured.";
                return RedirectToPage();
            }

            CleanupExpiredPendingImports();

            var token = Guid.NewGuid().ToString("N");
            var tempDir = Path.Combine(Path.GetTempPath(), "tinweb-answer-import");
            Directory.CreateDirectory(tempDir);
            var tempFilePath = Path.Combine(tempDir, $"{token}.xlsx");

            await using (var tempFileStream = System.IO.File.Create(tempFilePath))
            {
                await importFile.CopyToAsync(tempFileStream);
            }

            AnswerService.AnswerImportPreviewResult preview;
            await using (var readStream = System.IO.File.OpenRead(tempFilePath))
            {
                preview = await _answerService.PreviewAnswersImportFromExcelAsync(readStream, effectiveYear.Value);
            }

            PendingImports[token] = new PendingAnswerImport
            {
                Token = token,
                FinancialYear = effectiveYear.Value,
                Kind = "Standard",
                TempFilePath = tempFilePath,
                CreatedUtc = DateTime.UtcNow
            };

            await LoadPageDataAsync(effectiveYear.Value, null);
            ImportPreview = preview;
            PendingImportToken = token;
            return Page();
        }

        public async Task<IActionResult> OnPostPreviewGlobalImportAsync(IFormFile? importFile, int? financialYear)
        {
            if (importFile == null || importFile.Length == 0)
            {
                ErrorMessage = "Global import failed: please select an Excel file.";
                return RedirectToPage(new { financialYear });
            }

            var fileName = importFile.FileName ?? string.Empty;
            if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = "Global import failed: only .xlsx Excel files are supported.";
                return RedirectToPage(new { financialYear });
            }

            var effectiveYear = financialYear ?? await _answerService.GetCurrentSurveyFinancialYearAsync();
            if (!effectiveYear.HasValue)
            {
                ErrorMessage = "Global import failed: no financial year is selected and no current survey year is configured.";
                return RedirectToPage();
            }

            CleanupExpiredPendingImports();

            var token = Guid.NewGuid().ToString("N");
            var tempDir = Path.Combine(Path.GetTempPath(), "tinweb-answer-import");
            Directory.CreateDirectory(tempDir);
            var tempFilePath = Path.Combine(tempDir, $"{token}.xlsx");

            await using (var tempFileStream = System.IO.File.Create(tempFilePath))
            {
                await importFile.CopyToAsync(tempFileStream);
            }

            AnswerService.AnswerImportPreviewResult preview;
            await using (var readStream = System.IO.File.OpenRead(tempFilePath))
            {
                preview = await _answerService.PreviewGlobalAnswersImportFromExcelAsync(readStream, effectiveYear.Value);
            }

            PendingImports[token] = new PendingAnswerImport
            {
                Token = token,
                FinancialYear = effectiveYear.Value,
                Kind = "Global",
                TempFilePath = tempFilePath,
                CreatedUtc = DateTime.UtcNow
            };

            await LoadPageDataAsync(effectiveYear.Value, null);
            GlobalImportPreview = preview;
            PendingGlobalImportToken = token;
            return Page();
        }

        public async Task<IActionResult> OnPostApplyImportAsync(string? previewToken)
        {
            CleanupExpiredPendingImports();

            if (string.IsNullOrWhiteSpace(previewToken) || !PendingImports.TryGetValue(previewToken, out var pendingImport) || pendingImport.Kind != "Standard")
            {
                ErrorMessage = "Apply import failed: preview session not found or expired. Please preview the file again.";
                return RedirectToPage();
            }

            try
            {
                await using var stream = System.IO.File.OpenRead(pendingImport.TempFilePath);
                var result = await _answerService.ImportAnswersFromExcelAsync(stream, pendingImport.FinancialYear);
                if (result.Errors.Any())
                {
                    StatusMessage = $"Import completed with warnings. Inserted: {result.InsertedCount}, Updated: {result.UpdatedCount}. Warnings: {string.Join(" ", result.Errors.Take(5))}";
                }
                else
                {
                    StatusMessage = $"Import completed. Inserted: {result.InsertedCount}, Updated: {result.UpdatedCount}.";
                }
            }
            finally
            {
                PendingImports.TryRemove(previewToken, out _);
                if (System.IO.File.Exists(pendingImport.TempFilePath))
                {
                    System.IO.File.Delete(pendingImport.TempFilePath);
                }
            }

            return RedirectToPage(new { financialYear = pendingImport.FinancialYear });
        }

        public async Task<IActionResult> OnPostApplyGlobalImportAsync(string? previewToken)
        {
            CleanupExpiredPendingImports();

            if (string.IsNullOrWhiteSpace(previewToken) || !PendingImports.TryGetValue(previewToken, out var pendingImport) || pendingImport.Kind != "Global")
            {
                ErrorMessage = "Apply global import failed: preview session not found or expired. Please preview the file again.";
                return RedirectToPage();
            }

            try
            {
                await using var stream = System.IO.File.OpenRead(pendingImport.TempFilePath);
                var result = await _answerService.ImportGlobalAnswersFromExcelAsync(stream, pendingImport.FinancialYear);
                if (result.Errors.Any())
                {
                    StatusMessage = $"Global import completed with warnings. Inserted: {result.InsertedCount}, Updated: {result.UpdatedCount}. Warnings: {string.Join(" ", result.Errors.Take(5))}";
                }
                else
                {
                    StatusMessage = $"Global import completed. Inserted: {result.InsertedCount}, Updated: {result.UpdatedCount}.";
                }
            }
            finally
            {
                PendingImports.TryRemove(previewToken, out _);
                if (System.IO.File.Exists(pendingImport.TempFilePath))
                {
                    System.IO.File.Delete(pendingImport.TempFilePath);
                }
            }

            return RedirectToPage(new { financialYear = pendingImport.FinancialYear });
        }

        private static void CleanupExpiredPendingImports()
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-30);
            foreach (var key in PendingImports.Keys)
            {
                if (PendingImports.TryGetValue(key, out var pending) && pending.CreatedUtc < cutoff)
                {
                    PendingImports.TryRemove(key, out _);
                    if (System.IO.File.Exists(pending.TempFilePath))
                    {
                        System.IO.File.Delete(pending.TempFilePath);
                    }
                }
            }
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var escaped = value.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }

        private sealed class PendingAnswerImport
        {
            public string Token { get; set; } = string.Empty;
            public int FinancialYear { get; set; }
            public string Kind { get; set; } = "Standard";
            public string TempFilePath { get; set; } = string.Empty;
            public DateTime CreatedUtc { get; set; }
        }
    }
}
