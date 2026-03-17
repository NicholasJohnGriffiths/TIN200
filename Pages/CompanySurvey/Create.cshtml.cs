using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Services;

namespace TINWeb.Pages.CompanySurvey
{
    public class CreateModel : PageModel
    {
        private readonly CompanySurveyService _service;

        [BindProperty]
        public Models.CompanySurvey Record { get; set; } = new();

        public CreateModel(CompanySurveyService service)
        {
            _service = service;
        }

        public void OnGet()
        {
            Record.Locked = false;
            Record.Estimate = false;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            Record.Locked ??= false;
            Record.Estimate ??= false;
            await _service.CreateAsync(Record);
            return RedirectToPage("./Index");
        }
    }
}
