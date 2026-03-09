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
                AnswerType = answer.AnswerType,
                ChoiceOptions = answer.ChoiceOptions,
                SelectedChoices = ParseMultiChoiceAnswer(answer.AnswerText),
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

                if (Details != null)
                {
                    Input.AnswerType = Details.AnswerType;
                    Input.ChoiceOptions = Details.ChoiceOptions;
                    Input.SelectedChoices = ParseMultiChoiceAnswer(Input.AnswerText);
                }

                return Page();
            }

            Details = await _answerService.GetAnswerForEditAsync(Input.Id);
            if (Details != null)
            {
                Input.AnswerType = Details.AnswerType;
                Input.ChoiceOptions = Details.ChoiceOptions;
            }

            var updated = await _answerService.UpdateAnswerAsync(Input);
            if (!updated)
            {
                return NotFound();
            }

            return RedirectToPage("./Index", new { companySurveyId = Input.CompanySurveyId });
        }

        private static List<string> ParseMultiChoiceAnswer(string? answerText)
        {
            if (string.IsNullOrWhiteSpace(answerText))
            {
                return new List<string>();
            }

            return answerText
                .Split(new[] { ';', ',', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct()
                .ToList();
        }
    }
}
