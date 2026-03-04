using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using TINWorkspaceTemp.Models;
using TINWorkspaceTemp.Services;

namespace TINWorkspaceTemp.Pages.Questions
{
    public class IndexModel : PageModel
    {
        private readonly QuestionService _service;

        public List<Question> Records { get; set; } = new();

        public IndexModel(QuestionService service)
        {
            _service = service;
        }

        public async Task OnGetAsync()
        {
            Records = await _service.GetAllAsync();
        }

        public async Task<IActionResult> OnPostMoveUpAsync(int id)
        {
            await _service.MoveUpAsync(id);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostMoveDownAsync(int id)
        {
            await _service.MoveDownAsync(id);
            return RedirectToPage();
        }
    }
}
