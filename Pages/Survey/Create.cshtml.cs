using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Services;

namespace TINWeb.Pages.Survey
{
    public class CreateModel : PageModel
    {
        private readonly SurveyService _service;

        [BindProperty]
        public Models.Survey Record { get; set; } = new();

        public CreateModel(SurveyService service)
        {
            _service = service;
        }

        public void OnGet()
        {
            Record.Title = "Survey Answers";
            Record.Description = "Please complete the survey answers for the current survey year.";
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            await _service.CreateAsync(Record);
            return RedirectToPage("./Index");
        }
    }
}
