using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.Testing.TestCompanies
{
    public class IndexModel : PageModel
    {
        private readonly CompanyService _service;

        public IndexModel(CompanyService service)
        {
            _service = service;
        }

        public List<Tin200> Records { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string Search { get; set; } = string.Empty;

        [TempData]
        public string? StatusMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            Records = await _service.GetTestCompaniesAsync(Search);
        }

        public async Task<IActionResult> OnPostDuplicateAsync(int id)
        {
            try
            {
                var duplicatedCompany = await _service.DuplicateTestCompanyAsync(id);
                StatusMessage = $"Test company duplicated: {duplicatedCompany.CompanyName} (External ID: {duplicatedCompany.ExternalId}).";
            }
            catch (Exception ex)
            {
                var rootErrorMessage = ex.GetBaseException().Message;
                ErrorMessage = $"Failed to duplicate test company. {rootErrorMessage}";
            }

            return RedirectToPage(new { search = Search });
        }
    }
}
