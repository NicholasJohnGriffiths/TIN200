using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.Survey
{
    public class IndexModel : PageModel
    {
        private readonly SurveyService _service;

        public List<Models.Survey> Records { get; set; } = new();

        public IndexModel(SurveyService service)
        {
            _service = service;
        }

        public async Task OnGetAsync()
        {
            Records = await _service.GetAllAsync();
        }
    }
}
