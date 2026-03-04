using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWorkspaceTemp.Services;

namespace TINWorkspaceTemp.Pages.CompanySurvey
{
    public class IndexModel : PageModel
    {
        private readonly CompanySurveyService _service;

        public List<CompanySurveyService.CompanySurveyListRow> Records { get; set; } = new();
        public List<int> FinancialYears { get; set; } = new();
        public int? SelectedFinancialYear { get; set; }

        public IndexModel(CompanySurveyService service)
        {
            _service = service;
        }

        public async Task OnGetAsync(int? financialYear)
        {
            FinancialYears = await _service.GetAvailableFinancialYearsAsync();

            SelectedFinancialYear = financialYear ?? await _service.GetCurrentSurveyFinancialYearAsync();

            Records = await _service.GetListRowsAsync(SelectedFinancialYear);
        }
    }
}
