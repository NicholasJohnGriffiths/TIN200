using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWorkspaceTemp.Models;
using TINWorkspaceTemp.Services;

namespace TINWorkspaceTemp.Pages.Company
{
    public class IndexModel : PageModel
    {
        private readonly CompanyService _service;

        public List<Models.Tin200> Records { get; set; } = new();
        public List<int> Years { get; set; } = new();
        public int? SelectedYear { get; set; }

        public IndexModel(CompanyService service)
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
                // default to all records when no filter is provided
                SelectedYear = null;
            }

            Records = await _service.GetAllCompaniesAsync(SelectedYear);
        }
    }
}

