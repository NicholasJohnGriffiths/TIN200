using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Services;

namespace TINWeb.Pages.CompanySurvey
{
    public class EditModel : PageModel
    {
        private readonly CompanySurveyService _service;
        private readonly ApplicationDbContext _context;

        [BindProperty]
        public Models.CompanySurvey Record { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int? FinancialYear { get; set; }

        public string? CompanyName { get; set; }

        public EditModel(CompanySurveyService service, ApplicationDbContext context)
        {
            _service = service;
            _context = context;
        }

        public async Task<IActionResult> OnGetAsync(int? id, int? financialYear)
        {
            FinancialYear = financialYear;

            if (id == null)
            {
                return NotFound();
            }

            var record = await _service.GetByIdAsync(id.Value);
            if (record == null)
            {
                return NotFound();
            }

            Record = record;
            Record.Locked ??= false;
            Record.Estimate ??= false;
            CompanyName = await _context.Tin200
                .Where(c => c.Id == record.CompanyId)
                .Select(c => c.CompanyName)
                .FirstOrDefaultAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (!await _service.ExistsAsync(Record.Id))
            {
                return NotFound();
            }

            Record.Locked ??= false;
            Record.Estimate ??= false;
            await _service.UpdateAsync(Record);
            return RedirectToPage("./Index", new { financialYear = FinancialYear });
        }
    }
}
