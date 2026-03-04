using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWorkspaceTemp.Services;

namespace TINWorkspaceTemp.Pages.Survey
{
    public class DeleteModel : PageModel
    {
        private readonly SurveyService _service;

        [BindProperty]
        public Models.Survey Record { get; set; } = new();

        public DeleteModel(SurveyService service)
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
