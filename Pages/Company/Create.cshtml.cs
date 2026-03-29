using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.Company
{
    public class CreateModel : PageModel
    {
        private readonly CompanyService _service;

        [BindProperty]
        public Models.Tin200 Record { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? ReturnTo { get; set; }

        public CreateModel(CompanyService service)
        {
            _service = service;
        }

        public void OnGet(bool isTest = false, string? returnTo = null)
        {
            Record.Test = isTest;
            ReturnTo = returnTo;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            await _service.CreateCompanyAsync(Record);
            if (string.Equals(ReturnTo, "testing", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToPage("/Testing/TestCompanies/Index");
            }

            return RedirectToPage("./Index");
        }
    }
}

