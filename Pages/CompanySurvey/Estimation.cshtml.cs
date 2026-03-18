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
        private static readonly bool EnableWagesSectorFallback = true;
        private const decimal WagesPrimaryRatio = 0.6m;
        private const decimal WagesSecondaryRatio = 0.3m;
        private static readonly bool EnableEbitdaSectorFallback = false;
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
        public string GeneratedSummary { get; set; } = string.Empty;
        public string SaveMessage { get; set; } = string.Empty;

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
            ForecastedRevenue = await CalculateForecastedRevenueWithSectorFallbackAsync(allRevenueHistory, TargetFinancialYear, out var revenueReason);
            ForecastReason = revenueReason;

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

            ForecastedEmployment = await CalculateForecastedEmploymentWithSectorFallbackAsync(allEmploymentHistory, TargetFinancialYear, out var employmentReason);
            EmploymentForecastReason = employmentReason;

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

            ForecastedWages = await CalculateForecastedWagesWithSectorFallbackAsync(wagesHistory, revenueHistory, TargetFinancialYear, out var wagesReason);
            WagesForecastReason = wagesReason;

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

            ForecastedResearchDevelopment = await CalculateForecastedResearchDevelopmentWithSectorFallbackAsync(researchDevelopmentHistory, revenueHistory, TargetFinancialYear, out var researchDevelopmentReason);
            ResearchDevelopmentForecastReason = researchDevelopmentReason;

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

            ForecastedSalesMarketing = await CalculateForecastedSalesMarketingWithSectorFallbackAsync(salesMarketingHistory, revenueHistory, TargetFinancialYear, out var salesMarketingReason);
            SalesMarketingForecastReason = salesMarketingReason;

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

            ForecastedEbitda = CalculateForecastedEbitda(ebitdaHistory, revenueHistory, TargetFinancialYear, out var ebitdaReason);
            EbitdaForecastReason = ebitdaReason;

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

            ForecastedRevenueNz = CalculateForecastedRevenueNz(revenueNzHistory, revenueHistory, TargetFinancialYear, out var revenueNzReason);
            RevenueNzForecastReason = revenueNzReason;

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

            ForecastedRevenueAustralia = CalculateForecastedRevenueAustralia(revenueAustraliaHistory, revenueHistory, TargetFinancialYear, out var revenueAustraliaReason);
            RevenueAustraliaForecastReason = revenueAustraliaReason;

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
                        ForecastedRevenueChina = CalculateForecastedRevenueAustralia(revenueChinaHistory, revenueHistory, TargetFinancialYear, out var revenueChinaReason);
                        RevenueChinaForecastReason = revenueChinaReason.Replace("Global Revenue Australia", "Global Revenue China");
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
                        ForecastedRevenueRestOfAsia = CalculateForecastedRevenueAustralia(revenueRestOfAsiaHistory, revenueHistory, TargetFinancialYear, out var revenueRestOfAsiaReason);
                        RevenueRestOfAsiaForecastReason = revenueRestOfAsiaReason.Replace("Global Revenue Australia", "Global Revenue Rest of Asia");
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
                        ForecastedRevenueNorthAmerica = CalculateForecastedRevenueAustralia(revenueNorthAmericaHistory, revenueHistory, TargetFinancialYear, out var revenueNorthAmericaReason);
                        RevenueNorthAmericaForecastReason = revenueNorthAmericaReason.Replace("Global Revenue Australia", "Global Revenue North America");
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
                        ForecastedRevenueEurope = CalculateForecastedRevenueAustralia(revenueEuropeHistory, revenueHistory, TargetFinancialYear, out var revenueEuropeReason);
                        RevenueEuropeForecastReason = revenueEuropeReason.Replace("Global Revenue Australia", "Global Revenue Europe");
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
                        ForecastedRevenueMiddleEast = CalculateForecastedRevenueAustralia(revenueMiddleEastHistory, revenueHistory, TargetFinancialYear, out var revenueMiddleEastReason);
                        RevenueMiddleEastForecastReason = revenueMiddleEastReason.Replace("Global Revenue Australia", "Global Revenue Middle East");
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
                        ForecastedRevenueLatinAmerica = CalculateForecastedRevenueAustralia(revenueLatinAmericaHistory, revenueHistory, TargetFinancialYear, out var revenueLatinAmericaReason);
                        RevenueLatinAmericaForecastReason = revenueLatinAmericaReason.Replace("Global Revenue Australia", "Global Revenue Latin America");
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
                        ForecastedRevenueAfrica = CalculateForecastedRevenueAustralia(revenueAfricaHistory, revenueHistory, TargetFinancialYear, out var revenueAfricaReason);
                        RevenueAfricaForecastReason = revenueAfricaReason.Replace("Global Revenue Australia", "Global Revenue Africa");
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
                        ForecastedRevenueOther = CalculateForecastedRevenueAustralia(revenueOtherHistory, revenueHistory, TargetFinancialYear, out var revenueOtherReason);
                        RevenueOtherForecastReason = revenueOtherReason.Replace("Global Revenue Australia", "Global Revenue Other");
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

            return true;
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

        private async Task<string?> GetCompanySecondarySectorAsync(int companyId)
        {
            var secondarySector = await (
                        private async Task<string?> GetCompanyPrimarySectorAsync(int companyId)
                        {
                            var primarySector = await (
                                from companySurvey in _context.CompanySurvey
                                join answer in _context.Answer on companySurvey.Id equals answer.CompanySurveyId
                                join question in _context.Question on answer.QuestionId equals question.Id
                                where companySurvey.CompanyId == companyId && question.Title == "Primary Sector"
                                orderby companySurvey.Id descending
                                select answer.AnswerText
                            ).FirstOrDefaultAsync();

                            return primarySector;
                        }

                from companySurvey in _context.CompanySurvey
                join answer in _context.Answer on companySurvey.Id equals answer.CompanySurveyId
                join question in _context.Question on answer.QuestionId equals question.Id
                where companySurvey.CompanyId == companyId && question.Title == "Secondary Sector"
                orderby companySurvey.Id descending
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

        private async Task<decimal?> CalculateForecastedEmploymentWithSectorFallbackAsync(
            List<MetricHistoryRow> employmentHistory,
            int targetYear,
            out string reason)
        {
            // Step 1: Use actual if exists for target year
            var actual = employmentHistory
                .Where(x => x.FinancialYear == targetYear)
                .OrderByDescending(x => x.CompanySurveyId)
                .Select(x => x.Value)
                .FirstOrDefault();

            if (actual.HasValue)
            {
                reason = "Using actual employment for target financial year.";
                return actual.Value;
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
                    reason = $"Log-linear trend fit from {trendPoints.Count} historical data point(s) (minimum 3 positive points).";
                    return result;
                }
            }

            // Step 3: Sector CAGR fallback (now active)
            var secondarySector = await GetCompanySecondarySectorAsync(CompanyId);
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
                    reason = $"Insufficient company-level historical data ({trendPoints.Count} points, minimum 3 required). Using sector CAGR ({sectorCAGR:P2}) from secondary sector '{secondarySector}' applied to latest employment over {yearsToForecast} year(s).";
                    return forecastValue > 0 ? forecastValue : 0m;
                }
            }

            // Step 4: No result
            reason = $"No actual value and insufficient positive historical points ({trendPoints.Count}, minimum 3 required) for trend fit. Sector CAGR fallback not available (no sector data or insufficient sector history).";
            return null;
        }

        private async Task<decimal?> CalculateForecastedRevenueWithSectorFallbackAsync(
            List<MetricHistoryRow> revenueHistory,
            int targetYear,
            out string reason)
        {
            // Step 1: Use actual if exists for target year
            var actual = revenueHistory
                .Where(x => x.FinancialYear == targetYear)
                .OrderByDescending(x => x.CompanySurveyId)
                .Select(x => x.Value)
                .FirstOrDefault();

            if (actual.HasValue)
            {
                reason = "Using actual revenue for target financial year.";
                return actual.Value;
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
                    reason = $"Log-linear trend fit from {trendPoints.Count} historical data point(s) (minimum 3 positive points).";
                    return result;
                }
            }

            // Step 3: Sector CAGR fallback (now active)
            var secondarySector = await GetCompanySecondarySectorAsync(CompanyId);
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
                    reason = $"Insufficient company-level historical data ({trendPoints.Count} points, minimum 3 required). Using sector CAGR ({sectorCAGR:P2}) from secondary sector '{secondarySector}' applied to latest revenue over {yearsToForecast} year(s).";
                    return forecastValue > 0 ? forecastValue : 0m;
                }
            }

            // Step 4: No result
            reason = $"No actual value and insufficient positive historical points ({trendPoints.Count}, minimum 3 required) for trend fit. Sector CAGR fallback not available (no sector data or insufficient sector history).";
            return null;
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

        private decimal? CalculateForecastedRevenueAustralia(
            List<MetricHistoryRow> revenueAustraliaHistory,
            List<MetricHistoryRow> revenueHistory,
            int targetYear,
            out string reason)
        {
            var actual = revenueAustraliaHistory
                .Where(x => x.FinancialYear == targetYear)
                .OrderByDescending(x => x.CompanySurveyId)
                .Select(x => x.Value)
                .FirstOrDefault();

            if (actual.HasValue)
            {
                reason = "Using actual Global Revenue Australia for target financial year.";
                return actual.Value;
            }

            var trendPoints = revenueAustraliaHistory
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
                    reason = $"Log-linear growth fit from {trendPoints.Count} historical data point(s) (minimum 2 positive points).";
                    return result;
                }
            }

            if (EnableRevenueAustraliaSectorFallback)
            {
                var latestRevenue = revenueHistory
                    .Where(x => x.FinancialYear == targetYear)
                    .OrderByDescending(x => x.CompanySurveyId)
                    .Select(x => x.Value)
                    .FirstOrDefault() ?? 0m;

                reason = $"Insufficient historical data. Sector-based export ratio fallback applied: total revenue × {RevenueAustraliaExportRatio:P0}.";
                return latestRevenue * RevenueAustraliaExportRatio;
            }

            reason = $"No actual value and insufficient positive historical points ({trendPoints.Count}, minimum 2 required) for growth fit. Sector export ratio fallback is not currently active.";
            return null;
        }

        private decimal? CalculateForecastedRevenueNz(
            List<MetricHistoryRow> revenueNzHistory,
            List<MetricHistoryRow> revenueHistory,
            int targetYear,
            out string reason)
        {
            // 1. Use actual if exists for target year
            var actual = revenueNzHistory
                .Where(x => x.FinancialYear == targetYear)
                .OrderByDescending(x => x.CompanySurveyId)
                .Select(x => x.Value)
                .FirstOrDefault();

            if (actual.HasValue)
            {
                reason = "Using actual Global Revenue NZ for target financial year.";
                return actual.Value;
            }

            // 2. Log-linear growth fit (>= 2 positive historical points)
            var trendPoints = revenueNzHistory
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
                    reason = $"Log-linear growth fit from {trendPoints.Count} historical data point(s) (minimum 2 positive points).";
                    return result;
                }
            }

            // 3. Sector export ratio fallback (inactive)
            if (EnableRevenueNzSectorFallback)
            {
                var latestRevenue = revenueHistory
                    .Where(x => x.FinancialYear == targetYear)
                    .OrderByDescending(x => x.CompanySurveyId)
                    .Select(x => x.Value)
                    .FirstOrDefault() ?? 0m;

                reason = $"Insufficient historical data. Sector-based export ratio fallback applied: total revenue × {RevenueNzExportRatio:P0}.";
                return latestRevenue * RevenueNzExportRatio;
            }

            reason = $"No actual value and insufficient positive historical points ({trendPoints.Count}, minimum 2 required) for growth fit. Sector export ratio fallback is not currently active.";
            return null;
        }

        private decimal? CalculateForecastedEbitda(
            List<MetricHistoryRow> ebitdaHistory,
            List<MetricHistoryRow> revenueHistory,
            int targetYear,
            out string reason)
        {
            // Use actual if exists for target year
            var actual = ebitdaHistory
                .Where(x => x.FinancialYear == targetYear)
                .OrderByDescending(x => x.CompanySurveyId)
                .Select(x => x.Value)
                .FirstOrDefault();

            if (actual.HasValue)
            {
                reason = "Using actual EBITDA for target financial year.";
                return actual.Value;
            }

            // Historical data for linear regression (years < targetYear, non-null)
            var trendPoints = ebitdaHistory
                .Where(x => x.FinancialYear < targetYear && x.Value.HasValue)
                .Select(x => (Year: (double)x.FinancialYear, Value: (double)x.Value!.Value))
                .ToList();

            int numPoints = trendPoints.Count;

            if (numPoints <= 3)
            {
                if (EnableEbitdaSectorFallback)
                {
                    var latestRevenue = revenueHistory
                        .Where(x => x.FinancialYear == targetYear)
                        .OrderByDescending(x => x.CompanySurveyId)
                        .Select(x => x.Value)
                        .FirstOrDefault() ?? 0m;

                    reason = $"Insufficient historical data ({numPoints} points, minimum 4 required). Sector-based fallback applied: revenue * (primary {EbitdaPrimaryRatio:P0} + secondary {EbitdaSecondaryRatio:P0}).";
                    return latestRevenue * EbitdaPrimaryRatio + latestRevenue * EbitdaSecondaryRatio;
                }

                reason = $"Insufficient historical data ({numPoints} data points, minimum 4 required for linear trend). Sector-based fallback is not currently active.";
                return null;
            }

            // Linear regression (FORECAST.LINEAR equivalent)
            double avgX = trendPoints.Average(d => d.Year);
            double avgY = trendPoints.Average(d => d.Value);
            double numerator = trendPoints.Sum(d => (d.Year - avgX) * (d.Value - avgY));
            double denominator = trendPoints.Sum(d => Math.Pow(d.Year - avgX, 2));

            if (denominator == 0)
            {
                reason = "Linear regression failed (all financial years are identical).";
                return null;
            }

            double slope = numerator / denominator;
            double intercept = avgY - slope * avgX;
            double forecastValue = slope * targetYear + intercept;

            // Floor at 0
            decimal result = forecastValue < 0 ? 0m : Convert.ToDecimal(forecastValue);
            reason = $"Linear trend fitted on {numPoints} historical data points (FY{(int)trendPoints.Min(d => d.Year)}\u2013FY{(int)trendPoints.Max(d => d.Year)}). Slope: {slope:N4}, Intercept: {intercept:N2}. Forecast floored at 0.";
            return result;
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

        private async Task<decimal?> CalculateForecastedWagesWithSectorFallbackAsync(
            List<MetricHistoryRow> wagesHistory,
            List<MetricHistoryRow> revenueHistory,
            int targetYear,
            out string reason)
        {
            // Step 1: Use actual if exists for target year
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

                    reason = "Using log-linear trend fit from previous years for wages and salaries (more than 3 points).";
                    return Convert.ToDecimal(trendFit);
                }
            }

            // Step 3: Sector-ratio fallback (active)
            var primarySector = await GetCompanyPrimarySectorAsync(CompanyId);
            var sectorRatio = await CalculateSectorWagesRatioAsync(primarySector, targetYear);

            if (sectorRatio.HasValue && sectorRatio.Value > 0)
            {
                var forecastedRevenue = await CalculateForecastedRevenueWithSectorFallbackAsync(revenueHistory, targetYear, out _);
                
                if (forecastedRevenue.HasValue && forecastedRevenue.Value > 0)
                {
                    var forecastValue = forecastedRevenue.Value * sectorRatio.Value;
                    reason = $"Insufficient company-level historical data ({trendPoints.Count} points, maximum 3 required). Using sector wages-to-revenue ratio ({sectorRatio:P2}) from primary sector '{primarySector}' applied to forecasted revenue.";
                    return forecastValue > 0 ? forecastValue : 0m;
                }
            }

            // Step 4: No result
            reason = $"No actual value and insufficient positive historical points ({trendPoints.Count}, more than 3 required) for trend fit. Sector-based fallback not available (no sector data or unable to forecast base revenue).";
            return null;
        }

        private async Task<decimal?> CalculateForecastedResearchDevelopmentWithSectorFallbackAsync(
            List<MetricHistoryRow> researchDevelopmentHistory,
            List<MetricHistoryRow> revenueHistory,
            int targetYear,
            out string reason)
        {
            // Step 1: Use actual if exists for target year
            var actual = researchDevelopmentHistory
                .Where(x => x.FinancialYear == targetYear)
                .OrderByDescending(x => x.CompanySurveyId)
                .Select(x => x.Value)
                .FirstOrDefault();

            if (actual.HasValue)
            {
                reason = "Using actual research and development for target financial year.";
                return actual.Value;
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

                    reason = "Using log-linear trend fit from previous years for research and development (minimum 3 points).";
                    return Convert.ToDecimal(trendFit);
                }
            }

            // Step 3: Sector-ratio fallback (active)
            var primarySector = await GetCompanyPrimarySectorAsync(CompanyId);
            var sectorRatio = await CalculateSectorResearchDevelopmentRatioAsync(primarySector, targetYear);

            if (sectorRatio.HasValue && sectorRatio.Value > 0)
            {
                var forecastedRevenue = await CalculateForecastedRevenueWithSectorFallbackAsync(revenueHistory, targetYear, out _);
                
                if (forecastedRevenue.HasValue && forecastedRevenue.Value > 0)
                {
                    var forecastValue = forecastedRevenue.Value * sectorRatio.Value;
                    reason = $"Insufficient company-level historical data ({trendPoints.Count} points, minimum 3 required). Using sector R&D-to-revenue ratio ({sectorRatio:P2}) from primary sector '{primarySector}' applied to forecasted revenue.";
                    return forecastValue > 0 ? forecastValue : 0m;
                }
            }

            // Step 4: No result
            reason = $"No actual value and insufficient positive historical points ({trendPoints.Count}, minimum 3 required) for trend fit. Sector-based fallback not available (no sector data or unable to forecast base revenue).";
            return null;
        }

        private async Task<decimal?> CalculateForecastedSalesMarketingWithSectorFallbackAsync(
            List<MetricHistoryRow> salesMarketingHistory,
            List<MetricHistoryRow> revenueHistory,
            int targetYear,
            out string reason)
        {
            // Step 1: Use actual if exists for target year
            var actual = salesMarketingHistory
                .Where(x => x.FinancialYear == targetYear)
                .OrderByDescending(x => x.CompanySurveyId)
                .Select(x => x.Value)
                .FirstOrDefault();

            if (actual.HasValue)
            {
                reason = "Using actual sales and marketing for target financial year.";
                return actual.Value;
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

                    reason = "Using log-linear trend fit from previous years for sales and marketing (minimum 3 points).";
                    return Convert.ToDecimal(trendFit);
                }
            }

            // Step 3: Sector-ratio fallback (active)
            var primarySector = await GetCompanyPrimarySectorAsync(CompanyId);
            var sectorRatio = await CalculateSectorSalesMarketingRatioAsync(primarySector, targetYear);

            if (sectorRatio.HasValue && sectorRatio.Value > 0)
            {
                var forecastedRevenue = await CalculateForecastedRevenueWithSectorFallbackAsync(revenueHistory, targetYear, out _);
                
                if (forecastedRevenue.HasValue && forecastedRevenue.Value > 0)
                {
                    var forecastValue = forecastedRevenue.Value * sectorRatio.Value;
                    reason = $"Insufficient company-level historical data ({trendPoints.Count} points, minimum 3 required). Using sector S&M-to-revenue ratio ({sectorRatio:P2}) from primary sector '{primarySector}' applied to forecasted revenue.";
                    return forecastValue > 0 ? forecastValue : 0m;
                }
            }

            // Step 4: No result
            reason = $"No actual value and insufficient positive historical points ({trendPoints.Count}, minimum 3 required) for trend fit. Sector-based fallback not available (no sector data or unable to forecast base revenue).";
            return null;
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
    }
}
