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
            ReturnTo = returnTo;
            Record.TinStatus = isTest ? (int)TinStatus.TinTest : (int)TinStatus.Tin200;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!TinStatusHelper.IsValidSelection(Record.TinStatus))
            {
                ModelState.AddModelError("Record.TinStatus", "TIN Status must be Blank, TIN200, TIN200Potential, TIN1000, or TINTest.");
            }

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

