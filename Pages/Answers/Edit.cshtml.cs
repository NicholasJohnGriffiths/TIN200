using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Services;

namespace TINWeb.Pages.Answers
{
    public class EditModel : PageModel
    {
        private readonly AnswerService _answerService;

        [BindProperty]
        public AnswerService.AnswerEditInput Input { get; set; } = new();

        public AnswerService.AnswerEditRow? Details { get; set; }

        public EditModel(AnswerService answerService)
        {
            _answerService = answerService;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var answer = await _answerService.GetAnswerForEditAsync(id);
            if (answer == null)
            {
                return NotFound();
            }

            Details = answer;
            Input = new AnswerService.AnswerEditInput
            {
                Id = answer.Id,
                CompanySurveyId = answer.CompanySurveyId,
                AnswerText = answer.AnswerText,
                AnswerNumber = answer.AnswerNumber,
                AnswerCurrency = answer.AnswerCurrency
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                Details = await _answerService.GetAnswerForEditAsync(Input.Id);
                return Page();
            }

            var updated = await _answerService.UpdateAnswerAsync(Input);
            if (!updated)
            {
                return NotFound();
            }

            return RedirectToPage("./Index", new { companySurveyId = Input.CompanySurveyId });
        }
    }
}
