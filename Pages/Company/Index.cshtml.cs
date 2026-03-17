using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Models;
using TINWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace TINWeb.Pages.Company
{
    public class IndexModel : PageModel
    {
        private readonly CompanyService _service;

        public List<Models.Tin200> Records { get; set; } = new();
        public List<int> Years { get; set; } = new();
        public int? SelectedYear { get; set; }
        public CompanyService.ResetFyeValuesResult? PreviewSummary { get; set; }

        [TempData]
        public string? StatusMessage { get; set; }

        public IndexModel(CompanyService service)
        {
            _service = service;
        }

        public async Task OnGetAsync(int? year)
        {
            await LoadPageAsync(year);
        }

        public async Task<IActionResult> OnPostPreviewResetFyeValuesAsync(int? year)
        {
            PreviewSummary = await _service.PreviewResetFyeValuesFromSurveyAnswersAsync();
            await LoadPageAsync(year);
            return Page();
        }

        public async Task<IActionResult> OnPostResetFyeValuesAsync(int? year)
        {
            var result = await _service.ResetFyeValuesFromSurveyAnswersAsync();

            if (!result.HasCurrentSurvey)
            {
                StatusMessage = "Reset FYE Values skipped: no current survey is configured.";
                return RedirectToPage(new { year });
            }

            StatusMessage = $"Reset FYE Values complete (Current survey year: {result.CurrentSurveyYear}). Updated {result.UpdatedCompanyCount} of {result.TotalMatchedCompanies} matched company record(s).";
            return RedirectToPage(new { year });
        }

        private async Task LoadPageAsync(int? year)
        {
            Years = await _service.GetAvailableFinancialYearsAsync();
            if (year.HasValue)
            {
                SelectedYear = year.Value;
            }
            else
            {
                // default to all records when no filter is provided
                SelectedYear = null;
            }

            Records = await _service.GetAllCompaniesAsync(SelectedYear);
        }
    }
}

