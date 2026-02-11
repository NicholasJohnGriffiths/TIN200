using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWorkspaceTemp.Models;
using TINWorkspaceTemp.Services;

namespace TINWorkspaceTemp.Pages.Tin200
{
    public class DeleteModel : PageModel
    {
        private readonly Tin200Service _service;

        [BindProperty]
        public Models.Tin200? Record { get; set; }

        public DeleteModel(Tin200Service service)
        {
            _service = service;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Record = await _service.GetTin200ByIdAsync(id);
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

            await _service.DeleteTin200Async(Record.Id);
            return RedirectToPage("./Index");
        }
    }
}
