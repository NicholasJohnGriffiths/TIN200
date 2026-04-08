using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Models;

namespace TINWeb.Pages.AppUsers;

[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public IList<AppUser> Records { get; set; } = new List<AppUser>();

    [TempData]
    public string? StatusMessage { get; set; }

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task OnGetAsync()
    {
        Records = await _context.AppUsers
            .AsNoTracking()
            .OrderBy(u => u.UserType)
            .ThenBy(u => u.Name)
            .ThenBy(u => u.UserName)
            .ToListAsync();
    }

    public string GetUserTypeLabel(int userType) => userType == 1 ? "Admin" : $"UserType {userType}";
}
