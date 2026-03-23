using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Services;

namespace TINWeb.Pages.Company
{
    public class SurveyHistoryModel : PageModel
    {
        private readonly AnswerService _answerService;

        public AnswerService.CompanySurveyHistoryResult? History { get; private set; }

        public SurveyHistoryModel(AnswerService answerService)
        {
            _answerService = answerService;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            History = await _answerService.GetCompanySurveyHistoryAsync(id);
            if (History == null)
            {
                return NotFound();
            }

            return Page();
        }
    }
}