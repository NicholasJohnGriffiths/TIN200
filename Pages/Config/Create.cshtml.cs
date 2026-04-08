using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Models;

namespace TINWeb.Pages.Config;

[Authorize(Policy = "AdminOnly")]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;

    [BindProperty]
    public AppConfig Record { get; set; } = new() { Id = 1 };

    [TempData]
    public string? StatusMessage { get; set; }

    public CreateModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Record.Id = 1;
        Record.AdminEmail = Record.AdminEmail?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(Record.AdminEmail))
        {
            ModelState.AddModelError("Record.AdminEmail", "Admin email is required.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var exists = await _context.AppConfig.AnyAsync();
        if (exists)
        {
            ModelState.AddModelError(string.Empty, "A config row already exists. Edit the existing row instead.");
            return Page();
        }

        _context.AppConfig.Add(Record);
        await _context.SaveChangesAsync();

        StatusMessage = "Config created successfully.";
        return RedirectToPage("./Index");
    }
}
