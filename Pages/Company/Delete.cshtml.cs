using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWorkspaceTemp.Models;
using TINWorkspaceTemp.Services;

namespace TINWorkspaceTemp.Pages.Company
{
    public class DeleteModel : PageModel
    {
        private readonly CompanyService _service;

        [BindProperty]
        public Models.Tin200? Record { get; set; }

        public DeleteModel(CompanyService service)
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

        public async Task<IActionResult> OnPostAsync()
        {
            if (Record?.Id == null)
            {
                return NotFound();
            }

            await _service.DeleteCompanyAsync(Record.Id);
            return RedirectToPage("./Index");
        }
    }
}

