using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.CompanySurvey
{
    public class LockingModel : PageModel
    {
        private readonly CompanySurveyService _service;

        public List<CompanySurveyService.CompanySurveyListRow> Records { get; set; } = new();
        public List<int> FinancialYears { get; set; } = new();
        public int? SelectedFinancialYear { get; set; }
        [BindProperty(SupportsGet = true)]
        public int SelectedTinStatus { get; set; } = (int)TinStatus.Tin200;
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
            SelectedTinStatus = NormalizeTinStatusFilter(SelectedTinStatus);
            FinancialYears = await _service.GetAvailableFinancialYearsAsync();
            SelectedFinancialYear = financialYear ?? await _service.GetCurrentSurveyFinancialYearAsync();

            Records = await _service.GetListRowsAsync(SelectedFinancialYear);
            Records = Records.Where(r => r.TinStatus == SelectedTinStatus).ToList();
            TotalCompaniesWithAnswers = Records.Count(r => r.AnswerCount > 0);
            TotalLockedCompanies = Records.Count(r => r.Locked);
        }

        public async Task<IActionResult> OnPostLockAsync(int? financialYear, int selectedTinStatus)
        {
            var updatedCount = await _service.SetLockedAsync(SelectedCompanySurveyIds, true);
            StatusMessage = $"Locked {updatedCount} company survey record(s).";
            return RedirectToPage(new { financialYear, selectedTinStatus });
        }

        public async Task<IActionResult> OnPostUnlockAsync(int? financialYear, int selectedTinStatus)
        {
            var updatedCount = await _service.SetLockedAsync(SelectedCompanySurveyIds, false);
            StatusMessage = $"Unlocked {updatedCount} company survey record(s).";
            return RedirectToPage(new { financialYear, selectedTinStatus });
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