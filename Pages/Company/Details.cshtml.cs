using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.Company
{
    public class DetailsModel : PageModel
    {
        private readonly CompanyService _service;

        public Models.Tin200? Record { get; set; }

        public DetailsModel(CompanyService service)
        {
            _service = service;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Record = await _service.GetCompanyByIdAsync(id);
            if (Record == null)
            {
                return NotFound();
            }

            return Page();
        }
    }
}

