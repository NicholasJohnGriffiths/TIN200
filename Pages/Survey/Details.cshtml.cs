using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWorkspaceTemp.Services;

namespace TINWorkspaceTemp.Pages.Survey
{
    public class DetailsModel : PageModel
    {
        private readonly SurveyService _service;

        public Models.Survey Record { get; set; } = new();

        public DetailsModel(SurveyService service)
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
    }
}
