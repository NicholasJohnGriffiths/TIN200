using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
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
                return await query.OrderByDescending(t => t.Id).ToListAsync();
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
            const string titleLastFinancialYear = "Total Revenue Last Financial Year";
            const string titleYearMinus1 = "Total Revenue Year-1";
            const string titleYearMinus2 = "Total Revenue Year-2";

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

            var revenueTitles = new[]
            {
                titleLastFinancialYear,
                titleYearMinus1,
                titleYearMinus2
            };

            var revenueQuestionIds = await _context.Question
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

            var matchedRevenueQuestions = revenueQuestionIds
                .Select(q => new
                {
                    q.Id,
                    NormalizedTitle = (q.Title ?? string.Empty).Trim()
                })
                .Where(q => revenueTitles.Any(t => string.Equals(t, q.NormalizedTitle, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var matchedRevenueQuestionIds = matchedRevenueQuestions
                .Select(q => q.Id)
                .Distinct()
                .ToList();

            var questionIdToFyeField = matchedRevenueQuestions
                .ToDictionary(
                    q => q.Id,
                    q => string.Equals(q.NormalizedTitle, titleLastFinancialYear, StringComparison.OrdinalIgnoreCase)
                        ? "FYE2025"
                        : string.Equals(q.NormalizedTitle, titleYearMinus1, StringComparison.OrdinalIgnoreCase)
                            ? "FYE2024"
                            : "FYE2023");

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

            var answerRows = await (
                from companySurvey in _context.CompanySurvey.AsNoTracking()
                join answer in _context.Answer.AsNoTracking() on companySurvey.Id equals answer.CompanySurveyId
                where companySurvey.SurveyId == currentSurveyId.Value
                    && matchedRevenueQuestionIds.Contains(answer.QuestionId)
                select new
                {
                    companySurvey.CompanyId,
                    answer.QuestionId,
                    answer.Id,
                    answer.AnswerCurrency,
                    answer.AnswerNumber,
                    answer.AnswerText
                })
                .ToListAsync();

            var latestAnswerByCompanyAndField = answerRows
                .Where(x => questionIdToFyeField.ContainsKey(x.QuestionId))
                .Select(x => new
                {
                    x.CompanyId,
                    FyeField = questionIdToFyeField[x.QuestionId],
                    x.Id,
                    x.AnswerCurrency,
                    x.AnswerNumber,
                    x.AnswerText
                })
                .GroupBy(x => new { x.CompanyId, x.FyeField })
                .ToDictionary(
                    g => (g.Key.CompanyId, g.Key.FyeField),
                    g => g.OrderByDescending(x => x.Id).First());

            var companyIds = latestAnswerByCompanyAndField.Keys
                .Select(x => x.CompanyId)
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
            var changedRows = new List<ResetFyePreviewRow>();

            foreach (var company in companies)
            {
                latestAnswerByCompanyAndField.TryGetValue((company.Id, "FYE2025"), out var lastFinancialYearAnswer);
                latestAnswerByCompanyAndField.TryGetValue((company.Id, "FYE2024"), out var yearMinus1Answer);
                latestAnswerByCompanyAndField.TryGetValue((company.Id, "FYE2023"), out var yearMinus2Answer);

                var newFye2025 = ParseRevenueAnswer(lastFinancialYearAnswer?.AnswerCurrency, lastFinancialYearAnswer?.AnswerNumber, lastFinancialYearAnswer?.AnswerText);
                var newFye2024 = ParseRevenueAnswer(yearMinus1Answer?.AnswerCurrency, yearMinus1Answer?.AnswerNumber, yearMinus1Answer?.AnswerText);
                var newFye2023 = ParseRevenueAnswer(yearMinus2Answer?.AnswerCurrency, yearMinus2Answer?.AnswerNumber, yearMinus2Answer?.AnswerText);

                var hasChanges = company.Fye2025 != newFye2025 || company.Fye2024 != newFye2024 || company.Fye2023 != newFye2023;

                if (hasChanges)
                {
                    changedRows.Add(new ResetFyePreviewRow
                    {
                        CompanyId = company.Id,
                        CompanyName = company.CompanyName,
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
                    company.Fye2025 = newFye2025;
                    company.Fye2024 = newFye2024;
                    company.Fye2023 = newFye2023;
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
            public int UpdatedCompanyCount { get; set; }
            public int WouldUpdateCompanyCount { get; set; }
            public List<ResetFyePreviewRow> PreviewRows { get; set; } = new();
        }

        public class ResetFyePreviewRow
        {
            public int CompanyId { get; set; }
            public string? CompanyName { get; set; }
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
    [{map["ExternalIdImportColumnName"]}] AS ExternalIdImportColumnName,
    [{map["CompanyNameImportColumnName"]}] AS CompanyNameImportColumnName,
    [{map["CompanyDescriptionImportColumnName"]}] AS CompanyDescriptionImportColumnName,
    [{map["Fye2025"]}] AS Fye2025,
    [{map["Fye2024"]}] AS Fye2024,
    [{map["Fye2023"]}] AS Fye2023,
    [{map["LastTIN200Year"]}] AS LastTIN200Year
FROM [Company]";

                if (lastTin200Year.HasValue)
                {
                    sql += " WHERE [" + map["LastTIN200Year"] + "] = @lastTin200Year";
                    var yearParameter = cmd.CreateParameter();
                    yearParameter.ParameterName = "@lastTin200Year";
                    yearParameter.Value = lastTin200Year.Value;
                    cmd.Parameters.Add(yearParameter);
                }

                sql += $" ORDER BY [{map["Id"]}] DESC";
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
                        LastTIN200Year = GetNullableInt32(reader, "LastTIN200Year")
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

                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Id"] = Pick("Id"),
                    ["CeoFirstName"] = Pick("CEOFirstName", "CeoFirstName", "CEO First Name", "CEO First Name "),
                    ["CeoLastName"] = Pick("CEOLastName", "CeoLastName", "CEO Last Name", "CEO Last Name "),
                    ["Email"] = Pick("Email", "Email "),
                    ["ExternalId"] = Pick("ExternalID", "ExternalId", "External ID"),
                    ["CompanyName"] = Pick("CompanyName", "Company Name"),
                    ["CompanyDescription"] = Pick("CompanyDescription", "Company Description"),
                    ["ExternalIdImportColumnName"] = Pick("ExternalId_ImportColumnName", "External ID Import Column Name"),
                    ["CompanyNameImportColumnName"] = Pick("CompanyName_ImportColumnName", "Company Name Import Column Name"),
                    ["CompanyDescriptionImportColumnName"] = Pick("CompanyDescription_ImportColumnName", "Company Description Import Column Name"),
                    ["Fye2025"] = Pick("FYE2025", "Fye2025", "FYE 2025"),
                    ["Fye2024"] = Pick("FYE2024", "Fye2024", "FYE 2024"),
                    ["Fye2023"] = Pick("FYE2023", "Fye2023", "FYE 2023"),
                    ["LastTIN200Year"] = Pick("LastTIN200Year", "Last TIN200 Year")
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
