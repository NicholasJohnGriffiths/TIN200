using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Services;

namespace TINWeb.Pages.CompanySurvey
{
    public class IndexModel : PageModel
    {
        private readonly CompanySurveyService _service;

        public List<CompanySurveyService.CompanySurveyListRow> Records { get; set; } = new();
        public List<int> FinancialYears { get; set; } = new();
        public int? SelectedFinancialYear { get; set; }
        public int TotalCompaniesWithAnswers { get; set; }
        public string CompanySearch { get; set; } = string.Empty;
        public string SurveyEmailSentFilter { get; set; } = "all";
        public string SortBy { get; set; } = "CompanyName";
        public string SortDir { get; set; } = "asc";

        public IndexModel(CompanySurveyService service)
        {
            _service = service;
        }

        public async Task OnGetAsync(int? financialYear, string? sortBy, string? sortDir, string? companySearch, string? surveyEmailSentFilter)
        {
            FinancialYears = await _service.GetAvailableFinancialYearsAsync();

            SelectedFinancialYear = financialYear ?? await _service.GetCurrentSurveyFinancialYearAsync();
            CompanySearch = (companySearch ?? string.Empty).Trim();
            SurveyEmailSentFilter = NormalizeSurveyEmailSentFilter(surveyEmailSentFilter);
            SortBy = NormalizeSortBy(sortBy);
            SortDir = NormalizeSortDir(sortDir);

            Records = await _service.GetListRowsAsync(SelectedFinancialYear);

            if (string.Equals(SurveyEmailSentFilter, "sent", StringComparison.OrdinalIgnoreCase))
            {
                Records = Records.Where(x => x.SurveyEmailSent).ToList();
            }
            else if (string.Equals(SurveyEmailSentFilter, "not-sent", StringComparison.OrdinalIgnoreCase))
            {
                Records = Records.Where(x => !x.SurveyEmailSent).ToList();
            }

            if (!string.IsNullOrWhiteSpace(CompanySearch))
            {
                Records = Records
                    .Where(x =>
                        (!string.IsNullOrWhiteSpace(x.CompanyName) && x.CompanyName.Contains(CompanySearch, StringComparison.OrdinalIgnoreCase))
                        || (!string.IsNullOrWhiteSpace(x.ExternalId) && x.ExternalId.Contains(CompanySearch, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }
            Records = ApplySorting(Records, SortBy, SortDir).ToList();
            TotalCompaniesWithAnswers = Records.Count(r => r.AnswerCount > 0);
        }

        public async Task<IActionResult> OnPostBulkSubmitWithAnswersAsync(int? financialYear, string? companySearch, string? surveyEmailSentFilter)
        {
            await _service.BulkSubmitWithAnswersAsync(financialYear);
            return RedirectToPage(new { financialYear, companySearch, surveyEmailSentFilter });
        }

        private static string NormalizeSurveyEmailSentFilter(string? surveyEmailSentFilter)
        {
            return surveyEmailSentFilter?.Trim().ToLowerInvariant() switch
            {
                "sent" => "sent",
                "not-sent" => "not-sent",
                _ => "all"
            };
        }

        public string GetNextSortDirection(string column)
        {
            return string.Equals(SortBy, column, StringComparison.OrdinalIgnoreCase) && string.Equals(SortDir, "asc", StringComparison.OrdinalIgnoreCase)
                ? "desc"
                : "asc";
        }

        public string GetSortIndicator(string column)
        {
            if (!string.Equals(SortBy, column, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return string.Equals(SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? " ▲" : " ▼";
        }

        private static string NormalizeSortBy(string? sortBy)
        {
            return sortBy switch
            {
                "CompanyName" => "CompanyName",
                "Saved" => "Saved",
                "SavedDate" => "SavedDate",
                "Submitted" => "Submitted",
                "SubmittedDate" => "SubmittedDate",
                "Requested" => "Requested",
                "RequestedDate" => "RequestedDate",
                "Locked" => "Locked",
                "Estimate" => "Estimate",
                "AnswerCount" => "AnswerCount",
                _ => "CompanyName"
            };
        }

        private static string NormalizeSortDir(string? sortDir)
        {
            return string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";
        }

        private static IEnumerable<CompanySurveyService.CompanySurveyListRow> ApplySorting(
            IEnumerable<CompanySurveyService.CompanySurveyListRow> records,
            string sortBy,
            string sortDir)
        {
            var descending = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);

            return sortBy switch
            {
                "Saved" => descending ? records.OrderByDescending(r => r.Saved).ThenBy(r => r.CompanyName) : records.OrderBy(r => r.Saved).ThenBy(r => r.CompanyName),
                "SavedDate" => descending ? records.OrderByDescending(r => r.SavedDate).ThenBy(r => r.CompanyName) : records.OrderBy(r => r.SavedDate).ThenBy(r => r.CompanyName),
                "Submitted" => descending ? records.OrderByDescending(r => r.Submitted).ThenBy(r => r.CompanyName) : records.OrderBy(r => r.Submitted).ThenBy(r => r.CompanyName),
                "SubmittedDate" => descending ? records.OrderByDescending(r => r.SubmittedDate).ThenBy(r => r.CompanyName) : records.OrderBy(r => r.SubmittedDate).ThenBy(r => r.CompanyName),
                "Requested" => descending ? records.OrderByDescending(r => r.Requested).ThenBy(r => r.CompanyName) : records.OrderBy(r => r.Requested).ThenBy(r => r.CompanyName),
                "RequestedDate" => descending ? records.OrderByDescending(r => r.RequestedDate).ThenBy(r => r.CompanyName) : records.OrderBy(r => r.RequestedDate).ThenBy(r => r.CompanyName),
                "Locked" => descending ? records.OrderByDescending(r => r.Locked).ThenBy(r => r.CompanyName) : records.OrderBy(r => r.Locked).ThenBy(r => r.CompanyName),
                "Estimate" => descending ? records.OrderByDescending(r => r.Estimate).ThenBy(r => r.CompanyName) : records.OrderBy(r => r.Estimate).ThenBy(r => r.CompanyName),
                "AnswerCount" => descending ? records.OrderByDescending(r => r.AnswerCount).ThenBy(r => r.CompanyName) : records.OrderBy(r => r.AnswerCount).ThenBy(r => r.CompanyName),
                _ => descending ? records.OrderByDescending(r => r.CompanyName).ThenByDescending(r => r.Id) : records.OrderBy(r => r.CompanyName).ThenBy(r => r.Id)
            };
        }
    }
}
