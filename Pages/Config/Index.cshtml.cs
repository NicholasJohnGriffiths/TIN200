using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Models;

namespace TINWeb.Pages.Config;

[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public IList<AppConfig> Records { get; set; } = new List<AppConfig>();

    [TempData]
    public string? StatusMessage { get; set; }

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task OnGetAsync()
    {
        Records = await _context.AppConfig
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .ToListAsync();
    }
}
