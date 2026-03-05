using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.Questions
{
    public class DeleteModel : PageModel
    {
        private readonly QuestionService _service;

        [BindProperty]
        public Question Record { get; set; } = new();

        public DeleteModel(QuestionService service)
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

        public async Task<IActionResult> OnPostAsync(int id)
        {
            await _service.DeleteAsync(id);
            return RedirectToPage("./Index");
        }
    }
}
