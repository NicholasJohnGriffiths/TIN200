using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWorkspaceTemp.Services;

namespace TINWorkspaceTemp.Pages.CompanySurvey
{
    public class IndexModel : PageModel
    {
        private readonly CompanySurveyService _service;

        public List<CompanySurveyService.CompanySurveyListRow> Records { get; set; } = new();

        public IndexModel(CompanySurveyService service)
        {
            _service = service;
        }

        public async Task OnGetAsync()
        {
            Records = await _service.GetListRowsAsync();
        }
    }
}
