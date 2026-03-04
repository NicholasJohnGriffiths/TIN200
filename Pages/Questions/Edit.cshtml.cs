using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWorkspaceTemp.Models;
using TINWorkspaceTemp.Services;

namespace TINWorkspaceTemp.Pages.Questions
{
    public class EditModel : PageModel
    {
        private readonly QuestionService _service;

        [BindProperty]
        public Question Record { get; set; } = new();

        public List<string> AnswerTypeOptions { get; } = Enum.GetNames<QuestionAnswerType>().ToList();

        public EditModel(QuestionService service)
        {
            _service = service;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var record = await _service.GetByIdAsync(id);
            if (record == null)
            {
                return NotFound();
            }

            Record = record;
            return Page();
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

            if (!await _service.ExistsAsync(Record.Id))
            {
                return NotFound();
            }

            await _service.UpdateAsync(Record);
            return RedirectToPage("./Index");
        }
    }
}
