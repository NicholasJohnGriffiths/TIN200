using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using TINWeb.Data;
using TINWeb.Models;

namespace TINWeb.Pages.Answers
{
    public class CheckRevenueDataModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public List<CurrencyQuestionColumn> Columns { get; } = new();
        public List<RevenueDataRow> Records { get; set; } = new();
        public List<(int Value, string Label)> TinStatusOptions { get; } = TinStatusHelper.DropdownOptions.ToList();
        public RevenueFieldAdjustPreviewResult? PreviewResult { get; set; }

        [TempData]
        public string? StatusMessage { get; set; }

        [BindProperty(SupportsGet = true)]
        public int SelectedTinStatus { get; set; } = (int)TinStatus.Tin200;

        [BindProperty]
        public int RevenueDecimalPlacesAdjustment { get; set; }

        [BindProperty]
        public List<int> SelectedCompanyIds { get; set; } = new();

        [BindProperty]
        public List<int> SelectedQuestionIds { get; set; } = new();

        public CheckRevenueDataModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task OnGetAsync()
        {
            SelectedTinStatus = NormalizeTinStatusFilter(SelectedTinStatus);
            await LoadRowsAsync();
        }

        public async Task<IActionResult> OnPostPreviewAdjustSelectedRevenueAsync()
        {
            SelectedTinStatus = NormalizeTinStatusFilter(SelectedTinStatus);
            await LoadRowsAsync();

            var selectedCompanyIds = ResolveSelectedCompanyIds(SelectedCompanyIds, Records);
            var selectedQuestionIds = ResolveSelectedQuestionIds(SelectedQuestionIds, Columns);

            SelectedCompanyIds = selectedCompanyIds;
            SelectedQuestionIds = selectedQuestionIds;

            if (selectedCompanyIds.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Select at least one company row first.");
                return Page();
            }

            if (selectedQuestionIds.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Select at least one field first.");
                return Page();
            }

            if (RevenueDecimalPlacesAdjustment == 0)
            {
                ModelState.AddModelError(nameof(RevenueDecimalPlacesAdjustment), "Enter a non-zero decimal point adjustment.");
                return Page();
            }

            PreviewResult = await BuildRevenueFieldAdjustPreviewAsync(selectedCompanyIds, selectedQuestionIds, RevenueDecimalPlacesAdjustment, applyUpdates: false);
            return Page();
        }

        public async Task<IActionResult> OnPostApplyAdjustSelectedRevenueAsync()
        {
            SelectedTinStatus = NormalizeTinStatusFilter(SelectedTinStatus);
            await LoadRowsAsync();

            var selectedCompanyIds = ResolveSelectedCompanyIds(SelectedCompanyIds, Records);
            var selectedQuestionIds = ResolveSelectedQuestionIds(SelectedQuestionIds, Columns);

            if (selectedCompanyIds.Count == 0)
            {
                StatusMessage = "Error: Select at least one company row first.";
                return RedirectToPage(new { selectedTinStatus = SelectedTinStatus });
            }

            if (selectedQuestionIds.Count == 0)
            {
                StatusMessage = "Error: Select at least one field first.";
                return RedirectToPage(new { selectedTinStatus = SelectedTinStatus });
            }

            if (RevenueDecimalPlacesAdjustment == 0)
            {
                StatusMessage = "Error: Enter a non-zero decimal point adjustment.";
                return RedirectToPage(new { selectedTinStatus = SelectedTinStatus });
            }

            var result = await BuildRevenueFieldAdjustPreviewAsync(selectedCompanyIds, selectedQuestionIds, RevenueDecimalPlacesAdjustment, applyUpdates: true);

            StatusMessage = $"Revenue update applied. Selected companies: {result.SelectedCompanyCount}. Selected fields: {result.SelectedQuestionCount}. Revenue answers updated: {result.ChangedCount}. Decimal point adjustment: {RevenueDecimalPlacesAdjustment}.";
            return RedirectToPage(new { selectedTinStatus = SelectedTinStatus });
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
                    x.ExternalId
                })
                .OrderBy(x => x.CompanyName)
                .ThenBy(x => x.Id)
                .ToListAsync();

            if (companies.Count == 0)
            {
                Records = new List<RevenueDataRow>();
                Columns.Clear();
                return;
            }

            var companyIds = companies.Select(x => x.Id).ToList();

            var latestSurveyRows = await (
                from companySurvey in _context.CompanySurvey.AsNoTracking()
                join survey in _context.Survey.AsNoTracking() on companySurvey.SurveyId equals survey.Id
                where companyIds.Contains(companySurvey.CompanyId)
                select new
                {
                    companySurvey.CompanyId,
                    CompanySurveyId = companySurvey.Id,
                    survey.CurrentSurvey,
                    survey.FinancialYear
                })
                .ToListAsync();

            var latestSurveyByCompany = latestSurveyRows
                .GroupBy(x => x.CompanyId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.CurrentSurvey)
                        .ThenByDescending(x => x.FinancialYear)
                        .ThenByDescending(x => x.CompanySurveyId)
                        .First());

            var currencyQuestionRows = await _context.Question
                .AsNoTracking()
                .Where(q => q.Active != false && q.AnswerType == "Currency")
                .Select(q => new
                {
                    q.Id,
                    q.Title,
                    q.QuestionText,
                    q.ImportColumnName,
                    q.ImportColumnNameAlt,
                    q.OrderNumber,
                    q.GroupTitle
                })
                .ToListAsync();

            var orderedCurrencyQuestions = currencyQuestionRows
                .OrderBy(q => string.IsNullOrWhiteSpace(q.GroupTitle) ? string.Empty : q.GroupTitle)
                .ThenBy(q => q.OrderNumber ?? int.MaxValue)
                .ThenBy(q => string.IsNullOrWhiteSpace(q.Title) ? string.Empty : q.Title)
                .ThenBy(q => q.Id)
                .Select(q => new CurrencyQuestionColumn
                {
                    QuestionId = q.Id,
                    Header = ResolveQuestionHeader(q.Title, q.QuestionText, q.ImportColumnName, q.ImportColumnNameAlt)
                })
                .ToList();

            Columns.Clear();
            Columns.AddRange(orderedCurrencyQuestions);

            var questionIds = Columns.Select(x => x.QuestionId).ToList();
            var latestSurveyIds = latestSurveyByCompany.Values
                .Select(x => x.CompanySurveyId)
                .Distinct()
                .ToList();

            var answerRows = questionIds.Count == 0 || latestSurveyIds.Count == 0
                ? new List<Answer>()
                : await _context.Answer
                    .AsNoTracking()
                    .Where(a => latestSurveyIds.Contains(a.CompanySurveyId) && questionIds.Contains(a.QuestionId))
                    .ToListAsync();

            var answerLookup = answerRows
                .GroupBy(a => (a.CompanySurveyId, a.QuestionId))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());

            var records = new List<RevenueDataRow>();

            foreach (var company in companies)
            {
                latestSurveyByCompany.TryGetValue(company.Id, out var latestSurvey);

                var row = new RevenueDataRow
                {
                    CompanyId = company.Id,
                    CompanyName = string.IsNullOrWhiteSpace(company.CompanyName) ? "(No company name)" : company.CompanyName.Trim(),
                    ExternalId = company.ExternalId?.Trim() ?? string.Empty,
                    LatestSurveyYear = latestSurvey?.FinancialYear
                };

                if (latestSurvey != null)
                {
                    foreach (var column in Columns)
                    {
                        if (answerLookup.TryGetValue((latestSurvey.CompanySurveyId, column.QuestionId), out var answer))
                        {
                            row.Values[column.QuestionId] = ResolveRawCurrencyValue(answer);
                        }
                    }
                }

                records.Add(row);
            }

            Records = records;
        }

        private static string ResolveQuestionHeader(string? title, string? questionText, string? importColumnName, string? importColumnNameAlt)
        {
            var candidates = new[] { title, questionText, importColumnName, importColumnNameAlt };

            foreach (var candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate.Trim();
                }
            }

            return "Question";
        }

        private static string ResolveRawCurrencyValue(Answer answer)
        {
            if (!string.IsNullOrWhiteSpace(answer.AnswerText))
            {
                return answer.AnswerText.Trim();
            }

            if (answer.AnswerCurrency.HasValue)
            {
                return Math.Truncate(answer.AnswerCurrency.Value).ToString("0", CultureInfo.InvariantCulture);
            }

            if (answer.AnswerNumber.HasValue)
            {
                return Math.Truncate(answer.AnswerNumber.Value).ToString("0", CultureInfo.InvariantCulture);
            }

            return string.Empty;
        }

        private static List<int> ResolveSelectedCompanyIds(IEnumerable<int>? selectedCompanyIds, IEnumerable<RevenueDataRow> records)
        {
            var validIds = records.Select(x => x.CompanyId).ToHashSet();

            return (selectedCompanyIds ?? Enumerable.Empty<int>())
                .Where(id => validIds.Contains(id))
                .Distinct()
                .ToList();
        }

        private static List<int> ResolveSelectedQuestionIds(IEnumerable<int>? selectedQuestionIds, IEnumerable<CurrencyQuestionColumn> columns)
        {
            var validIds = columns.Select(x => x.QuestionId).ToHashSet();

            return (selectedQuestionIds ?? Enumerable.Empty<int>())
                .Where(id => validIds.Contains(id))
                .Distinct()
                .ToList();
        }

        private async Task<RevenueFieldAdjustPreviewResult> BuildRevenueFieldAdjustPreviewAsync(IReadOnlyCollection<int> selectedCompanyIds, IReadOnlyCollection<int> selectedQuestionIds, int decimalPlacesAdjustment, bool applyUpdates)
        {
            var result = new RevenueFieldAdjustPreviewResult
            {
                SelectedCompanyCount = selectedCompanyIds.Count,
                SelectedQuestionCount = selectedQuestionIds.Count,
                DecimalPlacesAdjustment = decimalPlacesAdjustment
            };

            var latestCompanySurveyRows = await (
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

            var targetCompanySurveyIds = latestCompanySurveyRows
                .GroupBy(x => x.CompanyId)
                .Select(g => g
                    .OrderByDescending(x => x.CurrentSurvey)
                    .ThenByDescending(x => x.FinancialYear)
                    .ThenByDescending(x => x.Id)
                    .Select(x => x.Id)
                    .First())
                .ToHashSet();

            if (targetCompanySurveyIds.Count == 0 || selectedQuestionIds.Count == 0)
            {
                return result;
            }

            var questionLookup = Columns.ToDictionary(x => x.QuestionId, x => x.Header);

            var candidates = await (
                from answer in _context.Answer
                join question in _context.Question on answer.QuestionId equals question.Id
                join companySurvey in _context.CompanySurvey on answer.CompanySurveyId equals companySurvey.Id
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                join company in _context.Tin200 on companySurvey.CompanyId equals company.Id
                where targetCompanySurveyIds.Contains(companySurvey.Id) && selectedQuestionIds.Contains(question.Id)
                select new
                {
                    Answer = answer,
                    QuestionId = question.Id,
                    QuestionTitle = question.Title,
                    question.QuestionText,
                    question.ImportColumnName,
                    question.ImportColumnNameAlt,
                    CompanyName = company.CompanyName,
                    company.ExternalId,
                    SurveyYear = survey.FinancialYear
                })
                .ToListAsync();

            var previewRows = new List<RevenueFieldAdjustPreviewRow>();

            foreach (var candidate in candidates)
            {
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
                    previewRows.Add(new RevenueFieldAdjustPreviewRow
                    {
                        CompanyName = string.IsNullOrWhiteSpace(candidate.CompanyName) ? "(No company name)" : candidate.CompanyName.Trim(),
                        ExternalId = candidate.ExternalId?.Trim() ?? string.Empty,
                        SurveyYear = candidate.SurveyYear,
                        QuestionTitle = questionLookup.TryGetValue(candidate.QuestionId, out var header) ? header : ResolveQuestionHeader(candidate.QuestionTitle, candidate.QuestionText, candidate.ImportColumnName, candidate.ImportColumnNameAlt),
                        FieldType = fieldType,
                        OldValue = FormatShiftedValue(oldCurrency, oldNumber),
                        NewValue = FormatShiftedValue(newCurrency, newNumber)
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

        private static string FormatShiftedValue(decimal? currencyValue, double? numberValue)
        {
            if (currencyValue.HasValue)
            {
                return currencyValue.Value.ToString(CultureInfo.InvariantCulture);
            }

            if (numberValue.HasValue)
            {
                return numberValue.Value.ToString(CultureInfo.InvariantCulture);
            }

            return string.Empty;
        }

        private static decimal ShiftDecimalPlaces(decimal value, int places)
        {
            if (places == 0)
            {
                return value;
            }

            var factor = (decimal)Math.Pow(10, Math.Abs(places));
            return places > 0 ? value * factor : value / factor;
        }

        private static double ShiftDecimalPlaces(double value, int places)
        {
            if (places == 0)
            {
                return value;
            }

            var factor = Math.Pow(10, Math.Abs(places));
            return places > 0 ? value * factor : value / factor;
        }

        public class RevenueFieldAdjustPreviewResult
        {
            public int SelectedCompanyCount { get; set; }
            public int SelectedQuestionCount { get; set; }
            public int DecimalPlacesAdjustment { get; set; }
            public int TotalRevenueAnswersFound { get; set; }
            public int ChangedCount { get; set; }
            public List<RevenueFieldAdjustPreviewRow> PreviewRows { get; set; } = new();
        }

        public class RevenueFieldAdjustPreviewRow
        {
            public string CompanyName { get; set; } = string.Empty;
            public string ExternalId { get; set; } = string.Empty;
            public int SurveyYear { get; set; }
            public string QuestionTitle { get; set; } = string.Empty;
            public string FieldType { get; set; } = string.Empty;
            public string OldValue { get; set; } = string.Empty;
            public string NewValue { get; set; } = string.Empty;
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

        public class RevenueDataRow
        {
            public int CompanyId { get; set; }
            public string CompanyName { get; set; } = string.Empty;
            public string ExternalId { get; set; } = string.Empty;
            public int? LatestSurveyYear { get; set; }
            public Dictionary<int, string> Values { get; set; } = new();

            public string GetValue(int questionId)
            {
                return Values.TryGetValue(questionId, out var value) ? value : string.Empty;
            }
        }

        public class CurrencyQuestionColumn
        {
            public int QuestionId { get; set; }
            public string Header { get; set; } = string.Empty;
        }
    }
}