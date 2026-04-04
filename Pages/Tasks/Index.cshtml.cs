using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.Tasks
{
    public class IndexModel : PageModel
    {
        private readonly TaskService _service;

        public List<TaskItem> Records { get; set; } = new();

        [BindProperty(SupportsGet = true, Name = "status")]
        public string? StatusFilter { get; set; }

        [TempData]
        public string? StatusMessage { get; set; }

        public IndexModel(TaskService service)
        {
            _service = service;
        }

        public string CurrentFilterLabel => GetSelectedStatus()?.ToString() ?? "All";

        public bool IsSelectedStatus(TaskItemStatus status) => GetSelectedStatus() == status;

        public async Task OnGetAsync()
        {
            Records = await _service.GetAllAsync(GetSelectedStatus());
        }

        private TaskItemStatus? GetSelectedStatus()
        {
            if (string.IsNullOrWhiteSpace(StatusFilter))
            {
                return TaskItemStatus.Active;
            }

            if (string.Equals(StatusFilter, "all", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return Enum.TryParse<TaskItemStatus>(StatusFilter, true, out var status)
                ? status
                : TaskItemStatus.Active;
        }
    }
}
