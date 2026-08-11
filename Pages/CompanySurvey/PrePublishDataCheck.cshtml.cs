using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using System.Globalization;
using System.Text.RegularExpressions;
using TINWeb.Data;
using TINWeb.Models;

namespace TINWeb.Pages.CompanySurvey
{
    public class PrePublishDataCheckModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public List<ColumnDefinition> Columns { get; } = BuildColumns();
        public List<PrePublishDataRow> Records { get; set; } = new();
        public List<(int Value, string Label)> TinStatusOptions { get; } = TinStatusHelper.DropdownOptions.ToList();
        public RevenueAdjustPreviewResult? PreviewResult { get; set; }
        public PopulateOwnershipFormedPreviewResult? PopulatePreviewResult { get; set; }
        public EstimatedImportPreviewResult? EstimatedImportPreview { get; set; }

        [BindProperty(SupportsGet = true)]
        public int SelectedTinStatus { get; set; } = (int)TinStatus.Tin200;

        [BindProperty]
        public int RevenueDecimalPlacesAdjustment { get; set; }

        [BindProperty]
        public List<int> SelectedCompanyIds { get; set; } = new();

        [TempData]
        public string? StatusMessage { get; set; }

        public PrePublishDataCheckModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task OnGetAsync()
        {
            SelectedTinStatus = NormalizeTinStatusFilter(SelectedTinStatus);

            await LoadRowsAsync();
        }

        public async Task<IActionResult> OnPostPreviewAdjustRevenueAsync()
        {
            SelectedTinStatus = NormalizeTinStatusFilter(SelectedTinStatus);
            await LoadRowsAsync();

            var selectedCompanyIds = ResolveSelectedCompanyIds(SelectedCompanyIds, Records);
            SelectedCompanyIds = selectedCompanyIds;

            if (selectedCompanyIds.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Select at least one company row first.");
                return Page();
            }

            if (RevenueDecimalPlacesAdjustment == 0)
            {
                ModelState.AddModelError(nameof(RevenueDecimalPlacesAdjustment), "Enter a non-zero decimal place adjustment.");
                return Page();
            }

            PreviewResult = await BuildRevenueAdjustPreviewAsync(selectedCompanyIds, RevenueDecimalPlacesAdjustment, applyUpdates: false);
            return Page();
        }

        public async Task<IActionResult> OnPostApplyAdjustRevenueAsync()
        {
            SelectedTinStatus = NormalizeTinStatusFilter(SelectedTinStatus);
            await LoadRowsAsync();

            var selectedCompanyIds = ResolveSelectedCompanyIds(SelectedCompanyIds, Records);

            if (selectedCompanyIds.Count == 0)
            {
                StatusMessage = "Error: Select at least one company row first.";
                return RedirectToPage(new { selectedTinStatus = SelectedTinStatus });
            }

            if (RevenueDecimalPlacesAdjustment == 0)
            {
                StatusMessage = "Error: Enter a non-zero decimal place adjustment.";
                return RedirectToPage(new { selectedTinStatus = SelectedTinStatus });
            }

            var result = await BuildRevenueAdjustPreviewAsync(selectedCompanyIds, RevenueDecimalPlacesAdjustment, applyUpdates: true);

            StatusMessage = $"Revenue update applied. Selected companies: {result.SelectedCompanyCount}. Revenue answers updated: {result.ChangedCount}. Decimal place adjustment: {RevenueDecimalPlacesAdjustment}.";
            return RedirectToPage(new { selectedTinStatus = SelectedTinStatus });
        }

        public async Task<IActionResult> OnPostPreviewPopulateOwnershipFormedAsync()
        {
            SelectedTinStatus = NormalizeTinStatusFilter(SelectedTinStatus);
            await LoadRowsAsync();

            var selectedCompanyIds = ResolveSelectedCompanyIds(SelectedCompanyIds, Records);
            SelectedCompanyIds = selectedCompanyIds;

            if (selectedCompanyIds.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Select at least one company row first.");
                return Page();
            }

            PopulatePreviewResult = await BuildPopulateOwnershipFormedPreviewAsync(selectedCompanyIds, applyUpdates: false);
            return Page();
        }

        public async Task<IActionResult> OnPostApplyPopulateOwnershipFormedAsync()
        {
            SelectedTinStatus = NormalizeTinStatusFilter(SelectedTinStatus);
            await LoadRowsAsync();

            var selectedCompanyIds = ResolveSelectedCompanyIds(SelectedCompanyIds, Records);

            if (selectedCompanyIds.Count == 0)
            {
                StatusMessage = "Error: Select at least one company row first.";
                return RedirectToPage(new { selectedTinStatus = SelectedTinStatus });
            }

            var result = await BuildPopulateOwnershipFormedPreviewAsync(selectedCompanyIds, applyUpdates: true);
            StatusMessage = $"Populate Missing Data From Prev Year applied. Selected companies: {result.SelectedCompanyCount}. Answers populated: {result.ChangedCount}.";
            return RedirectToPage(new { selectedTinStatus = SelectedTinStatus });
        }

        public async Task<IActionResult> OnPostPreviewEstimatedImportAsync(IFormFile? importFile)
        {
            SelectedTinStatus = NormalizeTinStatusFilter(SelectedTinStatus);
            await LoadRowsAsync();

            var selectedCompanyIds = ResolveSelectedCompanyIds(SelectedCompanyIds, Records);
            SelectedCompanyIds = selectedCompanyIds;

            if (selectedCompanyIds.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Select at least one company row first.");
                return Page();
            }

            if (importFile == null || importFile.Length == 0)
            {
                ModelState.AddModelError(string.Empty, "Select an Excel file (.xlsx) to preview.");
                return Page();
            }

            if (!importFile.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, "Only .xlsx Excel files are supported.");
                return Page();
            }

            await using var stream = importFile.OpenReadStream();
            EstimatedImportPreview = await BuildEstimatedImportPreviewAsync(selectedCompanyIds, stream, applyUpdates: false);
            return Page();
        }

        public async Task<IActionResult> OnPostApplyEstimatedImportAsync(IFormFile? importFile)
        {
            SelectedTinStatus = NormalizeTinStatusFilter(SelectedTinStatus);
            await LoadRowsAsync();

            var selectedCompanyIds = ResolveSelectedCompanyIds(SelectedCompanyIds, Records);

            if (selectedCompanyIds.Count == 0)
            {
                StatusMessage = "Error: Select at least one company row first.";
                return RedirectToPage(new { selectedTinStatus = SelectedTinStatus });
            }

            if (importFile == null || importFile.Length == 0)
            {
                StatusMessage = "Error: Select an Excel file (.xlsx) to import.";
                return RedirectToPage(new { selectedTinStatus = SelectedTinStatus });
            }

            if (!importFile.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = "Error: Only .xlsx Excel files are supported.";
                return RedirectToPage(new { selectedTinStatus = SelectedTinStatus });
            }

            await using var stream = importFile.OpenReadStream();
            var result = await BuildEstimatedImportPreviewAsync(selectedCompanyIds, stream, applyUpdates: true);
            StatusMessage = $"Estimated import applied. Selected companies: {result.SelectedCompanyCount}. Fields updated: {result.ChangedCount}. Rows without match: {result.UnmatchedCompanyCount}.";
            return RedirectToPage(new { selectedTinStatus = SelectedTinStatus });
        }

        private static List<int> ResolveSelectedCompanyIds(IEnumerable<int>? selectedCompanyIds, IEnumerable<PrePublishDataRow> records)
        {
            var validIds = records.Select(x => x.CompanyId).ToHashSet();

            return (selectedCompanyIds ?? Enumerable.Empty<int>())
                .Where(id => validIds.Contains(id))
                .Distinct()
                .ToList();
        }

        private async Task<RevenueAdjustPreviewResult> BuildRevenueAdjustPreviewAsync(IReadOnlyCollection<int> selectedCompanyIds, int decimalPlacesAdjustment, bool applyUpdates)
        {
            var result = new RevenueAdjustPreviewResult
            {
                SelectedCompanyCount = selectedCompanyIds.Count,
                DecimalPlacesAdjustment = decimalPlacesAdjustment
            };

            var latestCompanySurveyIds = await (
                from companySurvey in _context.CompanySurvey
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                where selectedCompanyIds.Contains(companySurvey.CompanyId)
                select new
                {
                    companySurvey.CompanyId,
                    companySurvey.Id,
                    survey.CurrentSurvey,
                    survey.FinancialYear
                })
                .ToListAsync();

            var targetCompanySurveyIds = latestCompanySurveyIds
                .GroupBy(x => x.CompanyId)
                .Select(g => g
                    .OrderByDescending(x => x.CurrentSurvey)
                    .ThenByDescending(x => x.FinancialYear)
                    .ThenByDescending(x => x.Id)
                    .Select(x => x.Id)
                    .First())
                .ToHashSet();

            if (targetCompanySurveyIds.Count == 0)
            {
                return result;
            }

            var candidates = await (
                from answer in _context.Answer
                join question in _context.Question on answer.QuestionId equals question.Id
                join companySurvey in _context.CompanySurvey on answer.CompanySurveyId equals companySurvey.Id
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                join company in _context.Tin200 on companySurvey.CompanyId equals company.Id
                where targetCompanySurveyIds.Contains(companySurvey.Id)
                select new
                {
                    Answer = answer,
                    QuestionTitle = question.Title,
                    QuestionText = question.QuestionText,
                    question.ImportColumnName,
                    question.ImportColumnNameAlt,
                    CompanyName = company.CompanyName,
                    SurveyYear = survey.FinancialYear
                })
                .ToListAsync();

            var previewRows = new List<RevenueAdjustPreviewRow>();

            foreach (var candidate in candidates)
            {
                if (!IsRevenueQuestion(candidate.QuestionTitle, candidate.QuestionText, candidate.ImportColumnName, candidate.ImportColumnNameAlt))
                {
                    continue;
                }

                var hasCurrency = candidate.Answer.AnswerCurrency.HasValue;
                var hasNumber = candidate.Answer.AnswerNumber.HasValue;
                if (!hasCurrency && !hasNumber)
                {
                    continue;
                }

                var oldCurrency = candidate.Answer.AnswerCurrency;
                var oldNumber = candidate.Answer.AnswerNumber;
                var newCurrency = hasCurrency
                    ? ShiftDecimalPlaces(candidate.Answer.AnswerCurrency!.Value, decimalPlacesAdjustment)
                    : (decimal?)null;
                var newNumber = hasNumber
                    ? ShiftDecimalPlaces(candidate.Answer.AnswerNumber!.Value, decimalPlacesAdjustment)
                    : (double?)null;

                var isChanged = (oldCurrency != newCurrency) || (oldNumber != newNumber);
                if (!isChanged)
                {
                    continue;
                }

                result.TotalRevenueAnswersFound++;
                result.ChangedCount++;

                if (previewRows.Count < 200)
                {
                    var fieldType = hasCurrency ? "Currency" : "Number";
                    var oldDisplay = hasCurrency
                        ? oldCurrency?.ToString("N2", CultureInfo.InvariantCulture) ?? string.Empty
                        : oldNumber?.ToString("N2", CultureInfo.InvariantCulture) ?? string.Empty;
                    var newDisplay = hasCurrency
                        ? newCurrency?.ToString("N2", CultureInfo.InvariantCulture) ?? string.Empty
                        : newNumber?.ToString("N2", CultureInfo.InvariantCulture) ?? string.Empty;

                    previewRows.Add(new RevenueAdjustPreviewRow
                    {
                        CompanyName = string.IsNullOrWhiteSpace(candidate.CompanyName) ? "(No company name)" : candidate.CompanyName.Trim(),
                        SurveyYear = candidate.SurveyYear,
                        QuestionTitle = candidate.QuestionTitle ?? candidate.ImportColumnName ?? candidate.ImportColumnNameAlt ?? "Revenue Question",
                        FieldType = fieldType,
                        OldValue = oldDisplay,
                        NewValue = newDisplay
                    });
                }

                if (applyUpdates)
                {
                    candidate.Answer.AnswerCurrency = newCurrency;
                    candidate.Answer.AnswerNumber = newNumber;
                }
            }

            result.PreviewRows = previewRows;

            if (applyUpdates && result.ChangedCount > 0)
            {
                await _context.SaveChangesAsync();
            }

            return result;
        }

        private async Task<PopulateOwnershipFormedPreviewResult> BuildPopulateOwnershipFormedPreviewAsync(IReadOnlyCollection<int> selectedCompanyIds, bool applyUpdates)
        {
            var result = new PopulateOwnershipFormedPreviewResult
            {
                SelectedCompanyCount = selectedCompanyIds.Count
            };

            var companySurveyRows = await (
                from companySurvey in _context.CompanySurvey
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                join company in _context.Tin200 on companySurvey.CompanyId equals company.Id
                where selectedCompanyIds.Contains(companySurvey.CompanyId)
                select new
                {
                    companySurvey.CompanyId,
                    companySurvey.Id,
                    survey.CurrentSurvey,
                    survey.FinancialYear,
                    CompanyName = company.CompanyName
                })
                .ToListAsync();

            var latestByCompany = companySurveyRows
                .GroupBy(x => x.CompanyId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.CurrentSurvey)
                        .ThenByDescending(x => x.FinancialYear)
                        .ThenByDescending(x => x.Id)
                        .ToList());

            var companyPairs = latestByCompany
                .Select(x => new
                {
                    CompanyId = x.Key,
                    Latest = x.Value.ElementAtOrDefault(0),
                    Previous = x.Value.ElementAtOrDefault(1)
                })
                .Where(x => x.Latest != null && x.Previous != null)
                .ToList();

            if (companyPairs.Count == 0)
            {
                return result;
            }

            var targetQuestionRows = await _context.Question
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

            var targetQuestionIds = targetQuestionRows
                .Where(q => IsOwnershipFormedDescriptionQuestion(q.Title, q.QuestionText, q.ImportColumnName, q.ImportColumnNameAlt))
                .Select(q => q.Id)
                .ToHashSet();

            if (targetQuestionIds.Count == 0)
            {
                return result;
            }

            var latestSurveyIds = companyPairs.Select(x => x.Latest!.Id).ToHashSet();
            var previousSurveyIds = companyPairs.Select(x => x.Previous!.Id).ToHashSet();
            var allSurveyIds = latestSurveyIds.Concat(previousSurveyIds).Distinct().ToList();

            var answerRows = await _context.Answer
                .Where(a => allSurveyIds.Contains(a.CompanySurveyId) && targetQuestionIds.Contains(a.QuestionId))
                .ToListAsync();

            var answersBySurvey = answerRows
                .GroupBy(a => a.CompanySurveyId)
                .ToDictionary(
                    g => g.Key,
                    g => g.GroupBy(a => a.QuestionId).ToDictionary(ga => ga.Key, ga => ga.OrderByDescending(x => x.Id).First()));

            var questionLabelById = targetQuestionRows
                .Where(q => targetQuestionIds.Contains(q.Id))
                .ToDictionary(
                    q => q.Id,
                    q => string.IsNullOrWhiteSpace(q.Title)
                        ? (q.ImportColumnName ?? q.ImportColumnNameAlt ?? $"Question {q.Id}")
                        : q.Title!.Trim());

            var previewRows = new List<PopulateOwnershipFormedPreviewRow>();

            foreach (var pair in companyPairs)
            {
                var latestSurveyId = pair.Latest!.Id;
                var previousSurveyId = pair.Previous!.Id;

                answersBySurvey.TryGetValue(latestSurveyId, out var latestAnswers);
                latestAnswers ??= new Dictionary<int, Answer>();
                answersBySurvey.TryGetValue(previousSurveyId, out var previousAnswers);
                previousAnswers ??= new Dictionary<int, Answer>();

                foreach (var questionId in targetQuestionIds)
                {
                    previousAnswers.TryGetValue(questionId, out var sourceAnswer);
                    if (!HasAnswerValue(sourceAnswer))
                    {
                        continue;
                    }

                    latestAnswers.TryGetValue(questionId, out var targetAnswer);
                    if (HasAnswerValue(targetAnswer))
                    {
                        continue;
                    }

                    result.ChangedCount++;

                    if (previewRows.Count < 200)
                    {
                        previewRows.Add(new PopulateOwnershipFormedPreviewRow
                        {
                            CompanyName = string.IsNullOrWhiteSpace(pair.Latest.CompanyName) ? "(No company name)" : pair.Latest.CompanyName.Trim(),
                            LatestSurveyYear = pair.Latest.FinancialYear,
                            PreviousSurveyYear = pair.Previous.FinancialYear,
                            QuestionTitle = questionLabelById.TryGetValue(questionId, out var questionLabel) ? questionLabel : $"Question {questionId}",
                            NewValue = FormatAnswerValue(sourceAnswer)
                        });
                    }

                    if (applyUpdates)
                    {
                        if (targetAnswer == null)
                        {
                            var created = new Answer
                            {
                                CompanySurveyId = latestSurveyId,
                                QuestionId = questionId,
                                AnswerText = sourceAnswer!.AnswerText,
                                AnswerCurrency = sourceAnswer.AnswerCurrency,
                                AnswerNumber = sourceAnswer.AnswerNumber
                            };

                            _context.Answer.Add(created);
                            latestAnswers[questionId] = created;
                        }
                        else
                        {
                            targetAnswer.AnswerText = sourceAnswer!.AnswerText;
                            targetAnswer.AnswerCurrency = sourceAnswer.AnswerCurrency;
                            targetAnswer.AnswerNumber = sourceAnswer.AnswerNumber;
                        }
                    }
                }
            }

            result.PreviewRows = previewRows;

            if (applyUpdates && result.ChangedCount > 0)
            {
                await _context.SaveChangesAsync();
            }

            return result;
        }

        private static bool IsRevenueQuestion(params string?[] values)
        {
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var normalized = CanonicalizeKey(value);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                if (normalized.Contains("revenue", StringComparison.OrdinalIgnoreCase)
                    || normalized.StartsWith("fye", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("fyelastfinancialyear", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("fyeyear1", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("fyeyear2", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsOwnershipFormedDescriptionQuestion(params string?[] values)
        {
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var key = CanonicalizeKey(value);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (key.Contains("ownership", StringComparison.OrdinalIgnoreCase)
                    || key.Contains("formationyear", StringComparison.OrdinalIgnoreCase)
                    || key.Contains("yearofformation", StringComparison.OrdinalIgnoreCase)
                    || key.Contains("formed", StringComparison.OrdinalIgnoreCase)
                    || key.Contains("founded", StringComparison.OrdinalIgnoreCase)
                    || key.Contains("companydescription", StringComparison.OrdinalIgnoreCase)
                    || key.Contains("physicaladdress", StringComparison.OrdinalIgnoreCase)
                    || key.Contains("primarysector", StringComparison.OrdinalIgnoreCase)
                    || key.Contains("secondarysector", StringComparison.OrdinalIgnoreCase)
                    || key.Contains("businessdecision", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAnswerValue(Answer? answer)
        {
            if (answer == null)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(answer.AnswerText)
                || answer.AnswerCurrency.HasValue
                || answer.AnswerNumber.HasValue;
        }

        private static string FormatAnswerValue(Answer? answer)
        {
            if (answer == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(answer.AnswerText))
            {
                return answer.AnswerText.Trim();
            }

            if (answer.AnswerCurrency.HasValue)
            {
                return answer.AnswerCurrency.Value.ToString("N2", CultureInfo.InvariantCulture);
            }

            if (answer.AnswerNumber.HasValue)
            {
                return answer.AnswerNumber.Value.ToString("N2", CultureInfo.InvariantCulture);
            }

            return string.Empty;
        }

        private static decimal ShiftDecimalPlaces(decimal value, int places)
        {
            if (places == 0)
            {
                return value;
            }

            var factor = Pow10Decimal(Math.Abs(places));
            return places > 0 ? value * factor : value / factor;
        }

        private static double ShiftDecimalPlaces(double value, int places)
        {
            if (places == 0)
            {
                return value;
            }

            var factor = Math.Pow(10d, Math.Abs(places));
            return places > 0 ? value * factor : value / factor;
        }

        private static decimal Pow10Decimal(int exponent)
        {
            var factor = 1m;
            for (var i = 0; i < exponent; i++)
            {
                factor *= 10m;
            }

            return factor;
        }

        private async Task<EstimatedImportPreviewResult> BuildEstimatedImportPreviewAsync(IReadOnlyCollection<int> selectedCompanyIds, Stream excelStream, bool applyUpdates)
        {
            var result = new EstimatedImportPreviewResult
            {
                SelectedCompanyCount = selectedCompanyIds.Count
            };

            var importedRows = ParseEstimatedImportRows(excelStream, result.Warnings);
            result.RowsRead = importedRows.Count;
            if (importedRows.Count == 0)
            {
                return result;
            }

            var selectedCompanies = await _context.Tin200
                .Where(x => selectedCompanyIds.Contains(x.Id))
                .Select(x => new { x.Id, x.CompanyName })
                .ToListAsync();

            var companyLookup = selectedCompanies
                .Where(x => !string.IsNullOrWhiteSpace(x.CompanyName))
                .GroupBy(x => NormalizeCompanyName(x.CompanyName!))
                .ToDictionary(g => g.Key, g => g.Select(x => x.Id).Distinct().ToList(), StringComparer.OrdinalIgnoreCase);

            var matchedCompanyIds = new HashSet<int>();
            var companyMatches = new List<(EstimatedImportRow Row, int CompanyId)>();

            foreach (var row in importedRows)
            {
                var normalizedName = NormalizeCompanyName(row.CompanyName);
                if (string.IsNullOrWhiteSpace(normalizedName)
                    || !companyLookup.TryGetValue(normalizedName, out var companyIds)
                    || companyIds.Count == 0)
                {
                    result.UnmatchedCompanyCount++;
                    continue;
                }

                if (companyIds.Count > 1)
                {
                    result.Warnings.Add($"Skipped '{row.CompanyName}': multiple selected companies share this name.");
                    continue;
                }

                var companyId = companyIds[0];
                matchedCompanyIds.Add(companyId);
                companyMatches.Add((row, companyId));
            }

            result.MatchedCompanyCount = matchedCompanyIds.Count;
            if (companyMatches.Count == 0)
            {
                return result;
            }

            var latestSurveyRows = await (
                from cs in _context.CompanySurvey
                join s in _context.Survey on cs.SurveyId equals s.Id
                where matchedCompanyIds.Contains(cs.CompanyId)
                select new
                {
                    cs.CompanyId,
                    CompanySurveyId = cs.Id,
                    s.CurrentSurvey,
                    s.FinancialYear
                })
                .ToListAsync();

            var latestSurveyByCompany = latestSurveyRows
                .GroupBy(x => x.CompanyId)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderByDescending(x => x.CurrentSurvey)
                        .ThenByDescending(x => x.FinancialYear)
                        .ThenByDescending(x => x.CompanySurveyId)
                        .First());

            var questionRows = await _context.Question
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

            var revenueQuestionId = questionRows
                .Where(q => IsRevenueLastFinancialYearQuestion(q.Title, q.QuestionText, q.ImportColumnName, q.ImportColumnNameAlt))
                .Select(q => q.Id)
                .FirstOrDefault();

            var employmentQuestionId = questionRows
                .Where(q => IsEmploymentLastFinancialYearQuestion(q.Title, q.QuestionText, q.ImportColumnName, q.ImportColumnNameAlt))
                .Select(q => q.Id)
                .FirstOrDefault();

            if (revenueQuestionId <= 0)
            {
                result.Warnings.Add("Could not find question mapping for Total Revenue Last Financial Year.");
            }

            if (employmentQuestionId <= 0)
            {
                result.Warnings.Add("Could not find question mapping for Total Employment Last Financial Year.");
            }

            var targetSurveyIds = latestSurveyByCompany.Values
                .Select(x => x.CompanySurveyId)
                .Distinct()
                .ToList();

            if (targetSurveyIds.Count == 0)
            {
                result.Warnings.Add("No latest surveys found for matched selected companies.");
                return result;
            }

            var trackedAnswers = await _context.Answer
                .Where(a => targetSurveyIds.Contains(a.CompanySurveyId)
                    && ((revenueQuestionId > 0 && a.QuestionId == revenueQuestionId)
                        || (employmentQuestionId > 0 && a.QuestionId == employmentQuestionId)))
                .ToListAsync();

            var answerLookup = trackedAnswers
                .GroupBy(a => (a.CompanySurveyId, a.QuestionId))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());

            var companyNameById = selectedCompanies.ToDictionary(
                x => x.Id,
                x => string.IsNullOrWhiteSpace(x.CompanyName) ? "(No company name)" : x.CompanyName!.Trim());

            foreach (var match in companyMatches)
            {
                if (!latestSurveyByCompany.TryGetValue(match.CompanyId, out var latestSurvey))
                {
                    result.Warnings.Add($"Skipped '{match.Row.CompanyName}': no latest survey record found.");
                    continue;
                }

                if (match.Row.RevenueEstimated && match.Row.RevenueValue.HasValue && revenueQuestionId > 0)
                {
                    ProcessEstimatedField(
                        result,
                        answerLookup,
                        latestSurvey.CompanySurveyId,
                        latestSurvey.FinancialYear,
                        revenueQuestionId,
                        companyNameById.GetValueOrDefault(match.CompanyId, match.Row.CompanyName),
                        "Total Revenue Last Financial Year",
                        match.Row.RevenueValue.Value,
                        isRevenue: true,
                        applyUpdates: applyUpdates);
                }

                if (match.Row.EmployeesEstimated && match.Row.EmployeesValue.HasValue && employmentQuestionId > 0)
                {
                    ProcessEstimatedField(
                        result,
                        answerLookup,
                        latestSurvey.CompanySurveyId,
                        latestSurvey.FinancialYear,
                        employmentQuestionId,
                        companyNameById.GetValueOrDefault(match.CompanyId, match.Row.CompanyName),
                        "Total Employment Last Financial Year",
                        match.Row.EmployeesValue.Value,
                        isRevenue: false,
                        applyUpdates: applyUpdates);
                }
            }

            if (applyUpdates && result.ChangedCount > 0)
            {
                await _context.SaveChangesAsync();
            }

            return result;
        }

        private void ProcessEstimatedField(
            EstimatedImportPreviewResult result,
            Dictionary<(int CompanySurveyId, int QuestionId), Answer> answerLookup,
            int companySurveyId,
            int latestSurveyYear,
            int questionId,
            string companyName,
            string fieldName,
            decimal importedValue,
            bool isRevenue,
            bool applyUpdates)
        {
            answerLookup.TryGetValue((companySurveyId, questionId), out var targetAnswer);
            if (HasAnswerValue(targetAnswer))
            {
                return;
            }

            var oldValue = FormatAnswerValue(targetAnswer);
            var newValue = importedValue.ToString("N2", CultureInfo.InvariantCulture);
            result.ChangedCount++;

            if (result.PreviewRows.Count < 200)
            {
                result.PreviewRows.Add(new EstimatedImportPreviewRow
                {
                    CompanyName = companyName,
                    LatestSurveyYear = latestSurveyYear,
                    FieldName = fieldName,
                    OldValue = oldValue,
                    NewValue = newValue
                });
            }

            if (!applyUpdates)
            {
                return;
            }

            if (targetAnswer == null)
            {
                targetAnswer = new Answer
                {
                    CompanySurveyId = companySurveyId,
                    QuestionId = questionId
                };

                _context.Answer.Add(targetAnswer);
                answerLookup[(companySurveyId, questionId)] = targetAnswer;
            }

            targetAnswer.AnswerText = null;
            if (isRevenue)
            {
                targetAnswer.AnswerCurrency = importedValue;
                targetAnswer.AnswerNumber = null;
            }
            else
            {
                targetAnswer.AnswerCurrency = null;
                targetAnswer.AnswerNumber = (double)importedValue;
            }
        }

        private static List<EstimatedImportRow> ParseEstimatedImportRows(Stream excelStream, ICollection<string> warnings)
        {
            using var workbook = new XLWorkbook(excelStream);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
            {
                warnings.Add("The workbook does not contain any worksheets.");
                return new List<EstimatedImportRow>();
            }

            var usedRange = worksheet.RangeUsed();
            if (usedRange == null)
            {
                warnings.Add("The worksheet is empty.");
                return new List<EstimatedImportRow>();
            }

            var headerRowNumber = usedRange.FirstRow().RowNumber();
            var lastRowNumber = usedRange.LastRow().RowNumber();
            var headerCells = worksheet.Row(headerRowNumber).CellsUsed();

            var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in headerCells)
            {
                var headerText = NormalizeTemplateKey(cell.GetString());
                if (string.IsNullOrWhiteSpace(headerText))
                {
                    continue;
                }

                var canonical = CanonicalizeKey(headerText);
                if (string.IsNullOrWhiteSpace(canonical) || headerMap.ContainsKey(canonical))
                {
                    continue;
                }

                headerMap[canonical] = cell.Address.ColumnNumber;
            }

            var companyNameCol = FindHeaderColumn(headerMap, "companyname");
            var revenueCol = FindHeaderColumn(headerMap, "revenue000", "revenue");
            var revenueEstimatedCol = FindHeaderColumn(headerMap, "revenueestimated");
            var employeesCol = FindHeaderColumn(headerMap, "employees", "employee");
            var employeesEstimatedCol = FindHeaderColumn(headerMap, "employeesestimated", "employeeestimated");

            if (companyNameCol <= 0 || revenueCol <= 0 || revenueEstimatedCol <= 0 || employeesCol <= 0 || employeesEstimatedCol <= 0)
            {
                warnings.Add("Missing required headers. Expected: Company Name, Revenue ($000), Revenue Estimated, Employees, Employees Estimated.");
                return new List<EstimatedImportRow>();
            }

            var rows = new List<EstimatedImportRow>();

            for (var rowNumber = headerRowNumber + 1; rowNumber <= lastRowNumber; rowNumber++)
            {
                var row = worksheet.Row(rowNumber);
                var companyName = NormalizeTemplateKey(row.Cell(companyNameCol).GetString());
                if (string.IsNullOrWhiteSpace(companyName))
                {
                    continue;
                }

                var revenueEstimated = IsEstimatedFlag(row.Cell(revenueEstimatedCol).GetString());
                var employeesEstimated = IsEstimatedFlag(row.Cell(employeesEstimatedCol).GetString());
                if (!revenueEstimated && !employeesEstimated)
                {
                    continue;
                }

                var revenueRaw = row.Cell(revenueCol).GetString();
                var employeesRaw = row.Cell(employeesCol).GetString();

                var revenueValue = ParseNullableDecimal(revenueRaw);
                var employeesValue = ParseNullableDecimal(employeesRaw);

                if (revenueEstimated && !revenueValue.HasValue)
                {
                    warnings.Add($"Row {rowNumber} ({companyName}): Revenue Estimated is set but Revenue ($000) is not numeric.");
                }

                if (employeesEstimated && !employeesValue.HasValue)
                {
                    warnings.Add($"Row {rowNumber} ({companyName}): Employees Estimated is set but Employees is not numeric.");
                }

                rows.Add(new EstimatedImportRow
                {
                    CompanyName = companyName,
                    RevenueEstimated = revenueEstimated,
                    RevenueValue = revenueValue,
                    EmployeesEstimated = employeesEstimated,
                    EmployeesValue = employeesValue
                });
            }

            return rows;
        }

        private static int FindHeaderColumn(IReadOnlyDictionary<string, int> headerMap, params string[] canonicalCandidates)
        {
            foreach (var candidate in canonicalCandidates)
            {
                if (headerMap.TryGetValue(candidate, out var col))
                {
                    return col;
                }
            }

            return 0;
        }

        private static bool IsEstimatedFlag(string? raw)
        {
            return string.Equals(NormalizeTemplateKey(raw), "Estimated", StringComparison.OrdinalIgnoreCase);
        }

        private static decimal? ParseNullableDecimal(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var cleaned = raw.Trim()
                .Replace("$", string.Empty, StringComparison.Ordinal)
                .Replace(",", string.Empty, StringComparison.Ordinal);

            if (decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static string NormalizeCompanyName(string raw)
        {
            return NormalizeTemplateKey(raw).Trim();
        }

        private static bool IsRevenueLastFinancialYearQuestion(params string?[] values)
        {
            foreach (var value in values)
            {
                var canonical = CanonicalizeKey(value);
                if (string.IsNullOrWhiteSpace(canonical))
                {
                    continue;
                }

                if (canonical == "totalrevenuelastfinancialyear"
                    || canonical == "totalrevenuelastfinancialyear000"
                    || canonical == "revenuelastfinancialyear"
                    || canonical == "revenuelastfinancialyear000")
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsEmploymentLastFinancialYearQuestion(params string?[] values)
        {
            foreach (var value in values)
            {
                var canonical = CanonicalizeKey(value);
                if (string.IsNullOrWhiteSpace(canonical))
                {
                    continue;
                }

                if (canonical == "totalemploymentlastfinancialyear"
                    || canonical == "totalemplymentlastfinancialyear"
                    || canonical == "employmentlastfinancialyear"
                    || canonical == "staffemployedlastfinancialyear")
                {
                    return true;
                }
            }

            return false;
        }

        private async Task LoadRowsAsync()
        {
            var companies = await _context.Tin200
                .AsNoTracking()
                .Where(x => (x.TinStatus ?? (int)TinStatus.Tin200) == SelectedTinStatus)
                .Select(x => new
                {
                    x.Id,
                    x.CompanyName,
                    x.CompanyDescription,
                    x.CeoFirstName,
                    x.CeoLastName,
                    x.Fye2025,
                    x.Fye2024,
                    x.Website,
                    x.Phone,
                    x.AddStreet,
                    x.AddSuburb,
                    x.AddCity,
                    x.AddPostcode
                })
                .OrderBy(x => x.CompanyName)
                .ThenBy(x => x.Id)
                .ToListAsync();

            if (companies.Count == 0)
            {
                Records = new List<PrePublishDataRow>();
                return;
            }

            var companyIds = companies.Select(x => x.Id).ToList();

            var historyRows = await (
                from cs in _context.CompanySurvey.AsNoTracking()
                join s in _context.Survey.AsNoTracking() on cs.SurveyId equals s.Id
                where companyIds.Contains(cs.CompanyId)
                orderby cs.CompanyId, s.CurrentSurvey descending, s.FinancialYear descending, cs.Id descending
                select new
                {
                    cs.CompanyId,
                    CompanySurveyId = (int?)cs.Id,
                    s.FinancialYear,
                    cs.Estimate
                })
                .ToListAsync();

            var latestSurveyInfoByCompanyId = historyRows
                .GroupBy(x => x.CompanyId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var latest = g.FirstOrDefault();
                        var previous = latest == null
                            ? null
                            : g.FirstOrDefault(x => x.FinancialYear < latest.FinancialYear);

                        return new
                        {
                            Latest = latest,
                            Previous = previous
                        };
                    });

            var companySurveyIds = latestSurveyInfoByCompanyId.Values
                .SelectMany(x => new[] { x.Latest?.CompanySurveyId, x.Previous?.CompanySurveyId })
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            var answersByCompanySurveyId = new Dictionary<int, Dictionary<string, string>>();

            if (companySurveyIds.Count > 0)
            {
                var answerRows = await (
                    from a in _context.Answer.AsNoTracking()
                    join q in _context.Question.AsNoTracking() on a.QuestionId equals q.Id
                    where companySurveyIds.Contains(a.CompanySurveyId) && q.Title != null
                    orderby a.CompanySurveyId, a.Id descending
                    select new
                    {
                        a.CompanySurveyId,
                        QuestionTitle = q.Title!,
                        a.AnswerText,
                        a.AnswerCurrency,
                        a.AnswerNumber
                    })
                    .ToListAsync();

                foreach (var row in answerRows)
                {
                    if (!answersByCompanySurveyId.TryGetValue(row.CompanySurveyId, out var answerMap))
                    {
                        answerMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        answersByCompanySurveyId[row.CompanySurveyId] = answerMap;
                    }

                    var normalizedTitle = NormalizeTemplateKey(row.QuestionTitle);
                    if (string.IsNullOrWhiteSpace(normalizedTitle) || answerMap.ContainsKey(normalizedTitle))
                    {
                        continue;
                    }

                    var value = ResolveAnswerValue(row.QuestionTitle, row.AnswerText, row.AnswerCurrency, row.AnswerNumber);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        answerMap[normalizedTitle] = value;
                    }
                }
            }

            var records = new List<PrePublishDataRow>();

            foreach (var company in companies)
            {
                latestSurveyInfoByCompanyId.TryGetValue(company.Id, out var surveyInfo);

                var templateValues = BuildBaseTemplateValues(
                    company.CompanyName,
                    company.Website,
                    company.Phone,
                    company.AddStreet,
                    company.AddSuburb,
                    company.AddCity,
                    company.AddPostcode,
                    surveyInfo?.Latest?.Estimate,
                    surveyInfo?.Previous?.Estimate,
                    surveyInfo?.Previous != null);

                var latestCompanySurveyId = surveyInfo?.Latest?.CompanySurveyId;
                if (latestCompanySurveyId.HasValue
                    && answersByCompanySurveyId.TryGetValue(latestCompanySurveyId.Value, out var answerMap))
                {
                    foreach (var pair in answerMap)
                    {
                        if (!templateValues.ContainsKey(pair.Key))
                        {
                            templateValues[pair.Key] = pair.Value;
                        }
                    }
                }

                var row = new PrePublishDataRow
                {
                    CompanyId = company.Id,
                    CompanyName = string.IsNullOrWhiteSpace(company.CompanyName) ? "(No company name)" : company.CompanyName.Trim()
                };

                var ceoFullName = string.Join(" ", new[] { company.CeoFirstName, company.CeoLastName }
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!.Trim()));
                var canonicalTemplateValues = BuildCanonicalLookup(templateValues);
                var previousTemplateValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var previousCanonicalTemplateValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                var previousCompanySurveyId = surveyInfo?.Previous?.CompanySurveyId;
                if (previousCompanySurveyId.HasValue
                    && answersByCompanySurveyId.TryGetValue(previousCompanySurveyId.Value, out var previousAnswerMap))
                {
                    previousTemplateValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var pair in previousAnswerMap)
                    {
                        previousTemplateValues[pair.Key] = pair.Value;
                    }

                    previousCanonicalTemplateValues = BuildCanonicalLookup(previousTemplateValues);
                }

                foreach (var column in Columns)
                {
                    row.Values[column.Header] = ResolveColumnValue(
                        column,
                        templateValues,
                        canonicalTemplateValues,
                        previousTemplateValues,
                        previousCanonicalTemplateValues,
                        company.CompanyDescription,
                        ceoFullName,
                        company.Fye2025,
                        company.Fye2024);
                }

                records.Add(row);
            }

            Records = records;
        }

        private static Dictionary<string, string> BuildBaseTemplateValues(
            string? companyName,
            string? website,
            string? phone,
            string? street,
            string? suburb,
            string? city,
            string? postcode,
            bool? isEstimated,
            bool? isEstimatedYearMinusOne,
            bool hasPreviousYear)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var resolvedCompanyName = companyName?.Trim() ?? string.Empty;
            values[NormalizeTemplateKey("CompanyName")] = resolvedCompanyName;
            values[NormalizeTemplateKey("Company Name")] = resolvedCompanyName;

            values[NormalizeTemplateKey("Estimated")] = isEstimated == true ? "Estimated" : "Not Estimated";
            values[NormalizeTemplateKey("Estimated Year-1")] = !hasPreviousYear
                ? string.Empty
                : (isEstimatedYearMinusOne == true ? "Estimated" : "Not Estimated");

            var webAddress = website?.Trim();
            var companyPhone = phone?.Trim();
            var physicalAddress = BuildPhysicalAddress(street, suburb, city, postcode);

            values[NormalizeTemplateKey("Web Address")] = string.IsNullOrWhiteSpace(webAddress) ? "TBC" : webAddress;
            values[NormalizeTemplateKey("WebAddress")] = string.IsNullOrWhiteSpace(webAddress) ? "TBC" : webAddress;
            values[NormalizeTemplateKey("Company Phone")] = string.IsNullOrWhiteSpace(companyPhone) ? "TBC" : companyPhone;
            values[NormalizeTemplateKey("CompanyPhone")] = string.IsNullOrWhiteSpace(companyPhone) ? "TBC" : companyPhone;
            values[NormalizeTemplateKey("Physical Address")] = string.IsNullOrWhiteSpace(physicalAddress) ? "TBC" : physicalAddress;
            values[NormalizeTemplateKey("PhysicalAddress")] = string.IsNullOrWhiteSpace(physicalAddress) ? "TBC" : physicalAddress;

            return values;
        }

        private static string ResolveColumnValue(
            ColumnDefinition column,
            IReadOnlyDictionary<string, string> templateValues,
            IReadOnlyDictionary<string, string> canonicalTemplateValues,
            IReadOnlyDictionary<string, string> previousTemplateValues,
            IReadOnlyDictionary<string, string> previousCanonicalTemplateValues,
            string? companyDescription,
            string ceoFullName,
            decimal? fyeLastFinancialYear,
            decimal? fyeYearMinusOne)
        {
            if (string.Equals(column.Header, "Company description", StringComparison.OrdinalIgnoreCase))
            {
                var description = companyDescription?.Trim();
                if (!string.IsNullOrWhiteSpace(description))
                {
                    return description;
                }
            }

            if (string.Equals(column.Header, "CEO", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(ceoFullName))
            {
                return ceoFullName;
            }

            if (string.Equals(column.Header, "Revenue Last Financial Year ($000)", StringComparison.OrdinalIgnoreCase))
            {
                var mappedRevenue = GetFirstMappedValue(templateValues, canonicalTemplateValues, column.SourceKeys);
                if (!string.IsNullOrWhiteSpace(mappedRevenue))
                {
                    return mappedRevenue;
                }

                if (fyeLastFinancialYear.HasValue)
                {
                    return Math.Round(fyeLastFinancialYear.Value, 0, MidpointRounding.AwayFromZero).ToString("N0");
                }

                return string.Empty;
            }

            if (string.Equals(column.Header, "Revenue Year-1 ($000)", StringComparison.OrdinalIgnoreCase))
            {
                var mappedRevenue = GetFirstMappedValue(previousTemplateValues, previousCanonicalTemplateValues, column.SourceKeys);
                if (!string.IsNullOrWhiteSpace(mappedRevenue))
                {
                    return mappedRevenue;
                }

                mappedRevenue = GetFirstMappedValue(templateValues, canonicalTemplateValues, "Revenue year-1 ($000)", "Revenue year-1", "Total Revenue Year-1 ($000)", "Total Revenue Year-1");
                if (!string.IsNullOrWhiteSpace(mappedRevenue))
                {
                    return mappedRevenue;
                }

                if (fyeYearMinusOne.HasValue)
                {
                    return Math.Round(fyeYearMinusOne.Value, 0, MidpointRounding.AwayFromZero).ToString("N0");
                }

                return string.Empty;
            }

            if (string.Equals(column.Header, "Staff employed Year-1", StringComparison.OrdinalIgnoreCase))
            {
                var mappedStaff = GetFirstMappedValue(previousTemplateValues, previousCanonicalTemplateValues, column.SourceKeys);
                if (!string.IsNullOrWhiteSpace(mappedStaff))
                {
                    return mappedStaff;
                }

                mappedStaff = GetFirstMappedValue(templateValues, canonicalTemplateValues, "Staff employed year-1", "Total staff employed year-1", "Employment year-1", "Staff employed previous year");
                return mappedStaff;
            }

            return GetFirstMappedValue(templateValues, canonicalTemplateValues, column.SourceKeys);
        }

        private static string GetFirstMappedValue(
            IReadOnlyDictionary<string, string> templateValues,
            IReadOnlyDictionary<string, string> canonicalTemplateValues,
            IEnumerable<string> sourceKeys)
        {
            foreach (var key in sourceKeys)
            {
                var normalized = NormalizeTemplateKey(key);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                if (templateValues.TryGetValue(normalized, out var value)
                    && !string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }

                var canonical = CanonicalizeKey(normalized);
                if (!string.IsNullOrWhiteSpace(canonical)
                    && canonicalTemplateValues.TryGetValue(canonical, out var canonicalValue)
                    && !string.IsNullOrWhiteSpace(canonicalValue))
                {
                    return canonicalValue.Trim();
                }
            }

            return string.Empty;
        }

        private static string GetFirstMappedValue(
            IReadOnlyDictionary<string, string> templateValues,
            IReadOnlyDictionary<string, string> canonicalTemplateValues,
            params string[] sourceKeys)
        {
            return GetFirstMappedValue(templateValues, canonicalTemplateValues, (IEnumerable<string>)sourceKeys);
        }

        private static Dictionary<string, string> BuildCanonicalLookup(IReadOnlyDictionary<string, string> templateValues)
        {
            var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in templateValues)
            {
                if (string.IsNullOrWhiteSpace(pair.Value))
                {
                    continue;
                }

                var canonical = CanonicalizeKey(pair.Key);
                if (string.IsNullOrWhiteSpace(canonical) || lookup.ContainsKey(canonical))
                {
                    continue;
                }

                lookup[canonical] = pair.Value;
            }

            return lookup;
        }

        private static List<ColumnDefinition> BuildColumns()
        {
            return new List<ColumnDefinition>
            {
                new("Company name", "Company Name", "CompanyName"),
                new("Company description", "Company Description", "CompanyDescription"),
                new("Ownership", "Ownership", "Ownership Type", "OwnershipType", "Ownership structure", "Ownership Structure"),
                new("Formed", "Formed", "Formation Year", "Year of Formation", "Company formed", "Company Formed", "Year Company Formed", "Year founded", "Year founded?", "Year established", "Company founded", "Founded", "Date founded", "Date established", "Year of establishment"),
                new("Staff employed Last Financial Year", "Staff employed last financial year", "Total staff employed last financial year", "Employment last financial year", "Staff employed current", "Total Employment Last Financial Year", "Total Emplyment Last Financial Year"),
                new("Staff employed Year-1", "Staff employed last financial year", "Total staff employed last financial year", "Employment last financial year", "Staff employed current", "Total Employment Last Financial Year", "Total Emplyment Last Financial Year", "Staff employed year-1", "Total staff employed year-1", "Employment year-1", "Staff employed previous year"),
                new("Revenue Last Financial Year ($000)", "Revenue last financial year ($000)", "Revenue last financial year", "Total Revenue Last Financial Year ($000)", "Total Revenue Last Financial Year", "Revenue 2026 ($000)", "Revenue 2026", "Total Revenue 2026 ($000)", "Total Revenue 2026"),
                new("Revenue Year-1 ($000)", "Revenue last financial year ($000)", "Revenue last financial year", "Total Revenue Last Financial Year ($000)", "Total Revenue Last Financial Year", "Revenue year-1 ($000)", "Revenue year-1", "Total Revenue Year-1 ($000)", "Total Revenue Year-1", "Revenue 2025 ($000)", "Revenue 2025", "Total Revenue 2025 ($000)", "Total Revenue 2025"),
                new("Estimated Last Financial Year", "Estimated"),
                new("Estimated Year-1", "Estimated Year-1"),
                new("CEO", "CEO", "CEO Name", "Chief Executive Officer"),
                new("Web address", "Web Address", "WebAddress", "Website"),
                new("Company phone", "Company Phone", "CompanyPhone", "Phone"),
                new("Physical address", "Physical Address", "PhysicalAddress"),
                new("Primary sector", "Primary sector", "Primary Sector"),
                new("Secondary sector", "Secondary sector", "Secondary Sector"),
                new("Business Decision", "Business Decision", "Business Decision"),
                new("Key products", "Key products", "Key Products")
            };
        }

        private static string ResolveAnswerValue(string? questionTitle, string? answerText, decimal? answerCurrency, double? answerNumber)
        {
            if (!string.IsNullOrWhiteSpace(answerText))
            {
                return answerText.Trim();
            }

            var normalizedQuestionTitle = NormalizeTemplateKey(questionTitle);
            var useWholeNumberFormat = ShouldFormatAsWholeNumber(normalizedQuestionTitle);

            if (answerCurrency.HasValue)
            {
                return useWholeNumberFormat
                    ? Math.Round(answerCurrency.Value, 0, MidpointRounding.AwayFromZero).ToString("N0")
                    : answerCurrency.Value.ToString("N2");
            }

            if (answerNumber.HasValue)
            {
                return useWholeNumberFormat
                    ? Math.Round((decimal)answerNumber.Value, 0, MidpointRounding.AwayFromZero).ToString("N0")
                    : answerNumber.Value.ToString("0.##");
            }

            return string.Empty;
        }

        private static bool ShouldFormatAsWholeNumber(string? normalizedQuestionTitle)
        {
            if (string.IsNullOrWhiteSpace(normalizedQuestionTitle))
            {
                return false;
            }

            return normalizedQuestionTitle.Contains("revenue", StringComparison.OrdinalIgnoreCase)
                || normalizedQuestionTitle.Contains("employment", StringComparison.OrdinalIgnoreCase)
                || normalizedQuestionTitle.Contains("staffemployed", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildPhysicalAddress(string? street, string? suburb, string? city, string? postcode)
        {
            var parts = new[] { street, suburb, city, postcode }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .ToList();

            return parts.Count == 0
                ? string.Empty
                : string.Join(", ", parts);
        }

        private static string NormalizeTemplateKey(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            return Regex.Replace(raw.Trim(), @"\s+", " ");
        }

        private static string CanonicalizeKey(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var normalized = NormalizeTemplateKey(raw).ToLowerInvariant();
            return Regex.Replace(normalized, @"[^a-z0-9]", string.Empty);
        }

        private static int NormalizeTinStatusFilter(int tinStatus)
        {
            return tinStatus switch
            {
                (int)TinStatus.Tin200 => (int)TinStatus.Tin200,
                (int)TinStatus.Tin200Potential => (int)TinStatus.Tin200Potential,
                (int)TinStatus.Tin1000 => (int)TinStatus.Tin1000,
                (int)TinStatus.TinTest => (int)TinStatus.TinTest,
                _ => (int)TinStatus.Tin200
            };
        }

        public class PrePublishDataRow
        {
            public int CompanyId { get; set; }
            public string CompanyName { get; set; } = string.Empty;
            public Dictionary<string, string> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);

            public string GetValue(string columnHeader)
            {
                return Values.TryGetValue(columnHeader, out var value)
                    ? value
                    : string.Empty;
            }
        }

        public class ColumnDefinition
        {
            public string Header { get; }
            public IReadOnlyList<string> SourceKeys { get; }

            public ColumnDefinition(string header, params string[] sourceKeys)
            {
                Header = header;
                SourceKeys = sourceKeys;
            }
        }

        public class RevenueAdjustPreviewResult
        {
            public int SelectedCompanyCount { get; set; }
            public int DecimalPlacesAdjustment { get; set; }
            public int TotalRevenueAnswersFound { get; set; }
            public int ChangedCount { get; set; }
            public List<RevenueAdjustPreviewRow> PreviewRows { get; set; } = new();
        }

        public class RevenueAdjustPreviewRow
        {
            public string CompanyName { get; set; } = string.Empty;
            public int SurveyYear { get; set; }
            public string QuestionTitle { get; set; } = string.Empty;
            public string FieldType { get; set; } = string.Empty;
            public string OldValue { get; set; } = string.Empty;
            public string NewValue { get; set; } = string.Empty;
        }

        public class PopulateOwnershipFormedPreviewResult
        {
            public int SelectedCompanyCount { get; set; }
            public int ChangedCount { get; set; }
            public List<PopulateOwnershipFormedPreviewRow> PreviewRows { get; set; } = new();
        }

        public class PopulateOwnershipFormedPreviewRow
        {
            public string CompanyName { get; set; } = string.Empty;
            public int LatestSurveyYear { get; set; }
            public int PreviousSurveyYear { get; set; }
            public string QuestionTitle { get; set; } = string.Empty;
            public string NewValue { get; set; } = string.Empty;
        }

        private sealed class EstimatedImportRow
        {
            public string CompanyName { get; set; } = string.Empty;
            public decimal? RevenueValue { get; set; }
            public bool RevenueEstimated { get; set; }
            public decimal? EmployeesValue { get; set; }
            public bool EmployeesEstimated { get; set; }
        }

        public class EstimatedImportPreviewResult
        {
            public int SelectedCompanyCount { get; set; }
            public int RowsRead { get; set; }
            public int MatchedCompanyCount { get; set; }
            public int UnmatchedCompanyCount { get; set; }
            public int ChangedCount { get; set; }
            public List<string> Warnings { get; set; } = new();
            public List<EstimatedImportPreviewRow> PreviewRows { get; set; } = new();
        }

        public class EstimatedImportPreviewRow
        {
            public string CompanyName { get; set; } = string.Empty;
            public int LatestSurveyYear { get; set; }
            public string FieldName { get; set; } = string.Empty;
            public string OldValue { get; set; } = string.Empty;
            public string NewValue { get; set; } = string.Empty;
        }

    }
}
