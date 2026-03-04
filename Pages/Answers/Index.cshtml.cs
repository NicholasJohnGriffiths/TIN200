using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWorkspaceTemp.Services;

namespace TINWorkspaceTemp.Pages.Answers
{
    public class IndexModel : PageModel
    {
        private readonly AnswerService _answerService;

        public List<AnswerService.AnswerListRow> Rows { get; set; } = new();
        public List<int> FinancialYears { get; set; } = new();
        public List<AnswerService.CompanySurveyOption> CompanySurveyOptions { get; set; } = new();
        public int? SelectedFinancialYear { get; set; }
        public int? SelectedCompanySurveyId { get; set; }

        public IndexModel(AnswerService answerService)
        {
            _answerService = answerService;
        }

        public async Task OnGetAsync(int? financialYear, int? companySurveyId)
        {
            FinancialYears = await _answerService.GetAvailableFinancialYearsAsync();
            SelectedFinancialYear = financialYear;
            CompanySurveyOptions = await _answerService.GetCompanySurveyOptionsAsync(financialYear);
            SelectedCompanySurveyId = companySurveyId;

            if (!companySurveyId.HasValue)
            {
                Rows = new List<AnswerService.AnswerListRow>();
                return;
            }

            Rows = await _answerService.GetAnswerRowsAsync(companySurveyId.Value);
        }
    }
}
