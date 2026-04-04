using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.Tasks
{
    public class CreateModel : PageModel
    {
        private readonly TaskService _service;

        [BindProperty]
        public TaskItem Record { get; set; } = new()
        {
            CreatedDatetime = DateTime.Now,
            Status = TaskItemStatus.Active
        };

        public List<SelectListItem> StatusOptions { get; private set; } = new();

        public CreateModel(TaskService service)
        {
            _service = service;
        }

        public void OnGet()
        {
            LoadOptions();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            LoadOptions();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            await _service.CreateAsync(Record);
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
