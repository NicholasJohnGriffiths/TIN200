using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWorkspaceTemp.Services;

namespace TINWorkspaceTemp.Pages.Answers
{
    public class IndexModel : PageModel
    {
        private readonly AnswerService _answerService;

        public List<AnswerService.AnswerListRow> Rows { get; set; } = new();
        public List<int> FinancialYears { get; set; } = new();
        public int? SelectedFinancialYear { get; set; }

        public IndexModel(AnswerService answerService)
        {
            _answerService = answerService;
        }

        public async Task OnGetAsync(int? financialYear)
        {
            FinancialYears = await _answerService.GetAvailableFinancialYearsAsync();
            SelectedFinancialYear = financialYear;
            Rows = await _answerService.GetAnswerRowsAsync(financialYear);
        }
    }
}
