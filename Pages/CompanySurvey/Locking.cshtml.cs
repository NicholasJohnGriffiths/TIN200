using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Services;

namespace TINWeb.Pages.CompanySurvey
{
    public class LockingModel : PageModel
    {
        private readonly CompanySurveyService _service;

        public List<CompanySurveyService.CompanySurveyListRow> Records { get; set; } = new();
        public List<int> FinancialYears { get; set; } = new();
        public int? SelectedFinancialYear { get; set; }
        public int TotalCompaniesWithAnswers { get; set; }
        public int TotalLockedCompanies { get; set; }

        [BindProperty]
        public List<int> SelectedCompanySurveyIds { get; set; } = new();

        [TempData]
        public string? StatusMessage { get; set; }

        public LockingModel(CompanySurveyService service)
        {
            _service = service;
        }

        public async Task OnGetAsync(int? financialYear)
        {
            FinancialYears = await _service.GetAvailableFinancialYearsAsync();
            SelectedFinancialYear = financialYear ?? await _service.GetCurrentSurveyFinancialYearAsync();

            Records = await _service.GetListRowsAsync(SelectedFinancialYear);
            TotalCompaniesWithAnswers = Records.Count(r => r.AnswerCount > 0);
            TotalLockedCompanies = Records.Count(r => r.Locked);
        }

        public async Task<IActionResult> OnPostLockAsync(int? financialYear)
        {
            var updatedCount = await _service.SetLockedAsync(SelectedCompanySurveyIds, true);
            StatusMessage = $"Locked {updatedCount} company survey record(s).";
            return RedirectToPage(new { financialYear });
        }

        public async Task<IActionResult> OnPostUnlockAsync(int? financialYear)
        {
            var updatedCount = await _service.SetLockedAsync(SelectedCompanySurveyIds, false);
            StatusMessage = $"Unlocked {updatedCount} company survey record(s).";
            return RedirectToPage(new { financialYear });
        }
    }
}