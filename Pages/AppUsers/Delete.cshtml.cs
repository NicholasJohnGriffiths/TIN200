using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Models;

namespace TINWeb.Pages.AppUsers;

[Authorize(Policy = "AdminOnly")]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _context;

    [BindProperty]
    public AppUser Record { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public DeleteModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var record = await _context.AppUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (record == null)
        {
            return NotFound();
        }

        Record = record;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var record = await _context.AppUsers.FirstOrDefaultAsync(u => u.Id == Record.Id);
        if (record == null)
        {
            return NotFound();
        }

        _context.AppUsers.Remove(record);
        await _context.SaveChangesAsync();

        StatusMessage = "App user deleted successfully.";
        return RedirectToPage("./Index");
    }
}
