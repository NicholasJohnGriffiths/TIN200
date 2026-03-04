using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWorkspaceTemp.Models;
using TINWorkspaceTemp.Services;

namespace TINWorkspaceTemp.Pages.Tin200
{
    public class CreateModel : PageModel
    {
        private readonly CompanyService _service;

        [BindProperty]
        public Models.Tin200 Record { get; set; } = new();

        public CreateModel(CompanyService service)
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

            await _service.CreateCompanyAsync(Record);
            return RedirectToPage("./Index");
        }
    }
}
