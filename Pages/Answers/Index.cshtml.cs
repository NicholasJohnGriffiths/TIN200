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
        public int QuestionCount { get; set; }
        public int AnsweredCount { get; set; }

        public IndexModel(AnswerService answerService)
        {
            _answerService = answerService;
        }

        public async Task OnGetAsync(int? financialYear, int? companySurveyId)
        {
            QuestionCount = await _answerService.GetQuestionCountAsync();
            FinancialYears = await _answerService.GetAvailableFinancialYearsAsync();
            SelectedFinancialYear = financialYear ?? await _answerService.GetCurrentSurveyFinancialYearAsync();

            if (!SelectedFinancialYear.HasValue && FinancialYears.Any())
            {
                SelectedFinancialYear = FinancialYears.First();
            }

            CompanySurveyOptions = await _answerService.GetCompanySurveyOptionsAsync(SelectedFinancialYear);
            SelectedCompanySurveyId = companySurveyId;

            if (!SelectedCompanySurveyId.HasValue)
            {
                SelectedCompanySurveyId = CompanySurveyOptions
                    .OrderByDescending(x => x.AnswerCount)
                    .ThenBy(x => x.CompanyName)
                    .Select(x => (int?)x.CompanySurveyId)
                    .FirstOrDefault();
            }

            if (!SelectedCompanySurveyId.HasValue)
            {
                Rows = new List<AnswerService.AnswerListRow>();
                AnsweredCount = 0;
                return;
            }

            Rows = await _answerService.GetAnswerRowsAsync(SelectedCompanySurveyId.Value);
            AnsweredCount = Rows.Count(x => x.Id > 0);
        }
    }
}
