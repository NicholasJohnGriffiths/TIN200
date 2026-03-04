using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWorkspaceTemp.Services;

namespace TINWorkspaceTemp.Pages.Survey
{
    public class EditModel : PageModel
    {
        private readonly SurveyService _service;

        [BindProperty]
        public Models.Survey Record { get; set; } = new();

        public EditModel(SurveyService service)
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
