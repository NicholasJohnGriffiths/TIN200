using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.QuestionGroups
{
    public class CreateModel : PageModel
    {
        private readonly QuestionGroupService _service;

        [BindProperty]
        public QuestionGroup Record { get; set; } = new();

        [BindProperty]
        public IFormFile? Image1File { get; set; }

        [BindProperty]
        public IFormFile? Image2File { get; set; }

        [BindProperty]
        public IFormFile? Image3File { get; set; }

        public CreateModel(QuestionGroupService service)
        {
            _service = service;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            await _service.CreateAsync(Record, Image1File, Image2File, Image3File);
            return RedirectToPage("./Index");
        }
    }
}
