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

        [BindProperty(SupportsGet = true)]
        public int? LastTin200Year { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ReturnTo { get; set; }

        public async Task<IActionResult> OnGetAsync(int id, int? lastTin200Year, string? returnTo)
        {
            LastTin200Year = lastTin200Year;
            ReturnTo = returnTo;
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

            if (string.Equals(ReturnTo, "testing", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToPage("/Testing/TestCompanies/Index");
            }

            return RedirectToPage("./Index", new { lastTin200Year = LastTin200Year, focusId = Record.Id });
        }
    }
}

