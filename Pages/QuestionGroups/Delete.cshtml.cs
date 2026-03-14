using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.QuestionGroups
{
    public class DeleteModel : PageModel
    {
        private readonly QuestionGroupService _service;

        [BindProperty]
        public QuestionGroup Record { get; set; } = new();

        public DeleteModel(QuestionGroupService service)
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
