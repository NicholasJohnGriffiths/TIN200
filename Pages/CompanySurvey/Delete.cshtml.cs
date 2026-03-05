using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Services;

namespace TINWeb.Pages.CompanySurvey
{
    public class DeleteModel : PageModel
    {
        private readonly CompanySurveyService _service;

        [BindProperty]
        public Models.CompanySurvey Record { get; set; } = new();

        public DeleteModel(CompanySurveyService service)
        {
            _service = service;
        }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
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
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            await _service.DeleteAsync(id.Value);
            return RedirectToPage("./Index");
        }
    }
}
