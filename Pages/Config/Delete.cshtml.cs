using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Models;

namespace TINWeb.Pages.Config;

[Authorize(Policy = "AdminOnly")]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _context;

    [BindProperty]
    public AppConfig Record { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public DeleteModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var record = await _context.AppConfig.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (record == null)
        {
            return NotFound();
        }

        Record = record;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var record = await _context.AppConfig.FirstOrDefaultAsync(c => c.Id == Record.Id);
        if (record == null)
        {
            return NotFound();
        }

        _context.AppConfig.Remove(record);
        await _context.SaveChangesAsync();

        StatusMessage = "Config deleted successfully.";
        return RedirectToPage("./Index");
    }
}
