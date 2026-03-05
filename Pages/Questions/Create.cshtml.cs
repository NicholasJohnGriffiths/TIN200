using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.Questions
{
    public class CreateModel : PageModel
    {
        private readonly QuestionService _service;

        [BindProperty]
        public Question Record { get; set; } = new();

        public List<string> AnswerTypeOptions { get; } = Enum.GetNames<QuestionAnswerType>().ToList();

        public CreateModel(QuestionService service)
        {
            _service = service;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!Enum.TryParse<QuestionAnswerType>(Record.AnswerType, out _))
            {
                ModelState.AddModelError("Record.AnswerType", "Answer Type must be one of: Text, Currency, Number, SingleChoice, Multichoice.");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            await _service.CreateAsync(Record);
            return RedirectToPage("./Index");
        }
    }
}
