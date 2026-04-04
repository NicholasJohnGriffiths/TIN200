using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.Tasks
{
    public class EditModel : PageModel
    {
        private readonly TaskService _service;

        [BindProperty]
        public TaskItem Record { get; set; } = new();

        public List<SelectListItem> StatusOptions { get; private set; } = new();

        public EditModel(TaskService service)
        {
            _service = service;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            LoadOptions();
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
            LoadOptions();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            await _service.UpdateAsync(Record);
            return RedirectToPage("./Index", new { status = Record.Status?.ToString() ?? "Active" });
        }

        private void LoadOptions()
        {
            StatusOptions = Enum.GetValues<TaskItemStatus>()
                .Select(status => new SelectListItem(status.ToString(), status.ToString()))
                .ToList();
        }
    }
}
