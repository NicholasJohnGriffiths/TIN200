using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWorkspaceTemp.Models;
using TINWorkspaceTemp.Services;

namespace TINWorkspaceTemp.Pages.Survey
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
