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
        public HashSet<string> UnavailableUniqueSelectionValues { get; set; } = new(StringComparer.Ordinal);
        public bool UniqueSelectionEnforced { get; set; }

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

            UniqueSelectionEnforced = await _answerService.IsUniqueSelectionEnforcedForAnswerAsync(answer.Id);
            await LoadUnavailableUniqueSelectionValuesAsync(answer.Id, Input.AnswerText);

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
                    UniqueSelectionEnforced = await _answerService.IsUniqueSelectionEnforcedForAnswerAsync(Details.Id);
                    await LoadUnavailableUniqueSelectionValuesAsync(Details.Id, Input.AnswerText);
                }

                return Page();
            }

            Details = await _answerService.GetAnswerForEditAsync(Input.Id);
            if (Details != null)
            {
                Input.AnswerType = Details.AnswerType;
                Input.ChoiceOptions = Details.ChoiceOptions;
                UniqueSelectionEnforced = await _answerService.IsUniqueSelectionEnforcedForAnswerAsync(Details.Id);

                if (IsSingleChoiceType(Input.AnswerType))
                {
                    Input.AnswerText = NormalizeSingleChoiceSelection(Input.AnswerText, Input.ChoiceOptions);
                    var hasDuplicate = await _answerService.HasDuplicateUniqueSelectionAsync(Input.Id, Input.AnswerText);
                    if (hasDuplicate)
                    {
                        ModelState.AddModelError(string.Empty, "This value is already used in another answer in the same group. Each value can only be used once.");
                        await LoadUnavailableUniqueSelectionValuesAsync(Details.Id, Input.AnswerText);
                        return Page();
                    }
                }

                await LoadUnavailableUniqueSelectionValuesAsync(Details.Id, Input.AnswerText);
            }

            var updated = await _answerService.UpdateAnswerAsync(Input);
            if (!updated)
            {
                return NotFound();
            }

            return RedirectToPage("./Index", new { companySurveyId = Input.CompanySurveyId });
        }

        private static bool IsSingleChoiceType(string? answerType)
        {
            var normalized = (answerType ?? string.Empty).Trim();
            return normalized.Equals("SingleChoice", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Radio", StringComparison.OrdinalIgnoreCase);
        }

        private static string? NormalizeSingleChoiceSelection(string? candidate, IReadOnlyCollection<string>? options)
        {
            if (string.IsNullOrWhiteSpace(candidate) || options == null)
            {
                return null;
            }

            var normalizedCandidate = candidate.Trim();
            return options.Contains(normalizedCandidate, StringComparer.Ordinal)
                ? normalizedCandidate
                : null;
        }

        private async Task LoadUnavailableUniqueSelectionValuesAsync(int answerId, string? currentValue)
        {
            UnavailableUniqueSelectionValues = await _answerService.GetUnavailableUniqueSelectionValuesAsync(answerId);

            var normalizedCurrentValue = (currentValue ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(normalizedCurrentValue))
            {
                UnavailableUniqueSelectionValues.Remove(normalizedCurrentValue);
            }
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
