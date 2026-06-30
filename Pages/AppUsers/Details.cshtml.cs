using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.AppUsers;

[Authorize(Policy = "AdminOnly")]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public AppUser Record { get; set; } = new();

    public DetailsModel(ApplicationDbContext context)
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

    public string GetUserTypeLabel(int userType) => UserTypes.GetLabel(userType);
}
