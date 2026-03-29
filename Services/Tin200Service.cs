using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TINWeb.Data;
using TINWeb.Models;
using System.Data;
using System.Globalization;

namespace TINWeb.Services
{
    public class CompanyService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CompanyService> _logger;

        public CompanyService(ApplicationDbContext context, ILogger<CompanyService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Tin200>> GetAllCompaniesAsync(int? lastTin200Year = null)
        {
            try
            {
                var query = _context.Tin200.AsQueryable();
                if (lastTin200Year.HasValue && lastTin200Year.Value > 0)
                {
                    var year = lastTin200Year.Value;
                    query = query.Where(t => t.LastTIN200Year == year);
                }
                return await query
                    .OrderBy(t => string.IsNullOrWhiteSpace(t.CompanyName))
                    .ThenBy(t => t.CompanyName)
                    .ThenBy(t => t.Id)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Primary TIN200 query failed.");
                try
                {
                    return await GetAllCompaniesFallbackAsync(lastTin200Year);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Fallback TIN200 query failed.");
                    return new List<Tin200>();
                }
            }
        }

        public async Task<List<Tin200>> GetTestCompaniesAsync(string? search = null)
        {
            var query = _context.Tin200
                .Where(x => x.Test)
                .AsQueryable();

            var normalizedSearch = (search ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                query = query.Where(x =>
                    (!string.IsNullOrWhiteSpace(x.CompanyName) && x.CompanyName.Contains(normalizedSearch))
                    || (!string.IsNullOrWhiteSpace(x.ExternalId) && x.ExternalId.Contains(normalizedSearch))
                    || (!string.IsNullOrWhiteSpace(x.Email) && x.Email.Contains(normalizedSearch)));
            }

            return await query
                .OrderBy(x => x.CompanyName)
                .ThenBy(x => x.Id)
                .ToListAsync();
        }

        public async Task<List<int>> GetAvailableLastTin200YearsAsync()
        {
            try
            {
                var lastTin200Years = await _context.Tin200
                    .Where(t => t.LastTIN200Year.HasValue && t.LastTIN200Year.Value > 0)
                    .Select(t => t.LastTIN200Year!.Value)
                    .Distinct()
                    .OrderByDescending(y => y)
                    .ToListAsync();

                if (lastTin200Years.Any())
                {
                    return lastTin200Years;
                }

                var fallbackYears = new List<int>();
                if (await _context.Tin200.AnyAsync(t => t.Fye2025 != null)) fallbackYears.Add(2025);
                if (await _context.Tin200.AnyAsync(t => t.Fye2024 != null)) fallbackYears.Add(2024);
                if (await _context.Tin200.AnyAsync(t => t.Fye2023 != null)) fallbackYears.Add(2023);
                return fallbackYears;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read available LastTIN200Year values from TIN200.");
                try
                {
                    var rows = await GetAllCompaniesFallbackAsync();
                    return rows
                        .Where(r => r.LastTIN200Year.HasValue && r.LastTIN200Year.Value > 0)
                        .Select(r => r.LastTIN200Year!.Value)
                        .Distinct()
                        .OrderByDescending(y => y)
                        .ToList();
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Fallback failed to derive available LastTIN200Year values from TIN200.");
                    return new List<int>();
                }
            }
        }

        public async Task<Tin200?> GetCompanyByIdAsync(int id)
        {
            try
            {
                return await _context.Tin200.FindAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load TIN200 record by id {Id}.", id);
                try
                {
                    var all = await GetAllCompaniesFallbackAsync();
                    return all.FirstOrDefault(x => x.Id == id);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Fallback failed to load TIN200 record by id {Id}.", id);
                    return null;
                }
            }
        }

        public async Task<int> GetHighestNumericExternalIdAsync()
        {
            try
            {
                var companies = await _context.Tin200
                    .Where(x => x.ExternalId != null && x.ExternalId != "")
                    .Select(x => x.ExternalId)
                    .ToListAsync();

                int maxId = 0;
                foreach (var extId in companies)
                {
                    if (int.TryParse(extId, out int numId) && numId > maxId)
                    {
                        maxId = numId;
                    }
                }

                return maxId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get highest numeric ExternalId.");
                return 0;
            }
        }

        public async Task<Tin200> CreateCompanyAsync(Tin200 company)
        {
            _context.Tin200.Add(company);
            await _context.SaveChangesAsync();
            return company;
        }

        public async Task<Tin200> UpdateCompanyAsync(Tin200 company)
        {
            _context.Tin200.Update(company);
            await _context.SaveChangesAsync();
            return company;
        }

        public async Task<Tin200> DuplicateTestCompanyAsync(int sourceCompanyId)
        {
            var sourceCompany = await _context.Tin200.FirstOrDefaultAsync(x => x.Id == sourceCompanyId);
            if (sourceCompany == null)
            {
                throw new InvalidOperationException($"Company with ID {sourceCompanyId} was not found.");
            }

            var existingExternalIds = await _context.Tin200
                .Where(x => !string.IsNullOrWhiteSpace(x.ExternalId))
                .Select(x => x.ExternalId!)
                .ToListAsync();

            var nextExternalId = ResolveNextExternalId(sourceCompany.ExternalId, existingExternalIds);
            var duplicateName = BuildDuplicateCompanyName(sourceCompany.CompanyName);
            var companyIdIsIdentity = await IsCompanyIdIdentityAsync();

            await using var transaction = await _context.Database.BeginTransactionAsync();

            Tin200 duplicatedCompany;
            if (companyIdIsIdentity)
            {
                var duplicatedCompanyId = await InsertCompanyShellForIdentityAsync(ClampToMaxLength(nextExternalId, 50), duplicateName);
                duplicatedCompany = await _context.Tin200.FirstAsync(x => x.Id == duplicatedCompanyId);

                duplicatedCompany.CeoFirstName = sourceCompany.CeoFirstName;
                duplicatedCompany.CeoLastName = sourceCompany.CeoLastName;
                duplicatedCompany.Email = sourceCompany.Email;
                duplicatedCompany.ExternalId = ClampToMaxLength(nextExternalId, 50);
                duplicatedCompany.CompanyName = duplicateName;
                duplicatedCompany.CompanyDescription = sourceCompany.CompanyDescription;
                duplicatedCompany.ExternalIdImportColumnName = sourceCompany.ExternalIdImportColumnName;
                duplicatedCompany.CompanyNameImportColumnName = sourceCompany.CompanyNameImportColumnName;
                duplicatedCompany.CompanyDescriptionImportColumnName = sourceCompany.CompanyDescriptionImportColumnName;
                duplicatedCompany.Fye2025 = sourceCompany.Fye2025;
                duplicatedCompany.Fye2024 = sourceCompany.Fye2024;
                duplicatedCompany.Fye2023 = sourceCompany.Fye2023;
                duplicatedCompany.FinancialYear = sourceCompany.FinancialYear;
                duplicatedCompany.LastTIN200Year = sourceCompany.LastTIN200Year;
                duplicatedCompany.Test = true;

                await _context.SaveChangesAsync();
            }
            else
            {
                var nextCompanyId = await _context.Tin200
                    .Select(x => (int?)x.Id)
                    .MaxAsync() ?? 0;

                duplicatedCompany = new Tin200
                {
                    Id = nextCompanyId + 1,
                    CeoFirstName = sourceCompany.CeoFirstName,
                    CeoLastName = sourceCompany.CeoLastName,
                    Email = sourceCompany.Email,
                    ExternalId = ClampToMaxLength(nextExternalId, 50),
                    CompanyName = duplicateName,
                    CompanyDescription = sourceCompany.CompanyDescription,
                    ExternalIdImportColumnName = sourceCompany.ExternalIdImportColumnName,
                    CompanyNameImportColumnName = sourceCompany.CompanyNameImportColumnName,
                    CompanyDescriptionImportColumnName = sourceCompany.CompanyDescriptionImportColumnName,
                    Fye2025 = sourceCompany.Fye2025,
                    Fye2024 = sourceCompany.Fye2024,
                    Fye2023 = sourceCompany.Fye2023,
                    FinancialYear = sourceCompany.FinancialYear,
                    LastTIN200Year = sourceCompany.LastTIN200Year,
                    Test = true
                };

                _context.Tin200.Add(duplicatedCompany);
                await _context.SaveChangesAsync();
            }

            var sourceCompanySurveys = await _context.CompanySurvey
                .Where(x => x.CompanyId == sourceCompanyId)
                .OrderBy(x => x.Id)
                .ToListAsync();

            var surveyIdMap = new Dictionary<int, int>();
            foreach (var sourceSurvey in sourceCompanySurveys)
            {
                var duplicateSurvey = new CompanySurvey
                {
                    CompanyId = duplicatedCompany.Id,
                    SurveyId = sourceSurvey.SurveyId,
                    Saved = sourceSurvey.Saved,
                    Submitted = sourceSurvey.Submitted,
                    Requested = sourceSurvey.Requested,
                    Locked = sourceSurvey.Locked,
                    Estimate = sourceSurvey.Estimate,
                    SavedDate = sourceSurvey.SavedDate,
                    SubmittedDate = sourceSurvey.SubmittedDate,
                    RequestedDate = sourceSurvey.RequestedDate,
                    SurveyEmailSent = sourceSurvey.SurveyEmailSent,
                    SurveyEmailSentLastDate = sourceSurvey.SurveyEmailSentLastDate
                };

                _context.CompanySurvey.Add(duplicateSurvey);
                await _context.SaveChangesAsync();
                surveyIdMap[sourceSurvey.Id] = duplicateSurvey.Id;
            }

            if (surveyIdMap.Count > 0)
            {
                var sourceAnswers = await _context.Answer
                    .Where(x => surveyIdMap.Keys.Contains(x.CompanySurveyId))
                    .ToListAsync();

                foreach (var sourceAnswer in sourceAnswers)
                {
                    _context.Answer.Add(new Answer
                    {
                        CompanySurveyId = surveyIdMap[sourceAnswer.CompanySurveyId],
                        QuestionId = sourceAnswer.QuestionId,
                        AnswerText = sourceAnswer.AnswerText,
                        AnswerCurrency = sourceAnswer.AnswerCurrency,
                        AnswerNumber = sourceAnswer.AnswerNumber
                    });
                }

                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();
            return duplicatedCompany;
        }

        public async Task DeleteCompanyAsync(int id)
        {
            var company = await GetCompanyByIdAsync(id);
            if (company != null)
            {
                _context.Tin200.Remove(company);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> CompanyExistsAsync(int id)
        {
            return await _context.Tin200.AnyAsync(t => t.Id == id);
        }

        private static string ResolveNextExternalId(string? sourceExternalId, List<string> allExternalIds)
        {
            var normalizedExternalIds = allExternalIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (int.TryParse((sourceExternalId ?? string.Empty).Trim(), out var sourceNumericExternalId))
            {
                var nextValue = sourceNumericExternalId + 1;
                while (normalizedExternalIds.Contains(nextValue.ToString(CultureInfo.InvariantCulture)))
                {
                    nextValue++;
                }

                return nextValue.ToString(CultureInfo.InvariantCulture);
            }

            var maxNumericExternalId = allExternalIds
                .Select(x => int.TryParse((x ?? string.Empty).Trim(), out var parsed) ? parsed : 0)
                .DefaultIfEmpty(0)
                .Max();

            return (maxNumericExternalId + 1).ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildDuplicateCompanyName(string? sourceCompanyName)
        {
            var baseName = string.IsNullOrWhiteSpace(sourceCompanyName)
                ? "Company"
                : sourceCompanyName.Trim();

            var suffix = " - copy";
            var maxBaseLength = Math.Max(1, 255 - suffix.Length);
            if (baseName.Length > maxBaseLength)
            {
                baseName = baseName.Substring(0, maxBaseLength);
            }

            return baseName + suffix;
        }

        private static string ClampToMaxLength(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength);
        }

        private async Task<bool> IsCompanyIdIdentityAsync()
        {
            var db = _context.Database.GetDbConnection();
            var shouldClose = db.State != ConnectionState.Open;
            if (shouldClose)
            {
                await db.OpenAsync();
            }

            try
            {
                using var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT CAST(COLUMNPROPERTY(OBJECT_ID('dbo.Company'), 'Id', 'IsIdentity') AS int)";
                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result ?? 0) == 1;
            }
            finally
            {
                if (shouldClose)
                {
                    await db.CloseAsync();
                }
            }
        }

        private async Task<int> InsertCompanyShellForIdentityAsync(string externalId, string companyName)
        {
            var db = _context.Database.GetDbConnection();
            var shouldClose = db.State != ConnectionState.Open;
            if (shouldClose)
            {
                await db.OpenAsync();
            }

            try
            {
                using var cmd = db.CreateCommand();
                var currentTransaction = _context.Database.CurrentTransaction;
                if (currentTransaction != null)
                {
                    cmd.Transaction = currentTransaction.GetDbTransaction();
                }

                cmd.CommandText = @"
INSERT INTO [Company] ([ExternalID], [CompanyName], [Test])
OUTPUT INSERTED.[Id]
VALUES (@externalId, @companyName, @test);";

                var externalIdParam = cmd.CreateParameter();
                externalIdParam.ParameterName = "@externalId";
                externalIdParam.Value = (object?)externalId ?? DBNull.Value;
                cmd.Parameters.Add(externalIdParam);

                var companyNameParam = cmd.CreateParameter();
                companyNameParam.ParameterName = "@companyName";
                companyNameParam.Value = (object?)companyName ?? DBNull.Value;
                cmd.Parameters.Add(companyNameParam);

                var testParam = cmd.CreateParameter();
                testParam.ParameterName = "@test";
                testParam.Value = true;
                cmd.Parameters.Add(testParam);

                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result ?? 0);
            }
            finally
            {
                if (shouldClose)
                {
                    await db.CloseAsync();
                }
            }
        }

        public async Task<CompanyGlobalImportPreviewResult> PreviewGlobalImportFromExcelAsync(Stream excelStream, int importYear)
        {
            var plan = await BuildCompanyGlobalImportPlanAsync(excelStream);
            var previewErrors = new List<string>(plan.Errors);
            var estimatedCompanySurveyCreatedCount = await EstimateCompanySurveyRowsForImportAsync(plan, importYear, previewErrors);
            var matchedHeaders = new[]
            {
                plan.MatchedExternalIdHeader,
                plan.MatchedCompanyNameHeader,
                plan.MatchedCompanyDescriptionHeader
            }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

            var unmatchedHeaders = plan.AvailableHeaders
                .Except(matchedHeaders, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new CompanyGlobalImportPreviewResult
            {
            ImportYear = importYear,
                AvailableHeaders = plan.AvailableHeaders,
                MatchedHeaders = matchedHeaders,
                UnmatchedHeaders = unmatchedHeaders,
                MatchedFields = plan.MatchedFields,
                Errors = previewErrors,
                InsertedCount = plan.Operations.Count(x => x.Action == CompanyImportAction.Add),
                UpdatedCount = plan.Operations.Count(x => x.Action == CompanyImportAction.Update),
                UnchangedCount = plan.Operations.Count(x => x.Action == CompanyImportAction.Unchanged),
                EstimatedCompanySurveyCreatedCount = estimatedCompanySurveyCreatedCount,
                PreviewRows = plan.Operations
                    .OrderBy(x => x.RowNumber)
                    .Take(200)
                    .Select(x => new CompanyGlobalImportPreviewRow
                    {
                        RowNumber = x.RowNumber,
                        Action = x.Action.ToString(),
                        ExistingCompanyId = x.ExistingCompanyId,
                        ExistingExternalId = x.ExistingExternalId,
                        ExistingCompanyName = x.ExistingCompanyName,
                        ExistingCompanyDescription = x.ExistingCompanyDescription,
                        ImportedExternalId = x.ImportedExternalId,
                        ImportedCompanyName = x.ImportedCompanyName,
                        ImportedCompanyDescription = x.ImportedCompanyDescription
                    })
                    .ToList()
            };
        }

        public async Task<CompanyGlobalImportResult> ImportGlobalFromExcelAsync(Stream excelStream, int importYear)
        {
            var plan = await BuildCompanyGlobalImportPlanAsync(excelStream);
            var result = new CompanyGlobalImportResult
            {
            ImportYear = importYear,
                Errors = new List<string>(plan.Errors),
                InsertedCount = plan.Operations.Count(x => x.Action == CompanyImportAction.Add),
                UpdatedCount = plan.Operations.Count(x => x.Action == CompanyImportAction.Update),
                UnchangedCount = plan.Operations.Count(x => x.Action == CompanyImportAction.Unchanged)
            };

            if (!plan.Operations.Any(x => x.Action == CompanyImportAction.Add || x.Action == CompanyImportAction.Update))
            {
                result.CompanySurveyCreatedCount = await EnsureCompanySurveyRowsForImportAsync(plan, importYear, result.Errors);
                return result;
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            foreach (var operation in plan.Operations.Where(x => x.Action == CompanyImportAction.Add))
            {
                await _context.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO [Company] ([ExternalID], [CompanyName], [CompanyDescription], [ExternalId_ImportColumnName], [CompanyName_ImportColumnName], [CompanyDescription_ImportColumnName], [FinancialYear], [LastTIN200Year], [TIN200])
VALUES ({operation.ImportedExternalId}, {operation.ImportedCompanyName}, {operation.ImportedCompanyDescription}, {plan.MatchedExternalIdHeader}, {plan.MatchedCompanyNameHeader}, {plan.MatchedCompanyDescriptionHeader}, {importYear}, {importYear}, {1})");
            }

            result.CompanySurveyCreatedCount = await EnsureCompanySurveyRowsForImportAsync(plan, importYear, result.Errors);

            await transaction.CommitAsync();
            return result;
        }

        private async Task<int> EstimateCompanySurveyRowsForImportAsync(CompanyGlobalImportPlan plan, int importYear, List<string> errors)
        {
            var surveyId = await _context.Survey
                .Where(s => s.FinancialYear == importYear)
                .OrderByDescending(s => s.CurrentSurvey)
                .ThenByDescending(s => s.Id)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync();

            if (!surveyId.HasValue)
            {
                errors.Add($"No Survey exists for import year {importYear}; preview estimate for CompanySurvey creation is 0.");
                return 0;
            }

            var companyIdsFromImport = await ResolveCompanyIdsFromImportPlanAsync(plan, errors);
            if (!companyIdsFromImport.Any())
            {
                return 0;
            }

            var existingCompanySurveyCompanyIds = await _context.CompanySurvey
                .Where(cs => cs.SurveyId == surveyId.Value && companyIdsFromImport.Contains(cs.CompanyId))
                .Select(cs => cs.CompanyId)
                .Distinct()
                .ToListAsync();

            return companyIdsFromImport.Except(existingCompanySurveyCompanyIds).Count();
        }

        private async Task<int> EnsureCompanySurveyRowsForImportAsync(CompanyGlobalImportPlan plan, int importYear, List<string> errors)
        {
            var surveyId = await _context.Survey
                .Where(s => s.FinancialYear == importYear)
                .OrderByDescending(s => s.CurrentSurvey)
                .ThenByDescending(s => s.Id)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync();

            if (!surveyId.HasValue)
            {
                errors.Add($"No Survey exists for import year {importYear}; skipped CompanySurvey creation.");
                return 0;
            }

            var companyIdsFromImport = await ResolveCompanyIdsFromImportPlanAsync(plan, errors);

            if (!companyIdsFromImport.Any())
            {
                return 0;
            }

            var existingCompanySurveyCompanyIds = await _context.CompanySurvey
                .Where(cs => cs.SurveyId == surveyId.Value && companyIdsFromImport.Contains(cs.CompanyId))
                .Select(cs => cs.CompanyId)
                .Distinct()
                .ToListAsync();

            var missingCompanyIds = companyIdsFromImport
                .Except(existingCompanySurveyCompanyIds)
                .ToList();

            foreach (var companyId in missingCompanyIds)
            {
                _context.CompanySurvey.Add(new CompanySurvey
                {
                    CompanyId = companyId,
                    SurveyId = surveyId.Value,
                    Saved = false,
                    Submitted = false,
                    Requested = false,
                    Locked = false,
                    Estimate = false,
                    SavedDate = null,
                    SubmittedDate = null,
                    RequestedDate = null
                });
            }

            if (missingCompanyIds.Any())
            {
                await _context.SaveChangesAsync();
            }

            return missingCompanyIds.Count;
        }

        private async Task<HashSet<int>> ResolveCompanyIdsFromImportPlanAsync(CompanyGlobalImportPlan plan, List<string> errors)
        {
            var unresolvedOperations = plan.Operations
                .Where(o => !o.ExistingCompanyId.HasValue)
                .ToList();

            var unresolvedExternalIds = unresolvedOperations
                .Select(o => o.ImportedExternalId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var unresolvedNames = unresolvedOperations
                .Select(o => o.ImportedCompanyName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var matchedCompanies = await _context.Tin200
                .Where(c =>
                    (!string.IsNullOrWhiteSpace(c.ExternalId) && unresolvedExternalIds.Contains(c.ExternalId))
                    || (!string.IsNullOrWhiteSpace(c.CompanyName) && unresolvedNames.Contains(c.CompanyName)))
                .Select(c => new { c.Id, c.ExternalId, c.CompanyName })
                .ToListAsync();

            var companyIdByExternalId = matchedCompanies
                .Where(x => !string.IsNullOrWhiteSpace(x.ExternalId))
                .GroupBy(x => x.ExternalId!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First().Id, StringComparer.OrdinalIgnoreCase);

            var companyIdByName = matchedCompanies
                .Where(x => !string.IsNullOrWhiteSpace(x.CompanyName))
                .GroupBy(x => x.CompanyName!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First().Id, StringComparer.OrdinalIgnoreCase);

            var companyIdsFromImport = new HashSet<int>();
            foreach (var operation in plan.Operations)
            {
                if (operation.ExistingCompanyId.HasValue)
                {
                    companyIdsFromImport.Add(operation.ExistingCompanyId.Value);
                    continue;
                }

                var resolvedCompanyId = 0;
                var resolved = false;
                var importedExternalId = operation.ImportedExternalId?.Trim();
                var importedCompanyName = operation.ImportedCompanyName?.Trim();

                if (!string.IsNullOrWhiteSpace(importedExternalId)
                    && companyIdByExternalId.TryGetValue(importedExternalId, out resolvedCompanyId))
                {
                    resolved = true;
                }
                else if (!string.IsNullOrWhiteSpace(importedCompanyName)
                    && companyIdByName.TryGetValue(importedCompanyName, out resolvedCompanyId))
                {
                    resolved = true;
                }

                if (resolved)
                {
                    companyIdsFromImport.Add(resolvedCompanyId);
                }
                else
                {
                    var key = !string.IsNullOrWhiteSpace(importedExternalId)
                        ? importedExternalId
                        : importedCompanyName;
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        errors.Add($"Could not resolve company id for imported row key '{key}'; skipped CompanySurvey creation for that row.");
                    }
                }
            }

            return companyIdsFromImport;
        }

        public async Task<ResetFyeValuesResult> PreviewResetFyeValuesFromSurveyAnswersAsync(int previewLimit = 25)
        {
            return await BuildResetFyeValuesResultAsync(applyUpdates: false, previewLimit: previewLimit);
        }

        public async Task<ResetFyeValuesResult> ResetFyeValuesFromSurveyAnswersAsync()
        {
            return await BuildResetFyeValuesResultAsync(applyUpdates: true, previewLimit: 10);
        }

        private async Task<ResetFyeValuesResult> BuildResetFyeValuesResultAsync(bool applyUpdates, int previewLimit)
        {
            var currentSurveyYear = await _context.Survey
                .Where(s => s.CurrentSurvey)
                .OrderByDescending(s => s.FinancialYear)
                .ThenByDescending(s => s.Id)
                .Select(s => (int?)s.FinancialYear)
                .FirstOrDefaultAsync();

            if (!currentSurveyYear.HasValue)
            {
                return new ResetFyeValuesResult
                {
                    HasCurrentSurvey = false,
                    PreviewRows = new List<ResetFyePreviewRow>()
                };
            }

            var currentSurveyId = await _context.Survey
                .Where(s => s.CurrentSurvey)
                .OrderByDescending(s => s.FinancialYear)
                .ThenByDescending(s => s.Id)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync();

            if (!currentSurveyId.HasValue)
            {
                return new ResetFyeValuesResult
                {
                    HasCurrentSurvey = false,
                    PreviewRows = new List<ResetFyePreviewRow>()
                };
            }

            var allQuestions = await _context.Question
                .AsNoTracking()
                .Select(q => new
                {
                    q.Id,
                    q.Title,
                    q.QuestionText,
                    q.ImportColumnName,
                    q.ImportColumnNameAlt
                })
                .ToListAsync();

            var matchedRevenueQuestions = allQuestions
                .Select(q => new
                {
                    q.Id,
                    FyeField = ResolveFyeFieldFromQuestionMetadata(q.Title, q.QuestionText, q.ImportColumnName, q.ImportColumnNameAlt)
                })
                .Where(q => q.FyeField != null)
                .ToList();

            var matchedCeoQuestions = allQuestions
                .Select(q => new
                {
                    q.Id,
                    CeoField = ResolveCeoFieldFromQuestionMetadata(q.Title, q.QuestionText, q.ImportColumnName, q.ImportColumnNameAlt)
                })
                .Where(q => q.CeoField != null)
                .ToList();

            var matchedRevenueQuestionIds = matchedRevenueQuestions
                .Select(q => q.Id)
                .Distinct()
                .ToList();

            var matchedCeoQuestionIds = matchedCeoQuestions
                .Select(q => q.Id)
                .Distinct()
                .ToList();

            var questionIdToFyeField = matchedRevenueQuestions
                .ToDictionary(
                    q => q.Id,
                    q => q.FyeField!);

            var fyeFieldToOffset = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["FYE2025"] = 0,
                ["FYE2024"] = 1,
                ["FYE2023"] = 2
            };

            var questionIdToCeoField = matchedCeoQuestions
                .ToDictionary(
                    q => q.Id,
                    q => q.CeoField!);

            if (!matchedRevenueQuestionIds.Any())
            {
                return new ResetFyeValuesResult
                {
                    HasCurrentSurvey = true,
                    CurrentSurveyYear = currentSurveyYear.Value,
                    TotalMatchedCompanies = 0,
                    UpdatedCompanyCount = 0,
                    WouldUpdateCompanyCount = 0,
                    PreviewRows = new List<ResetFyePreviewRow>()
                };
            }

            var revenueAnswerRows = await (
                from companySurvey in _context.CompanySurvey.AsNoTracking()
                join survey in _context.Survey.AsNoTracking() on companySurvey.SurveyId equals survey.Id
                join answer in _context.Answer.AsNoTracking() on companySurvey.Id equals answer.CompanySurveyId
                where matchedRevenueQuestionIds.Contains(answer.QuestionId)
                select new
                {
                    companySurvey.CompanyId,
                    companySurvey.SurveyId,
                    SurveyFinancialYear = survey.FinancialYear,
                    answer.QuestionId,
                    answer.Id,
                    answer.AnswerCurrency,
                    answer.AnswerNumber,
                    answer.AnswerText
                })
                .ToListAsync();

            var ceoAnswerRows = await (
                from companySurvey in _context.CompanySurvey.AsNoTracking()
                join survey in _context.Survey.AsNoTracking() on companySurvey.SurveyId equals survey.Id
                join answer in _context.Answer.AsNoTracking() on companySurvey.Id equals answer.CompanySurveyId
                where matchedCeoQuestionIds.Contains(answer.QuestionId)
                select new
                {
                    companySurvey.CompanyId,
                    companySurvey.SurveyId,
                    SurveyFinancialYear = survey.FinancialYear,
                    answer.QuestionId,
                    answer.Id,
                    answer.AnswerText
                })
                .ToListAsync();

            var latestRevenueByCompanyYearOffset = revenueAnswerRows
                .Where(x => questionIdToFyeField.ContainsKey(x.QuestionId))
                .Select(x => new
                {
                    x.CompanyId,
                    x.SurveyId,
                    x.SurveyFinancialYear,
                    Offset = fyeFieldToOffset[questionIdToFyeField[x.QuestionId]],
                    x.Id,
                    ParsedValue = ParseRevenueAnswer(x.AnswerCurrency, x.AnswerNumber, x.AnswerText)
                })
                .Where(x => x.ParsedValue.HasValue)
                .GroupBy(x => new { x.CompanyId, x.SurveyFinancialYear, x.Offset })
                .ToDictionary(
                    g => (g.Key.CompanyId, g.Key.SurveyFinancialYear, g.Key.Offset),
                    g => g.OrderByDescending(x => x.SurveyId)
                          .ThenByDescending(x => x.Id)
                          .Select(x => x.ParsedValue)
                          .FirstOrDefault());

            var latestCeoAnswerByCompanyAndField = ceoAnswerRows
                .Where(x => questionIdToCeoField.ContainsKey(x.QuestionId))
                .Select(x => new
                {
                    x.CompanyId,
                    CeoField = questionIdToCeoField[x.QuestionId],
                    x.SurveyFinancialYear,
                    x.SurveyId,
                    x.Id,
                    x.AnswerText
                })
                .GroupBy(x => new { x.CompanyId, x.CeoField })
                .ToDictionary(
                    g => (g.Key.CompanyId, g.Key.CeoField),
                    g => g.OrderByDescending(x => x.SurveyFinancialYear)
                          .ThenByDescending(x => x.SurveyId)
                          .ThenByDescending(x => x.Id)
                          .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.AnswerText)));

            var companyIds = latestRevenueByCompanyYearOffset.Keys
                .Select(x => x.CompanyId)
                .Concat(latestCeoAnswerByCompanyAndField.Keys.Select(x => x.CompanyId))
                .Distinct()
                .ToList();

            if (!companyIds.Any())
            {
                return new ResetFyeValuesResult
                {
                    HasCurrentSurvey = true,
                    CurrentSurveyYear = currentSurveyYear.Value,
                    TotalMatchedCompanies = 0,
                    UpdatedCompanyCount = 0,
                    WouldUpdateCompanyCount = 0,
                    PreviewRows = new List<ResetFyePreviewRow>()
                };
            }

            var companies = await _context.Tin200
                .Where(c => companyIds.Contains(c.Id))
                .ToListAsync();

            var updatedCount = 0;
            var companiesWithRevenueData = 0;
            var companiesWithCeoData = 0;
            var changedRows = new List<ResetFyePreviewRow>();

            decimal? ResolveRevenueValueForTargetField(int companyId, int targetOffset)
            {
                // Example for current year Y:
                // targetOffset 0 => (Y,0)
                // targetOffset 1 => (Y,1) then (Y-1,0)
                // targetOffset 2 => (Y,2) then (Y-1,1) then (Y-2,0)
                for (var step = 0; step <= targetOffset; step++)
                {
                    var surveyYear = currentSurveyYear.Value - step;
                    var sourceOffset = targetOffset - step;
                    if (latestRevenueByCompanyYearOffset.TryGetValue((companyId, surveyYear, sourceOffset), out var value) && value.HasValue)
                    {
                        return value.Value;
                    }
                }

                return null;
            }

            foreach (var company in companies)
            {
                latestCeoAnswerByCompanyAndField.TryGetValue((company.Id, "CeoFirstName"), out var ceoFirstNameAnswer);
                latestCeoAnswerByCompanyAndField.TryGetValue((company.Id, "CeoLastName"), out var ceoLastNameAnswer);
                latestCeoAnswerByCompanyAndField.TryGetValue((company.Id, "Email"), out var ceoEmailAnswer);

                var newFye2025 = ResolveRevenueValueForTargetField(company.Id, 0);
                var newFye2024 = ResolveRevenueValueForTargetField(company.Id, 1);
                var newFye2023 = ResolveRevenueValueForTargetField(company.Id, 2);

                var newCeoFirstName = string.IsNullOrWhiteSpace(ceoFirstNameAnswer?.AnswerText) ? null : ceoFirstNameAnswer!.AnswerText?.Trim();
                var newCeoLastName = string.IsNullOrWhiteSpace(ceoLastNameAnswer?.AnswerText) ? null : ceoLastNameAnswer!.AnswerText?.Trim();
                var newEmail = string.IsNullOrWhiteSpace(ceoEmailAnswer?.AnswerText) ? null : ceoEmailAnswer!.AnswerText?.Trim();

                if (newFye2025 != null || newFye2024 != null || newFye2023 != null) companiesWithRevenueData++;
                if (newCeoFirstName != null || newCeoLastName != null || newEmail != null) companiesWithCeoData++;

                var fyeChanged = (newFye2025 != null && company.Fye2025 != newFye2025)
                              || (newFye2024 != null && company.Fye2024 != newFye2024)
                              || (newFye2023 != null && company.Fye2023 != newFye2023);
                var ceoChanged = (newCeoFirstName != null && company.CeoFirstName != newCeoFirstName)
                              || (newCeoLastName != null && company.CeoLastName != newCeoLastName)
                              || (newEmail != null && company.Email != newEmail);
                var hasChanges = fyeChanged || ceoChanged;

                if (hasChanges)
                {
                    changedRows.Add(new ResetFyePreviewRow
                    {
                        CompanyId = company.Id,
                        CompanyName = company.CompanyName,
                        CurrentCeoFirstName = company.CeoFirstName,
                        NewCeoFirstName = newCeoFirstName,
                        CurrentCeoLastName = company.CeoLastName,
                        NewCeoLastName = newCeoLastName,
                        CurrentEmail = company.Email,
                        NewEmail = newEmail,
                        CurrentFye2025 = company.Fye2025,
                        NewFye2025 = newFye2025,
                        CurrentFye2024 = company.Fye2024,
                        NewFye2024 = newFye2024,
                        CurrentFye2023 = company.Fye2023,
                        NewFye2023 = newFye2023
                    });
                }

                if (hasChanges && applyUpdates)
                {
                    if (newCeoFirstName != null) company.CeoFirstName = newCeoFirstName;
                    if (newCeoLastName != null) company.CeoLastName = newCeoLastName;
                    if (newEmail != null) company.Email = newEmail;
                    if (newFye2025 != null) company.Fye2025 = newFye2025;
                    if (newFye2024 != null) company.Fye2024 = newFye2024;
                    if (newFye2023 != null) company.Fye2023 = newFye2023;
                    updatedCount++;
                }
            }

            if (applyUpdates && updatedCount > 0)
            {
                await _context.SaveChangesAsync();
            }

            return new ResetFyeValuesResult
            {
                HasCurrentSurvey = true,
                CurrentSurveyYear = currentSurveyYear.Value,
                TotalMatchedCompanies = companies.Count,
                CompaniesWithRevenueData = companiesWithRevenueData,
                CompaniesWithCeoData = companiesWithCeoData,
                UpdatedCompanyCount = updatedCount,
                WouldUpdateCompanyCount = changedRows.Count,
                PreviewRows = changedRows
                    .OrderBy(x => x.CompanyName)
                    .ThenBy(x => x.CompanyId)
                    .Take(Math.Max(1, previewLimit))
                    .ToList()
            };
        }

        private static decimal? ParseRevenueAnswer(decimal? answerCurrency, double? answerNumber, string? answerText)
        {
            if (answerCurrency.HasValue)
            {
                return Math.Round(answerCurrency.Value, 0, MidpointRounding.AwayFromZero);
            }

            if (answerNumber.HasValue)
            {
                return Math.Round((decimal)answerNumber.Value, 0, MidpointRounding.AwayFromZero);
            }

            if (string.IsNullOrWhiteSpace(answerText))
            {
                return null;
            }

            var cleaned = answerText
                .Replace("$", string.Empty)
                .Replace(",", string.Empty)
                .Trim();

            if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                return Math.Round(parsed, 0, MidpointRounding.AwayFromZero);
            }

            if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.CurrentCulture, out parsed))
            {
                return Math.Round(parsed, 0, MidpointRounding.AwayFromZero);
            }

            return null;
        }

        private static string? ResolveFyeFieldFromQuestionMetadata(params string?[] candidates)
        {
            foreach (var candidate in candidates)
            {
                var key = NormalizeQuestionKey(candidate);
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                // FYE Last Financial Year
                if (key is "totalrevenuelastfinancialyear" or "totalreveuelastfinancialyear" or "revenuelastfinancialyear" or "fyelastfinancialyear" or "fye2025")
                {
                    return "FYE2025";
                }

                // FYE Year-1
                if (key is "totalrevenueyear1" or "totalrevenueyear01" or "totalreveueyear1" or "totalreveueyear01" or "fyeyear1" or "fye2024")
                {
                    return "FYE2024";
                }

                // FYE Year-2
                if (key is "totalrevenueyear2" or "totalrevenueyear02" or "totalreveueyear2" or "totalreveueyear02" or "fyeyear2" or "fye2023")
                {
                    return "FYE2023";
                }
            }

            return null;
        }

        private static string? ResolveCeoFieldFromQuestionMetadata(params string?[] candidates)
        {
            foreach (var candidate in candidates)
            {
                var key = NormalizeQuestionKey(candidate);
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                if (key == "ceofirstname")
                {
                    return "CeoFirstName";
                }

                if (key == "ceolastname")
                {
                    return "CeoLastName";
                }

                if (key is "ceoemail" or "recipientemail")
                {
                    return "Email";
                }
            }

            return null;
        }

        private static string NormalizeQuestionKey(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return new string(text
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
        }

        private async Task<CompanyGlobalImportPlan> BuildCompanyGlobalImportPlanAsync(Stream excelStream)
        {
            var plan = new CompanyGlobalImportPlan();

            using var workbook = new XLWorkbook(excelStream);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
            {
                plan.Errors.Add("The Excel workbook does not contain any worksheets.");
                return plan;
            }

            var headerRowNumber = worksheet.FirstRowUsed()?.RowNumber() ?? 0;
            if (headerRowNumber <= 0)
            {
                plan.Errors.Add("The worksheet must contain a header row.");
                return plan;
            }

            var headerRow = worksheet.Row(headerRowNumber);
            var lastColumnNumber = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            var columnByHeader = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var column = 1; column <= lastColumnNumber; column++)
            {
                var header = headerRow.Cell(column).GetString().Trim();
                if (string.IsNullOrWhiteSpace(header) || columnByHeader.ContainsKey(header))
                {
                    continue;
                }

                columnByHeader[header] = column;
            }

            if (!columnByHeader.Any())
            {
                plan.Errors.Add("The worksheet header row does not contain any usable column names.");
                return plan;
            }

            plan.AvailableHeaders = columnByHeader.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

            var normalizedColumnByHeader = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in columnByHeader)
            {
                var normalizedHeader = NormalizeHeader(header.Key);
                if (!string.IsNullOrWhiteSpace(normalizedHeader) && !normalizedColumnByHeader.ContainsKey(normalizedHeader))
                {
                    normalizedColumnByHeader[normalizedHeader] = header.Value;
                }
            }

            var headerByColumn = columnByHeader.ToDictionary(x => x.Value, x => x.Key);

            bool TryGetColumn(List<string> aliases, out int columnNumber, out string matchedHeader)
            {
                foreach (var alias in aliases)
                {
                    if (columnByHeader.TryGetValue(alias, out columnNumber))
                    {
                        matchedHeader = alias;
                        return true;
                    }

                    var normalizedAlias = NormalizeHeader(alias);
                    if (!string.IsNullOrWhiteSpace(normalizedAlias)
                        && normalizedColumnByHeader.TryGetValue(normalizedAlias, out columnNumber))
                    {
                        matchedHeader = headerByColumn.TryGetValue(columnNumber, out var resolvedHeader)
                            ? resolvedHeader
                            : alias;
                        return true;
                    }
                }

                columnNumber = 0;
                matchedHeader = string.Empty;
                return false;
            }

            var externalIdAliases = new List<string> { "ID", "External ID", "ExternalID" };
            var companyNameAliases = new List<string> { "Name", "Company Name", "CompanyName" };
            var companyDescriptionAliases = new List<string> { "Description", "Company Description", "CompanyDescription" };

            if (!TryGetColumn(externalIdAliases, out var externalIdColumn, out var matchedExternalIdHeader))
            {
                plan.Errors.Add($"Could not find an External ID column. Tried: {string.Join(", ", externalIdAliases)}");
            }
            else
            {
                plan.MatchedExternalIdHeader = matchedExternalIdHeader;
                plan.MatchedFields.Add($"External ID -> {matchedExternalIdHeader}");
            }

            if (!TryGetColumn(companyNameAliases, out var companyNameColumn, out var matchedCompanyNameHeader))
            {
                plan.Errors.Add($"Could not find a Company Name column. Tried: {string.Join(", ", companyNameAliases)}");
            }
            else
            {
                plan.MatchedCompanyNameHeader = matchedCompanyNameHeader;
                plan.MatchedFields.Add($"Company Name -> {matchedCompanyNameHeader}");
            }

            var hasDescriptionColumn = TryGetColumn(companyDescriptionAliases, out var companyDescriptionColumn, out var matchedCompanyDescriptionHeader);
            if (hasDescriptionColumn)
            {
                plan.MatchedCompanyDescriptionHeader = matchedCompanyDescriptionHeader;
                plan.MatchedFields.Add($"Company Description -> {matchedCompanyDescriptionHeader}");
            }

            if (string.IsNullOrWhiteSpace(plan.MatchedExternalIdHeader) || string.IsNullOrWhiteSpace(plan.MatchedCompanyNameHeader))
            {
                return plan;
            }

            var existingCompanies = await _context.Tin200
                .AsNoTracking()
                .Select(t => new
                {
                    t.Id,
                    t.ExternalId,
                    t.CompanyName,
                    t.CompanyDescription,
                    t.ExternalIdImportColumnName,
                    t.CompanyNameImportColumnName,
                    t.CompanyDescriptionImportColumnName
                })
                .ToListAsync();

            var companyByExternalId = existingCompanies
                .Where(x => !string.IsNullOrWhiteSpace(x.ExternalId))
                .GroupBy(x => x.ExternalId!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First(), StringComparer.OrdinalIgnoreCase);

            var companyByName = existingCompanies
                .Where(x => !string.IsNullOrWhiteSpace(x.CompanyName))
                .GroupBy(x => x.CompanyName!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First(), StringComparer.OrdinalIgnoreCase);

            var seenImportKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var firstDataRowNumber = headerRowNumber + 1;
            var lastRowNumber = worksheet.LastRowUsed()?.RowNumber() ?? headerRowNumber;

            for (var rowNumber = firstDataRowNumber; rowNumber <= lastRowNumber; rowNumber++)
            {
                var row = worksheet.Row(rowNumber);
                var importedExternalId = externalIdColumn > 0 ? row.Cell(externalIdColumn).GetString().Trim() : string.Empty;
                var importedCompanyName = companyNameColumn > 0 ? row.Cell(companyNameColumn).GetString().Trim() : string.Empty;
                var importedCompanyDescription = hasDescriptionColumn ? row.Cell(companyDescriptionColumn).GetString().Trim() : string.Empty;

                if (string.IsNullOrWhiteSpace(importedExternalId)
                    && string.IsNullOrWhiteSpace(importedCompanyName)
                    && string.IsNullOrWhiteSpace(importedCompanyDescription))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(importedExternalId) && string.IsNullOrWhiteSpace(importedCompanyName))
                {
                    plan.Errors.Add($"Row {rowNumber}: skipped because both External ID and Company Name are blank.");
                    continue;
                }

                var importKey = !string.IsNullOrWhiteSpace(importedExternalId)
                    ? $"id:{importedExternalId}"
                    : $"name:{importedCompanyName}";

                if (!seenImportKeys.Add(importKey))
                {
                    plan.Errors.Add($"Row {rowNumber}: duplicate import key '{importKey}' skipped.");
                    continue;
                }

                var existing = !string.IsNullOrWhiteSpace(importedExternalId) && companyByExternalId.TryGetValue(importedExternalId, out var matchedByExternalId)
                    ? matchedByExternalId
                    : (!string.IsNullOrWhiteSpace(importedCompanyName) && companyByName.TryGetValue(importedCompanyName, out var matchedByName)
                        ? matchedByName
                        : null);

                var finalImportedDescription = hasDescriptionColumn
                    ? NullIfWhiteSpace(importedCompanyDescription)
                    : existing?.CompanyDescription;

                var action = CompanyImportAction.Add;
                if (existing != null)
                {
                    // Existing company rows are treated as unchanged by design for global import.
                    action = CompanyImportAction.Unchanged;
                }

                plan.Operations.Add(new CompanyImportOperation
                {
                    RowNumber = rowNumber,
                    Action = action,
                    ExistingCompanyId = existing?.Id,
                    ExistingExternalId = existing?.ExternalId,
                    ExistingCompanyName = existing?.CompanyName,
                    ExistingCompanyDescription = existing?.CompanyDescription,
                    ImportedExternalId = NullIfWhiteSpace(importedExternalId),
                    ImportedCompanyName = NullIfWhiteSpace(importedCompanyName),
                    ImportedCompanyDescription = finalImportedDescription
                });
            }

            return plan;
        }

        private static List<string> BuildHeaderAliases(IEnumerable<string?> configuredValues, params string[] fallbackAliases)
        {
            var aliases = configuredValues
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var fallbackAlias in fallbackAliases)
            {
                if (!aliases.Contains(fallbackAlias, StringComparer.OrdinalIgnoreCase))
                {
                    aliases.Add(fallbackAlias);
                }
            }

            return aliases;
        }

        private static string NormalizeHeader(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var chars = value.Trim()
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray();

            return new string(chars);
        }

        private static string? NullIfWhiteSpace(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        public class ResetFyeValuesResult
        {
            public bool HasCurrentSurvey { get; set; }
            public int? CurrentSurveyYear { get; set; }
            public int TotalMatchedCompanies { get; set; }
            public int CompaniesWithRevenueData { get; set; }
            public int CompaniesWithCeoData { get; set; }
            public int UpdatedCompanyCount { get; set; }
            public int WouldUpdateCompanyCount { get; set; }
            public List<ResetFyePreviewRow> PreviewRows { get; set; } = new();
        }

        public class ResetFyePreviewRow
        {
            public int CompanyId { get; set; }
            public string? CompanyName { get; set; }
            public string? CurrentCeoFirstName { get; set; }
            public string? NewCeoFirstName { get; set; }
            public string? CurrentCeoLastName { get; set; }
            public string? NewCeoLastName { get; set; }
            public string? CurrentEmail { get; set; }
            public string? NewEmail { get; set; }
            public decimal? CurrentFye2025 { get; set; }
            public decimal? NewFye2025 { get; set; }
            public decimal? CurrentFye2024 { get; set; }
            public decimal? NewFye2024 { get; set; }
            public decimal? CurrentFye2023 { get; set; }
            public decimal? NewFye2023 { get; set; }
        }

        public class CompanyGlobalImportPreviewResult
        {
            public int ImportYear { get; set; }
            public List<string> AvailableHeaders { get; set; } = new();
            public List<string> MatchedHeaders { get; set; } = new();
            public List<string> UnmatchedHeaders { get; set; } = new();
            public List<string> MatchedFields { get; set; } = new();
            public List<string> Errors { get; set; } = new();
            public int InsertedCount { get; set; }
            public int UpdatedCount { get; set; }
            public int UnchangedCount { get; set; }
            public int EstimatedCompanySurveyCreatedCount { get; set; }
            public List<CompanyGlobalImportPreviewRow> PreviewRows { get; set; } = new();
        }

        public class CompanyGlobalImportPreviewRow
        {
            public int RowNumber { get; set; }
            public string Action { get; set; } = string.Empty;
            public int? ExistingCompanyId { get; set; }
            public string? ExistingExternalId { get; set; }
            public string? ExistingCompanyName { get; set; }
            public string? ExistingCompanyDescription { get; set; }
            public string? ImportedExternalId { get; set; }
            public string? ImportedCompanyName { get; set; }
            public string? ImportedCompanyDescription { get; set; }
        }

        public class CompanyGlobalImportResult
        {
            public int ImportYear { get; set; }
            public int InsertedCount { get; set; }
            public int UpdatedCount { get; set; }
            public int UnchangedCount { get; set; }
            public int CompanySurveyCreatedCount { get; set; }
            public List<string> Errors { get; set; } = new();
        }

        private sealed class CompanyGlobalImportPlan
        {
            public List<string> AvailableHeaders { get; set; } = new();
            public List<string> MatchedFields { get; set; } = new();
            public List<string> Errors { get; set; } = new();
            public string? MatchedExternalIdHeader { get; set; }
            public string? MatchedCompanyNameHeader { get; set; }
            public string? MatchedCompanyDescriptionHeader { get; set; }
            public List<CompanyImportOperation> Operations { get; set; } = new();
        }

        private sealed class CompanyImportOperation
        {
            public int RowNumber { get; set; }
            public CompanyImportAction Action { get; set; }
            public int? ExistingCompanyId { get; set; }
            public string? ExistingExternalId { get; set; }
            public string? ExistingCompanyName { get; set; }
            public string? ExistingCompanyDescription { get; set; }
            public string? ImportedExternalId { get; set; }
            public string? ImportedCompanyName { get; set; }
            public string? ImportedCompanyDescription { get; set; }
        }

        private enum CompanyImportAction
        {
            Add,
            Update,
            Unchanged
        }

        private async Task<List<Tin200>> GetAllCompaniesFallbackAsync(int? lastTin200Year = null)
        {
            var map = await GetCompanyColumnMapAsync();
            var rows = new List<Tin200>();

            static string SelectOrDefault(Dictionary<string, string?> columnMap, string key, string alias, string defaultSql = "NULL")
            {
                if (columnMap.TryGetValue(key, out var columnName) && !string.IsNullOrWhiteSpace(columnName))
                {
                    return $"[{columnName}] AS {alias}";
                }

                return $"{defaultSql} AS {alias}";
            }

            var db = _context.Database.GetDbConnection();
            var shouldClose = db.State != ConnectionState.Open;
            if (shouldClose)
            {
                await db.OpenAsync();
            }

            try
            {
                using var cmd = db.CreateCommand();

                var sql = $@"
SELECT
    [{map["Id"]}] AS Id,
    [{map["CeoFirstName"]}] AS CeoFirstName,
    [{map["CeoLastName"]}] AS CeoLastName,
    [{map["Email"]}] AS Email,
    [{map["ExternalId"]}] AS ExternalId,
    [{map["CompanyName"]}] AS CompanyName,
    [{map["CompanyDescription"]}] AS CompanyDescription,
    {SelectOrDefault(map, "ExternalIdImportColumnName", "ExternalIdImportColumnName")},
    {SelectOrDefault(map, "CompanyNameImportColumnName", "CompanyNameImportColumnName")},
    {SelectOrDefault(map, "CompanyDescriptionImportColumnName", "CompanyDescriptionImportColumnName")},
    {SelectOrDefault(map, "Fye2025", "Fye2025")},
    {SelectOrDefault(map, "Fye2024", "Fye2024")},
    {SelectOrDefault(map, "Fye2023", "Fye2023")},
    {SelectOrDefault(map, "LastTIN200Year", "LastTIN200Year")},
    {SelectOrDefault(map, "Test", "Test", "CAST(0 AS bit)")}
FROM [Company]";

                if (lastTin200Year.HasValue)
                {
                    sql += " WHERE [" + map["LastTIN200Year"] + "] = @lastTin200Year";
                    var yearParameter = cmd.CreateParameter();
                    yearParameter.ParameterName = "@lastTin200Year";
                    yearParameter.Value = lastTin200Year.Value;
                    cmd.Parameters.Add(yearParameter);
                }

                sql += $" ORDER BY CASE WHEN [{map["CompanyName"]}] IS NULL OR LTRIM(RTRIM([{map["CompanyName"]}])) = '' THEN 1 ELSE 0 END ASC, [{map["CompanyName"]}] ASC, [{map["Id"]}] ASC";
                cmd.CommandText = sql;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new Tin200
                    {
                        Id = GetInt32(reader, "Id"),
                        CeoFirstName = GetString(reader, "CeoFirstName"),
                        CeoLastName = GetString(reader, "CeoLastName"),
                        Email = GetString(reader, "Email"),
                        ExternalId = GetString(reader, "ExternalId"),
                        CompanyName = GetString(reader, "CompanyName"),
                        CompanyDescription = GetString(reader, "CompanyDescription"),
                        ExternalIdImportColumnName = GetString(reader, "ExternalIdImportColumnName"),
                        CompanyNameImportColumnName = GetString(reader, "CompanyNameImportColumnName"),
                        CompanyDescriptionImportColumnName = GetString(reader, "CompanyDescriptionImportColumnName"),
                        Fye2025 = GetDecimal(reader, "Fye2025"),
                        Fye2024 = GetDecimal(reader, "Fye2024"),
                        Fye2023 = GetDecimal(reader, "Fye2023"),
                        LastTIN200Year = GetNullableInt32(reader, "LastTIN200Year"),
                        Test = GetBoolean(reader, "Test")
                    });
                }

                return rows;
            }
            finally
            {
                if (shouldClose)
                {
                    await db.CloseAsync();
                }
            }
        }

        private async Task<Dictionary<string, string>> GetCompanyColumnMapAsync()
        {
            var db = _context.Database.GetDbConnection();
            var shouldClose = db.State != ConnectionState.Open;
            if (shouldClose)
            {
                await db.OpenAsync();
            }

            try
            {
                using var cmd = db.CreateCommand();
                cmd.CommandText = @"
SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'Company'";

                var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    existing.Add(reader.GetString(0));
                }

                string Pick(params string[] names)
                {
                    foreach (var name in names)
                    {
                        if (existing.Contains(name))
                        {
                            return name;
                        }
                    }
                    throw new InvalidOperationException($"None of the expected columns were found: {string.Join(", ", names)}");
                }

                string? PickOptional(params string[] names)
                {
                    foreach (var name in names)
                    {
                        if (existing.Contains(name))
                        {
                            return name;
                        }
                    }

                    return null;
                }

                return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Id"] = Pick("Id"),
                    ["CeoFirstName"] = Pick("CEOFirstName", "CeoFirstName", "CEO First Name", "CEO First Name "),
                    ["CeoLastName"] = Pick("CEOLastName", "CeoLastName", "CEO Last Name", "CEO Last Name "),
                    ["Email"] = Pick("Email", "Email "),
                    ["ExternalId"] = Pick("ExternalID", "ExternalId", "External ID"),
                    ["CompanyName"] = Pick("CompanyName", "Company Name"),
                    ["CompanyDescription"] = Pick("CompanyDescription", "Company Description"),
                    ["ExternalIdImportColumnName"] = PickOptional("ExternalId_ImportColumnName", "External ID Import Column Name"),
                    ["CompanyNameImportColumnName"] = PickOptional("CompanyName_ImportColumnName", "Company Name Import Column Name"),
                    ["CompanyDescriptionImportColumnName"] = PickOptional("CompanyDescription_ImportColumnName", "Company Description Import Column Name"),
                    ["Fye2025"] = PickOptional("FYELastFinancialYear", "FYE2025", "Fye2025", "FYE 2025"),
                    ["Fye2024"] = PickOptional("FYEYear-1", "FYE2024", "Fye2024", "FYE 2024"),
                    ["Fye2023"] = PickOptional("FYEYear-2", "FYE2023", "Fye2023", "FYE 2023"),
                    ["LastTIN200Year"] = PickOptional("LastTIN200Year", "Last TIN200 Year"),
                    ["Test"] = PickOptional("Test")
                };
            }
            finally
            {
                if (shouldClose)
                {
                    await db.CloseAsync();
                }
            }
        }

        private static string? GetString(IDataRecord record, string name)
        {
            var ordinal = record.GetOrdinal(name);
            return record.IsDBNull(ordinal) ? null : record.GetString(ordinal);
        }

        private static int GetInt32(IDataRecord record, string name)
        {
            var ordinal = record.GetOrdinal(name);
            return record.GetInt32(ordinal);
        }

        private static int? GetNullableInt32(IDataRecord record, string name)
        {
            var ordinal = record.GetOrdinal(name);
            if (record.IsDBNull(ordinal))
            {
                return null;
            }

            return Convert.ToInt32(record.GetValue(ordinal));
        }

        private static decimal? GetDecimal(IDataRecord record, string name)
        {
            var ordinal = record.GetOrdinal(name);
            if (record.IsDBNull(ordinal))
            {
                return null;
            }

            return Convert.ToDecimal(record.GetValue(ordinal));
        }

        private static bool GetBoolean(IDataRecord record, string name)
        {
            var ordinal = record.GetOrdinal(name);
            if (record.IsDBNull(ordinal))
            {
                return false;
            }

            return Convert.ToBoolean(record.GetValue(ordinal));
        }
    }

    public class Tin200Service : CompanyService
    {
        public Tin200Service(ApplicationDbContext context, ILogger<CompanyService> logger)
            : base(context, logger)
        {
        }

        public Task<List<Tin200>> GetAllTin200Async(int? lastTin200Year = null) => GetAllCompaniesAsync(lastTin200Year);
        public Task<Tin200?> GetTin200ByIdAsync(int id) => GetCompanyByIdAsync(id);
        public Task<Tin200> CreateTin200Async(Tin200 tin200) => CreateCompanyAsync(tin200);
        public Task<Tin200> UpdateTin200Async(Tin200 tin200) => UpdateCompanyAsync(tin200);
        public Task DeleteTin200Async(int id) => DeleteCompanyAsync(id);
        public Task<bool> Tin200ExistsAsync(int id) => CompanyExistsAsync(id);
    }
}
