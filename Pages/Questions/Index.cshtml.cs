using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.Questions
{
    public class IndexModel : PageModel
    {
        private readonly QuestionService _service;

        public List<Question> Records { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int? FocusId { get; set; }

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

        public async Task<IActionResult> OnPostReorderAsync()
        {
            await _service.NormalizeOrderNumbersAsync();
            return RedirectToPage();
        }
    }
}
