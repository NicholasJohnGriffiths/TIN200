using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.Company
{
    public class EditModel : PageModel
    {
        private readonly CompanyService _service;

        [BindProperty]
        public Models.Tin200 Record { get; set; } = new();

        public EditModel(CompanyService service)
        {
            _service = service;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var record = await _service.GetCompanyByIdAsync(id);
            if (record == null)
            {
                return NotFound();
            }

            Record = record;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var exists = await _service.CompanyExistsAsync(Record.Id);
            if (!exists)
            {
                return NotFound();
            }

            // Auto-generate ExternalId if blank
            if (string.IsNullOrWhiteSpace(Record.ExternalId))
            {
                var highestId = await _service.GetHighestNumericExternalIdAsync();
                Record.ExternalId = (highestId + 1).ToString();
            }

            await _service.UpdateCompanyAsync(Record);
            return RedirectToPage("./Index");
        }
    }
}

