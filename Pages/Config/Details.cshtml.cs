using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Models;

namespace TINWeb.Pages.Config;

[Authorize(Policy = "AdminOnly")]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public AppConfig Record { get; set; } = new();

    public DetailsModel(ApplicationDbContext context)
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
}
