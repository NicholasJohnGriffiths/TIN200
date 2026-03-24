using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Models;

namespace TINWeb.Pages.CompanySurvey
{
    public class EstimationModel : PageModel
    {
        private const string RevenueQuestionTitle = "Total Revenue Last Financial Year";
        private const string EmploymentQuestionTitle = "Total Employment Last Financial Year";
        private const string EmploymentQuestionTitleLegacy = "Total Emplyment Last Financial Year";
        private const string WagesQuestionTitle = "Cost Structures Last Financial Year - Wages and Salaries";
        private const string ResearchDevelopmentQuestionTitle = "Cost Structures Last Financial Year - Research and Development";
        private const string SalesMarketingQuestionTitle = "Cost Structures Last Financial Year - Sales and Marketing";
        private const string EbitdaQuestionTitle = "EBITDA Last Financial Year";
        private const string RevenueNzQuestionTitle = "Global Revenue NZ Last Financial Year";
        private const string RevenueAustraliaQuestionTitle = "Global Revenue Australia Last Financial Year";
        private const string RevenueChinaQuestionTitle = "Global Revenue China Last Financial Year";
        private const string RevenueRestOfAsiaQuestionTitle = "Global Revenue Rest of Asia Last Financial Year";
        private const string RevenueNorthAmericaQuestionTitle = "Global Revenue North America Last Financial Year";
        private const string RevenueEuropeQuestionTitle = "Global Revenue Europe Last Financial Year";
        private const string RevenueMiddleEastQuestionTitle = "Global Revenue Middle East Last Financial Year";
        private const string RevenueLatinAmericaQuestionTitle = "Global Revenue Latin America Last Financial Year";
        private const string RevenueAfricaQuestionTitle = "Global Revenue Africa Last Financial Year";
        private const string RevenueOtherQuestionTitle = "Global Revenue Other Last Financial Year";
        private const string RegionalEmploymentQuestionPrefix = "Regional Employment ";
        private const string RegionalEmploymentQuestionSuffix = " Last Financial Year";
        private static readonly bool EnableWagesSectorFallback = true;
        private const decimal WagesPrimaryRatio = 0.6m;
        private const decimal WagesSecondaryRatio = 0.3m;
        private static readonly bool EnableEbitdaSectorFallback = true;
        private const decimal EbitdaPrimaryRatio = 0.15m;
        private const decimal EbitdaSecondaryRatio = 0.10m;
        private static readonly bool EnableRevenueNzSectorFallback = false;
        private const decimal RevenueNzExportRatio = 0.20m;
        private static readonly bool EnableRevenueAustraliaSectorFallback = false;
        private const decimal RevenueAustraliaExportRatio = 0.20m;
    private static readonly bool EnableRevenueChinaSectorFallback = false;
    private const decimal RevenueChinaExportRatio = 0.20m;
    private static readonly bool EnableRevenueRestOfAsiaSectorFallback = false;
    private const decimal RevenueRestOfAsiaExportRatio = 0.20m;
    private static readonly bool EnableRevenueNorthAmericaSectorFallback = false;
    private const decimal RevenueNorthAmericaExportRatio = 0.20m;
    private static readonly bool EnableRevenueEuropeSectorFallback = false;
    private const decimal RevenueEuropeExportRatio = 0.20m;
    private static readonly bool EnableRevenueMiddleEastSectorFallback = false;
    private const decimal RevenueMiddleEastExportRatio = 0.20m;
    private static readonly bool EnableRevenueLatinAmericaSectorFallback = false;
    private const decimal RevenueLatinAmericaExportRatio = 0.20m;
    private static readonly bool EnableRevenueAfricaSectorFallback = false;
    private const decimal RevenueAfricaExportRatio = 0.20m;
    private static readonly bool EnableRevenueOtherSectorFallback = false;
    private const decimal RevenueOtherExportRatio = 0.20m;

        private readonly ApplicationDbContext _context;

        public int CompanySurveyId { get; set; }
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public int TargetFinancialYear { get; set; }
        public int? FinancialYearFilter { get; set; }
        public bool EstimateEnabled { get; set; }
        public bool IsLocked { get; set; }
        public decimal? ForecastedRevenue { get; set; }
        public string ForecastReason { get; set; } = string.Empty;
        public decimal? ForecastedEmployment { get; set; }
        public string EmploymentForecastReason { get; set; } = string.Empty;
        public decimal? ForecastedWages { get; set; }
        public string WagesForecastReason { get; set; } = string.Empty;
        public decimal? ForecastedResearchDevelopment { get; set; }
        public string ResearchDevelopmentForecastReason { get; set; } = string.Empty;
        public decimal? ForecastedSalesMarketing { get; set; }
        public string SalesMarketingForecastReason { get; set; } = string.Empty;
        public decimal? ForecastedEbitda { get; set; }
        public string EbitdaForecastReason { get; set; } = string.Empty;
        public decimal? ForecastedRevenueNz { get; set; }
        public string RevenueNzForecastReason { get; set; } = string.Empty;
        public decimal? ForecastedRevenueAustralia { get; set; }
        public string RevenueAustraliaForecastReason { get; set; } = string.Empty;
        public decimal? ForecastedRevenueChina { get; set; }
        public string RevenueChinaForecastReason { get; set; } = string.Empty;
        public decimal? ForecastedRevenueRestOfAsia { get; set; }
        public string RevenueRestOfAsiaForecastReason { get; set; } = string.Empty;
        public decimal? ForecastedRevenueNorthAmerica { get; set; }
        public string RevenueNorthAmericaForecastReason { get; set; } = string.Empty;
        public decimal? ForecastedRevenueEurope { get; set; }
        public string RevenueEuropeForecastReason { get; set; } = string.Empty;
        public decimal? ForecastedRevenueMiddleEast { get; set; }
        public string RevenueMiddleEastForecastReason { get; set; } = string.Empty;
        public decimal? ForecastedRevenueLatinAmerica { get; set; }
        public string RevenueLatinAmericaForecastReason { get; set; } = string.Empty;
        public decimal? ForecastedRevenueAfrica { get; set; }
        public string RevenueAfricaForecastReason { get; set; } = string.Empty;
        public decimal? ForecastedRevenueOther { get; set; }
        public string RevenueOtherForecastReason { get; set; } = string.Empty;
        public decimal? ForecastedRegionalEmployment { get; set; }
        public string RegionalEmploymentForecastReason { get; set; } = string.Empty;
        public string SelectedRegionalEmploymentQuestionTitle { get; set; } = string.Empty;
        public string SelectedRegionalEmploymentRegionLabel { get; set; } = string.Empty;
        public string GeneratedSummary { get; set; } = string.Empty;
        public string SaveMessage { get; set; } = string.Empty;
        public string CheckMetricLabel { get; set; } = string.Empty;
        public string CheckMetricKey { get; set; } = string.Empty;
        public decimal? CheckSelectedValue { get; set; }
        public string AppliedMetricKey { get; set; } = string.Empty;
        public string AppliedRegionQuestionTitle { get; set; } = string.Empty;
        public List<CalculationCandidate> CheckCandidates { get; set; } = new();

        public List<MetricHistoryRow> LastFiveYearsRevenue { get; set; } = new();
        public List<MetricHistoryRow> LastFiveYearsEmployment { get; set; } = new();
        public List<MetricHistoryRow> LastFiveYearsWages { get; set; } = new();
        public List<MetricHistoryRow> LastFiveYearsResearchDevelopment { get; set; } = new();
        public List<MetricHistoryRow> LastFiveYearsSalesMarketing { get; set; } = new();
        public List<MetricHistoryRow> LastFiveYearsEbitda { get; set; } = new();
        public List<MetricHistoryRow> LastFiveYearsRevenueNz { get; set; } = new();
        public List<MetricHistoryRow> LastFiveYearsRevenueAustralia { get; set; } = new();
    public List<MetricHistoryRow> LastFiveYearsRevenueChina { get; set; } = new();
    public List<MetricHistoryRow> LastFiveYearsRevenueRestOfAsia { get; set; } = new();
    public List<MetricHistoryRow> LastFiveYearsRevenueNorthAmerica { get; set; } = new();
    public List<MetricHistoryRow> LastFiveYearsRevenueEurope { get; set; } = new();
    public List<MetricHistoryRow> LastFiveYearsRevenueMiddleEast { get; set; } = new();
    public List<MetricHistoryRow> LastFiveYearsRevenueLatinAmerica { get; set; } = new();
    public List<MetricHistoryRow> LastFiveYearsRevenueAfrica { get; set; } = new();
    public List<MetricHistoryRow> LastFiveYearsRevenueOther { get; set; } = new();
    public List<string> AvailableRegionalEmploymentQuestionTitles { get; set; } = new();
    public List<MetricHistoryRow> LastFiveYearsRegionalEmploymentSelected { get; set; } = new();
    public Dictionary<string, List<MetricHistoryRow>> LastFiveYearsRegionalEmploymentByQuestionTitle { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public List<EstimationGroupSection> EstimationGroupSections { get; set; } = new();
        public Dictionary<string, decimal> GroupQuestionAppliedValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> GroupQuestionAppliedReasons { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public EstimationModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> OnGetAsync(int? companySurveyId, int? financialYear)
        {
            if (!companySurveyId.HasValue)
            {
                return NotFound();
            }

            CompanySurveyId = companySurveyId.Value;
            FinancialYearFilter = financialYear;

            var loaded = await LoadPageDataAsync();
            if (!loaded)
            {
                return NotFound();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostCalculateRevenueAsync(int companySurveyId, int? financialYear)
        {
            CompanySurveyId = companySurveyId;
            FinancialYearFilter = financialYear;

            var loaded = await LoadPageDataAsync();
            if (!loaded)
            {
                return NotFound();
            }

            if (!EstimateEnabled)
            {
                ModelState.AddModelError(string.Empty, "Revenue calculation is only available when Estimate is enabled for this Company Survey record.");
                return Page();
            }

            if (IsLocked)
            {
                ModelState.AddModelError(string.Empty, "Revenue calculation is not allowed because this Company Survey record is locked.");
                return Page();
            }

            var allRevenueHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);
            var revenueForecast = await CalculateForecastedRevenueWithSectorFallbackAsync(allRevenueHistory, TargetFinancialYear);
            ForecastedRevenue = revenueForecast.Value;
            ForecastReason = revenueForecast.Reason;

            ForecastedEmployment = null;
            EmploymentForecastReason = string.Empty;
            ForecastedWages = null;
            WagesForecastReason = string.Empty;
            ForecastedResearchDevelopment = null;
            ResearchDevelopmentForecastReason = string.Empty;
            ForecastedSalesMarketing = null;
            SalesMarketingForecastReason = string.Empty;
            ForecastedEbitda = null;
            EbitdaForecastReason = string.Empty;
            ForecastedRevenueNz = null;
            RevenueNzForecastReason = string.Empty;
            ForecastedRevenueAustralia = null;
            RevenueAustraliaForecastReason = string.Empty;
            ForecastedRevenueChina = null;
            RevenueChinaForecastReason = string.Empty;
            ForecastedRevenueRestOfAsia = null;
            RevenueRestOfAsiaForecastReason = string.Empty;
            ForecastedRevenueNorthAmerica = null;
            RevenueNorthAmericaForecastReason = string.Empty;
            ForecastedRevenueEurope = null;
            RevenueEuropeForecastReason = string.Empty;
            ForecastedRevenueMiddleEast = null;
            RevenueMiddleEastForecastReason = string.Empty;
            ForecastedRevenueLatinAmerica = null;
            RevenueLatinAmericaForecastReason = string.Empty;
            ForecastedRevenueAfrica = null;
            RevenueAfricaForecastReason = string.Empty;
            ForecastedRevenueOther = null;
            RevenueOtherForecastReason = string.Empty;

            GeneratedSummary = BuildSingleMetricSummary(
                metricName: "Revenue",
                targetYear: TargetFinancialYear,
                history: allRevenueHistory,
                forecastValue: ForecastedRevenue,
                reason: ForecastReason);

            return Page();
        }

        public async Task<IActionResult> OnPostCheckAsync(int companySurveyId, int? financialYear, string metricKey, string? regionQuestionTitle)
        {
            CompanySurveyId = companySurveyId;
            FinancialYearFilter = financialYear;
            SelectedRegionalEmploymentQuestionTitle = regionQuestionTitle ?? string.Empty;

            var loaded = await LoadPageDataAsync();
            if (!loaded)
            {
                return NotFound();
            }

            var preview = await BuildCheckPreviewAsync(metricKey, regionQuestionTitle);
            CheckMetricLabel = preview.MetricLabel;
            CheckMetricKey = metricKey;
            CheckCandidates = preview.Candidates;
            CheckSelectedValue = CheckCandidates.FirstOrDefault(x => x.IsUsed)?.Value;

            return Page();
        }

        public async Task<IActionResult> OnPostApplyAsync(int companySurveyId, int? financialYear, string metricKey, string? regionQuestionTitle, decimal? manualValue)
        {
            CompanySurveyId = companySurveyId;
            FinancialYearFilter = financialYear;
            SelectedRegionalEmploymentQuestionTitle = regionQuestionTitle ?? string.Empty;

            var loaded = await LoadPageDataAsync();
            if (!loaded)
            {
                return NotFound();
            }

            if (!EstimateEnabled)
            {
                ModelState.AddModelError(string.Empty, "Apply is only available when Estimate is enabled for this Company Survey record.");
                return Page();
            }

            if (IsLocked)
            {
                ModelState.AddModelError(string.Empty, "Apply is not allowed because this Company Survey record is locked.");
                return Page();
            }

            if (manualValue.HasValue && manualValue.Value < 0)
            {
                ModelState.AddModelError(string.Empty, "Manual value must be zero or greater.");
                return Page();
            }

            if (manualValue.HasValue && HasMoreThanTwoDecimalPlaces(manualValue.Value))
            {
                ModelState.AddModelError(string.Empty, "Manual value can have at most 2 decimal places.");
                return Page();
            }

            var calculation = await CalculateMetricByKeyAsync(metricKey, regionQuestionTitle);
            var valueToApply = manualValue ?? calculation.Value;

            if (!valueToApply.HasValue)
            {
                ModelState.AddModelError(string.Empty, "No valid value available to apply. Enter a manual value or use Check to verify a calculated value exists.");
                return Page();
            }

            var reasonToApply = manualValue.HasValue
                ? $"Manual input applied ({manualValue.Value:N2})."
                : calculation.Reason;

            ClearForecastOutputs();
            ApplyForecastByMetricKey(metricKey, regionQuestionTitle, valueToApply.Value, reasonToApply);

            // Save the applied value to the Answer table in the database
            await SaveAppliedAnswerAsync(metricKey, regionQuestionTitle, valueToApply.Value, TargetFinancialYear);

            // Reload the historical data for the applied metric so the table reflects the new 2026 value
            await ReloadHistoricalDataForMetricAsync(metricKey, regionQuestionTitle);

            GeneratedSummary = BuildSingleMetricSummary(
                metricName: calculation.MetricLabel,
                targetYear: TargetFinancialYear,
                history: calculation.History,
                forecastValue: valueToApply,
                reason: reasonToApply);

            AppliedMetricKey = metricKey ?? string.Empty;
            AppliedRegionQuestionTitle = regionQuestionTitle ?? string.Empty;

            return Page();
        }

        public async Task<IActionResult> OnPostCalculateEmploymentAsync(int companySurveyId, int? financialYear)
        {
            CompanySurveyId = companySurveyId;
            FinancialYearFilter = financialYear;

            var loaded = await LoadPageDataAsync();
            if (!loaded)
            {
                return NotFound();
            }

            if (!EstimateEnabled)
            {
                ModelState.AddModelError(string.Empty, "Employment calculation is only available when Estimate is enabled for this Company Survey record.");
                return Page();
            }

            if (IsLocked)
            {
                ModelState.AddModelError(string.Empty, "Employment calculation is not allowed because this Company Survey record is locked.");
                return Page();
            }

            var allEmploymentHistory = await GetCompanyMetricHistoryAsync(CompanyId, EmploymentQuestionTitle, EmploymentQuestionTitleLegacy);

            var employmentForecast = await CalculateForecastedEmploymentWithSectorFallbackAsync(allEmploymentHistory, TargetFinancialYear);
            ForecastedEmployment = employmentForecast.Value;
            EmploymentForecastReason = employmentForecast.Reason;

            ForecastedRevenue = null;
            ForecastReason = string.Empty;
            ForecastedWages = null;
            WagesForecastReason = string.Empty;
            ForecastedResearchDevelopment = null;
            ResearchDevelopmentForecastReason = string.Empty;
            ForecastedSalesMarketing = null;
            SalesMarketingForecastReason = string.Empty;
            ForecastedEbitda = null;
            EbitdaForecastReason = string.Empty;
            ForecastedRevenueNz = null;
            RevenueNzForecastReason = string.Empty;
            ForecastedRevenueAustralia = null;
            RevenueAustraliaForecastReason = string.Empty;
            ForecastedRevenueChina = null;
            RevenueChinaForecastReason = string.Empty;
            ForecastedRevenueRestOfAsia = null;
            RevenueRestOfAsiaForecastReason = string.Empty;
            ForecastedRevenueNorthAmerica = null;
            RevenueNorthAmericaForecastReason = string.Empty;
            ForecastedRevenueEurope = null;
            RevenueEuropeForecastReason = string.Empty;
            ForecastedRevenueMiddleEast = null;
            RevenueMiddleEastForecastReason = string.Empty;
            ForecastedRevenueLatinAmerica = null;
            RevenueLatinAmericaForecastReason = string.Empty;
            ForecastedRevenueAfrica = null;
            RevenueAfricaForecastReason = string.Empty;
            ForecastedRevenueOther = null;
            RevenueOtherForecastReason = string.Empty;

            GeneratedSummary = BuildSingleMetricSummary(
                metricName: "Employment",
                targetYear: TargetFinancialYear,
                history: allEmploymentHistory,
                forecastValue: ForecastedEmployment,
                reason: EmploymentForecastReason);

            return Page();
        }

        public async Task<IActionResult> OnPostCalculateWagesAsync(int companySurveyId, int? financialYear)
        {
            CompanySurveyId = companySurveyId;
            FinancialYearFilter = financialYear;

            var loaded = await LoadPageDataAsync();
            if (!loaded)
            {
                return NotFound();
            }

            if (!EstimateEnabled)
            {
                ModelState.AddModelError(string.Empty, "Wages and salaries calculation is only available when Estimate is enabled for this Company Survey record.");
                return Page();
            }

            if (IsLocked)
            {
                ModelState.AddModelError(string.Empty, "Wages and salaries calculation is not allowed because this Company Survey record is locked.");
                return Page();
            }

            var wagesHistory = await GetCompanyMetricHistoryAsync(CompanyId, WagesQuestionTitle);
            var revenueHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);

            var wagesForecast = await CalculateForecastedWagesWithSectorFallbackAsync(wagesHistory, revenueHistory, TargetFinancialYear);
            ForecastedWages = wagesForecast.Value;
            WagesForecastReason = wagesForecast.Reason;

            ForecastedRevenue = null;
            ForecastReason = string.Empty;
            ForecastedEmployment = null;
            EmploymentForecastReason = string.Empty;
            ForecastedResearchDevelopment = null;
            ResearchDevelopmentForecastReason = string.Empty;
            ForecastedSalesMarketing = null;
            SalesMarketingForecastReason = string.Empty;
            ForecastedEbitda = null;
            EbitdaForecastReason = string.Empty;
            ForecastedRevenueNz = null;
            RevenueNzForecastReason = string.Empty;
            ForecastedRevenueAustralia = null;
            RevenueAustraliaForecastReason = string.Empty;
            ForecastedRevenueChina = null;
            RevenueChinaForecastReason = string.Empty;
            ForecastedRevenueRestOfAsia = null;
            RevenueRestOfAsiaForecastReason = string.Empty;
            ForecastedRevenueNorthAmerica = null;
            RevenueNorthAmericaForecastReason = string.Empty;
            ForecastedRevenueEurope = null;
            RevenueEuropeForecastReason = string.Empty;
            ForecastedRevenueMiddleEast = null;
            RevenueMiddleEastForecastReason = string.Empty;
            ForecastedRevenueLatinAmerica = null;
            RevenueLatinAmericaForecastReason = string.Empty;
            ForecastedRevenueAfrica = null;
            RevenueAfricaForecastReason = string.Empty;
            ForecastedRevenueOther = null;
            RevenueOtherForecastReason = string.Empty;

            GeneratedSummary = BuildSingleMetricSummary(
                metricName: "Wages & Salaries",
                targetYear: TargetFinancialYear,
                history: wagesHistory,
                forecastValue: ForecastedWages,
                reason: WagesForecastReason);

            return Page();
        }

        public async Task<IActionResult> OnPostCalculateRegionalEmploymentAsync(int companySurveyId, int? financialYear, string regionQuestionTitle)
        {
            CompanySurveyId = companySurveyId;
            FinancialYearFilter = financialYear;
            SelectedRegionalEmploymentQuestionTitle = regionQuestionTitle ?? string.Empty;

            var loaded = await LoadPageDataAsync();
            if (!loaded)
            {
                return NotFound();
            }

            if (!EstimateEnabled)
            {
                ModelState.AddModelError(string.Empty, "Regional employment calculation is only available when Estimate is enabled for this Company Survey record.");
                return Page();
            }

            if (IsLocked)
            {
                ModelState.AddModelError(string.Empty, "Regional employment calculation is not allowed because this Company Survey record is locked.");
                return Page();
            }

            if (string.IsNullOrWhiteSpace(SelectedRegionalEmploymentQuestionTitle)
                || !AvailableRegionalEmploymentQuestionTitles.Contains(SelectedRegionalEmploymentQuestionTitle, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, "Regional employment question is not available for this survey.");
                return Page();
            }

            var regionalHistory = await GetCompanyMetricHistoryAsync(CompanyId, SelectedRegionalEmploymentQuestionTitle);
            var totalEmploymentHistory = await GetCompanyMetricHistoryAsync(CompanyId, EmploymentQuestionTitle, EmploymentQuestionTitleLegacy);

            var regionalForecast = await CalculateForecastedRegionalEmploymentWithSectorFallbackAsync(
                regionalHistory,
                totalEmploymentHistory,
                TargetFinancialYear,
                SelectedRegionalEmploymentQuestionTitle);

            ForecastedRegionalEmployment = regionalForecast.Value;
            RegionalEmploymentForecastReason = regionalForecast.Reason;
            SelectedRegionalEmploymentRegionLabel = GetRegionalEmploymentRegionLabel(SelectedRegionalEmploymentQuestionTitle);
            LastFiveYearsRegionalEmploymentSelected = GetLastFiveYears(regionalHistory);

            GeneratedSummary = BuildSingleMetricSummary(
                metricName: $"Regional Employment - {SelectedRegionalEmploymentRegionLabel}",
                targetYear: TargetFinancialYear,
                history: regionalHistory,
                forecastValue: ForecastedRegionalEmployment,
                reason: RegionalEmploymentForecastReason);

            return Page();
        }

        public async Task<IActionResult> OnPostCalculateGroupQuestionAsync(int companySurveyId, int? financialYear, string? regionQuestionTitle)
        {
            CompanySurveyId = companySurveyId;
            FinancialYearFilter = financialYear;

            var loaded = await LoadPageDataAsync();
            if (!loaded) return NotFound();

            if (!EstimateEnabled)
            {
                ModelState.AddModelError(string.Empty, "Group question calculation is only available when Estimate is enabled for this Company Survey record.");
                return Page();
            }

            if (IsLocked)
            {
                ModelState.AddModelError(string.Empty, "Group question calculation is not allowed because this Company Survey record is locked.");
                return Page();
            }

            if (string.IsNullOrWhiteSpace(regionQuestionTitle))
            {
                ModelState.AddModelError(string.Empty, "Question title is required.");
                return Page();
            }

            var gqHistory = await GetCompanyMetricHistoryAsync(CompanyId, regionQuestionTitle);
            var calc = CalculateSimpleTrendForecast(gqHistory, TargetFinancialYear);

            ClearForecastOutputs();
            if (calc.Value.HasValue)
            {
                GroupQuestionAppliedValues[regionQuestionTitle] = calc.Value.Value;
                GroupQuestionAppliedReasons[regionQuestionTitle] = calc.Reason;
            }

            GeneratedSummary = BuildSingleMetricSummary(
                metricName: regionQuestionTitle,
                targetYear: TargetFinancialYear,
                history: gqHistory,
                forecastValue: calc.Value,
                reason: calc.Reason);

            return Page();
        }

        public async Task<IActionResult> OnPostCalculateResearchDevelopmentAsync(int companySurveyId, int? financialYear)
        {
            CompanySurveyId = companySurveyId;
            FinancialYearFilter = financialYear;

            var loaded = await LoadPageDataAsync();
            if (!loaded)
            {
                return NotFound();
            }

            if (!EstimateEnabled)
            {
                ModelState.AddModelError(string.Empty, "Research and development calculation is only available when Estimate is enabled for this Company Survey record.");
                return Page();
            }

            if (IsLocked)
            {
                ModelState.AddModelError(string.Empty, "Research and development calculation is not allowed because this Company Survey record is locked.");
                return Page();
            }

            var researchDevelopmentHistory = await GetCompanyMetricHistoryAsync(CompanyId, ResearchDevelopmentQuestionTitle);
            var revenueHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);

            var researchDevelopmentForecast = await CalculateForecastedResearchDevelopmentWithSectorFallbackAsync(researchDevelopmentHistory, revenueHistory, TargetFinancialYear);
            ForecastedResearchDevelopment = researchDevelopmentForecast.Value;
            ResearchDevelopmentForecastReason = researchDevelopmentForecast.Reason;

            ForecastedRevenue = null;
            ForecastReason = string.Empty;
            ForecastedEmployment = null;
            EmploymentForecastReason = string.Empty;
            ForecastedWages = null;
            WagesForecastReason = string.Empty;
            ForecastedSalesMarketing = null;
            SalesMarketingForecastReason = string.Empty;
            ForecastedEbitda = null;
            EbitdaForecastReason = string.Empty;
            ForecastedRevenueNz = null;
            RevenueNzForecastReason = string.Empty;
            ForecastedRevenueAustralia = null;
            RevenueAustraliaForecastReason = string.Empty;
            ForecastedRevenueChina = null;
            RevenueChinaForecastReason = string.Empty;
            ForecastedRevenueRestOfAsia = null;
            RevenueRestOfAsiaForecastReason = string.Empty;
            ForecastedRevenueNorthAmerica = null;
            RevenueNorthAmericaForecastReason = string.Empty;
            ForecastedRevenueEurope = null;
            RevenueEuropeForecastReason = string.Empty;
            ForecastedRevenueMiddleEast = null;
            RevenueMiddleEastForecastReason = string.Empty;
            ForecastedRevenueLatinAmerica = null;
            RevenueLatinAmericaForecastReason = string.Empty;
            ForecastedRevenueAfrica = null;
            RevenueAfricaForecastReason = string.Empty;
            ForecastedRevenueOther = null;
            RevenueOtherForecastReason = string.Empty;

            GeneratedSummary = BuildSingleMetricSummary(
                metricName: "Research & Development",
                targetYear: TargetFinancialYear,
                history: researchDevelopmentHistory,
                forecastValue: ForecastedResearchDevelopment,
                reason: ResearchDevelopmentForecastReason);

            return Page();
        }

        public async Task<IActionResult> OnPostCalculateSalesMarketingAsync(int companySurveyId, int? financialYear)
        {
            CompanySurveyId = companySurveyId;
            FinancialYearFilter = financialYear;

            var loaded = await LoadPageDataAsync();
            if (!loaded)
            {
                return NotFound();
            }

            if (!EstimateEnabled)
            {
                ModelState.AddModelError(string.Empty, "Sales and marketing calculation is only available when Estimate is enabled for this Company Survey record.");
                return Page();
            }

            if (IsLocked)
            {
                ModelState.AddModelError(string.Empty, "Sales and marketing calculation is not allowed because this Company Survey record is locked.");
                return Page();
            }

            var salesMarketingHistory = await GetCompanyMetricHistoryAsync(CompanyId, SalesMarketingQuestionTitle);
            var revenueHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);

            var salesMarketingForecast = await CalculateForecastedSalesMarketingWithSectorFallbackAsync(salesMarketingHistory, revenueHistory, TargetFinancialYear);
            ForecastedSalesMarketing = salesMarketingForecast.Value;
            SalesMarketingForecastReason = salesMarketingForecast.Reason;

            ForecastedRevenue = null;
            ForecastReason = string.Empty;
            ForecastedEmployment = null;
            EmploymentForecastReason = string.Empty;
            ForecastedWages = null;
            WagesForecastReason = string.Empty;
            ForecastedResearchDevelopment = null;
            ResearchDevelopmentForecastReason = string.Empty;
            ForecastedEbitda = null;
            EbitdaForecastReason = string.Empty;
            ForecastedRevenueNz = null;
            RevenueNzForecastReason = string.Empty;
            ForecastedRevenueAustralia = null;
            RevenueAustraliaForecastReason = string.Empty;
            ForecastedRevenueChina = null;
            RevenueChinaForecastReason = string.Empty;
            ForecastedRevenueRestOfAsia = null;
            RevenueRestOfAsiaForecastReason = string.Empty;
            ForecastedRevenueNorthAmerica = null;
            RevenueNorthAmericaForecastReason = string.Empty;
            ForecastedRevenueEurope = null;
            RevenueEuropeForecastReason = string.Empty;
            ForecastedRevenueMiddleEast = null;
            RevenueMiddleEastForecastReason = string.Empty;
            ForecastedRevenueLatinAmerica = null;
            RevenueLatinAmericaForecastReason = string.Empty;
            ForecastedRevenueAfrica = null;
            RevenueAfricaForecastReason = string.Empty;
            ForecastedRevenueOther = null;
            RevenueOtherForecastReason = string.Empty;

            GeneratedSummary = BuildSingleMetricSummary(
                metricName: "Sales & Marketing",
                targetYear: TargetFinancialYear,
                history: salesMarketingHistory,
                forecastValue: ForecastedSalesMarketing,
                reason: SalesMarketingForecastReason);

            return Page();
        }

        public async Task<IActionResult> OnPostCalculateEbitdaAsync(int companySurveyId, int? financialYear)
        {
            CompanySurveyId = companySurveyId;
            FinancialYearFilter = financialYear;

            var loaded = await LoadPageDataAsync();
            if (!loaded)
            {
                return NotFound();
            }

            if (!EstimateEnabled)
            {
                ModelState.AddModelError(string.Empty, "EBITDA calculation is only available when Estimate is enabled for this Company Survey record.");
                return Page();
            }

            if (IsLocked)
            {
                ModelState.AddModelError(string.Empty, "EBITDA calculation is not allowed because this Company Survey record is locked.");
                return Page();
            }

            var ebitdaHistory = await GetCompanyMetricHistoryAsync(CompanyId, EbitdaQuestionTitle);
            var revenueHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);

            var ebitdaForecast = await CalculateForecastedEbitdaWithSectorFallbackAsync(ebitdaHistory, revenueHistory, TargetFinancialYear);
            ForecastedEbitda = ebitdaForecast.Value;
            EbitdaForecastReason = ebitdaForecast.Reason;

            ForecastedRevenue = null;
            ForecastReason = string.Empty;
            ForecastedEmployment = null;
            EmploymentForecastReason = string.Empty;
            ForecastedWages = null;
            WagesForecastReason = string.Empty;
            ForecastedResearchDevelopment = null;
            ResearchDevelopmentForecastReason = string.Empty;
            ForecastedSalesMarketing = null;
            SalesMarketingForecastReason = string.Empty;
            ForecastedRevenueNz = null;
            RevenueNzForecastReason = string.Empty;
            ForecastedRevenueAustralia = null;
            RevenueAustraliaForecastReason = string.Empty;
            ForecastedRevenueChina = null;
            RevenueChinaForecastReason = string.Empty;
            ForecastedRevenueRestOfAsia = null;
            RevenueRestOfAsiaForecastReason = string.Empty;
            ForecastedRevenueNorthAmerica = null;
            RevenueNorthAmericaForecastReason = string.Empty;
            ForecastedRevenueEurope = null;
            RevenueEuropeForecastReason = string.Empty;
            ForecastedRevenueMiddleEast = null;
            RevenueMiddleEastForecastReason = string.Empty;
            ForecastedRevenueLatinAmerica = null;
            RevenueLatinAmericaForecastReason = string.Empty;
            ForecastedRevenueAfrica = null;
            RevenueAfricaForecastReason = string.Empty;
            ForecastedRevenueOther = null;
            RevenueOtherForecastReason = string.Empty;

            GeneratedSummary = BuildSingleMetricSummary(
                metricName: "EBITDA",
                targetYear: TargetFinancialYear,
                history: ebitdaHistory,
                forecastValue: ForecastedEbitda,
                reason: EbitdaForecastReason);

            return Page();
        }

        public async Task<IActionResult> OnPostCalculateRevenueNzAsync(int companySurveyId, int? financialYear)
        {
            CompanySurveyId = companySurveyId;
            FinancialYearFilter = financialYear;

            var loaded = await LoadPageDataAsync();
            if (!loaded)
            {
                return NotFound();
            }

            if (!EstimateEnabled)
            {
                ModelState.AddModelError(string.Empty, "Revenue NZ calculation is only available when Estimate is enabled for this Company Survey record.");
                return Page();
            }

            if (IsLocked)
            {
                ModelState.AddModelError(string.Empty, "Revenue NZ calculation is not allowed because this Company Survey record is locked.");
                return Page();
            }

            var revenueNzHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueNzQuestionTitle);
            var revenueHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);

            var revenueNzForecast = await CalculateForecastedRegionalRevenueWithSectorFallbackAsync(revenueNzHistory, revenueHistory, TargetFinancialYear, RevenueNzQuestionTitle, "Global Revenue NZ");
            ForecastedRevenueNz = revenueNzForecast.Value;
            RevenueNzForecastReason = revenueNzForecast.Reason;

            ForecastedRevenue = null;
            ForecastReason = string.Empty;
            ForecastedEmployment = null;
            EmploymentForecastReason = string.Empty;
            ForecastedWages = null;
            WagesForecastReason = string.Empty;
            ForecastedResearchDevelopment = null;
            ResearchDevelopmentForecastReason = string.Empty;
            ForecastedSalesMarketing = null;
            SalesMarketingForecastReason = string.Empty;
            ForecastedEbitda = null;
            EbitdaForecastReason = string.Empty;
            ForecastedRevenueAustralia = null;
            RevenueAustraliaForecastReason = string.Empty;
            ForecastedRevenueChina = null;
            RevenueChinaForecastReason = string.Empty;
            ForecastedRevenueRestOfAsia = null;
            RevenueRestOfAsiaForecastReason = string.Empty;
            ForecastedRevenueNorthAmerica = null;
            RevenueNorthAmericaForecastReason = string.Empty;
            ForecastedRevenueEurope = null;
            RevenueEuropeForecastReason = string.Empty;
            ForecastedRevenueMiddleEast = null;
            RevenueMiddleEastForecastReason = string.Empty;
            ForecastedRevenueLatinAmerica = null;
            RevenueLatinAmericaForecastReason = string.Empty;
            ForecastedRevenueAfrica = null;
            RevenueAfricaForecastReason = string.Empty;
            ForecastedRevenueOther = null;
            RevenueOtherForecastReason = string.Empty;

            GeneratedSummary = BuildSingleMetricSummary(
                metricName: "Revenue NZ",
                targetYear: TargetFinancialYear,
                history: revenueNzHistory,
                forecastValue: ForecastedRevenueNz,
                reason: RevenueNzForecastReason);

            return Page();
        }

        public async Task<IActionResult> OnPostCalculateRevenueAustraliaAsync(int companySurveyId, int? financialYear)
        {
            CompanySurveyId = companySurveyId;
            FinancialYearFilter = financialYear;

            var loaded = await LoadPageDataAsync();
            if (!loaded)
            {
                return NotFound();
            }

            if (!EstimateEnabled)
            {
                ModelState.AddModelError(string.Empty, "Revenue Australia calculation is only available when Estimate is enabled for this Company Survey record.");
                return Page();
            }

            if (IsLocked)
            {
                ModelState.AddModelError(string.Empty, "Revenue Australia calculation is not allowed because this Company Survey record is locked.");
                return Page();
            }

            var revenueAustraliaHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueAustraliaQuestionTitle);
            var revenueHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);

            var revenueAustraliaForecast = await CalculateForecastedRegionalRevenueWithSectorFallbackAsync(revenueAustraliaHistory, revenueHistory, TargetFinancialYear, RevenueAustraliaQuestionTitle, "Global Revenue Australia");
            ForecastedRevenueAustralia = revenueAustraliaForecast.Value;
            RevenueAustraliaForecastReason = revenueAustraliaForecast.Reason;

            ForecastedRevenue = null;
            ForecastReason = string.Empty;
            ForecastedEmployment = null;
            EmploymentForecastReason = string.Empty;
            ForecastedWages = null;
            WagesForecastReason = string.Empty;
            ForecastedResearchDevelopment = null;
            ResearchDevelopmentForecastReason = string.Empty;
            ForecastedSalesMarketing = null;
            SalesMarketingForecastReason = string.Empty;
            ForecastedEbitda = null;
            EbitdaForecastReason = string.Empty;
            ForecastedRevenueNz = null;
            RevenueNzForecastReason = string.Empty;
            ForecastedRevenueChina = null;
            RevenueChinaForecastReason = string.Empty;
            ForecastedRevenueRestOfAsia = null;
            RevenueRestOfAsiaForecastReason = string.Empty;
            ForecastedRevenueNorthAmerica = null;
            RevenueNorthAmericaForecastReason = string.Empty;
            ForecastedRevenueEurope = null;
            RevenueEuropeForecastReason = string.Empty;
            ForecastedRevenueMiddleEast = null;
            RevenueMiddleEastForecastReason = string.Empty;
            ForecastedRevenueLatinAmerica = null;
            RevenueLatinAmericaForecastReason = string.Empty;
            ForecastedRevenueAfrica = null;
            RevenueAfricaForecastReason = string.Empty;
            ForecastedRevenueOther = null;
            RevenueOtherForecastReason = string.Empty;

            GeneratedSummary = BuildSingleMetricSummary(
                metricName: "Revenue Australia",
                targetYear: TargetFinancialYear,
                history: revenueAustraliaHistory,
                forecastValue: ForecastedRevenueAustralia,
                reason: RevenueAustraliaForecastReason);

            return Page();
        }

                    public async Task<IActionResult> OnPostCalculateRevenueChinaAsync(int companySurveyId, int? financialYear)
                    {
                        CompanySurveyId = companySurveyId;
                        FinancialYearFilter = financialYear;
                        var loaded = await LoadPageDataAsync();
                        if (!loaded) return NotFound();
                        if (!EstimateEnabled) { ModelState.AddModelError(string.Empty, "Revenue China calculation is only available when Estimate is enabled for this Company Survey record."); return Page(); }
                        if (IsLocked) { ModelState.AddModelError(string.Empty, "Revenue China calculation is not allowed because this Company Survey record is locked."); return Page(); }
                        var revenueChinaHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueChinaQuestionTitle);
                        var revenueHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);
                        var revenueChinaForecast = await CalculateForecastedRegionalRevenueWithSectorFallbackAsync(revenueChinaHistory, revenueHistory, TargetFinancialYear, RevenueChinaQuestionTitle, "Global Revenue China");
                        ForecastedRevenueChina = revenueChinaForecast.Value;
                        RevenueChinaForecastReason = revenueChinaForecast.Reason;
                        ForecastedRevenue = null; ForecastReason = string.Empty;
                        ForecastedEmployment = null; EmploymentForecastReason = string.Empty;
                        ForecastedWages = null; WagesForecastReason = string.Empty;
                        ForecastedResearchDevelopment = null; ResearchDevelopmentForecastReason = string.Empty;
                        ForecastedSalesMarketing = null; SalesMarketingForecastReason = string.Empty;
                        ForecastedEbitda = null; EbitdaForecastReason = string.Empty;
                        ForecastedRevenueNz = null; RevenueNzForecastReason = string.Empty;
                        ForecastedRevenueAustralia = null; RevenueAustraliaForecastReason = string.Empty;
                        ForecastedRevenueRestOfAsia = null; RevenueRestOfAsiaForecastReason = string.Empty;
                        ForecastedRevenueNorthAmerica = null; RevenueNorthAmericaForecastReason = string.Empty;
                        ForecastedRevenueEurope = null; RevenueEuropeForecastReason = string.Empty;
                        ForecastedRevenueMiddleEast = null; RevenueMiddleEastForecastReason = string.Empty;
                        ForecastedRevenueLatinAmerica = null; RevenueLatinAmericaForecastReason = string.Empty;
                        ForecastedRevenueAfrica = null; RevenueAfricaForecastReason = string.Empty;
                        ForecastedRevenueOther = null; RevenueOtherForecastReason = string.Empty;
                        GeneratedSummary = BuildSingleMetricSummary("Revenue China", TargetFinancialYear, revenueChinaHistory, ForecastedRevenueChina, RevenueChinaForecastReason);
                        return Page();
                    }

                    public async Task<IActionResult> OnPostCalculateRevenueRestOfAsiaAsync(int companySurveyId, int? financialYear)
                    {
                        CompanySurveyId = companySurveyId;
                        FinancialYearFilter = financialYear;
                        var loaded = await LoadPageDataAsync();
                        if (!loaded) return NotFound();
                        if (!EstimateEnabled) { ModelState.AddModelError(string.Empty, "Revenue Rest of Asia calculation is only available when Estimate is enabled for this Company Survey record."); return Page(); }
                        if (IsLocked) { ModelState.AddModelError(string.Empty, "Revenue Rest of Asia calculation is not allowed because this Company Survey record is locked."); return Page(); }
                        var revenueRestOfAsiaHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueRestOfAsiaQuestionTitle);
                        var revenueHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);
                        var revenueRestOfAsiaForecast = await CalculateForecastedRegionalRevenueWithSectorFallbackAsync(revenueRestOfAsiaHistory, revenueHistory, TargetFinancialYear, RevenueRestOfAsiaQuestionTitle, "Global Revenue Rest of Asia");
                        ForecastedRevenueRestOfAsia = revenueRestOfAsiaForecast.Value;
                        RevenueRestOfAsiaForecastReason = revenueRestOfAsiaForecast.Reason;
                        ForecastedRevenue = null; ForecastReason = string.Empty;
                        ForecastedEmployment = null; EmploymentForecastReason = string.Empty;
                        ForecastedWages = null; WagesForecastReason = string.Empty;
                        ForecastedResearchDevelopment = null; ResearchDevelopmentForecastReason = string.Empty;
                        ForecastedSalesMarketing = null; SalesMarketingForecastReason = string.Empty;
                        ForecastedEbitda = null; EbitdaForecastReason = string.Empty;
                        ForecastedRevenueNz = null; RevenueNzForecastReason = string.Empty;
                        ForecastedRevenueAustralia = null; RevenueAustraliaForecastReason = string.Empty;
                        ForecastedRevenueChina = null; RevenueChinaForecastReason = string.Empty;
                        ForecastedRevenueNorthAmerica = null; RevenueNorthAmericaForecastReason = string.Empty;
                        ForecastedRevenueEurope = null; RevenueEuropeForecastReason = string.Empty;
                        ForecastedRevenueMiddleEast = null; RevenueMiddleEastForecastReason = string.Empty;
                        ForecastedRevenueLatinAmerica = null; RevenueLatinAmericaForecastReason = string.Empty;
                        ForecastedRevenueAfrica = null; RevenueAfricaForecastReason = string.Empty;
                        ForecastedRevenueOther = null; RevenueOtherForecastReason = string.Empty;
                        GeneratedSummary = BuildSingleMetricSummary("Revenue Rest of Asia", TargetFinancialYear, revenueRestOfAsiaHistory, ForecastedRevenueRestOfAsia, RevenueRestOfAsiaForecastReason);
                        return Page();
                    }

                    public async Task<IActionResult> OnPostCalculateRevenueNorthAmericaAsync(int companySurveyId, int? financialYear)
                    {
                        CompanySurveyId = companySurveyId;
                        FinancialYearFilter = financialYear;
                        var loaded = await LoadPageDataAsync();
                        if (!loaded) return NotFound();
                        if (!EstimateEnabled) { ModelState.AddModelError(string.Empty, "Revenue North America calculation is only available when Estimate is enabled for this Company Survey record."); return Page(); }
                        if (IsLocked) { ModelState.AddModelError(string.Empty, "Revenue North America calculation is not allowed because this Company Survey record is locked."); return Page(); }
                        var revenueNorthAmericaHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueNorthAmericaQuestionTitle);
                        var revenueHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);
                        var revenueNorthAmericaForecast = await CalculateForecastedRegionalRevenueWithSectorFallbackAsync(revenueNorthAmericaHistory, revenueHistory, TargetFinancialYear, RevenueNorthAmericaQuestionTitle, "Global Revenue North America");
                        ForecastedRevenueNorthAmerica = revenueNorthAmericaForecast.Value;
                        RevenueNorthAmericaForecastReason = revenueNorthAmericaForecast.Reason;
                        ForecastedRevenue = null; ForecastReason = string.Empty;
                        ForecastedEmployment = null; EmploymentForecastReason = string.Empty;
                        ForecastedWages = null; WagesForecastReason = string.Empty;
                        ForecastedResearchDevelopment = null; ResearchDevelopmentForecastReason = string.Empty;
                        ForecastedSalesMarketing = null; SalesMarketingForecastReason = string.Empty;
                        ForecastedEbitda = null; EbitdaForecastReason = string.Empty;
                        ForecastedRevenueNz = null; RevenueNzForecastReason = string.Empty;
                        ForecastedRevenueAustralia = null; RevenueAustraliaForecastReason = string.Empty;
                        ForecastedRevenueChina = null; RevenueChinaForecastReason = string.Empty;
                        ForecastedRevenueRestOfAsia = null; RevenueRestOfAsiaForecastReason = string.Empty;
                        ForecastedRevenueEurope = null; RevenueEuropeForecastReason = string.Empty;
                        ForecastedRevenueMiddleEast = null; RevenueMiddleEastForecastReason = string.Empty;
                        ForecastedRevenueLatinAmerica = null; RevenueLatinAmericaForecastReason = string.Empty;
                        ForecastedRevenueAfrica = null; RevenueAfricaForecastReason = string.Empty;
                        ForecastedRevenueOther = null; RevenueOtherForecastReason = string.Empty;
                        GeneratedSummary = BuildSingleMetricSummary("Revenue North America", TargetFinancialYear, revenueNorthAmericaHistory, ForecastedRevenueNorthAmerica, RevenueNorthAmericaForecastReason);
                        return Page();
                    }

                    public async Task<IActionResult> OnPostCalculateRevenueEuropeAsync(int companySurveyId, int? financialYear)
                    {
                        CompanySurveyId = companySurveyId;
                        FinancialYearFilter = financialYear;
                        var loaded = await LoadPageDataAsync();
                        if (!loaded) return NotFound();
                        if (!EstimateEnabled) { ModelState.AddModelError(string.Empty, "Revenue Europe calculation is only available when Estimate is enabled for this Company Survey record."); return Page(); }
                        if (IsLocked) { ModelState.AddModelError(string.Empty, "Revenue Europe calculation is not allowed because this Company Survey record is locked."); return Page(); }
                        var revenueEuropeHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueEuropeQuestionTitle);
                        var revenueHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);
                        var revenueEuropeForecast = await CalculateForecastedRegionalRevenueWithSectorFallbackAsync(revenueEuropeHistory, revenueHistory, TargetFinancialYear, RevenueEuropeQuestionTitle, "Global Revenue Europe");
                        ForecastedRevenueEurope = revenueEuropeForecast.Value;
                        RevenueEuropeForecastReason = revenueEuropeForecast.Reason;
                        ForecastedRevenue = null; ForecastReason = string.Empty;
                        ForecastedEmployment = null; EmploymentForecastReason = string.Empty;
                        ForecastedWages = null; WagesForecastReason = string.Empty;
                        ForecastedResearchDevelopment = null; ResearchDevelopmentForecastReason = string.Empty;
                        ForecastedSalesMarketing = null; SalesMarketingForecastReason = string.Empty;
                        ForecastedEbitda = null; EbitdaForecastReason = string.Empty;
                        ForecastedRevenueNz = null; RevenueNzForecastReason = string.Empty;
                        ForecastedRevenueAustralia = null; RevenueAustraliaForecastReason = string.Empty;
                        ForecastedRevenueChina = null; RevenueChinaForecastReason = string.Empty;
                        ForecastedRevenueRestOfAsia = null; RevenueRestOfAsiaForecastReason = string.Empty;
                        ForecastedRevenueNorthAmerica = null; RevenueNorthAmericaForecastReason = string.Empty;
                        ForecastedRevenueMiddleEast = null; RevenueMiddleEastForecastReason = string.Empty;
                        ForecastedRevenueLatinAmerica = null; RevenueLatinAmericaForecastReason = string.Empty;
                        ForecastedRevenueAfrica = null; RevenueAfricaForecastReason = string.Empty;
                        ForecastedRevenueOther = null; RevenueOtherForecastReason = string.Empty;
                        GeneratedSummary = BuildSingleMetricSummary("Revenue Europe", TargetFinancialYear, revenueEuropeHistory, ForecastedRevenueEurope, RevenueEuropeForecastReason);
                        return Page();
                    }

                    public async Task<IActionResult> OnPostCalculateRevenueMiddleEastAsync(int companySurveyId, int? financialYear)
                    {
                        CompanySurveyId = companySurveyId;
                        FinancialYearFilter = financialYear;
                        var loaded = await LoadPageDataAsync();
                        if (!loaded) return NotFound();
                        if (!EstimateEnabled) { ModelState.AddModelError(string.Empty, "Revenue Middle East calculation is only available when Estimate is enabled for this Company Survey record."); return Page(); }
                        if (IsLocked) { ModelState.AddModelError(string.Empty, "Revenue Middle East calculation is not allowed because this Company Survey record is locked."); return Page(); }
                        var revenueMiddleEastHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueMiddleEastQuestionTitle);
                        var revenueHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);
                        var revenueMiddleEastForecast = await CalculateForecastedRegionalRevenueWithSectorFallbackAsync(revenueMiddleEastHistory, revenueHistory, TargetFinancialYear, RevenueMiddleEastQuestionTitle, "Global Revenue Middle East");
                        ForecastedRevenueMiddleEast = revenueMiddleEastForecast.Value;
                        RevenueMiddleEastForecastReason = revenueMiddleEastForecast.Reason;
                        ForecastedRevenue = null; ForecastReason = string.Empty;
                        ForecastedEmployment = null; EmploymentForecastReason = string.Empty;
                        ForecastedWages = null; WagesForecastReason = string.Empty;
                        ForecastedResearchDevelopment = null; ResearchDevelopmentForecastReason = string.Empty;
                        ForecastedSalesMarketing = null; SalesMarketingForecastReason = string.Empty;
                        ForecastedEbitda = null; EbitdaForecastReason = string.Empty;
                        ForecastedRevenueNz = null; RevenueNzForecastReason = string.Empty;
                        ForecastedRevenueAustralia = null; RevenueAustraliaForecastReason = string.Empty;
                        ForecastedRevenueChina = null; RevenueChinaForecastReason = string.Empty;
                        ForecastedRevenueRestOfAsia = null; RevenueRestOfAsiaForecastReason = string.Empty;
                        ForecastedRevenueNorthAmerica = null; RevenueNorthAmericaForecastReason = string.Empty;
                        ForecastedRevenueEurope = null; RevenueEuropeForecastReason = string.Empty;
                        ForecastedRevenueLatinAmerica = null; RevenueLatinAmericaForecastReason = string.Empty;
                        ForecastedRevenueAfrica = null; RevenueAfricaForecastReason = string.Empty;
                        ForecastedRevenueOther = null; RevenueOtherForecastReason = string.Empty;
                        GeneratedSummary = BuildSingleMetricSummary("Revenue Middle East", TargetFinancialYear, revenueMiddleEastHistory, ForecastedRevenueMiddleEast, RevenueMiddleEastForecastReason);
                        return Page();
                    }

                    public async Task<IActionResult> OnPostCalculateRevenueLatinAmericaAsync(int companySurveyId, int? financialYear)
                    {
                        CompanySurveyId = companySurveyId;
                        FinancialYearFilter = financialYear;
                        var loaded = await LoadPageDataAsync();
                        if (!loaded) return NotFound();
                        if (!EstimateEnabled) { ModelState.AddModelError(string.Empty, "Revenue Latin America calculation is only available when Estimate is enabled for this Company Survey record."); return Page(); }
                        if (IsLocked) { ModelState.AddModelError(string.Empty, "Revenue Latin America calculation is not allowed because this Company Survey record is locked."); return Page(); }
                        var revenueLatinAmericaHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueLatinAmericaQuestionTitle);
                        var revenueHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);
                        var revenueLatinAmericaForecast = await CalculateForecastedRegionalRevenueWithSectorFallbackAsync(revenueLatinAmericaHistory, revenueHistory, TargetFinancialYear, RevenueLatinAmericaQuestionTitle, "Global Revenue Latin America");
                        ForecastedRevenueLatinAmerica = revenueLatinAmericaForecast.Value;
                        RevenueLatinAmericaForecastReason = revenueLatinAmericaForecast.Reason;
                        ForecastedRevenue = null; ForecastReason = string.Empty;
                        ForecastedEmployment = null; EmploymentForecastReason = string.Empty;
                        ForecastedWages = null; WagesForecastReason = string.Empty;
                        ForecastedResearchDevelopment = null; ResearchDevelopmentForecastReason = string.Empty;
                        ForecastedSalesMarketing = null; SalesMarketingForecastReason = string.Empty;
                        ForecastedEbitda = null; EbitdaForecastReason = string.Empty;
                        ForecastedRevenueNz = null; RevenueNzForecastReason = string.Empty;
                        ForecastedRevenueAustralia = null; RevenueAustraliaForecastReason = string.Empty;
                        ForecastedRevenueChina = null; RevenueChinaForecastReason = string.Empty;
                        ForecastedRevenueRestOfAsia = null; RevenueRestOfAsiaForecastReason = string.Empty;
                        ForecastedRevenueNorthAmerica = null; RevenueNorthAmericaForecastReason = string.Empty;
                        ForecastedRevenueEurope = null; RevenueEuropeForecastReason = string.Empty;
                        ForecastedRevenueMiddleEast = null; RevenueMiddleEastForecastReason = string.Empty;
                        ForecastedRevenueAfrica = null; RevenueAfricaForecastReason = string.Empty;
                        ForecastedRevenueOther = null; RevenueOtherForecastReason = string.Empty;
                        GeneratedSummary = BuildSingleMetricSummary("Revenue Latin America", TargetFinancialYear, revenueLatinAmericaHistory, ForecastedRevenueLatinAmerica, RevenueLatinAmericaForecastReason);
                        return Page();
                    }

                    public async Task<IActionResult> OnPostCalculateRevenueAfricaAsync(int companySurveyId, int? financialYear)
                    {
                        CompanySurveyId = companySurveyId;
                        FinancialYearFilter = financialYear;
                        var loaded = await LoadPageDataAsync();
                        if (!loaded) return NotFound();
                        if (!EstimateEnabled) { ModelState.AddModelError(string.Empty, "Revenue Africa calculation is only available when Estimate is enabled for this Company Survey record."); return Page(); }
                        if (IsLocked) { ModelState.AddModelError(string.Empty, "Revenue Africa calculation is not allowed because this Company Survey record is locked."); return Page(); }
                        var revenueAfricaHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueAfricaQuestionTitle);
                        var revenueHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);
                        var revenueAfricaForecast = await CalculateForecastedRegionalRevenueWithSectorFallbackAsync(revenueAfricaHistory, revenueHistory, TargetFinancialYear, RevenueAfricaQuestionTitle, "Global Revenue Africa");
                        ForecastedRevenueAfrica = revenueAfricaForecast.Value;
                        RevenueAfricaForecastReason = revenueAfricaForecast.Reason;
                        ForecastedRevenue = null; ForecastReason = string.Empty;
                        ForecastedEmployment = null; EmploymentForecastReason = string.Empty;
                        ForecastedWages = null; WagesForecastReason = string.Empty;
                        ForecastedResearchDevelopment = null; ResearchDevelopmentForecastReason = string.Empty;
                        ForecastedSalesMarketing = null; SalesMarketingForecastReason = string.Empty;
                        ForecastedEbitda = null; EbitdaForecastReason = string.Empty;
                        ForecastedRevenueNz = null; RevenueNzForecastReason = string.Empty;
                        ForecastedRevenueAustralia = null; RevenueAustraliaForecastReason = string.Empty;
                        ForecastedRevenueChina = null; RevenueChinaForecastReason = string.Empty;
                        ForecastedRevenueRestOfAsia = null; RevenueRestOfAsiaForecastReason = string.Empty;
                        ForecastedRevenueNorthAmerica = null; RevenueNorthAmericaForecastReason = string.Empty;
                        ForecastedRevenueEurope = null; RevenueEuropeForecastReason = string.Empty;
                        ForecastedRevenueMiddleEast = null; RevenueMiddleEastForecastReason = string.Empty;
                        ForecastedRevenueLatinAmerica = null; RevenueLatinAmericaForecastReason = string.Empty;
                        ForecastedRevenueOther = null; RevenueOtherForecastReason = string.Empty;
                        GeneratedSummary = BuildSingleMetricSummary("Revenue Africa", TargetFinancialYear, revenueAfricaHistory, ForecastedRevenueAfrica, RevenueAfricaForecastReason);
                        return Page();
                    }

                    public async Task<IActionResult> OnPostCalculateRevenueOtherAsync(int companySurveyId, int? financialYear)
                    {
                        CompanySurveyId = companySurveyId;
                        FinancialYearFilter = financialYear;
                        var loaded = await LoadPageDataAsync();
                        if (!loaded) return NotFound();
                        if (!EstimateEnabled) { ModelState.AddModelError(string.Empty, "Revenue Other calculation is only available when Estimate is enabled for this Company Survey record."); return Page(); }
                        if (IsLocked) { ModelState.AddModelError(string.Empty, "Revenue Other calculation is not allowed because this Company Survey record is locked."); return Page(); }
                        var revenueOtherHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueOtherQuestionTitle);
                        var revenueHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);
                        var revenueOtherForecast = await CalculateForecastedRegionalRevenueWithSectorFallbackAsync(revenueOtherHistory, revenueHistory, TargetFinancialYear, RevenueOtherQuestionTitle, "Global Revenue Other");
                        ForecastedRevenueOther = revenueOtherForecast.Value;
                        RevenueOtherForecastReason = revenueOtherForecast.Reason;
                        ForecastedRevenue = null; ForecastReason = string.Empty;
                        ForecastedEmployment = null; EmploymentForecastReason = string.Empty;
                        ForecastedWages = null; WagesForecastReason = string.Empty;
                        ForecastedResearchDevelopment = null; ResearchDevelopmentForecastReason = string.Empty;
                        ForecastedSalesMarketing = null; SalesMarketingForecastReason = string.Empty;
                        ForecastedEbitda = null; EbitdaForecastReason = string.Empty;
                        ForecastedRevenueNz = null; RevenueNzForecastReason = string.Empty;
                        ForecastedRevenueAustralia = null; RevenueAustraliaForecastReason = string.Empty;
                        ForecastedRevenueChina = null; RevenueChinaForecastReason = string.Empty;
                        ForecastedRevenueRestOfAsia = null; RevenueRestOfAsiaForecastReason = string.Empty;
                        ForecastedRevenueNorthAmerica = null; RevenueNorthAmericaForecastReason = string.Empty;
                        ForecastedRevenueEurope = null; RevenueEuropeForecastReason = string.Empty;
                        ForecastedRevenueMiddleEast = null; RevenueMiddleEastForecastReason = string.Empty;
                        ForecastedRevenueLatinAmerica = null; RevenueLatinAmericaForecastReason = string.Empty;
                        ForecastedRevenueAfrica = null; RevenueAfricaForecastReason = string.Empty;
                        GeneratedSummary = BuildSingleMetricSummary("Revenue Other", TargetFinancialYear, revenueOtherHistory, ForecastedRevenueOther, RevenueOtherForecastReason);
                        return Page();
                    }

                    public async Task<IActionResult> OnPostSaveEstimationAsync(
            int companySurveyId,
            int? financialYear,
            string? generatedSummary,
            decimal? forecastedRevenue,
            string? forecastReason,
            decimal? forecastedEmployment,
            string? employmentForecastReason,
            decimal? forecastedWages,
            string? wagesForecastReason,
            decimal? forecastedResearchDevelopment,
            string? researchDevelopmentForecastReason,
            decimal? forecastedSalesMarketing,
            string? salesMarketingForecastReason,
            decimal? forecastedEbitda,
            string? ebitdaForecastReason,
            decimal? forecastedRevenueNz,
            string? revenueNzForecastReason,
                decimal? forecastedRevenueAustralia,
                string? revenueAustraliaForecastReason,
                decimal? forecastedRevenueChina,
                    string? revenueChinaForecastReason,
                    decimal? forecastedRevenueRestOfAsia,
                    string? revenueRestOfAsiaForecastReason,
                    decimal? forecastedRevenueNorthAmerica,
                    string? revenueNorthAmericaForecastReason,
                    decimal? forecastedRevenueEurope,
                    string? revenueEuropeForecastReason,
                    decimal? forecastedRevenueMiddleEast,
                    string? revenueMiddleEastForecastReason,
                    decimal? forecastedRevenueLatinAmerica,
                    string? revenueLatinAmericaForecastReason,
                    decimal? forecastedRevenueAfrica,
                    string? revenueAfricaForecastReason,
                    decimal? forecastedRevenueOther,
                    string? revenueOtherForecastReason)
        {
            CompanySurveyId = companySurveyId;
            FinancialYearFilter = financialYear;

            var loaded = await LoadPageDataAsync();
            if (!loaded)
            {
                return NotFound();
            }

            ForecastedRevenue = forecastedRevenue;
            ForecastReason = forecastReason ?? string.Empty;
            ForecastedEmployment = forecastedEmployment;
            EmploymentForecastReason = employmentForecastReason ?? string.Empty;
            ForecastedWages = forecastedWages;
            WagesForecastReason = wagesForecastReason ?? string.Empty;
            ForecastedResearchDevelopment = forecastedResearchDevelopment;
            ResearchDevelopmentForecastReason = researchDevelopmentForecastReason ?? string.Empty;
            ForecastedSalesMarketing = forecastedSalesMarketing;
            SalesMarketingForecastReason = salesMarketingForecastReason ?? string.Empty;
            ForecastedEbitda = forecastedEbitda;
            EbitdaForecastReason = ebitdaForecastReason ?? string.Empty;
            ForecastedRevenueNz = forecastedRevenueNz;
            RevenueNzForecastReason = revenueNzForecastReason ?? string.Empty;
            ForecastedRevenueAustralia = forecastedRevenueAustralia;
            RevenueAustraliaForecastReason = revenueAustraliaForecastReason ?? string.Empty;
            ForecastedRevenueChina = forecastedRevenueChina;
            RevenueChinaForecastReason = revenueChinaForecastReason ?? string.Empty;
            ForecastedRevenueRestOfAsia = forecastedRevenueRestOfAsia;
            RevenueRestOfAsiaForecastReason = revenueRestOfAsiaForecastReason ?? string.Empty;
            ForecastedRevenueNorthAmerica = forecastedRevenueNorthAmerica;
            RevenueNorthAmericaForecastReason = revenueNorthAmericaForecastReason ?? string.Empty;
            ForecastedRevenueEurope = forecastedRevenueEurope;
            RevenueEuropeForecastReason = revenueEuropeForecastReason ?? string.Empty;
            ForecastedRevenueMiddleEast = forecastedRevenueMiddleEast;
            RevenueMiddleEastForecastReason = revenueMiddleEastForecastReason ?? string.Empty;
            ForecastedRevenueLatinAmerica = forecastedRevenueLatinAmerica;
            RevenueLatinAmericaForecastReason = revenueLatinAmericaForecastReason ?? string.Empty;
            ForecastedRevenueAfrica = forecastedRevenueAfrica;
            RevenueAfricaForecastReason = revenueAfricaForecastReason ?? string.Empty;
            ForecastedRevenueOther = forecastedRevenueOther;
            RevenueOtherForecastReason = revenueOtherForecastReason ?? string.Empty;
            GeneratedSummary = generatedSummary ?? string.Empty;

            if (!EstimateEnabled)
            {
                ModelState.AddModelError(string.Empty, "Revenue estimation save is only available when Estimate is enabled for this Company Survey record.");
                return Page();
            }

            if (string.IsNullOrWhiteSpace(GeneratedSummary))
            {
                ModelState.AddModelError(string.Empty, "No generated estimation summary found. Please calculate revenue first.");
                return Page();
            }

            var note = new CompanySurveyNote
            {
                CompanySurveyId = CompanySurveyId,
                NoteDateTime = DateTime.Now,
                User = User.Identity?.Name ?? "Unknown",
                Notes = GeneratedSummary
            };

            _context.CompanySurveyNotes.Add(note);
            await _context.SaveChangesAsync();

            SaveMessage = "Estimation summary saved successfully.";
            return Page();
        }

        public IActionResult OnPostCancelEstimation(int companySurveyId, int? financialYear)
        {
            return RedirectToPage(new { companySurveyId, financialYear });
        }

        private static string BuildSingleMetricSummary(
            string metricName,
            int targetYear,
            List<MetricHistoryRow> history,
            decimal? forecastValue,
            string reason)
        {
            var summaryLines = new List<string>
            {
                "Company Survey Estimation Summary",
                $"Target Financial Year: {targetYear}",
                $"Generated At: {DateTime.Now:dd/MM/yyyy HH:mm:ss}",
            };

            AppendMetricSummarySection(summaryLines, metricName, history, forecastValue, reason);

            return string.Join(Environment.NewLine, summaryLines);
        }

        private static void AppendMetricSummarySection(
            List<string> summaryLines,
            string metricName,
            List<MetricHistoryRow> history,
            decimal? forecastValue,
            string reason)
        {
            var historyLines = history
                .Where(x => x.Value.HasValue)
                .OrderByDescending(x => x.FinancialYear)
                .Take(5)
                .OrderByDescending(x => x.FinancialYear)
                .Select(x => $"- FY {x.FinancialYear}: {x.Value!.Value:N2} (CompanySurveyId {x.CompanySurveyId})")
                .ToList();

            summaryLines.Add(string.Empty);
            summaryLines.Add($"{metricName} Inputs (last 5 available years):");

            if (historyLines.Any())
            {
                summaryLines.AddRange(historyLines);
            }
            else
            {
                summaryLines.Add($"- No historical {metricName.ToLowerInvariant()} values available.");
            }

            summaryLines.Add(string.Empty);
            summaryLines.Add($"{metricName} Calculation Method: {reason}");
            summaryLines.Add($"Forecasted {metricName}: {(forecastValue.HasValue ? forecastValue.Value.ToString("N2") : "not available")}");
        }

        private static decimal? CalculateForecastedValue(List<MetricHistoryRow> history, int targetYear, string metricLabel, out string reason)
        {
            var actual = history
                .Where(x => x.FinancialYear == targetYear)
                .OrderByDescending(x => x.CompanySurveyId)
                .Select(x => x.Value)
                .FirstOrDefault();

            if (actual.HasValue)
            {
                reason = $"Using actual {metricLabel} for target financial year.";
                return actual.Value;
            }

            var trendPoints = history
                .Where(x => x.FinancialYear < targetYear && x.Value.HasValue && x.Value.Value > 0)
                .OrderByDescending(x => x.FinancialYear)
                .Take(5)
                .Select(x => new
                {
                    x.FinancialYear,
                    Value = (double)x.Value!.Value
                })
                .ToList();

            if (trendPoints.Count >= 3)
            {
                var meanYear = trendPoints.Average(x => (double)x.FinancialYear);
                var meanLnValue = trendPoints.Average(x => Math.Log(x.Value));

                var numerator = trendPoints.Sum(x => ((double)x.FinancialYear - meanYear) * (Math.Log(x.Value) - meanLnValue));
                var denominator = trendPoints.Sum(x => Math.Pow((double)x.FinancialYear - meanYear, 2));

                if (denominator > 0)
                {
                    var slope = numerator / denominator;
                    var lnForecast = meanLnValue + slope * (targetYear - meanYear);
                    var trendFit = Math.Exp(lnForecast);

                    reason = $"Using log-linear trend fit from previous years for {metricLabel} (minimum 3 points).";
                    return Convert.ToDecimal(trendFit);
                }
            }

            reason = $"No actual value and insufficient positive historical points for {metricLabel} trend fit (fallback CAGR not configured).";
            return null;
        }

        private async Task<(string MetricLabel, List<CalculationCandidate> Candidates)> BuildCheckPreviewAsync(string metricKey, string? regionQuestionTitle)
        {
            metricKey = (metricKey ?? string.Empty).Trim();

            switch (metricKey)
            {
                case "Revenue":
                {
                    var history = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);
                    return await BuildSectorCagrPreviewAsync("Revenue", history, RevenueQuestionTitle);
                }
                case "Employment":
                {
                    var history = await GetCompanyMetricHistoryAsync(CompanyId, EmploymentQuestionTitle, EmploymentQuestionTitleLegacy);
                    return await BuildSectorCagrPreviewAsync("Employment", history, EmploymentQuestionTitle, EmploymentQuestionTitleLegacy);
                }
                case "Wages":
                {
                    var history = await GetCompanyMetricHistoryAsync(CompanyId, WagesQuestionTitle);
                    var revenueHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);
                    return await BuildRevenueRatioPreviewAsync("Wages & Salaries", history, revenueHistory, WagesQuestionTitle, 4, CalculateSectorWagesRatioAsync);
                }
                case "ResearchDevelopment":
                {
                    var history = await GetCompanyMetricHistoryAsync(CompanyId, ResearchDevelopmentQuestionTitle);
                    var revenueHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);
                    return await BuildRevenueRatioPreviewAsync("Research & Development", history, revenueHistory, ResearchDevelopmentQuestionTitle, 3, CalculateSectorResearchDevelopmentRatioAsync);
                }
                case "SalesMarketing":
                {
                    var history = await GetCompanyMetricHistoryAsync(CompanyId, SalesMarketingQuestionTitle);
                    var revenueHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);
                    return await BuildRevenueRatioPreviewAsync("Sales & Marketing", history, revenueHistory, SalesMarketingQuestionTitle, 3, CalculateSectorSalesMarketingRatioAsync);
                }
                case "Ebitda":
                {
                    var history = await GetCompanyMetricHistoryAsync(CompanyId, EbitdaQuestionTitle);
                    var revenueHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);
                    return await BuildEbitdaPreviewAsync(history, revenueHistory);
                }
                case "RegionalEmployment":
                {
                    if (string.IsNullOrWhiteSpace(regionQuestionTitle))
                    {
                        return ("Regional Employment", new List<CalculationCandidate>
                        {
                            new() { StepName = "Actual", Details = "Regional question title is missing." },
                            new() { StepName = "Trend", Details = "Regional question title is missing." },
                            new() { StepName = "Fallback", Details = "Regional question title is missing." }
                        });
                    }

                    var regionLabel = GetRegionalEmploymentRegionLabel(regionQuestionTitle);
                    var history = await GetCompanyMetricHistoryAsync(CompanyId, regionQuestionTitle);
                    var totalEmploymentHistory = await GetCompanyMetricHistoryAsync(CompanyId, EmploymentQuestionTitle, EmploymentQuestionTitleLegacy);
                    return await BuildRegionalEmploymentPreviewAsync($"Regional Employment - {regionLabel}", history, totalEmploymentHistory, regionQuestionTitle);
                }
                case "RevenueNz":
                    return await BuildRegionalRevenuePreviewAsync("Revenue NZ", RevenueNzQuestionTitle);
                case "RevenueAustralia":
                    return await BuildRegionalRevenuePreviewAsync("Revenue Australia", RevenueAustraliaQuestionTitle);
                case "RevenueChina":
                    return await BuildRegionalRevenuePreviewAsync("Revenue China", RevenueChinaQuestionTitle);
                case "RevenueRestOfAsia":
                    return await BuildRegionalRevenuePreviewAsync("Revenue Rest of Asia", RevenueRestOfAsiaQuestionTitle);
                case "RevenueNorthAmerica":
                    return await BuildRegionalRevenuePreviewAsync("Revenue North America", RevenueNorthAmericaQuestionTitle);
                case "RevenueEurope":
                    return await BuildRegionalRevenuePreviewAsync("Revenue Europe", RevenueEuropeQuestionTitle);
                case "RevenueMiddleEast":
                    return await BuildRegionalRevenuePreviewAsync("Revenue Middle East", RevenueMiddleEastQuestionTitle);
                case "RevenueLatinAmerica":
                    return await BuildRegionalRevenuePreviewAsync("Revenue Latin America", RevenueLatinAmericaQuestionTitle);
                case "RevenueAfrica":
                    return await BuildRegionalRevenuePreviewAsync("Revenue Africa", RevenueAfricaQuestionTitle);
                case "RevenueOther":
                    return await BuildRegionalRevenuePreviewAsync("Revenue Other", RevenueOtherQuestionTitle);
                case "GroupQuestion":
                {
                    if (string.IsNullOrWhiteSpace(regionQuestionTitle))
                    {
                        return ("Group Question", new List<CalculationCandidate>
                        {
                            new() { StepName = "Actual", Details = "Question title is missing." },
                            new() { StepName = "Trend", Details = "Question title is missing." },
                            new() { StepName = "Fallback", Details = "Question title is missing." }
                        });
                    }
                    var gqHistory = await GetCompanyMetricHistoryAsync(CompanyId, regionQuestionTitle);
                    return await BuildSectorCagrPreviewAsync(regionQuestionTitle, gqHistory, regionQuestionTitle);
                }
                default:
                    return (metricKey, new List<CalculationCandidate>
                    {
                        new() { StepName = "Actual", Details = "Unsupported metric for Check preview." },
                        new() { StepName = "Trend", Details = "Unsupported metric for Check preview." },
                        new() { StepName = "Fallback", Details = "Unsupported metric for Check preview." }
                    });
            }
        }

        private async Task<(string MetricLabel, List<MetricHistoryRow> History, decimal? Value, string Reason)> CalculateMetricByKeyAsync(string metricKey, string? regionQuestionTitle)
        {
            metricKey = (metricKey ?? string.Empty).Trim();

            switch (metricKey)
            {
                case "Revenue":
                {
                    var history = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);
                    var calc = await CalculateForecastedRevenueWithSectorFallbackAsync(history, TargetFinancialYear);
                    return ("Revenue", history, calc.Value, calc.Reason);
                }
                case "Employment":
                {
                    var history = await GetCompanyMetricHistoryAsync(CompanyId, EmploymentQuestionTitle, EmploymentQuestionTitleLegacy);
                    var calc = await CalculateForecastedEmploymentWithSectorFallbackAsync(history, TargetFinancialYear);
                    return ("Employment", history, calc.Value, calc.Reason);
                }
                case "Wages":
                {
                    var history = await GetCompanyMetricHistoryAsync(CompanyId, WagesQuestionTitle);
                    var revenueHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);
                    var calc = await CalculateForecastedWagesWithSectorFallbackAsync(history, revenueHistory, TargetFinancialYear);
                    return ("Wages & Salaries", history, calc.Value, calc.Reason);
                }
                case "ResearchDevelopment":
                {
                    var history = await GetCompanyMetricHistoryAsync(CompanyId, ResearchDevelopmentQuestionTitle);
                    var revenueHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);
                    var calc = await CalculateForecastedResearchDevelopmentWithSectorFallbackAsync(history, revenueHistory, TargetFinancialYear);
                    return ("Research & Development", history, calc.Value, calc.Reason);
                }
                case "SalesMarketing":
                {
                    var history = await GetCompanyMetricHistoryAsync(CompanyId, SalesMarketingQuestionTitle);
                    var revenueHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);
                    var calc = await CalculateForecastedSalesMarketingWithSectorFallbackAsync(history, revenueHistory, TargetFinancialYear);
                    return ("Sales & Marketing", history, calc.Value, calc.Reason);
                }
                case "Ebitda":
                {
                    var history = await GetCompanyMetricHistoryAsync(CompanyId, EbitdaQuestionTitle);
                    var revenueHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);
                    var calc = await CalculateForecastedEbitdaWithSectorFallbackAsync(history, revenueHistory, TargetFinancialYear);
                    return ("EBITDA", history, calc.Value, calc.Reason);
                }
                case "RegionalEmployment":
                {
                    if (string.IsNullOrWhiteSpace(regionQuestionTitle))
                    {
                        return ("Regional Employment", new List<MetricHistoryRow>(), null, "Regional question title is required.");
                    }

                    var history = await GetCompanyMetricHistoryAsync(CompanyId, regionQuestionTitle);
                    var totalEmploymentHistory = await GetCompanyMetricHistoryAsync(CompanyId, EmploymentQuestionTitle, EmploymentQuestionTitleLegacy);
                    var calc = await CalculateForecastedRegionalEmploymentWithSectorFallbackAsync(history, totalEmploymentHistory, TargetFinancialYear, regionQuestionTitle);
                    var label = $"Regional Employment - {GetRegionalEmploymentRegionLabel(regionQuestionTitle)}";
                    return (label, history, calc.Value, calc.Reason);
                }
                case "RevenueNz":
                    return await CalculateRegionalRevenueByTitleAsync("Revenue NZ", RevenueNzQuestionTitle);
                case "RevenueAustralia":
                    return await CalculateRegionalRevenueByTitleAsync("Revenue Australia", RevenueAustraliaQuestionTitle);
                case "RevenueChina":
                    return await CalculateRegionalRevenueByTitleAsync("Revenue China", RevenueChinaQuestionTitle);
                case "RevenueRestOfAsia":
                    return await CalculateRegionalRevenueByTitleAsync("Revenue Rest of Asia", RevenueRestOfAsiaQuestionTitle);
                case "RevenueNorthAmerica":
                    return await CalculateRegionalRevenueByTitleAsync("Revenue North America", RevenueNorthAmericaQuestionTitle);
                case "RevenueEurope":
                    return await CalculateRegionalRevenueByTitleAsync("Revenue Europe", RevenueEuropeQuestionTitle);
                case "RevenueMiddleEast":
                    return await CalculateRegionalRevenueByTitleAsync("Revenue Middle East", RevenueMiddleEastQuestionTitle);
                case "RevenueLatinAmerica":
                    return await CalculateRegionalRevenueByTitleAsync("Revenue Latin America", RevenueLatinAmericaQuestionTitle);
                case "RevenueAfrica":
                    return await CalculateRegionalRevenueByTitleAsync("Revenue Africa", RevenueAfricaQuestionTitle);
                case "RevenueOther":
                    return await CalculateRegionalRevenueByTitleAsync("Revenue Other", RevenueOtherQuestionTitle);
                case "GroupQuestion":
                {
                    if (string.IsNullOrWhiteSpace(regionQuestionTitle))
                        return ("Group Question", new List<MetricHistoryRow>(), null, "Question title is required.");
                    var gqHistory = await GetCompanyMetricHistoryAsync(CompanyId, regionQuestionTitle);
                    var gqCalc = CalculateSimpleTrendForecast(gqHistory, TargetFinancialYear);
                    return (regionQuestionTitle, gqHistory, gqCalc.Value, gqCalc.Reason);
                }
                default:
                    return (metricKey, new List<MetricHistoryRow>(), null, "Unsupported metric for Apply.");
            }
        }

        private async Task<(string MetricLabel, List<MetricHistoryRow> History, decimal? Value, string Reason)> CalculateRegionalRevenueByTitleAsync(string metricLabel, string questionTitle)
        {
            var history = await GetCompanyMetricHistoryAsync(CompanyId, questionTitle);
            var revenueHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);
            var calc = await CalculateForecastedRegionalRevenueWithSectorFallbackAsync(history, revenueHistory, TargetFinancialYear, questionTitle, metricLabel);
            return (metricLabel, history, calc.Value, calc.Reason);
        }

        private void ClearForecastOutputs()
        {
            ForecastedRevenue = null;
            ForecastReason = string.Empty;
            ForecastedEmployment = null;
            EmploymentForecastReason = string.Empty;
            ForecastedWages = null;
            WagesForecastReason = string.Empty;
            ForecastedResearchDevelopment = null;
            ResearchDevelopmentForecastReason = string.Empty;
            ForecastedSalesMarketing = null;
            SalesMarketingForecastReason = string.Empty;
            ForecastedEbitda = null;
            EbitdaForecastReason = string.Empty;
            ForecastedRevenueNz = null;
            RevenueNzForecastReason = string.Empty;
            ForecastedRevenueAustralia = null;
            RevenueAustraliaForecastReason = string.Empty;
            ForecastedRevenueChina = null;
            RevenueChinaForecastReason = string.Empty;
            ForecastedRevenueRestOfAsia = null;
            RevenueRestOfAsiaForecastReason = string.Empty;
            ForecastedRevenueNorthAmerica = null;
            RevenueNorthAmericaForecastReason = string.Empty;
            ForecastedRevenueEurope = null;
            RevenueEuropeForecastReason = string.Empty;
            ForecastedRevenueMiddleEast = null;
            RevenueMiddleEastForecastReason = string.Empty;
            ForecastedRevenueLatinAmerica = null;
            RevenueLatinAmericaForecastReason = string.Empty;
            ForecastedRevenueAfrica = null;
            RevenueAfricaForecastReason = string.Empty;
            ForecastedRevenueOther = null;
            RevenueOtherForecastReason = string.Empty;
            ForecastedRegionalEmployment = null;
            RegionalEmploymentForecastReason = string.Empty;
            SelectedRegionalEmploymentRegionLabel = string.Empty;
            GroupQuestionAppliedValues.Clear();
            GroupQuestionAppliedReasons.Clear();
        }

        private async Task ReloadHistoricalDataForMetricAsync(string metricKey, string? regionQuestionTitle)
        {
            switch ((metricKey ?? string.Empty).Trim())
            {
                case "Revenue":
                    var revenueHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);
                    LastFiveYearsRevenue = GetLastFiveYears(revenueHistory);
                    break;
                case "Employment":
                    var employmentHistory = await GetCompanyMetricHistoryAsync(CompanyId, EmploymentQuestionTitle, EmploymentQuestionTitleLegacy);
                    LastFiveYearsEmployment = GetLastFiveYears(employmentHistory);
                    break;
                case "Wages":
                    var wagesHistory = await GetCompanyMetricHistoryAsync(CompanyId, WagesQuestionTitle);
                    LastFiveYearsWages = GetLastFiveYears(wagesHistory);
                    break;
                case "ResearchDevelopment":
                    var rdHistory = await GetCompanyMetricHistoryAsync(CompanyId, ResearchDevelopmentQuestionTitle);
                    LastFiveYearsResearchDevelopment = GetLastFiveYears(rdHistory);
                    break;
                case "SalesMarketing":
                    var smHistory = await GetCompanyMetricHistoryAsync(CompanyId, SalesMarketingQuestionTitle);
                    LastFiveYearsSalesMarketing = GetLastFiveYears(smHistory);
                    break;
                case "Ebitda":
                    var ebitdaHistory = await GetCompanyMetricHistoryAsync(CompanyId, EbitdaQuestionTitle);
                    LastFiveYearsEbitda = GetLastFiveYears(ebitdaHistory);
                    break;
                case "RevenueNz":
                    var revNzHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueNzQuestionTitle);
                    LastFiveYearsRevenueNz = GetLastFiveYears(revNzHistory);
                    break;
                case "RevenueAustralia":
                    var revAuHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueAustraliaQuestionTitle);
                    LastFiveYearsRevenueAustralia = GetLastFiveYears(revAuHistory);
                    break;
                case "RevenueChina":
                    var revCnHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueChinaQuestionTitle);
                    LastFiveYearsRevenueChina = GetLastFiveYears(revCnHistory);
                    break;
                case "RevenueRestOfAsia":
                    var revRoaHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueRestOfAsiaQuestionTitle);
                    LastFiveYearsRevenueRestOfAsia = GetLastFiveYears(revRoaHistory);
                    break;
                case "RevenueNorthAmerica":
                    var revNaHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueNorthAmericaQuestionTitle);
                    LastFiveYearsRevenueNorthAmerica = GetLastFiveYears(revNaHistory);
                    break;
                case "RevenueEurope":
                    var revEuHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueEuropeQuestionTitle);
                    LastFiveYearsRevenueEurope = GetLastFiveYears(revEuHistory);
                    break;
                case "RevenueMiddleEast":
                    var revMeHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueMiddleEastQuestionTitle);
                    LastFiveYearsRevenueMiddleEast = GetLastFiveYears(revMeHistory);
                    break;
                case "RevenueLatinAmerica":
                    var revLaHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueLatinAmericaQuestionTitle);
                    LastFiveYearsRevenueLatinAmerica = GetLastFiveYears(revLaHistory);
                    break;
                case "RevenueAfrica":
                    var revAfHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueAfricaQuestionTitle);
                    LastFiveYearsRevenueAfrica = GetLastFiveYears(revAfHistory);
                    break;
                case "RevenueOther":
                    var revOtherHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueOtherQuestionTitle);
                    LastFiveYearsRevenueOther = GetLastFiveYears(revOtherHistory);
                    break;
                case "RegionalEmployment":
                    if (!string.IsNullOrWhiteSpace(regionQuestionTitle))
                    {
                        var regionalHistory = await GetCompanyMetricHistoryAsync(CompanyId, regionQuestionTitle);
                        LastFiveYearsRegionalEmploymentByQuestionTitle[regionQuestionTitle] = GetLastFiveYears(regionalHistory);
                        LastFiveYearsRegionalEmploymentSelected = LastFiveYearsRegionalEmploymentByQuestionTitle[regionQuestionTitle];
                    }
                    break;
                case "GroupQuestion":
                    if (!string.IsNullOrWhiteSpace(regionQuestionTitle))
                    {
                        var gqReloadHistory = await GetCompanyMetricHistoryAsync(CompanyId, regionQuestionTitle);
                        var gqLastFive = GetLastFiveYears(gqReloadHistory);
                        foreach (var section in EstimationGroupSections)
                        {
                            var gqQuestion = section.Questions.FirstOrDefault(q =>
                                string.Equals(q.QuestionTitle, regionQuestionTitle, StringComparison.OrdinalIgnoreCase));
                            if (gqQuestion != null)
                            {
                                gqQuestion.LastFiveYears = gqLastFive;
                                break;
                            }
                        }
                    }
                    break;
            }
        }

        private async Task SaveAppliedAnswerAsync(string metricKey, string? regionQuestionTitle, decimal appliedValue, int targetFinancialYear)
        {
            string? questionTitle = metricKey switch
            {
                "Revenue" => RevenueQuestionTitle,
                "Employment" => EmploymentQuestionTitle,
                "Wages" => WagesQuestionTitle,
                "ResearchDevelopment" => ResearchDevelopmentQuestionTitle,
                "SalesMarketing" => SalesMarketingQuestionTitle,
                "Ebitda" => EbitdaQuestionTitle,
                "RevenueNz" => RevenueNzQuestionTitle,
                "RevenueAustralia" => RevenueAustraliaQuestionTitle,
                "RevenueChina" => RevenueChinaQuestionTitle,
                "RevenueRestOfAsia" => RevenueRestOfAsiaQuestionTitle,
                "RevenueNorthAmerica" => RevenueNorthAmericaQuestionTitle,
                "RevenueEurope" => RevenueEuropeQuestionTitle,
                "RevenueMiddleEast" => RevenueMiddleEastQuestionTitle,
                "RevenueLatinAmerica" => RevenueLatinAmericaQuestionTitle,
                "RevenueAfrica" => RevenueAfricaQuestionTitle,
                "RevenueOther" => RevenueOtherQuestionTitle,
                "RegionalEmployment" => regionQuestionTitle,
                "GroupQuestion" => regionQuestionTitle,
                _ => null
            };

            if (string.IsNullOrWhiteSpace(questionTitle))
            {
                return;
            }

            // Get the question ID
            var question = await _context.Question
                .AsNoTracking()
                .FirstOrDefaultAsync(q => q.Title == questionTitle);

            if (question == null)
            {
                return;
            }

            // Look for existing answer for this company survey and question
            var existingAnswer = await _context.Answer
                .FirstOrDefaultAsync(a =>
                    a.CompanySurveyId == CompanySurveyId &&
                    a.QuestionId == question.Id);

            // All metrics are stored as currency values
            if (existingAnswer != null)
            {
                existingAnswer.AnswerCurrency = appliedValue;
                existingAnswer.AnswerNumber = null;
                existingAnswer.AnswerText = null;
                _context.Answer.Update(existingAnswer);
            }
            else
            {
                var newAnswer = new Answer
                {
                    CompanySurveyId = CompanySurveyId,
                    QuestionId = question.Id,
                    AnswerCurrency = appliedValue,
                    AnswerNumber = null,
                    AnswerText = null
                };
                _context.Answer.Add(newAnswer);
            }

            await _context.SaveChangesAsync();
        }


        private void ApplyForecastByMetricKey(string metricKey, string? regionQuestionTitle, decimal value, string reason)
        {
            switch ((metricKey ?? string.Empty).Trim())
            {
                case "Revenue":
                    ForecastedRevenue = value;
                    ForecastReason = reason;
                    break;
                case "Employment":
                    ForecastedEmployment = value;
                    EmploymentForecastReason = reason;
                    break;
                case "Wages":
                    ForecastedWages = value;
                    WagesForecastReason = reason;
                    break;
                case "ResearchDevelopment":
                    ForecastedResearchDevelopment = value;
                    ResearchDevelopmentForecastReason = reason;
                    break;
                case "SalesMarketing":
                    ForecastedSalesMarketing = value;
                    SalesMarketingForecastReason = reason;
                    break;
                case "Ebitda":
                    ForecastedEbitda = value;
                    EbitdaForecastReason = reason;
                    break;
                case "RevenueNz":
                    ForecastedRevenueNz = value;
                    RevenueNzForecastReason = reason;
                    break;
                case "RevenueAustralia":
                    ForecastedRevenueAustralia = value;
                    RevenueAustraliaForecastReason = reason;
                    break;
                case "RevenueChina":
                    ForecastedRevenueChina = value;
                    RevenueChinaForecastReason = reason;
                    break;
                case "RevenueRestOfAsia":
                    ForecastedRevenueRestOfAsia = value;
                    RevenueRestOfAsiaForecastReason = reason;
                    break;
                case "RevenueNorthAmerica":
                    ForecastedRevenueNorthAmerica = value;
                    RevenueNorthAmericaForecastReason = reason;
                    break;
                case "RevenueEurope":
                    ForecastedRevenueEurope = value;
                    RevenueEuropeForecastReason = reason;
                    break;
                case "RevenueMiddleEast":
                    ForecastedRevenueMiddleEast = value;
                    RevenueMiddleEastForecastReason = reason;
                    break;
                case "RevenueLatinAmerica":
                    ForecastedRevenueLatinAmerica = value;
                    RevenueLatinAmericaForecastReason = reason;
                    break;
                case "RevenueAfrica":
                    ForecastedRevenueAfrica = value;
                    RevenueAfricaForecastReason = reason;
                    break;
                case "RevenueOther":
                    ForecastedRevenueOther = value;
                    RevenueOtherForecastReason = reason;
                    break;
                case "RegionalEmployment":
                    ForecastedRegionalEmployment = value;
                    RegionalEmploymentForecastReason = reason;
                    SelectedRegionalEmploymentQuestionTitle = regionQuestionTitle ?? string.Empty;
                    SelectedRegionalEmploymentRegionLabel = GetRegionalEmploymentRegionLabel(SelectedRegionalEmploymentQuestionTitle);
                    break;
                case "GroupQuestion":
                    if (!string.IsNullOrWhiteSpace(regionQuestionTitle))
                    {
                        GroupQuestionAppliedValues[regionQuestionTitle] = value;
                        GroupQuestionAppliedReasons[regionQuestionTitle] = reason;
                    }
                    break;
            }
        }

        private async Task<(string MetricLabel, List<CalculationCandidate> Candidates)> BuildSectorCagrPreviewAsync(string metricLabel, List<MetricHistoryRow> history, params string[] questionTitles)
        {
            var actual = GetActualValueForTargetYear(history, TargetFinancialYear);
            var trend = TryCalculateLogLinearTrend(history, TargetFinancialYear, 3, out var trendDetail);

            var secondarySector = await GetCompanySecondarySectorAsync(CompanyId, TargetFinancialYear);
            var sectorCagr = questionTitles.Contains(EmploymentQuestionTitle) || questionTitles.Contains(EmploymentQuestionTitleLegacy)
                ? await CalculateSectorCAGREmploymentAsync(secondarySector, TargetFinancialYear)
                : await CalculateSectorCAGRAsync(secondarySector, TargetFinancialYear);

            var latestPositive = history
                .Where(x => x.FinancialYear < TargetFinancialYear && x.Value.HasValue && x.Value.Value > 0)
                .OrderByDescending(x => x.FinancialYear)
                .FirstOrDefault();

            var fallbackReasons = new List<string>();
            if (string.IsNullOrWhiteSpace(secondarySector))
            {
                fallbackReasons.Add("No Secondary Sector answer is available for this company.");
            }
            if (latestPositive?.Value.HasValue != true)
            {
                fallbackReasons.Add($"No positive company value exists before FY {TargetFinancialYear}.");
            }
            if (!sectorCagr.HasValue)
            {
                fallbackReasons.Add("Sector CAGR could not be calculated from peer company history.");
            }
            else if (sectorCagr.Value <= -1)
            {
                fallbackReasons.Add($"Sector CAGR is invalid for forecasting ({sectorCagr.Value:P2}).");
            }

            var fallback = TryCalculateSectorCagrFallback(history, TargetFinancialYear, sectorCagr, out var fallbackDetail);
            if (!fallback.HasValue)
            {
                fallbackDetail = $"Secondary sector: {(string.IsNullOrWhiteSpace(secondarySector) ? "n/a" : secondarySector)}. "
                    + $"Latest positive company value before FY {TargetFinancialYear}: {(latestPositive?.Value.HasValue == true ? latestPositive.Value.Value.ToString("N2") : "n/a")}. "
                    + $"Sector CAGR: {(sectorCagr.HasValue ? sectorCagr.Value.ToString("P2") : "n/a")}. "
                    + fallbackDetail;
            }
            else
            {
                fallbackDetail = $"Secondary sector: {(string.IsNullOrWhiteSpace(secondarySector) ? "n/a" : secondarySector)}. Sector CAGR: {(sectorCagr.HasValue ? sectorCagr.Value.ToString("P2") : "n/a")}. {fallbackDetail}";
            }

            var candidates = BuildCandidates(actual, trend, fallback,
                "Actual",
                actual.HasValue ? $"Actual {metricLabel} for FY {TargetFinancialYear}." : $"No actual {metricLabel} for FY {TargetFinancialYear}.",
                "Trend",
                trendDetail,
                "Fallback",
                fallbackDetail,
                fallbackReasons);

            return (metricLabel, candidates);
        }

        private async Task<(string MetricLabel, List<CalculationCandidate> Candidates)> BuildRevenueRatioPreviewAsync(
            string metricLabel,
            List<MetricHistoryRow> metricHistory,
            List<MetricHistoryRow> revenueHistory,
            string metricQuestionTitle,
            int minimumTrendPoints,
            Func<string?, int, Task<decimal?>> sectorRatioFunc)
        {
            var actual = GetActualValueForTargetYear(metricHistory, TargetFinancialYear);
            var trend = TryCalculateLogLinearTrend(metricHistory, TargetFinancialYear, minimumTrendPoints, out var trendDetail);

            var primarySector = await GetCompanyPrimarySectorAsync(CompanyId, TargetFinancialYear);
            var sectorRatio = await sectorRatioFunc(primarySector, TargetFinancialYear);
            var revenueForecast = await CalculateForecastedRevenueWithSectorFallbackAsync(revenueHistory, TargetFinancialYear);

            decimal? fallback = null;
            var fallbackDetail = "Sector ratio fallback not available.";
            if (sectorRatio.HasValue && sectorRatio.Value > 0 && revenueForecast.Value.HasValue && revenueForecast.Value.Value > 0)
            {
                fallback = revenueForecast.Value.Value * sectorRatio.Value;
                fallbackDetail = $"Primary sector ratio {sectorRatio:P2} x forecasted revenue {revenueForecast.Value.Value:N2}.";
            }
            else
            {
                fallbackDetail = $"Missing ratio or revenue forecast (ratio: {(sectorRatio.HasValue ? sectorRatio.Value.ToString("P2") : "n/a")}, revenue forecast: {(revenueForecast.Value.HasValue ? revenueForecast.Value.Value.ToString("N2") : "n/a")}).";
            }

            var candidates = BuildCandidates(actual, trend, fallback,
                "Actual",
                actual.HasValue ? $"Actual {metricLabel} for FY {TargetFinancialYear}." : $"No actual {metricLabel} for FY {TargetFinancialYear}.",
                "Trend",
                trendDetail,
                "Fallback",
                fallbackDetail);

            return (metricLabel, candidates);
        }

        private async Task<(string MetricLabel, List<CalculationCandidate> Candidates)> BuildEbitdaPreviewAsync(
            List<MetricHistoryRow> ebitdaHistory,
            List<MetricHistoryRow> revenueHistory)
        {
            var actual = GetActualValueForTargetYear(ebitdaHistory, TargetFinancialYear);
            var trend = TryCalculateLinearTrend(ebitdaHistory, TargetFinancialYear, 4, out var trendDetail);

            var primarySector = await GetCompanyPrimarySectorAsync(CompanyId, TargetFinancialYear);
            var sectorRatio = await CalculateSectorEbitdaRatioAsync(primarySector, TargetFinancialYear);
            var revenueForecast = await CalculateForecastedRevenueWithSectorFallbackAsync(revenueHistory, TargetFinancialYear);

            decimal? fallback = null;
            var fallbackDetail = "Sector EBITDA ratio fallback not available.";
            if (EnableEbitdaSectorFallback && sectorRatio.HasValue && revenueForecast.Value.HasValue)
            {
                fallback = revenueForecast.Value.Value * sectorRatio.Value;
                fallbackDetail = $"Primary sector EBITDA/revenue ratio {sectorRatio:P2} x forecasted revenue {revenueForecast.Value.Value:N2}.";
            }
            else
            {
                fallbackDetail = $"Missing ratio or revenue forecast (ratio: {(sectorRatio.HasValue ? sectorRatio.Value.ToString("P2") : "n/a")}, revenue forecast: {(revenueForecast.Value.HasValue ? revenueForecast.Value.Value.ToString("N2") : "n/a")}).";
            }

            var candidates = BuildCandidates(actual, trend, fallback,
                "Actual",
                actual.HasValue ? $"Actual EBITDA for FY {TargetFinancialYear}." : $"No actual EBITDA for FY {TargetFinancialYear}.",
                "Trend",
                trendDetail,
                "Fallback",
                fallbackDetail);

            return ("EBITDA", candidates);
        }

        private async Task<(string MetricLabel, List<CalculationCandidate> Candidates)> BuildRegionalRevenuePreviewAsync(string metricLabel, string questionTitle)
        {
            var regionalHistory = await GetCompanyMetricHistoryAsync(CompanyId, questionTitle);
            var revenueHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);

            var actual = GetActualValueForTargetYear(regionalHistory, TargetFinancialYear);
            var trend = TryCalculateLogLinearTrend(regionalHistory, TargetFinancialYear, 2, out var trendDetail);

            var primarySector = await GetCompanyPrimarySectorAsync(CompanyId, TargetFinancialYear);
            var sectorRatio = await CalculateSectorRegionalExportRatioAsync(primarySector, questionTitle, TargetFinancialYear);
            var revenueForecast = await CalculateForecastedRevenueWithSectorFallbackAsync(revenueHistory, TargetFinancialYear);

            decimal? fallback = null;
            var fallbackDetail = "Sector export ratio fallback not available.";
            if (sectorRatio.HasValue && revenueForecast.Value.HasValue)
            {
                fallback = revenueForecast.Value.Value * sectorRatio.Value;
                fallbackDetail = $"Primary sector export ratio {sectorRatio:P2} x forecasted total revenue {revenueForecast.Value.Value:N2}.";
            }
            else
            {
                fallbackDetail = $"Missing ratio or revenue forecast (ratio: {(sectorRatio.HasValue ? sectorRatio.Value.ToString("P2") : "n/a")}, revenue forecast: {(revenueForecast.Value.HasValue ? revenueForecast.Value.Value.ToString("N2") : "n/a")}).";
            }

            var candidates = BuildCandidates(actual, trend, fallback,
                "Actual",
                actual.HasValue ? $"Actual {metricLabel} for FY {TargetFinancialYear}." : $"No actual {metricLabel} for FY {TargetFinancialYear}.",
                "Trend",
                trendDetail,
                "Fallback",
                fallbackDetail);

            return (metricLabel, candidates);
        }

        private async Task<(string MetricLabel, List<CalculationCandidate> Candidates)> BuildRegionalEmploymentPreviewAsync(
            string metricLabel,
            List<MetricHistoryRow> regionalHistory,
            List<MetricHistoryRow> totalEmploymentHistory,
            string questionTitle)
        {
            var actual = GetActualValueForTargetYear(regionalHistory, TargetFinancialYear);
            var trend = TryCalculateLogLinearTrend(regionalHistory, TargetFinancialYear, 2, out var trendDetail);

            var primarySector = await GetCompanyPrimarySectorAsync(CompanyId, TargetFinancialYear);
            var sectorRatio = await CalculateSectorRegionalEmploymentRatioAsync(primarySector, questionTitle, TargetFinancialYear);
            var employmentForecast = await CalculateForecastedEmploymentWithSectorFallbackAsync(totalEmploymentHistory, TargetFinancialYear);

            decimal? fallback = null;
            var fallbackDetail = "Sector employment ratio fallback not available.";
            if (sectorRatio.HasValue && employmentForecast.Value.HasValue)
            {
                fallback = employmentForecast.Value.Value * sectorRatio.Value;
                fallbackDetail = $"Primary sector employment share {sectorRatio:P2} x forecasted total employment {employmentForecast.Value.Value:N2}.";
            }
            else
            {
                fallbackDetail = $"Missing ratio or employment forecast (ratio: {(sectorRatio.HasValue ? sectorRatio.Value.ToString("P2") : "n/a")}, employment forecast: {(employmentForecast.Value.HasValue ? employmentForecast.Value.Value.ToString("N2") : "n/a")}).";
            }

            var candidates = BuildCandidates(actual, trend, fallback,
                "Actual",
                actual.HasValue ? $"Actual {metricLabel} for FY {TargetFinancialYear}." : $"No actual {metricLabel} for FY {TargetFinancialYear}.",
                "Trend",
                trendDetail,
                "Fallback",
                fallbackDetail);

            return (metricLabel, candidates);
        }

        private static decimal? GetActualValueForTargetYear(List<MetricHistoryRow> history, int targetYear)
        {
            return history
                .Where(x => x.FinancialYear == targetYear)
                .OrderByDescending(x => x.CompanySurveyId)
                .Select(x => x.Value)
                .FirstOrDefault();
        }

        private static (decimal? Value, string Reason) CalculateSimpleTrendForecast(List<MetricHistoryRow> history, int targetYear)
        {
            var actual = GetActualValueForTargetYear(history, targetYear);
            if (actual.HasValue)
                return (actual, $"Actual value exists for FY {targetYear}.");

            var trend = TryCalculateLogLinearTrend(history, targetYear, 3, out var trendDetail);
            if (trend.HasValue)
                return (trend, $"Log-linear trend (3-year): {trendDetail}");

            var lastKnown = history
                .Where(x => x.FinancialYear < targetYear && x.Value.HasValue)
                .OrderByDescending(x => x.FinancialYear)
                .FirstOrDefault();
            if (lastKnown?.Value.HasValue == true)
                return (lastKnown.Value, $"Last known value carried forward from FY {lastKnown.FinancialYear}.");

            return (null, "No historical data available to calculate a forecast.");
        }

        private static decimal? TryCalculateLogLinearTrend(List<MetricHistoryRow> history, int targetYear, int minimumPoints, out string details)
        {
            var points = history
                .Where(x => x.FinancialYear < targetYear && x.Value.HasValue && x.Value.Value > 0)
                .OrderByDescending(x => x.FinancialYear)
                .Take(5)
                .Select(x => (Year: (double)x.FinancialYear, Value: (double)x.Value!.Value))
                .ToList();

            if (points.Count < minimumPoints)
            {
                details = $"Insufficient positive points ({points.Count}, minimum {minimumPoints}).";
                return null;
            }

            var meanYear = points.Average(d => d.Year);
            var meanLnValue = points.Average(d => Math.Log(d.Value));
            var numerator = points.Sum(d => (d.Year - meanYear) * (Math.Log(d.Value) - meanLnValue));
            var denominator = points.Sum(d => Math.Pow(d.Year - meanYear, 2));

            if (denominator <= 0)
            {
                details = "Cannot fit trend (year variance is zero).";
                return null;
            }

            var slope = numerator / denominator;
            var lnForecast = meanLnValue + slope * (targetYear - meanYear);
            var growthFit = Math.Exp(lnForecast);

            details = $"Log-linear trend from {points.Count} point(s).";
            return growthFit < 0 ? 0m : Convert.ToDecimal(growthFit);
        }

        private static decimal? TryCalculateLinearTrend(List<MetricHistoryRow> history, int targetYear, int minimumPoints, out string details)
        {
            var points = history
                .Where(x => x.FinancialYear < targetYear && x.Value.HasValue)
                .Select(x => (Year: (double)x.FinancialYear, Value: (double)x.Value!.Value))
                .ToList();

            if (points.Count < minimumPoints)
            {
                details = $"Insufficient points ({points.Count}, minimum {minimumPoints}).";
                return null;
            }

            var avgX = points.Average(d => d.Year);
            var avgY = points.Average(d => d.Value);
            var numerator = points.Sum(d => (d.Year - avgX) * (d.Value - avgY));
            var denominator = points.Sum(d => Math.Pow(d.Year - avgX, 2));

            if (denominator == 0)
            {
                details = "Cannot fit linear trend (year variance is zero).";
                return null;
            }

            var slope = numerator / denominator;
            var intercept = avgY - slope * avgX;
            var forecastValue = slope * targetYear + intercept;
            details = $"Linear trend from {points.Count} point(s).";
            return forecastValue < 0 ? 0m : Convert.ToDecimal(forecastValue);
        }

        private static decimal? TryCalculateSectorCagrFallback(List<MetricHistoryRow> history, int targetYear, decimal? sectorCagr, out string details)
        {
            if (!sectorCagr.HasValue || sectorCagr.Value <= -1)
            {
                details = "Sector CAGR is not available.";
                return null;
            }

            var latest = history
                .Where(x => x.FinancialYear < targetYear && x.Value.HasValue && x.Value.Value > 0)
                .OrderByDescending(x => x.FinancialYear)
                .FirstOrDefault();

            if (latest == null || !latest.Value.HasValue || latest.Value.Value <= 0)
            {
                details = "No latest positive historical value available for CAGR fallback.";
                return null;
            }

            var yearsToForecast = targetYear - latest.FinancialYear;
            var fallback = latest.Value.Value * Convert.ToDecimal(Math.Pow(1 + (double)sectorCagr.Value, yearsToForecast));
            details = $"Sector CAGR {sectorCagr.Value:P2} applied to latest value over {yearsToForecast} year(s).";
            return fallback > 0 ? fallback : 0m;
        }

        private static List<CalculationCandidate> BuildCandidates(
            decimal? actual,
            decimal? trend,
            decimal? fallback,
            string actualStep,
            string actualDetail,
            string trendStep,
            string trendDetail,
            string fallbackStep,
            string fallbackDetail,
            List<string>? fallbackFailureReasons = null)
        {
            var usedStep = actual.HasValue ? actualStep : trend.HasValue ? trendStep : fallback.HasValue ? fallbackStep : string.Empty;

            return new List<CalculationCandidate>
            {
                new()
                {
                    StepName = actualStep,
                    Value = actual,
                    Details = actualDetail,
                    IsUsed = string.Equals(usedStep, actualStep, StringComparison.OrdinalIgnoreCase)
                },
                new()
                {
                    StepName = trendStep,
                    Value = trend,
                    Details = trendDetail,
                    IsUsed = string.Equals(usedStep, trendStep, StringComparison.OrdinalIgnoreCase)
                },
                new()
                {
                    StepName = fallbackStep,
                    Value = fallback,
                    Details = fallbackDetail,
                    FailureReasons = fallbackFailureReasons ?? new List<string>(),
                    IsUsed = string.Equals(usedStep, fallbackStep, StringComparison.OrdinalIgnoreCase)
                }
            };
        }

        private async Task<bool> LoadPageDataAsync()
        {
            var context = await (
                from companySurvey in _context.CompanySurvey
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                join company in _context.Tin200 on companySurvey.CompanyId equals company.Id
                where companySurvey.Id == CompanySurveyId
                select new
                {
                    companySurvey.CompanyId,
                    company.CompanyName,
                    survey.FinancialYear,
                    EstimateEnabled = companySurvey.Estimate ?? false,
                    IsLocked = companySurvey.Locked ?? false
                })
                .FirstOrDefaultAsync();

            if (context == null)
            {
                return false;
            }

            CompanyId = context.CompanyId;
            CompanyName = context.CompanyName ?? string.Empty;
            TargetFinancialYear = context.FinancialYear;
            EstimateEnabled = context.EstimateEnabled;
            IsLocked = context.IsLocked;

            var revenueHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueQuestionTitle);
            var employmentHistory = await GetCompanyMetricHistoryAsync(CompanyId, EmploymentQuestionTitle, EmploymentQuestionTitleLegacy);
            var wagesHistory = await GetCompanyMetricHistoryAsync(CompanyId, WagesQuestionTitle);
            var researchDevelopmentHistory = await GetCompanyMetricHistoryAsync(CompanyId, ResearchDevelopmentQuestionTitle);
            var salesMarketingHistory = await GetCompanyMetricHistoryAsync(CompanyId, SalesMarketingQuestionTitle);
            var ebitdaHistory = await GetCompanyMetricHistoryAsync(CompanyId, EbitdaQuestionTitle);
            var revenueNzHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueNzQuestionTitle);
            var revenueAustraliaHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueAustraliaQuestionTitle);
            var revenueChinaHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueChinaQuestionTitle);
            var revenueRestOfAsiaHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueRestOfAsiaQuestionTitle);
            var revenueNorthAmericaHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueNorthAmericaQuestionTitle);
            var revenueEuropeHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueEuropeQuestionTitle);
            var revenueMiddleEastHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueMiddleEastQuestionTitle);
            var revenueLatinAmericaHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueLatinAmericaQuestionTitle);
            var revenueAfricaHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueAfricaQuestionTitle);
            var revenueOtherHistory = await GetCompanyMetricHistoryAsync(CompanyId, RevenueOtherQuestionTitle);

            LastFiveYearsRevenue = GetLastFiveYears(revenueHistory);
            LastFiveYearsEmployment = GetLastFiveYears(employmentHistory);
            LastFiveYearsWages = GetLastFiveYears(wagesHistory);
            LastFiveYearsResearchDevelopment = GetLastFiveYears(researchDevelopmentHistory);
            LastFiveYearsSalesMarketing = GetLastFiveYears(salesMarketingHistory);
            LastFiveYearsEbitda = GetLastFiveYears(ebitdaHistory);
            LastFiveYearsRevenueNz = GetLastFiveYears(revenueNzHistory);
            LastFiveYearsRevenueAustralia = GetLastFiveYears(revenueAustraliaHistory);
            LastFiveYearsRevenueChina = GetLastFiveYears(revenueChinaHistory);
            LastFiveYearsRevenueRestOfAsia = GetLastFiveYears(revenueRestOfAsiaHistory);
            LastFiveYearsRevenueNorthAmerica = GetLastFiveYears(revenueNorthAmericaHistory);
            LastFiveYearsRevenueEurope = GetLastFiveYears(revenueEuropeHistory);
            LastFiveYearsRevenueMiddleEast = GetLastFiveYears(revenueMiddleEastHistory);
            LastFiveYearsRevenueLatinAmerica = GetLastFiveYears(revenueLatinAmericaHistory);
            LastFiveYearsRevenueAfrica = GetLastFiveYears(revenueAfricaHistory);
            LastFiveYearsRevenueOther = GetLastFiveYears(revenueOtherHistory);

            AvailableRegionalEmploymentQuestionTitles = await _context.Question
                .AsNoTracking()
                .Where(q => !string.IsNullOrWhiteSpace(q.Title)
                    && q.IncludeInEstimation == true
                    && q.Title!.StartsWith(RegionalEmploymentQuestionPrefix))
                .Select(q => q.Title!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            LastFiveYearsRegionalEmploymentByQuestionTitle = new Dictionary<string, List<MetricHistoryRow>>(StringComparer.OrdinalIgnoreCase);
            foreach (var regionalQuestionTitle in AvailableRegionalEmploymentQuestionTitles)
            {
                var regionalHistory = await GetCompanyMetricHistoryAsync(CompanyId, regionalQuestionTitle);
                LastFiveYearsRegionalEmploymentByQuestionTitle[regionalQuestionTitle] = GetLastFiveYears(regionalHistory);
            }

            if (!string.IsNullOrWhiteSpace(SelectedRegionalEmploymentQuestionTitle)
                && AvailableRegionalEmploymentQuestionTitles.Contains(SelectedRegionalEmploymentQuestionTitle, StringComparer.OrdinalIgnoreCase))
            {
                LastFiveYearsRegionalEmploymentSelected = LastFiveYearsRegionalEmploymentByQuestionTitle.TryGetValue(SelectedRegionalEmploymentQuestionTitle, out var selectedRegionalHistory)
                    ? selectedRegionalHistory
                    : new List<MetricHistoryRow>();
                SelectedRegionalEmploymentRegionLabel = GetRegionalEmploymentRegionLabel(SelectedRegionalEmploymentQuestionTitle);
            }
            else if (AvailableRegionalEmploymentQuestionTitles.Count > 0)
            {
                SelectedRegionalEmploymentQuestionTitle = AvailableRegionalEmploymentQuestionTitles[0];
                LastFiveYearsRegionalEmploymentSelected = LastFiveYearsRegionalEmploymentByQuestionTitle.TryGetValue(SelectedRegionalEmploymentQuestionTitle, out var selectedRegionalHistory)
                    ? selectedRegionalHistory
                    : new List<MetricHistoryRow>();
                SelectedRegionalEmploymentRegionLabel = GetRegionalEmploymentRegionLabel(SelectedRegionalEmploymentQuestionTitle);
            }
            else
            {
                LastFiveYearsRegionalEmploymentSelected = new List<MetricHistoryRow>();
                SelectedRegionalEmploymentRegionLabel = string.Empty;
            }

            var groupQRows = await (
                from q in _context.Question
                join g in _context.QuestionGroup on q.GroupId equals g.Id
                where q.IncludeInEstimation == true && !string.IsNullOrWhiteSpace(q.Title)
                    && !q.Title!.ToLower().Contains("year-1")
                    && !q.Title!.ToLower().Contains("year-2")
                orderby g.OrderNumber ?? 0, g.Id, q.OrderNumber ?? 0, q.Id
                select new { GroupId = g.Id, GroupTitle = g.Title ?? string.Empty, QuestionTitle = q.Title! }
            ).ToListAsync();

            EstimationGroupSections = new List<EstimationGroupSection>();
            foreach (var grouping in groupQRows.GroupBy(x => new { x.GroupId, x.GroupTitle }))
            {
                var section = new EstimationGroupSection { GroupId = grouping.Key.GroupId, GroupTitle = grouping.Key.GroupTitle };
                foreach (var gq in grouping)
                {
                    section.Questions.Add(await BuildEstimationGroupQuestionAsync(gq.QuestionTitle));
                }
                EstimationGroupSections.Add(section);
            }

            return true;
        }

        private async Task<EstimationGroupQuestion> BuildEstimationGroupQuestionAsync(string questionTitle)
        {
            var metadata = GetEstimationQuestionMetadata(questionTitle);
            var history = await GetCompanyMetricHistoryAsync(CompanyId, metadata.HistoryQuestionTitles);

            return new EstimationGroupQuestion
            {
                QuestionTitle = questionTitle,
                HandlerName = metadata.HandlerName,
                ValueLabel = metadata.ValueLabel,
                EmptyStateMessage = metadata.EmptyStateMessage,
                LastFiveYears = GetLastFiveYears(history)
            };
        }

        private (string HandlerName, string ValueLabel, string EmptyStateMessage, string[] HistoryQuestionTitles) GetEstimationQuestionMetadata(string questionTitle)
        {
            if (string.Equals(questionTitle, RevenueQuestionTitle, StringComparison.OrdinalIgnoreCase))
            {
                return (
                    "CalculateRevenue",
                    "Revenue",
                    "No historical revenue values were found for this company.",
                    new[] { RevenueQuestionTitle });
            }

            if (string.Equals(questionTitle, EmploymentQuestionTitle, StringComparison.OrdinalIgnoreCase)
                || string.Equals(questionTitle, EmploymentQuestionTitleLegacy, StringComparison.OrdinalIgnoreCase))
            {
                return (
                    "CalculateEmployment",
                    "Employment Total",
                    "No historical employment values were found for this company.",
                    new[] { EmploymentQuestionTitle, EmploymentQuestionTitleLegacy });
            }

            if (string.Equals(questionTitle, WagesQuestionTitle, StringComparison.OrdinalIgnoreCase))
            {
                return (
                    "CalculateWages",
                    "Wages & Salaries",
                    "No historical wages and salaries values were found for this company.",
                    new[] { WagesQuestionTitle });
            }

            if (string.Equals(questionTitle, ResearchDevelopmentQuestionTitle, StringComparison.OrdinalIgnoreCase))
            {
                return (
                    "CalculateResearchDevelopment",
                    "Research & Development",
                    "No historical research and development values were found for this company.",
                    new[] { ResearchDevelopmentQuestionTitle });
            }

            if (string.Equals(questionTitle, SalesMarketingQuestionTitle, StringComparison.OrdinalIgnoreCase))
            {
                return (
                    "CalculateSalesMarketing",
                    "Sales & Marketing",
                    "No historical sales and marketing values were found for this company.",
                    new[] { SalesMarketingQuestionTitle });
            }

            if (string.Equals(questionTitle, EbitdaQuestionTitle, StringComparison.OrdinalIgnoreCase))
            {
                return (
                    "CalculateEbitda",
                    "EBITDA",
                    "No historical EBITDA values were found for this company.",
                    new[] { EbitdaQuestionTitle });
            }

            if (string.Equals(questionTitle, RevenueNzQuestionTitle, StringComparison.OrdinalIgnoreCase))
            {
                return (
                    "CalculateRevenueNz",
                    "Revenue NZ",
                    "No historical Revenue NZ values were found for this company.",
                    new[] { RevenueNzQuestionTitle });
            }

            if (string.Equals(questionTitle, RevenueAustraliaQuestionTitle, StringComparison.OrdinalIgnoreCase))
            {
                return (
                    "CalculateRevenueAustralia",
                    "Revenue Australia",
                    "No historical Revenue Australia values were found for this company.",
                    new[] { RevenueAustraliaQuestionTitle });
            }

            if (string.Equals(questionTitle, RevenueChinaQuestionTitle, StringComparison.OrdinalIgnoreCase))
            {
                return (
                    "CalculateRevenueChina",
                    "Revenue China",
                    "No historical Revenue China values were found for this company.",
                    new[] { RevenueChinaQuestionTitle });
            }

            if (string.Equals(questionTitle, RevenueRestOfAsiaQuestionTitle, StringComparison.OrdinalIgnoreCase))
            {
                return (
                    "CalculateRevenueRestOfAsia",
                    "Revenue Rest of Asia",
                    "No historical Revenue Rest of Asia values were found for this company.",
                    new[] { RevenueRestOfAsiaQuestionTitle });
            }

            if (string.Equals(questionTitle, RevenueNorthAmericaQuestionTitle, StringComparison.OrdinalIgnoreCase))
            {
                return (
                    "CalculateRevenueNorthAmerica",
                    "Revenue North America",
                    "No historical Revenue North America values were found for this company.",
                    new[] { RevenueNorthAmericaQuestionTitle });
            }

            if (string.Equals(questionTitle, RevenueEuropeQuestionTitle, StringComparison.OrdinalIgnoreCase))
            {
                return (
                    "CalculateRevenueEurope",
                    "Revenue Europe",
                    "No historical Revenue Europe values were found for this company.",
                    new[] { RevenueEuropeQuestionTitle });
            }

            if (string.Equals(questionTitle, RevenueMiddleEastQuestionTitle, StringComparison.OrdinalIgnoreCase))
            {
                return (
                    "CalculateRevenueMiddleEast",
                    "Revenue Middle East",
                    "No historical Revenue Middle East values were found for this company.",
                    new[] { RevenueMiddleEastQuestionTitle });
            }

            if (string.Equals(questionTitle, RevenueLatinAmericaQuestionTitle, StringComparison.OrdinalIgnoreCase))
            {
                return (
                    "CalculateRevenueLatinAmerica",
                    "Revenue Latin America",
                    "No historical Revenue Latin America values were found for this company.",
                    new[] { RevenueLatinAmericaQuestionTitle });
            }

            if (string.Equals(questionTitle, RevenueAfricaQuestionTitle, StringComparison.OrdinalIgnoreCase))
            {
                return (
                    "CalculateRevenueAfrica",
                    "Revenue Africa",
                    "No historical Revenue Africa values were found for this company.",
                    new[] { RevenueAfricaQuestionTitle });
            }

            if (string.Equals(questionTitle, RevenueOtherQuestionTitle, StringComparison.OrdinalIgnoreCase))
            {
                return (
                    "CalculateRevenueOther",
                    "Revenue Other",
                    "No historical Revenue Other values were found for this company.",
                    new[] { RevenueOtherQuestionTitle });
            }

            if (!string.IsNullOrWhiteSpace(questionTitle)
                && questionTitle.StartsWith(RegionalEmploymentQuestionPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var regionLabel = GetRegionalEmploymentRegionLabel(questionTitle);
                return (
                    "CalculateRegionalEmployment",
                    $"Regional Employment ({regionLabel})",
                    $"No historical regional employment values were found for {regionLabel}.",
                    new[] { questionTitle });
            }

            return (
                "CalculateGroupQuestion",
                questionTitle,
                "No historical values were found for this question.",
                new[] { questionTitle });
        }

        private async Task<List<MetricHistoryRow>> GetCompanyMetricHistoryAsync(int companyId, params string[] questionTitles)
        {
            var rawRows = await (
                from companySurvey in _context.CompanySurvey
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                join answer in _context.Answer on companySurvey.Id equals answer.CompanySurveyId
                join question in _context.Question on answer.QuestionId equals question.Id
                where companySurvey.CompanyId == companyId && questionTitles.Contains(question.Title ?? string.Empty)
                select new
                {
                    companySurvey.Id,
                    survey.FinancialYear,
                    answer.AnswerCurrency,
                    answer.AnswerNumber,
                    answer.AnswerText
                })
                .ToListAsync();

            var rows = rawRows
                .Select(x => new MetricHistoryRow
                {
                    CompanySurveyId = x.Id,
                    FinancialYear = x.FinancialYear,
                    Value = ResolveMetricValue(x.AnswerCurrency, x.AnswerNumber, x.AnswerText)
                })
                .Where(x => x.Value.HasValue)
                .ToList();

            return rows;
        }

        private static List<MetricHistoryRow> GetLastFiveYears(List<MetricHistoryRow> history)
        {
            return history
                .OrderByDescending(x => x.FinancialYear)
                .Take(5)
                .OrderByDescending(x => x.FinancialYear)
                .ToList();
        }

        private static string GetRegionalEmploymentRegionLabel(string questionTitle)
        {
            if (string.IsNullOrWhiteSpace(questionTitle))
            {
                return string.Empty;
            }

            var label = questionTitle;
            if (label.StartsWith(RegionalEmploymentQuestionPrefix, StringComparison.OrdinalIgnoreCase))
            {
                label = label.Substring(RegionalEmploymentQuestionPrefix.Length);
            }

            if (label.EndsWith(RegionalEmploymentQuestionSuffix, StringComparison.OrdinalIgnoreCase))
            {
                label = label.Substring(0, label.Length - RegionalEmploymentQuestionSuffix.Length);
            }

            return label.Trim();
        }

        private static decimal? ResolveMetricValue(decimal? answerCurrency, double? answerNumber, string? answerText)
        {
            if (answerCurrency.HasValue)
            {
                return answerCurrency.Value;
            }

            if (answerNumber.HasValue)
            {
                return Convert.ToDecimal(answerNumber.Value);
            }

            if (!string.IsNullOrWhiteSpace(answerText) && decimal.TryParse(answerText, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static bool HasMoreThanTwoDecimalPlaces(decimal value)
        {
            return decimal.Round(value, 2, MidpointRounding.AwayFromZero) != value;
        }

        private async Task<string?> GetCompanyPrimarySectorAsync(int companyId, int targetYear)
        {
            var fallbackYear = targetYear - 1;

            var primarySector = await (
                from companySurvey in _context.CompanySurvey
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                join answer in _context.Answer on companySurvey.Id equals answer.CompanySurveyId
                join question in _context.Question on answer.QuestionId equals question.Id
                where companySurvey.CompanyId == companyId
                    && (survey.FinancialYear == targetYear || survey.FinancialYear == fallbackYear)
                    && question.Title == "Primary Sector"
                    && !string.IsNullOrWhiteSpace(answer.AnswerText)
                orderby survey.FinancialYear descending, companySurvey.Id descending
                select answer.AnswerText
            ).FirstOrDefaultAsync();

            return primarySector;
        }

        private async Task<string?> GetCompanySecondarySectorAsync(int companyId, int targetYear)
        {
            var fallbackYear = targetYear - 1;

            var secondarySector = await (
                from companySurvey in _context.CompanySurvey
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                join answer in _context.Answer on companySurvey.Id equals answer.CompanySurveyId
                join question in _context.Question on answer.QuestionId equals question.Id
                where companySurvey.CompanyId == companyId
                    && (survey.FinancialYear == targetYear || survey.FinancialYear == fallbackYear)
                    && question.Title == "Secondary Sector"
                    && !string.IsNullOrWhiteSpace(answer.AnswerText)
                orderby survey.FinancialYear descending, companySurvey.Id descending
                select answer.AnswerText
            ).FirstOrDefaultAsync();

            return secondarySector;
        }

        private async Task<decimal?> CalculateSectorCAGRAsync(string? secondarySector, int targetYear)
        {
            if (string.IsNullOrWhiteSpace(secondarySector))
            {
                return null;
            }

            // Get all companies with the same secondary sector and their revenue history for past 5 years
            var sectorRevenueData = await (
                from companySurvey in _context.CompanySurvey
                join answer in _context.Answer on companySurvey.Id equals answer.CompanySurveyId
                join question in _context.Question on answer.QuestionId equals question.Id
                join revenueAnswer in _context.Answer on companySurvey.Id equals revenueAnswer.CompanySurveyId
                join revenueQuestion in _context.Question on revenueAnswer.QuestionId equals revenueQuestion.Id
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                where question.Title == "Secondary Sector" 
                    && (answer.AnswerText ?? string.Empty) == secondarySector
                    && revenueQuestion.Title == RevenueQuestionTitle
                    && survey.FinancialYear < targetYear
                select new
                {
                    companySurvey.Id,
                    survey.FinancialYear,
                    revenueAnswer.AnswerCurrency,
                    revenueAnswer.AnswerNumber,
                    revenueAnswer.AnswerText
                }
            ).ToListAsync();

            // Resolve metric values and group by year
            var yearlyValues = sectorRevenueData
                .Select(x => new
                {
                    x.FinancialYear,
                    Value = ResolveMetricValue(x.AnswerCurrency, x.AnswerNumber, x.AnswerText) ?? 0m
                })
                .GroupBy(x => x.FinancialYear)
                .Select(g => new
                {
                    Year = g.Key,
                    AverageValue = g.Where(x => x.Value > 0).Average(x => (double)x.Value)
                })
                .Where(x => x.AverageValue > 0 && !double.IsNaN(x.AverageValue))
                .OrderByDescending(x => x.Year)
                .Take(5)
                .ToList();

            if (yearlyValues.Count < 2)
            {
                return null;
            }

            // Calculate CAGR: (EndValue / BeginValue) ^ (1 / NumYears) - 1
            var newestYear = yearlyValues.First();
            var oldestYear = yearlyValues.Last();
            var yearsSpan = newestYear.Year - oldestYear.Year;

            if (yearsSpan <= 0 || oldestYear.AverageValue == 0)
            {
                return null;
            }

            var cagrDecimal = Math.Pow(newestYear.AverageValue / oldestYear.AverageValue, 1.0 / yearsSpan) - 1.0;
            return Convert.ToDecimal(cagrDecimal);
        }

        private async Task<decimal?> CalculateSectorCAGREmploymentAsync(string? secondarySector, int targetYear)
        {
            if (string.IsNullOrWhiteSpace(secondarySector))
            {
                return null;
            }

            // Get all companies with the same secondary sector and their employment history for past 5 years
            var sectorEmploymentData = await (
                from companySurvey in _context.CompanySurvey
                join answer in _context.Answer on companySurvey.Id equals answer.CompanySurveyId
                join question in _context.Question on answer.QuestionId equals question.Id
                join employmentAnswer in _context.Answer on companySurvey.Id equals employmentAnswer.CompanySurveyId
                join employmentQuestion in _context.Question on employmentAnswer.QuestionId equals employmentQuestion.Id
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                where question.Title == "Secondary Sector" 
                    && (answer.AnswerText ?? string.Empty) == secondarySector
                    && (employmentQuestion.Title == EmploymentQuestionTitle || employmentQuestion.Title == EmploymentQuestionTitleLegacy)
                    && survey.FinancialYear < targetYear
                select new
                {
                    companySurvey.Id,
                    survey.FinancialYear,
                    employmentAnswer.AnswerCurrency,
                    employmentAnswer.AnswerNumber,
                    employmentAnswer.AnswerText
                }
            ).ToListAsync();

            // Resolve metric values and group by year
            var yearlyValues = sectorEmploymentData
                .Select(x => new
                {
                    x.FinancialYear,
                    Value = ResolveMetricValue(x.AnswerCurrency, x.AnswerNumber, x.AnswerText) ?? 0m
                })
                .GroupBy(x => x.FinancialYear)
                .Select(g => new
                {
                    Year = g.Key,
                    AverageValue = g.Where(x => x.Value > 0).Average(x => (double)x.Value)
                })
                .Where(x => x.AverageValue > 0 && !double.IsNaN(x.AverageValue))
                .OrderByDescending(x => x.Year)
                .Take(5)
                .ToList();

            if (yearlyValues.Count < 2)
            {
                return null;
            }

            // Calculate CAGR: (EndValue / BeginValue) ^ (1 / NumYears) - 1
            var newestYear = yearlyValues.First();
            var oldestYear = yearlyValues.Last();
            var yearsSpan = newestYear.Year - oldestYear.Year;

            if (yearsSpan <= 0 || oldestYear.AverageValue == 0)
            {
                return null;
            }

            var cagrDecimal = Math.Pow(newestYear.AverageValue / oldestYear.AverageValue, 1.0 / yearsSpan) - 1.0;
            return Convert.ToDecimal(cagrDecimal);
        }

        private async Task<(decimal? Value, string Reason)> CalculateForecastedEmploymentWithSectorFallbackAsync(
            List<MetricHistoryRow> employmentHistory,
            int targetYear)
        {
            // Step 1: Use actual if exists for target year
            var actual = employmentHistory
                .Where(x => x.FinancialYear == targetYear)
                .OrderByDescending(x => x.CompanySurveyId)
                .Select(x => x.Value)
                .FirstOrDefault();

            if (actual.HasValue)
            {
                return (actual.Value, "Using actual employment for target financial year.");
            }

            // Step 2: Log-linear trend fit (>= 3 positive points)
            var trendPoints = employmentHistory
                .Where(x => x.FinancialYear < targetYear && x.Value.HasValue && x.Value.Value > 0)
                .OrderByDescending(x => x.FinancialYear)
                .Take(5)
                .Select(x => (Year: (double)x.FinancialYear, Value: (double)x.Value!.Value))
                .ToList();

            if (trendPoints.Count >= 3)
            {
                double meanYear = trendPoints.Average(d => d.Year);
                double meanLnValue = trendPoints.Average(d => Math.Log(d.Value));
                double numerator = trendPoints.Sum(d => (d.Year - meanYear) * (Math.Log(d.Value) - meanLnValue));
                double denominator = trendPoints.Sum(d => Math.Pow(d.Year - meanYear, 2));

                if (denominator > 0)
                {
                    double slope = numerator / denominator;
                    double lnForecast = meanLnValue + slope * (targetYear - meanYear);
                    double growthFit = Math.Exp(lnForecast);
                    decimal result = growthFit < 0 ? 0m : Convert.ToDecimal(growthFit);
                    return (result, $"Log-linear trend fit from {trendPoints.Count} historical data point(s) (minimum 3 positive points).");
                }
            }

            // Step 3: Sector CAGR fallback (now active)
            var secondarySector = await GetCompanySecondarySectorAsync(CompanyId, TargetFinancialYear);
            var sectorCAGR = await CalculateSectorCAGREmploymentAsync(secondarySector, targetYear);

            if (sectorCAGR.HasValue && sectorCAGR.Value > -1) // CAGR must be > -100%
            {
                var latestEmployment = employmentHistory
                    .Where(x => x.FinancialYear < targetYear && x.Value.HasValue && x.Value.Value > 0)
                    .OrderByDescending(x => x.FinancialYear)
                    .Select(x => x.Value)
                    .FirstOrDefault() ?? 0m;

                if (latestEmployment > 0)
                {
                    var yearsToForecast = targetYear - employmentHistory
                        .Where(x => x.FinancialYear < targetYear && x.Value.HasValue && x.Value.Value > 0)
                        .OrderByDescending(x => x.FinancialYear)
                        .Select(x => x.FinancialYear)
                        .FirstOrDefault();

                    var forecastValue = latestEmployment * Convert.ToDecimal(Math.Pow(1 + (double)sectorCAGR, yearsToForecast));
                    return (forecastValue > 0 ? forecastValue : 0m, $"Insufficient company-level historical data ({trendPoints.Count} points, minimum 3 required). Using sector CAGR ({sectorCAGR:P2}) from secondary sector '{secondarySector}' applied to latest employment over {yearsToForecast} year(s).");
                }
            }

            // Step 4: No result
            return (null, $"No actual value and insufficient positive historical points ({trendPoints.Count}, minimum 3 required) for trend fit. Sector CAGR fallback not available (no sector data or insufficient sector history).");
        }

        private async Task<(decimal? Value, string Reason)> CalculateForecastedRevenueWithSectorFallbackAsync(
            List<MetricHistoryRow> revenueHistory,
            int targetYear)
        {
            // Step 1: Use actual if exists for target year
            var actual = revenueHistory
                .Where(x => x.FinancialYear == targetYear)
                .OrderByDescending(x => x.CompanySurveyId)
                .Select(x => x.Value)
                .FirstOrDefault();

            if (actual.HasValue)
            {
                return (actual.Value, "Using actual revenue for target financial year.");
            }

            // Step 2: Log-linear trend fit (>= 3 positive points)
            var trendPoints = revenueHistory
                .Where(x => x.FinancialYear < targetYear && x.Value.HasValue && x.Value.Value > 0)
                .OrderByDescending(x => x.FinancialYear)
                .Take(5)
                .Select(x => (Year: (double)x.FinancialYear, Value: (double)x.Value!.Value))
                .ToList();

            if (trendPoints.Count >= 3)
            {
                double meanYear = trendPoints.Average(d => d.Year);
                double meanLnValue = trendPoints.Average(d => Math.Log(d.Value));
                double numerator = trendPoints.Sum(d => (d.Year - meanYear) * (Math.Log(d.Value) - meanLnValue));
                double denominator = trendPoints.Sum(d => Math.Pow(d.Year - meanYear, 2));

                if (denominator > 0)
                {
                    double slope = numerator / denominator;
                    double lnForecast = meanLnValue + slope * (targetYear - meanYear);
                    double growthFit = Math.Exp(lnForecast);
                    decimal result = growthFit < 0 ? 0m : Convert.ToDecimal(growthFit);
                    return (result, $"Log-linear trend fit from {trendPoints.Count} historical data point(s) (minimum 3 positive points).");
                }
            }

            // Step 3: Sector CAGR fallback (now active)
            var secondarySector = await GetCompanySecondarySectorAsync(CompanyId, TargetFinancialYear);
            var sectorCAGR = await CalculateSectorCAGRAsync(secondarySector, targetYear);

            if (sectorCAGR.HasValue && sectorCAGR.Value > -1) // CAGR must be > -100%
            {
                var latestRevenue = revenueHistory
                    .Where(x => x.FinancialYear < targetYear && x.Value.HasValue && x.Value.Value > 0)
                    .OrderByDescending(x => x.FinancialYear)
                    .Select(x => x.Value)
                    .FirstOrDefault() ?? 0m;

                if (latestRevenue > 0)
                {
                    var yearsToForecast = targetYear - revenueHistory
                        .Where(x => x.FinancialYear < targetYear && x.Value.HasValue && x.Value.Value > 0)
                        .OrderByDescending(x => x.FinancialYear)
                        .Select(x => x.FinancialYear)
                        .FirstOrDefault();

                    var forecastValue = latestRevenue * Convert.ToDecimal(Math.Pow(1 + (double)sectorCAGR, yearsToForecast));
                    return (forecastValue > 0 ? forecastValue : 0m, $"Insufficient company-level historical data ({trendPoints.Count} points, minimum 3 required). Using sector CAGR ({sectorCAGR:P2}) from secondary sector '{secondarySector}' applied to latest revenue over {yearsToForecast} year(s).");
                }
            }

            // Step 4: No result
            return (null, $"No actual value and insufficient positive historical points ({trendPoints.Count}, minimum 3 required) for trend fit. Sector CAGR fallback not available (no sector data or insufficient sector history).");
        }

        private async Task<decimal?> CalculateSectorRegionalExportRatioAsync(string? primarySector, string regionQuestionTitle, int targetYear)
        {
            if (string.IsNullOrWhiteSpace(primarySector))
            {
                return null;
            }

            // Build a sector-specific regional export ratio (region revenue / total revenue) from the last 5 years.
            var sectorData = await (
                from companySurvey in _context.CompanySurvey
                join sectorAnswer in _context.Answer on companySurvey.Id equals sectorAnswer.CompanySurveyId
                join sectorQuestion in _context.Question on sectorAnswer.QuestionId equals sectorQuestion.Id
                join regionAnswer in _context.Answer on companySurvey.Id equals regionAnswer.CompanySurveyId
                join regionQuestion in _context.Question on regionAnswer.QuestionId equals regionQuestion.Id
                join revenueAnswer in _context.Answer on companySurvey.Id equals revenueAnswer.CompanySurveyId
                join revenueQuestion in _context.Question on revenueAnswer.QuestionId equals revenueQuestion.Id
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                where sectorQuestion.Title == "Primary Sector"
                    && (sectorAnswer.AnswerText ?? string.Empty) == primarySector
                    && regionQuestion.Title == regionQuestionTitle
                    && revenueQuestion.Title == RevenueQuestionTitle
                    && survey.FinancialYear < targetYear
                select new
                {
                    survey.FinancialYear,
                    RegionCurrency = regionAnswer.AnswerCurrency,
                    RegionNumber = regionAnswer.AnswerNumber,
                    RegionText = regionAnswer.AnswerText,
                    RevenueCurrency = revenueAnswer.AnswerCurrency,
                    RevenueNumber = revenueAnswer.AnswerNumber,
                    RevenueText = revenueAnswer.AnswerText
                }
            ).ToListAsync();

            var yearlyRatios = sectorData
                .Select(x => new
                {
                    x.FinancialYear,
                    RegionValue = ResolveMetricValue(x.RegionCurrency, x.RegionNumber, x.RegionText),
                    RevenueValue = ResolveMetricValue(x.RevenueCurrency, x.RevenueNumber, x.RevenueText)
                })
                .Where(x => x.RegionValue.HasValue && x.RevenueValue.HasValue && x.RevenueValue.Value > 0)
                .GroupBy(x => x.FinancialYear)
                .Select(g => new
                {
                    Year = g.Key,
                    AvgRegion = g.Average(x => (double)x.RegionValue!.Value),
                    AvgRevenue = g.Average(x => (double)x.RevenueValue!.Value)
                })
                .Where(x => x.AvgRevenue > 0)
                .OrderByDescending(x => x.Year)
                .Take(5)
                .ToList();

            if (yearlyRatios.Count == 0)
            {
                return null;
            }

            var averageRatio = yearlyRatios.Average(x => x.AvgRegion / x.AvgRevenue);
            return Convert.ToDecimal(averageRatio);
        }

        private async Task<(decimal? Value, string Reason)> CalculateForecastedRegionalRevenueWithSectorFallbackAsync(
            List<MetricHistoryRow> regionalHistory,
            List<MetricHistoryRow> revenueHistory,
            int targetYear,
            string regionQuestionTitle,
            string regionLabel)
        {
            // 1. Use actual if exists for target year
            var actual = regionalHistory
                .Where(x => x.FinancialYear == targetYear)
                .OrderByDescending(x => x.CompanySurveyId)
                .Select(x => x.Value)
                .FirstOrDefault();

            if (actual.HasValue)
            {
                return (actual.Value, $"Using actual {regionLabel} for target financial year.");
            }

            // 2. Log-linear growth fit (>= 2 positive historical points)
            var trendPoints = regionalHistory
                .Where(x => x.FinancialYear < targetYear && x.Value.HasValue && x.Value.Value > 0)
                .OrderByDescending(x => x.FinancialYear)
                .Take(5)
                .Select(x => (Year: (double)x.FinancialYear, Value: (double)x.Value!.Value))
                .ToList();

            if (trendPoints.Count >= 2)
            {
                double meanYear = trendPoints.Average(d => d.Year);
                double meanLnValue = trendPoints.Average(d => Math.Log(d.Value));
                double numerator = trendPoints.Sum(d => (d.Year - meanYear) * (Math.Log(d.Value) - meanLnValue));
                double denominator = trendPoints.Sum(d => Math.Pow(d.Year - meanYear, 2));

                if (denominator > 0)
                {
                    double slope = numerator / denominator;
                    double lnForecast = meanLnValue + slope * (targetYear - meanYear);
                    double growthFit = Math.Exp(lnForecast);
                    decimal result = growthFit < 0 ? 0m : Convert.ToDecimal(growthFit);
                    return (result, $"Log-linear growth fit from {trendPoints.Count} historical data point(s) (minimum 2 positive points).");
                }
            }

            // 3. Sector-specific export ratio fallback (active)
            var primarySector = await GetCompanyPrimarySectorAsync(CompanyId, TargetFinancialYear);
            var sectorExportRatio = await CalculateSectorRegionalExportRatioAsync(primarySector, regionQuestionTitle, targetYear);

            if (sectorExportRatio.HasValue)
            {
                var revenueForecast = await CalculateForecastedRevenueWithSectorFallbackAsync(revenueHistory, targetYear);

                if (revenueForecast.Value.HasValue)
                {
                    var forecastValue = revenueForecast.Value.Value * sectorExportRatio.Value;
                    return (forecastValue, $"Insufficient regional history ({trendPoints.Count} points, minimum 2 required). Using primary-sector export ratio ({sectorExportRatio:P2}) for {regionLabel} and applying it to forecasted total revenue.");
                }
            }

            // 4. No result
            return (null, $"No actual value and insufficient positive historical points ({trendPoints.Count}, minimum 2 required) for growth fit. Sector-specific export ratio fallback not available (missing sector ratio or total revenue forecast).");
        }

        private async Task<decimal?> CalculateSectorRegionalEmploymentRatioAsync(string? primarySector, string regionalEmploymentQuestionTitle, int targetYear)
        {
            if (string.IsNullOrWhiteSpace(primarySector))
            {
                return null;
            }

            // Build a sector-specific regional employment ratio (regional employment / total employment) from the last 5 years.
            var sectorData = await (
                from companySurvey in _context.CompanySurvey
                join sectorAnswer in _context.Answer on companySurvey.Id equals sectorAnswer.CompanySurveyId
                join sectorQuestion in _context.Question on sectorAnswer.QuestionId equals sectorQuestion.Id
                join regionalEmploymentAnswer in _context.Answer on companySurvey.Id equals regionalEmploymentAnswer.CompanySurveyId
                join regionalEmploymentQuestion in _context.Question on regionalEmploymentAnswer.QuestionId equals regionalEmploymentQuestion.Id
                join totalEmploymentAnswer in _context.Answer on companySurvey.Id equals totalEmploymentAnswer.CompanySurveyId
                join totalEmploymentQuestion in _context.Question on totalEmploymentAnswer.QuestionId equals totalEmploymentQuestion.Id
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                where sectorQuestion.Title == "Primary Sector"
                    && (sectorAnswer.AnswerText ?? string.Empty) == primarySector
                    && regionalEmploymentQuestion.Title == regionalEmploymentQuestionTitle
                    && (totalEmploymentQuestion.Title == EmploymentQuestionTitle || totalEmploymentQuestion.Title == EmploymentQuestionTitleLegacy)
                    && survey.FinancialYear < targetYear
                select new
                {
                    survey.FinancialYear,
                    RegionalEmploymentCurrency = regionalEmploymentAnswer.AnswerCurrency,
                    RegionalEmploymentNumber = regionalEmploymentAnswer.AnswerNumber,
                    RegionalEmploymentText = regionalEmploymentAnswer.AnswerText,
                    TotalEmploymentCurrency = totalEmploymentAnswer.AnswerCurrency,
                    TotalEmploymentNumber = totalEmploymentAnswer.AnswerNumber,
                    TotalEmploymentText = totalEmploymentAnswer.AnswerText
                }
            ).ToListAsync();

            var yearlyRatios = sectorData
                .Select(x => new
                {
                    x.FinancialYear,
                    RegionalEmploymentValue = ResolveMetricValue(x.RegionalEmploymentCurrency, x.RegionalEmploymentNumber, x.RegionalEmploymentText),
                    TotalEmploymentValue = ResolveMetricValue(x.TotalEmploymentCurrency, x.TotalEmploymentNumber, x.TotalEmploymentText)
                })
                .Where(x => x.RegionalEmploymentValue.HasValue && x.TotalEmploymentValue.HasValue && x.TotalEmploymentValue.Value > 0)
                .GroupBy(x => x.FinancialYear)
                .Select(g => new
                {
                    Year = g.Key,
                    AvgRegionalEmployment = g.Average(x => (double)x.RegionalEmploymentValue!.Value),
                    AvgTotalEmployment = g.Average(x => (double)x.TotalEmploymentValue!.Value)
                })
                .Where(x => x.AvgTotalEmployment > 0)
                .OrderByDescending(x => x.Year)
                .Take(5)
                .ToList();

            if (yearlyRatios.Count == 0)
            {
                return null;
            }

            var averageRatio = yearlyRatios.Average(x => x.AvgRegionalEmployment / x.AvgTotalEmployment);
            return Convert.ToDecimal(averageRatio);
        }

        private async Task<(decimal? Value, string Reason)> CalculateForecastedRegionalEmploymentWithSectorFallbackAsync(
            List<MetricHistoryRow> regionalEmploymentHistory,
            List<MetricHistoryRow> totalEmploymentHistory,
            int targetYear,
            string regionQuestionTitle)
        {
            var regionLabel = GetRegionalEmploymentRegionLabel(regionQuestionTitle);

            // 1. Use actual if exists for target year.
            var actual = regionalEmploymentHistory
                .Where(x => x.FinancialYear == targetYear)
                .OrderByDescending(x => x.CompanySurveyId)
                .Select(x => x.Value)
                .FirstOrDefault();

            if (actual.HasValue)
            {
                return (actual.Value, $"Using actual Regional Employment {regionLabel} for target financial year.");
            }

            // 2. Log-linear growth fit (>= 2 positive historical points).
            var trendPoints = regionalEmploymentHistory
                .Where(x => x.FinancialYear < targetYear && x.Value.HasValue && x.Value.Value > 0)
                .OrderByDescending(x => x.FinancialYear)
                .Take(5)
                .Select(x => (Year: (double)x.FinancialYear, Value: (double)x.Value!.Value))
                .ToList();

            if (trendPoints.Count >= 2)
            {
                double meanYear = trendPoints.Average(d => d.Year);
                double meanLnValue = trendPoints.Average(d => Math.Log(d.Value));
                double numerator = trendPoints.Sum(d => (d.Year - meanYear) * (Math.Log(d.Value) - meanLnValue));
                double denominator = trendPoints.Sum(d => Math.Pow(d.Year - meanYear, 2));

                if (denominator > 0)
                {
                    double slope = numerator / denominator;
                    double lnForecast = meanLnValue + slope * (targetYear - meanYear);
                    double growthFit = Math.Exp(lnForecast);
                    decimal result = growthFit < 0 ? 0m : Convert.ToDecimal(growthFit);
                    return (result, $"Log-linear growth fit from {trendPoints.Count} historical data point(s) (minimum 2 positive points) for Regional Employment {regionLabel}.");
                }
            }

            // 3. Sector-percentage fallback (active): Regional Employment = Sector % × Total Employment.
            var primarySector = await GetCompanyPrimarySectorAsync(CompanyId, TargetFinancialYear);
            var sectorRatio = await CalculateSectorRegionalEmploymentRatioAsync(primarySector, regionQuestionTitle, targetYear);

            if (sectorRatio.HasValue)
            {
                var totalEmploymentForecast = await CalculateForecastedEmploymentWithSectorFallbackAsync(totalEmploymentHistory, targetYear);
                if (totalEmploymentForecast.Value.HasValue)
                {
                    var forecastValue = totalEmploymentForecast.Value.Value * sectorRatio.Value;
                    return (forecastValue, $"Insufficient regional history ({trendPoints.Count} points, minimum 2 required). Using sector employment share ({sectorRatio:P2}) for {regionLabel} and applying it to forecasted total employment.");
                }
            }

            // 4. No result.
            return (null, $"No actual value and insufficient positive historical points ({trendPoints.Count}, minimum 2 required) for growth fit. Sector percentage fallback not available (missing sector share or total employment forecast).");
        }

        private async Task<decimal?> CalculateSectorEbitdaRatioAsync(string? primarySector, int targetYear)
        {
            if (string.IsNullOrWhiteSpace(primarySector))
            {
                return null;
            }

            // Use last 5 years of sector EBITDA/revenue relationship for the primary sector.
            var sectorData = await (
                from companySurvey in _context.CompanySurvey
                join sectorAnswer in _context.Answer on companySurvey.Id equals sectorAnswer.CompanySurveyId
                join sectorQuestion in _context.Question on sectorAnswer.QuestionId equals sectorQuestion.Id
                join ebitdaAnswer in _context.Answer on companySurvey.Id equals ebitdaAnswer.CompanySurveyId
                join ebitdaQuestion in _context.Question on ebitdaAnswer.QuestionId equals ebitdaQuestion.Id
                join revenueAnswer in _context.Answer on companySurvey.Id equals revenueAnswer.CompanySurveyId
                join revenueQuestion in _context.Question on revenueAnswer.QuestionId equals revenueQuestion.Id
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                where sectorQuestion.Title == "Primary Sector"
                    && (sectorAnswer.AnswerText ?? string.Empty) == primarySector
                    && ebitdaQuestion.Title == EbitdaQuestionTitle
                    && revenueQuestion.Title == RevenueQuestionTitle
                    && survey.FinancialYear < targetYear
                select new
                {
                    survey.FinancialYear,
                    EbitdaCurrency = ebitdaAnswer.AnswerCurrency,
                    EbitdaNumber = ebitdaAnswer.AnswerNumber,
                    EbitdaText = ebitdaAnswer.AnswerText,
                    RevenueCurrency = revenueAnswer.AnswerCurrency,
                    RevenueNumber = revenueAnswer.AnswerNumber,
                    RevenueText = revenueAnswer.AnswerText
                }
            ).ToListAsync();

            var yearlyRatios = sectorData
                .Select(x => new
                {
                    x.FinancialYear,
                    EbitdaValue = ResolveMetricValue(x.EbitdaCurrency, x.EbitdaNumber, x.EbitdaText),
                    RevenueValue = ResolveMetricValue(x.RevenueCurrency, x.RevenueNumber, x.RevenueText)
                })
                .Where(x => x.EbitdaValue.HasValue && x.RevenueValue.HasValue && x.RevenueValue.Value != 0)
                .GroupBy(x => x.FinancialYear)
                .Select(g => new
                {
                    Year = g.Key,
                    AvgEbitda = g.Average(x => (double)x.EbitdaValue!.Value),
                    AvgRevenue = g.Average(x => (double)x.RevenueValue!.Value)
                })
                .Where(x => x.AvgRevenue != 0)
                .OrderByDescending(x => x.Year)
                .Take(5)
                .ToList();

            if (yearlyRatios.Count == 0)
            {
                return null;
            }

            var averageRatio = yearlyRatios.Average(x => x.AvgEbitda / x.AvgRevenue);
            return Convert.ToDecimal(averageRatio);
        }

        private async Task<(decimal? Value, string Reason)> CalculateForecastedEbitdaWithSectorFallbackAsync(
            List<MetricHistoryRow> ebitdaHistory,
            List<MetricHistoryRow> revenueHistory,
            int targetYear)
        {
            // Use actual if exists for target year
            var actual = ebitdaHistory
                .Where(x => x.FinancialYear == targetYear)
                .OrderByDescending(x => x.CompanySurveyId)
                .Select(x => x.Value)
                .FirstOrDefault();

            if (actual.HasValue)
            {
                return (actual.Value, "Using actual EBITDA for target financial year.");
            }

            // Historical data for linear regression (years < targetYear, non-null)
            var trendPoints = ebitdaHistory
                .Where(x => x.FinancialYear < targetYear && x.Value.HasValue)
                .Select(x => (Year: (double)x.FinancialYear, Value: (double)x.Value!.Value))
                .ToList();

            int numPoints = trendPoints.Count;

            if (numPoints <= 3)
            {
                var primarySector = await GetCompanyPrimarySectorAsync(CompanyId, TargetFinancialYear);
                var sectorRatio = await CalculateSectorEbitdaRatioAsync(primarySector, targetYear);

                if (EnableEbitdaSectorFallback && sectorRatio.HasValue)
                {
                    var forecastedRevenue = await CalculateForecastedRevenueWithSectorFallbackAsync(revenueHistory, targetYear);

                    if (forecastedRevenue.Value.HasValue)
                    {
                        var result = forecastedRevenue.Value.Value * sectorRatio.Value;
                        return (result, $"Insufficient historical data ({numPoints} points, minimum 4 required). Sector-based fallback applied using primary-sector EBITDA/revenue ratio ({sectorRatio:P2}) over last 5 years: forecasted revenue × sector ratio.");
                    }
                }

                return (null, $"Insufficient historical data ({numPoints} data points, minimum 4 required for linear trend). Sector-based fallback could not be applied due to missing sector ratio or revenue forecast.");
            }

            // Linear regression (FORECAST.LINEAR equivalent)
            double avgX = trendPoints.Average(d => d.Year);
            double avgY = trendPoints.Average(d => d.Value);
            double numerator = trendPoints.Sum(d => (d.Year - avgX) * (d.Value - avgY));
            double denominator = trendPoints.Sum(d => Math.Pow(d.Year - avgX, 2));

            if (denominator == 0)
            {
                return (null, "Linear regression failed (all financial years are identical).");
            }

            double slope = numerator / denominator;
            double intercept = avgY - slope * avgX;
            double forecastValue = slope * targetYear + intercept;

            // Floor at 0
            decimal finalResult = forecastValue < 0 ? 0m : Convert.ToDecimal(forecastValue);
            return (finalResult, $"Linear trend fitted on {numPoints} historical data points (FY{(int)trendPoints.Min(d => d.Year)}\u2013FY{(int)trendPoints.Max(d => d.Year)}). Slope: {slope:N4}, Intercept: {intercept:N2}. Forecast floored at 0.");
        }


        private async Task<decimal?> CalculateSectorWagesRatioAsync(string? primarySector, int targetYear)
        {
            if (string.IsNullOrWhiteSpace(primarySector))
            {
                return null;
            }

            // Get all companies with the same primary sector and their wages/revenue history for past 5 years
            var sectorData = await (
                from companySurvey in _context.CompanySurvey
                join answer in _context.Answer on companySurvey.Id equals answer.CompanySurveyId
                join question in _context.Question on answer.QuestionId equals question.Id
                join wagesAnswer in _context.Answer on companySurvey.Id equals wagesAnswer.CompanySurveyId
                join wagesQuestion in _context.Question on wagesAnswer.QuestionId equals wagesQuestion.Id
                join revenueAnswer in _context.Answer on companySurvey.Id equals revenueAnswer.CompanySurveyId
                join revenueQuestion in _context.Question on revenueAnswer.QuestionId equals revenueQuestion.Id
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                where question.Title == "Primary Sector" 
                    && (answer.AnswerText ?? string.Empty) == primarySector
                    && wagesQuestion.Title == WagesQuestionTitle
                    && revenueQuestion.Title == RevenueQuestionTitle
                    && survey.FinancialYear < targetYear
                select new
                {
                    companySurvey.Id,
                    survey.FinancialYear,
                    WagesAnswerCurrency = wagesAnswer.AnswerCurrency,
                    WagesAnswerNumber = wagesAnswer.AnswerNumber,
                    WagesAnswerText = wagesAnswer.AnswerText,
                    RevenueAnswerCurrency = revenueAnswer.AnswerCurrency,
                    RevenueAnswerNumber = revenueAnswer.AnswerNumber,
                    RevenueAnswerText = revenueAnswer.AnswerText
                }
            ).ToListAsync();

            if (sectorData.Count == 0)
            {
                return null;
            }

            // Group by year and calculate average wages/revenue for last 5 years
            var yearlyRatios = sectorData
                .Select(x => new
                {
                    x.FinancialYear,
                    WagesValue = ResolveMetricValue(x.WagesAnswerCurrency, x.WagesAnswerNumber, x.WagesAnswerText) ?? 0m,
                    RevenueValue = ResolveMetricValue(x.RevenueAnswerCurrency, x.RevenueAnswerNumber, x.RevenueAnswerText) ?? 0m
                })
                .Where(x => x.WagesValue > 0 && x.RevenueValue > 0)
                .GroupBy(x => x.FinancialYear)
                .Select(g => new
                {
                    Year = g.Key,
                    AvgWages = g.Average(x => (double)x.WagesValue),
                    AvgRevenue = g.Average(x => (double)x.RevenueValue)
                })
                .OrderByDescending(x => x.Year)
                .Take(5)
                .ToList();

            if (yearlyRatios.Count == 0)
            {
                return null;
            }

            // Calculate average ratio across years
            var averageRatio = yearlyRatios.Average(x => x.AvgWages / x.AvgRevenue);
            return Convert.ToDecimal(averageRatio);
        }

        private async Task<decimal?> CalculateSectorResearchDevelopmentRatioAsync(string? primarySector, int targetYear)
        {
            if (string.IsNullOrWhiteSpace(primarySector))
            {
                return null;
            }

            // Get all companies with the same primary sector and their R&D/revenue history for past 5 years
            var sectorData = await (
                from companySurvey in _context.CompanySurvey
                join answer in _context.Answer on companySurvey.Id equals answer.CompanySurveyId
                join question in _context.Question on answer.QuestionId equals question.Id
                join rdAnswer in _context.Answer on companySurvey.Id equals rdAnswer.CompanySurveyId
                join rdQuestion in _context.Question on rdAnswer.QuestionId equals rdQuestion.Id
                join revenueAnswer in _context.Answer on companySurvey.Id equals revenueAnswer.CompanySurveyId
                join revenueQuestion in _context.Question on revenueAnswer.QuestionId equals revenueQuestion.Id
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                where question.Title == "Primary Sector" 
                    && (answer.AnswerText ?? string.Empty) == primarySector
                    && rdQuestion.Title == ResearchDevelopmentQuestionTitle
                    && revenueQuestion.Title == RevenueQuestionTitle
                    && survey.FinancialYear < targetYear
                select new
                {
                    companySurvey.Id,
                    survey.FinancialYear,
                    RdAnswerCurrency = rdAnswer.AnswerCurrency,
                    RdAnswerNumber = rdAnswer.AnswerNumber,
                    RdAnswerText = rdAnswer.AnswerText,
                    RevenueAnswerCurrency = revenueAnswer.AnswerCurrency,
                    RevenueAnswerNumber = revenueAnswer.AnswerNumber,
                    RevenueAnswerText = revenueAnswer.AnswerText
                }
            ).ToListAsync();

            if (sectorData.Count == 0)
            {
                return null;
            }

            // Group by year and calculate average R&D/revenue for last 5 years
            var yearlyRatios = sectorData
                .Select(x => new
                {
                    x.FinancialYear,
                    RdValue = ResolveMetricValue(x.RdAnswerCurrency, x.RdAnswerNumber, x.RdAnswerText) ?? 0m,
                    RevenueValue = ResolveMetricValue(x.RevenueAnswerCurrency, x.RevenueAnswerNumber, x.RevenueAnswerText) ?? 0m
                })
                .Where(x => x.RdValue > 0 && x.RevenueValue > 0)
                .GroupBy(x => x.FinancialYear)
                .Select(g => new
                {
                    Year = g.Key,
                    AvgRd = g.Average(x => (double)x.RdValue),
                    AvgRevenue = g.Average(x => (double)x.RevenueValue)
                })
                .OrderByDescending(x => x.Year)
                .Take(5)
                .ToList();

            if (yearlyRatios.Count == 0)
            {
                return null;
            }

            // Calculate average ratio across years
            var averageRatio = yearlyRatios.Average(x => x.AvgRd / x.AvgRevenue);
            return Convert.ToDecimal(averageRatio);
        }

        private async Task<decimal?> CalculateSectorSalesMarketingRatioAsync(string? primarySector, int targetYear)
        {
            if (string.IsNullOrWhiteSpace(primarySector))
            {
                return null;
            }

            // Get all companies with the same primary sector and their S&M/revenue history for past 5 years
            var sectorData = await (
                from companySurvey in _context.CompanySurvey
                join answer in _context.Answer on companySurvey.Id equals answer.CompanySurveyId
                join question in _context.Question on answer.QuestionId equals question.Id
                join smAnswer in _context.Answer on companySurvey.Id equals smAnswer.CompanySurveyId
                join smQuestion in _context.Question on smAnswer.QuestionId equals smQuestion.Id
                join revenueAnswer in _context.Answer on companySurvey.Id equals revenueAnswer.CompanySurveyId
                join revenueQuestion in _context.Question on revenueAnswer.QuestionId equals revenueQuestion.Id
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                where question.Title == "Primary Sector" 
                    && (answer.AnswerText ?? string.Empty) == primarySector
                    && smQuestion.Title == SalesMarketingQuestionTitle
                    && revenueQuestion.Title == RevenueQuestionTitle
                    && survey.FinancialYear < targetYear
                select new
                {
                    companySurvey.Id,
                    survey.FinancialYear,
                    SmAnswerCurrency = smAnswer.AnswerCurrency,
                    SmAnswerNumber = smAnswer.AnswerNumber,
                    SmAnswerText = smAnswer.AnswerText,
                    RevenueAnswerCurrency = revenueAnswer.AnswerCurrency,
                    RevenueAnswerNumber = revenueAnswer.AnswerNumber,
                    RevenueAnswerText = revenueAnswer.AnswerText
                }
            ).ToListAsync();

            if (sectorData.Count == 0)
            {
                return null;
            }

            // Group by year and calculate average S&M/revenue for last 5 years
            var yearlyRatios = sectorData
                .Select(x => new
                {
                    x.FinancialYear,
                    SmValue = ResolveMetricValue(x.SmAnswerCurrency, x.SmAnswerNumber, x.SmAnswerText) ?? 0m,
                    RevenueValue = ResolveMetricValue(x.RevenueAnswerCurrency, x.RevenueAnswerNumber, x.RevenueAnswerText) ?? 0m
                })
                .Where(x => x.SmValue > 0 && x.RevenueValue > 0)
                .GroupBy(x => x.FinancialYear)
                .Select(g => new
                {
                    Year = g.Key,
                    AvgSm = g.Average(x => (double)x.SmValue),
                    AvgRevenue = g.Average(x => (double)x.RevenueValue)
                })
                .OrderByDescending(x => x.Year)
                .Take(5)
                .ToList();

            if (yearlyRatios.Count == 0)
            {
                return null;
            }

            // Calculate average ratio across years
            var averageRatio = yearlyRatios.Average(x => x.AvgSm / x.AvgRevenue);
            return Convert.ToDecimal(averageRatio);
        }

        private async Task<(decimal? Value, string Reason)> CalculateForecastedWagesWithSectorFallbackAsync(
            List<MetricHistoryRow> wagesHistory,
            List<MetricHistoryRow> revenueHistory,
            int targetYear)
        {
            // Step 1: Use actual if exists for target year
            var actual = wagesHistory
                .Where(x => x.FinancialYear == targetYear)
                .OrderByDescending(x => x.CompanySurveyId)
                .Select(x => x.Value)
                .FirstOrDefault();

            if (actual.HasValue)
            {
                return (actual.Value, "Using actual wages and salaries for target financial year.");
            }

            // Step 2: Log-linear trend fit (> 3 positive points)
            var trendPoints = wagesHistory
                .Where(x => x.FinancialYear < targetYear && x.Value.HasValue && x.Value.Value > 0)
                .OrderByDescending(x => x.FinancialYear)
                .Take(5)
                .Select(x => new
                {
                    x.FinancialYear,
                    Value = (double)x.Value!.Value
                })
                .ToList();

            if (trendPoints.Count > 3)
            {
                var meanYear = trendPoints.Average(x => (double)x.FinancialYear);
                var meanLnValue = trendPoints.Average(x => Math.Log(x.Value));

                var numerator = trendPoints.Sum(x => ((double)x.FinancialYear - meanYear) * (Math.Log(x.Value) - meanLnValue));
                var denominator = trendPoints.Sum(x => Math.Pow((double)x.FinancialYear - meanYear, 2));

                if (denominator > 0)
                {
                    var slope = numerator / denominator;
                    var lnForecast = meanLnValue + slope * (targetYear - meanYear);
                    var trendFit = Math.Exp(lnForecast);

                    return (Convert.ToDecimal(trendFit), "Using log-linear trend fit from previous years for wages and salaries (more than 3 points).");
                }
            }

            // Step 3: Sector-ratio fallback (active)
            var primarySector = await GetCompanyPrimarySectorAsync(CompanyId, TargetFinancialYear);
            var sectorRatio = await CalculateSectorWagesRatioAsync(primarySector, targetYear);

            if (sectorRatio.HasValue && sectorRatio.Value > 0)
            {
                var forecastedRevenue = await CalculateForecastedRevenueWithSectorFallbackAsync(revenueHistory, targetYear);
                
                if (forecastedRevenue.Value.HasValue && forecastedRevenue.Value.Value > 0)
                {
                    var forecastValue = forecastedRevenue.Value.Value * sectorRatio.Value;
                    return (forecastValue > 0 ? forecastValue : 0m, $"Insufficient company-level historical data ({trendPoints.Count} points, maximum 3 required). Using sector wages-to-revenue ratio ({sectorRatio:P2}) from primary sector '{primarySector}' applied to forecasted revenue.");
                }
            }

            // Step 4: No result
            return (null, $"No actual value and insufficient positive historical points ({trendPoints.Count}, more than 3 required) for trend fit. Sector-based fallback not available (no sector data or unable to forecast base revenue).");
        }

        private async Task<(decimal? Value, string Reason)> CalculateForecastedResearchDevelopmentWithSectorFallbackAsync(
            List<MetricHistoryRow> researchDevelopmentHistory,
            List<MetricHistoryRow> revenueHistory,
            int targetYear)
        {
            // Step 1: Use actual if exists for target year
            var actual = researchDevelopmentHistory
                .Where(x => x.FinancialYear == targetYear)
                .OrderByDescending(x => x.CompanySurveyId)
                .Select(x => x.Value)
                .FirstOrDefault();

            if (actual.HasValue)
            {
                return (actual.Value, "Using actual research and development for target financial year.");
            }

            // Step 2: Log-linear trend fit (>= 3 positive points)
            var trendPoints = researchDevelopmentHistory
                .Where(x => x.FinancialYear < targetYear && x.Value.HasValue && x.Value.Value > 0)
                .OrderByDescending(x => x.FinancialYear)
                .Take(5)
                .Select(x => new
                {
                    x.FinancialYear,
                    Value = (double)x.Value!.Value
                })
                .ToList();

            if (trendPoints.Count >= 3)
            {
                var meanYear = trendPoints.Average(x => (double)x.FinancialYear);
                var meanLnValue = trendPoints.Average(x => Math.Log(x.Value));

                var numerator = trendPoints.Sum(x => ((double)x.FinancialYear - meanYear) * (Math.Log(x.Value) - meanLnValue));
                var denominator = trendPoints.Sum(x => Math.Pow((double)x.FinancialYear - meanYear, 2));

                if (denominator > 0)
                {
                    var slope = numerator / denominator;
                    var lnForecast = meanLnValue + slope * (targetYear - meanYear);
                    var trendFit = Math.Exp(lnForecast);

                    return (Convert.ToDecimal(trendFit), "Using log-linear trend fit from previous years for research and development (minimum 3 points).");
                }
            }

            // Step 3: Sector-ratio fallback (active)
            var primarySector = await GetCompanyPrimarySectorAsync(CompanyId, TargetFinancialYear);
            var sectorRatio = await CalculateSectorResearchDevelopmentRatioAsync(primarySector, targetYear);

            if (sectorRatio.HasValue && sectorRatio.Value > 0)
            {
                var forecastedRevenue = await CalculateForecastedRevenueWithSectorFallbackAsync(revenueHistory, targetYear);
                
                if (forecastedRevenue.Value.HasValue && forecastedRevenue.Value.Value > 0)
                {
                    var forecastValue = forecastedRevenue.Value.Value * sectorRatio.Value;
                    return (forecastValue > 0 ? forecastValue : 0m, $"Insufficient company-level historical data ({trendPoints.Count} points, minimum 3 required). Using sector R&D-to-revenue ratio ({sectorRatio:P2}) from primary sector '{primarySector}' applied to forecasted revenue.");
                }
            }

            // Step 4: No result
            return (null, $"No actual value and insufficient positive historical points ({trendPoints.Count}, minimum 3 required) for trend fit. Sector-based fallback not available (no sector data or unable to forecast base revenue).");
        }

        private async Task<(decimal? Value, string Reason)> CalculateForecastedSalesMarketingWithSectorFallbackAsync(
            List<MetricHistoryRow> salesMarketingHistory,
            List<MetricHistoryRow> revenueHistory,
            int targetYear)
        {
            // Step 1: Use actual if exists for target year
            var actual = salesMarketingHistory
                .Where(x => x.FinancialYear == targetYear)
                .OrderByDescending(x => x.CompanySurveyId)
                .Select(x => x.Value)
                .FirstOrDefault();

            if (actual.HasValue)
            {
                return (actual.Value, "Using actual sales and marketing for target financial year.");
            }

            // Step 2: Log-linear trend fit (>= 3 positive points)
            var trendPoints = salesMarketingHistory
                .Where(x => x.FinancialYear < targetYear && x.Value.HasValue && x.Value.Value > 0)
                .OrderByDescending(x => x.FinancialYear)
                .Take(5)
                .Select(x => new
                {
                    x.FinancialYear,
                    Value = (double)x.Value!.Value
                })
                .ToList();

            if (trendPoints.Count >= 3)
            {
                var meanYear = trendPoints.Average(x => (double)x.FinancialYear);
                var meanLnValue = trendPoints.Average(x => Math.Log(x.Value));

                var numerator = trendPoints.Sum(x => ((double)x.FinancialYear - meanYear) * (Math.Log(x.Value) - meanLnValue));
                var denominator = trendPoints.Sum(x => Math.Pow((double)x.FinancialYear - meanYear, 2));

                if (denominator > 0)
                {
                    var slope = numerator / denominator;
                    var lnForecast = meanLnValue + slope * (targetYear - meanYear);
                    var trendFit = Math.Exp(lnForecast);

                    return (Convert.ToDecimal(trendFit), "Using log-linear trend fit from previous years for sales and marketing (minimum 3 points).");
                }
            }

            // Step 3: Sector-ratio fallback (active)
            var primarySector = await GetCompanyPrimarySectorAsync(CompanyId, TargetFinancialYear);
            var sectorRatio = await CalculateSectorSalesMarketingRatioAsync(primarySector, targetYear);

            if (sectorRatio.HasValue && sectorRatio.Value > 0)
            {
                var forecastedRevenue = await CalculateForecastedRevenueWithSectorFallbackAsync(revenueHistory, targetYear);
                
                if (forecastedRevenue.Value.HasValue && forecastedRevenue.Value.Value > 0)
                {
                    var forecastValue = forecastedRevenue.Value.Value * sectorRatio.Value;
                    return (forecastValue > 0 ? forecastValue : 0m, $"Insufficient company-level historical data ({trendPoints.Count} points, minimum 3 required). Using sector S&M-to-revenue ratio ({sectorRatio:P2}) from primary sector '{primarySector}' applied to forecasted revenue.");
                }
            }

            // Step 4: No result
            return (null, $"No actual value and insufficient positive historical points ({trendPoints.Count}, minimum 3 required) for trend fit. Sector-based fallback not available (no sector data or unable to forecast base revenue).");
        }

        private static decimal? CalculateForecastedWages(
            List<MetricHistoryRow> wagesHistory,
            List<MetricHistoryRow> revenueHistory,
            int targetYear,
            out string reason)
        {
            var actual = wagesHistory
                .Where(x => x.FinancialYear == targetYear)
                .OrderByDescending(x => x.CompanySurveyId)
                .Select(x => x.Value)
                .FirstOrDefault();

            if (actual.HasValue)
            {
                reason = "Using actual wages and salaries for target financial year.";
                return actual.Value;
            }

            var trendPoints = wagesHistory
                .Where(x => x.FinancialYear < targetYear && x.Value.HasValue && x.Value.Value > 0)
                .OrderByDescending(x => x.FinancialYear)
                .Take(5)
                .Select(x => new
                {
                    x.FinancialYear,
                    Value = (double)x.Value!.Value
                })
                .ToList();

            if (trendPoints.Count > 3)
            {
                var meanYear = trendPoints.Average(x => (double)x.FinancialYear);
                var meanLnValue = trendPoints.Average(x => Math.Log(x.Value));

                var numerator = trendPoints.Sum(x => ((double)x.FinancialYear - meanYear) * (Math.Log(x.Value) - meanLnValue));
                var denominator = trendPoints.Sum(x => Math.Pow((double)x.FinancialYear - meanYear, 2));

                if (denominator > 0)
                {
                    var slope = numerator / denominator;
                    var lnForecast = meanLnValue + slope * (targetYear - meanYear);
                    var trendFit = Math.Exp(lnForecast);

                    reason = "Using log-linear trend fit from previous years for wages and salaries (more than 3 points).";
                    return Convert.ToDecimal(trendFit);
                }
            }

            if (EnableWagesSectorFallback)
            {
                var latestRevenue = revenueHistory
                    .Where(x => x.FinancialYear == targetYear)
                    .OrderByDescending(x => x.CompanySurveyId)
                    .Select(x => x.Value)
                    .FirstOrDefault() ?? 0m;

                reason = "Using sector-based fallback from latest revenue and configured primary/secondary ratios.";
                return latestRevenue * WagesPrimaryRatio + latestRevenue * WagesSecondaryRatio;
            }

            reason = "No actual value and insufficient positive historical points for wages trend fit. Sector-based fallback logic is configured but currently disabled.";
            return null;
        }

        public class MetricHistoryRow
        {
            public int CompanySurveyId { get; set; }
            public int FinancialYear { get; set; }
            public decimal? Value { get; set; }
        }

        public class EstimationGroupSection
        {
            public int GroupId { get; set; }
            public string GroupTitle { get; set; } = string.Empty;
            public List<EstimationGroupQuestion> Questions { get; set; } = new();
        }

        public class EstimationGroupQuestion
        {
            public string QuestionTitle { get; set; } = string.Empty;
            public string HandlerName { get; set; } = string.Empty;
            public string ValueLabel { get; set; } = string.Empty;
            public string EmptyStateMessage { get; set; } = string.Empty;
            public List<MetricHistoryRow> LastFiveYears { get; set; } = new();
        }

        public class CalculationCandidate
        {
            public string StepName { get; set; } = string.Empty;
            public decimal? Value { get; set; }
            public string Details { get; set; } = string.Empty;
            public List<string> FailureReasons { get; set; } = new();
            public bool IsUsed { get; set; }
        }
    }
}
