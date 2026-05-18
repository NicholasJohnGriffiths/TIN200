using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.CompanySurvey
{
    public class ProgressModel : PageModel
    {
        private readonly CompanySurveyService _companySurveyService;

        public List<CompanySurveyService.CompanySurveyListRow> Records { get; set; } = new();
        public List<int> FinancialYears { get; set; } = new();
        public List<int> LastTIN200Years { get; set; } = new();
        public int TotalCompanies { get; set; }
        public int SavedCompanies { get; set; }
        public int SubmittedCompanies { get; set; }
        public int SavedOrSubmittedCompanies { get; set; }
        public decimal ProgressPercent => TotalCompanies == 0
            ? 0
            : Math.Round((decimal)SavedOrSubmittedCompanies * 100m / TotalCompanies, 1);
        public decimal SubmittedProgressPercent => TotalCompanies == 0
            ? 0
            : Math.Round((decimal)SubmittedCompanies * 100m / TotalCompanies, 1);

        [BindProperty(SupportsGet = true)]
        public int? FinancialYear { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? LastTIN200Year { get; set; }

        [BindProperty(SupportsGet = true)]
        public string ProgressStatusFilter { get; set; } = "all";

        [BindProperty(SupportsGet = true)]
        public string SortBy { get; set; } = "activityDate";

        [BindProperty(SupportsGet = true)]
        public string SortDir { get; set; } = "desc";

        [BindProperty(SupportsGet = true)]
        public int SelectedTinStatus { get; set; } = (int)TinStatus.Tin200;

        public ProgressModel(CompanySurveyService companySurveyService)
        {
            _companySurveyService = companySurveyService;
        }

        public async Task OnGetAsync()
        {
            SelectedTinStatus = NormalizeTinStatusFilter(SelectedTinStatus);
            FinancialYears = await _companySurveyService.GetAvailableFinancialYearsAsync();

            if (!FinancialYear.HasValue)
            {
                FinancialYear = FinancialYears.FirstOrDefault();
            }

            var rows = await _companySurveyService.GetListRowsAsync(FinancialYear);
            rows = rows.Where(r => r.TinStatus == SelectedTinStatus).ToList();

            LastTIN200Years = rows
                .Where(r => r.LastTIN200Year.HasValue)
                .Select(r => r.LastTIN200Year!.Value)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();

            if (!LastTIN200Year.HasValue)
            {
                LastTIN200Year = LastTIN200Years.FirstOrDefault();
            }

            if (LastTIN200Year.HasValue)
            {
                rows = rows.Where(r => r.LastTIN200Year == LastTIN200Year.Value).ToList();
            }

            TotalCompanies = rows.Count;
            SavedCompanies = rows.Count(r => r.Saved);
            SubmittedCompanies = rows.Count(r => r.Submitted);
            SavedOrSubmittedCompanies = rows.Count(r => r.Saved || r.Submitted);

            Records = rows
                .Where(r => MatchesProgressStatusFilter(r.Saved, r.Submitted, ProgressStatusFilter))
                .OrderByDescending(r => GetActivityDate(r))
                .ThenBy(r => r.CompanyName)
                .ThenBy(r => r.Id)
                .ToList();

            Records = ApplySorting(Records, SortBy, SortDir);
        }

        public string GetContactName(CompanySurveyService.CompanySurveyListRow row)
        {
            var first = (row.ContactFirstName ?? string.Empty).Trim();
            var last = (row.ContactLastName ?? string.Empty).Trim();
            var full = string.Join(" ", new[] { first, last }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
            return string.IsNullOrWhiteSpace(full) ? string.Empty : full;
        }

        private static bool MatchesProgressStatusFilter(bool saved, bool submitted, string filter)
        {
            return filter switch
            {
                "savedSubmitted" => saved || submitted,
                "notSavedSubmitted" => !saved && !submitted,
                _ => true
            };
        }

        public string GetNextSortDirection(string column)
        {
            if (!string.Equals(SortBy, column, StringComparison.OrdinalIgnoreCase))
            {
                return "asc";
            }

            return string.Equals(SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";
        }

        public string GetSortIndicator(string column)
        {
            if (!string.Equals(SortBy, column, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return string.Equals(SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? " ▲" : " ▼";
        }

        private static List<CompanySurveyService.CompanySurveyListRow> ApplySorting(
            List<CompanySurveyService.CompanySurveyListRow> rows,
            string? sortBy,
            string? sortDir)
        {
            var isAsc = string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);

            return (sortBy ?? string.Empty).ToLowerInvariant() switch
            {
                "companyname" => isAsc
                    ? rows.OrderBy(r => r.CompanyName).ThenBy(r => r.Id).ToList()
                    : rows.OrderByDescending(r => r.CompanyName).ThenByDescending(r => r.Id).ToList(),
                "saveddate" => isAsc
                    ? rows.OrderBy(r => r.SavedDate).ThenBy(r => r.CompanyName).ToList()
                    : rows.OrderByDescending(r => r.SavedDate).ThenBy(r => r.CompanyName).ToList(),
                "submitteddate" => isAsc
                    ? rows.OrderBy(r => r.SubmittedDate).ThenBy(r => r.CompanyName).ToList()
                    : rows.OrderByDescending(r => r.SubmittedDate).ThenBy(r => r.CompanyName).ToList(),
                _ => isAsc
                    ? rows.OrderBy(r => GetActivityDate(r)).ThenBy(r => r.CompanyName).ToList()
                    : rows.OrderByDescending(r => GetActivityDate(r)).ThenBy(r => r.CompanyName).ToList()
            };
        }

        private static DateTime? GetActivityDate(CompanySurveyService.CompanySurveyListRow row)
        {
            if (row.SavedDate.HasValue && row.SubmittedDate.HasValue)
            {
                return row.SavedDate > row.SubmittedDate ? row.SavedDate : row.SubmittedDate;
            }

            return row.SavedDate ?? row.SubmittedDate;
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
    }
}
