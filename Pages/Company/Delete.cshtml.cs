using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.Company
{
    public class DeleteModel : PageModel
    {
        private readonly CompanyService _service;

        [BindProperty]
        public Models.Tin200? Record { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ReturnTo { get; set; }

        public DeleteModel(CompanyService service)
        {
            _service = service;
        }

        public async Task<IActionResult> OnGetAsync(int id, string? returnTo)
        {
            ReturnTo = returnTo;
            Record = await _service.GetCompanyByIdAsync(id);
            if (Record == null)
            {
                return NotFound();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (Record?.Id == null)
            {
                return NotFound();
            }

            await _service.DeleteCompanyAsync(Record.Id);

            if (string.Equals(ReturnTo, "testing", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToPage("/Testing/TestCompanies/Index");
            }

            return RedirectToPage("./Index");
        }
    }
}

