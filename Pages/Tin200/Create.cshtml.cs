using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWorkspaceTemp.Models;
using TINWorkspaceTemp.Services;

namespace TINWorkspaceTemp.Pages.Tin200
{
    public class CreateModel : PageModel
    {
        private readonly Tin200Service _service;

        [BindProperty]
        public Models.Tin200 Record { get; set; } = new();

        public CreateModel(Tin200Service service)
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

            await _service.CreateTin200Async(Record);
            return RedirectToPage("./Index");
        }
    }
}
