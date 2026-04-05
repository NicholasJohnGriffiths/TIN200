using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.Tasks
{
    public class DeleteModel : PageModel
    {
        private readonly TaskService _service;

        [BindProperty]
        public TaskItem Record { get; set; } = new();

        [TempData]
        public string? StatusMessage { get; set; }

        public DeleteModel(TaskService service)
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
            await _service.ArchiveAsync(id);
            StatusMessage = "Task archived.";
            return RedirectToPage("./Index", new { status = "Archived" });
        }
    }
}
