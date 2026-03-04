using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWorkspaceTemp.Services;

namespace TINWorkspaceTemp.Pages.CompanySurvey
{
    public class EditModel : PageModel
    {
        private readonly CompanySurveyService _service;

        [BindProperty]
        public Models.CompanySurvey Record { get; set; } = new();

        public EditModel(CompanySurveyService service)
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

            await _service.UpdateAsync(Record);
            return RedirectToPage("./Index");
        }
    }
}
