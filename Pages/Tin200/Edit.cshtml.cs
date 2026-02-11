using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWorkspaceTemp.Models;
using TINWorkspaceTemp.Services;

namespace TINWorkspaceTemp.Pages.Tin200
{
    public class EditModel : PageModel
    {
        private readonly Tin200Service _service;

        [BindProperty]
        public Models.Tin200 Record { get; set; } = new();

        public EditModel(Tin200Service service)
        {
            _service = service;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var record = await _service.GetTin200ByIdAsync(id);
            if (record == null)
            {
                return NotFound();
            }

            Record = record;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var exists = await _service.Tin200ExistsAsync(Record.Id);
            if (!exists)
            {
                return NotFound();
            }

            await _service.UpdateTin200Async(Record);
            return RedirectToPage("./Index");
        }
    }
}
