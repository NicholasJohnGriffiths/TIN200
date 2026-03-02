using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWorkspaceTemp.Models;
using TINWorkspaceTemp.Services;

namespace TINWorkspaceTemp.Pages.Tin200
{
    public class IndexModel : PageModel
    {
        private readonly Tin200Service _service;

        public List<Models.Tin200> Records { get; set; } = new();
        public List<int> Years { get; set; } = new();
        public int? SelectedYear { get; set; }

        public IndexModel(Tin200Service service)
        {
            _service = service;
        }

        public async Task OnGetAsync(int? year)
        {
            Years = await _service.GetAvailableFinancialYearsAsync();
            if (year.HasValue)
            {
                SelectedYear = year.Value;
            }
            else
            {
                // default to latest available year when not provided
                SelectedYear = Years.Any() ? Years.First() : null;
            }

            Records = await _service.GetAllTin200Async(SelectedYear);
        }
    }
}
