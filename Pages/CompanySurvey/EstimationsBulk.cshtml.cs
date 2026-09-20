using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Data;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.CompanySurvey
{
    public class EstimationsBulkModel : PageModel
    {
        private static readonly (string Key, string Label)[] EstimationMetrics =
        {
            ("Wages", "Wages & Salaries"),
            ("ResearchDevelopment", "Research & Development"),
            ("SalesMarketing", "Sales & Marketing"),
            ("Ebitda", "EBITDA")
        };

        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly CompanySurveyService _service;

        public EstimationsBulkModel(ApplicationDbContext context, IConfiguration configuration, CompanySurveyService service)
        {
            _context = context;
            _configuration = configuration;
            _service = service;
        }

        [BindProperty(SupportsGet = true)]
        public int? SelectedFinancialYear { get; set; }

        [BindProperty(SupportsGet = true)]
        public int SelectedTinStatus { get; set; } = (int)TinStatus.Tin200;

        public List<int> FinancialYears { get; set; } = new();
        public (int Value, string Label)[] TinStatusOptions => TinStatusHelper.DropdownOptions;
        public List<BulkEstimationRow> Rows { get; set; } = new();

        [TempData]
        public string? StatusMessage { get; set; }

        public async Task OnGetAsync()
        {
            await LoadAsync();
        }

        // Called on demand (via AJAX) by the row "Display" button so the initial page load doesn't have to
        // run every estimation methodology for every row.
        public async Task<IActionResult> OnGetRowEstimatesAsync(int companySurveyId)
        {
            var calc = new EstimationModel(_context, _configuration);
            var loaded = await calc.LoadForBulkEstimationAsync(companySurveyId);
            if (!loaded)
            {
                return NotFound();
            }

            var metrics = new List<object>();
            foreach (var (key, label) in EstimationMetrics)
            {
                var preview = await calc.BuildCheckPreviewAsync(key, null);
                var usedCandidate = preview.Candidates.FirstOrDefault(c => c.IsUsed);
                metrics.Add(new
                {
                    key,
                    label,
                    usedValue = usedCandidate?.Value,
                    candidates = preview.Candidates.Select(c => new
                    {
                        step = c.StepName,
                        value = c.Value,
                        details = c.Details,
                        failureReasons = c.FailureReasons,
                        isUsed = c.IsUsed
                    })
                });
            }

            return new JsonResult(new { companySurveyId, metrics });
        }

        public async Task<IActionResult> OnPostPreviewSelectedAsync(List<RowApplyInput> rows)
        {
            var result = new List<object>();

            foreach (var input in rows ?? new List<RowApplyInput>())
            {
                var calc = new EstimationModel(_context, _configuration);
                var loaded = await calc.LoadForBulkEstimationAsync(input.CompanySurveyId);
                if (!loaded)
                {
                    result.Add(new { input.CompanySurveyId, companyName = (string?)null, blockedReason = "Company survey record not found.", changes = Array.Empty<object>() });
                    continue;
                }

                var blockedReason = await GetBlockedReasonAsync(calc);
                var changes = new List<object>();

                foreach (var (metricKey, value) in EnumerateMetricValues(input))
                {
                    if (!value.HasValue)
                    {
                        continue;
                    }

                    var label = EstimationMetrics.First(m => m.Key == metricKey).Label;
                    var currentValue = await calc.GetCurrentYearMetricValueAsync(metricKey);
                    changes.Add(new { metricKey, label, currentValue, newValue = value });
                }

                result.Add(new
                {
                    companySurveyId = input.CompanySurveyId,
                    companyName = calc.CompanyName,
                    blockedReason,
                    changes
                });
            }

            return new JsonResult(new { rows = result });
        }

        public async Task<IActionResult> OnPostApplySelectedAsync(List<RowApplyInput> rows)
        {
            var appliedCompanies = 0;
            var appliedValues = 0;
            var skipped = new List<string>();

            foreach (var input in rows ?? new List<RowApplyInput>())
            {
                var calc = new EstimationModel(_context, _configuration);
                var loaded = await calc.LoadForBulkEstimationAsync(input.CompanySurveyId);
                if (!loaded)
                {
                    skipped.Add($"Company survey {input.CompanySurveyId} not found.");
                    continue;
                }

                var blockedReason = await GetBlockedReasonAsync(calc);
                if (blockedReason != null)
                {
                    skipped.Add($"{calc.CompanyName}: {blockedReason}");
                    continue;
                }

                var appliedForRow = 0;
                foreach (var (metricKey, value) in EnumerateMetricValues(input))
                {
                    if (value.HasValue && value.Value >= 0)
                    {
                        await calc.SaveAppliedAnswerAsync(metricKey, null, value.Value, calc.TargetFinancialYear);
                        appliedForRow++;
                    }
                }

                if (appliedForRow > 0)
                {
                    appliedCompanies++;
                    appliedValues += appliedForRow;
                }
            }

            StatusMessage = $"Applied {appliedValues} estimated value(s) across {appliedCompanies} compan{(appliedCompanies == 1 ? "y" : "ies")}."
                + (skipped.Count > 0 ? $" Skipped {skipped.Count}: {string.Join("; ", skipped)}" : string.Empty);

            return new JsonResult(new { appliedCompanies, appliedValues, skipped });
        }

        private static IEnumerable<(string Key, decimal? Value)> EnumerateMetricValues(RowApplyInput input)
        {
            yield return ("Wages", input.WagesValue);
            yield return ("ResearchDevelopment", input.ResearchDevelopmentValue);
            yield return ("SalesMarketing", input.SalesMarketingValue);
            yield return ("Ebitda", input.EbitdaValue);
        }

        private static async Task<string?> GetBlockedReasonAsync(EstimationModel calc)
        {
            if (!calc.EstimateEnabled)
            {
                return "Estimate is not enabled for this company survey.";
            }

            if (calc.IsLocked)
            {
                return "Company survey record is locked.";
            }

            var (revenue, employment) = await calc.GetCurrentYearActualRevenueAndEmploymentAsync();
            if (!(revenue > 0) || !(employment > 0))
            {
                return "Missing a valid current year Revenue or Employment answer.";
            }

            return null;
        }

        private async Task LoadAsync()
        {
            FinancialYears = await _service.GetAvailableFinancialYearsAsync();

            if (!SelectedFinancialYear.HasValue)
            {
                SelectedFinancialYear = await _service.GetCurrentSurveyFinancialYearAsync() ?? FinancialYears.FirstOrDefault();
            }

            if (!TinStatusHelper.IsValidSelection(SelectedTinStatus))
            {
                SelectedTinStatus = (int)TinStatus.Tin200;
            }

            var listRows = await _service.GetListRowsAsync(SelectedFinancialYear);
            var filtered = listRows.Where(r => r.TinStatus == SelectedTinStatus).ToList();

            // Actual current-year Revenue/Employment/metric answers are loaded up front (cheap history lookups only,
            // no sector-fallback calculation); the full estimation methodology breakdown is computed on demand via
            // OnGetRowEstimatesAsync when "Show Estimate Numbers"/"Detail" is used.
            var rows = new List<BulkEstimationRow>();
            foreach (var listRow in filtered)
            {
                var calc = new EstimationModel(_context, _configuration);
                var loaded = await calc.LoadForBulkEstimationAsync(listRow.Id);
                if (!loaded)
                {
                    continue;
                }

                var (revenue, employment) = await calc.GetCurrentYearActualRevenueAndEmploymentAsync();

                rows.Add(new BulkEstimationRow
                {
                    CompanySurveyId = listRow.Id,
                    CompanyName = calc.CompanyName,
                    CurrentRevenue = revenue,
                    CurrentEmployment = employment,
                    EstimateEnabled = calc.EstimateEnabled,
                    IsLocked = calc.IsLocked,
                    WagesActual = await calc.GetCurrentYearMetricValueAsync("Wages"),
                    ResearchDevelopmentActual = await calc.GetCurrentYearMetricValueAsync("ResearchDevelopment"),
                    SalesMarketingActual = await calc.GetCurrentYearMetricValueAsync("SalesMarketing"),
                    EbitdaActual = await calc.GetCurrentYearMetricValueAsync("Ebitda")
                });
            }

            Rows = rows.OrderBy(r => r.CompanyName).ToList();
        }

        public class BulkEstimationRow
        {
            public int CompanySurveyId { get; set; }
            public string CompanyName { get; set; } = string.Empty;
            public decimal? CurrentRevenue { get; set; }
            public decimal? CurrentEmployment { get; set; }
            public bool EstimateEnabled { get; set; }
            public bool IsLocked { get; set; }
            public decimal? WagesActual { get; set; }
            public decimal? ResearchDevelopmentActual { get; set; }
            public decimal? SalesMarketingActual { get; set; }
            public decimal? EbitdaActual { get; set; }

            public bool IsEditable =>
                EstimateEnabled
                && !IsLocked
                && CurrentRevenue.HasValue && CurrentRevenue.Value > 0
                && CurrentEmployment.HasValue && CurrentEmployment.Value > 0;
        }

        public class RowApplyInput
        {
            public int CompanySurveyId { get; set; }
            public decimal? WagesValue { get; set; }
            public decimal? ResearchDevelopmentValue { get; set; }
            public decimal? SalesMarketingValue { get; set; }
            public decimal? EbitdaValue { get; set; }
        }
    }
}
