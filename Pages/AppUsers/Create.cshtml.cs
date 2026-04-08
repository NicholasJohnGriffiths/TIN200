using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Models;

namespace TINWeb.Pages.AppUsers;

[Authorize(Policy = "AdminOnly")]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;

    [BindProperty]
    public AppUser Record { get; set; } = new() { UserType = 0 };

    [TempData]
    public string? StatusMessage { get; set; }

    public CreateModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        NormalizeRecord();
        ValidateRecord();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var normalizedEmail = Record.Email.ToLowerInvariant();
        var normalizedUserName = Record.UserName.ToLowerInvariant();

        var exists = await _context.AppUsers.AnyAsync(u =>
            u.Email.ToLower() == normalizedEmail || u.UserName.ToLower() == normalizedUserName);

        if (exists)
        {
            ModelState.AddModelError(string.Empty, "A user with that email or username already exists.");
            return Page();
        }

        _context.AppUsers.Add(Record);
        await _context.SaveChangesAsync();

        StatusMessage = "App user created successfully.";
        return RedirectToPage("./Index");
    }

    private void NormalizeRecord()
    {
        Record.Name = Record.Name?.Trim() ?? string.Empty;
        Record.Email = Record.Email?.Trim() ?? string.Empty;
        Record.UserName = Record.UserName?.Trim() ?? string.Empty;
        Record.Password = Record.Password?.Trim() ?? string.Empty;
    }

    private void ValidateRecord()
    {
        if (string.IsNullOrWhiteSpace(Record.Name))
        {
            ModelState.AddModelError("Record.Name", "Name is required.");
        }

        if (string.IsNullOrWhiteSpace(Record.Email))
        {
            ModelState.AddModelError("Record.Email", "Email is required.");
        }

        if (string.IsNullOrWhiteSpace(Record.UserName))
        {
            ModelState.AddModelError("Record.UserName", "Username is required.");
        }

        if (string.IsNullOrWhiteSpace(Record.Password))
        {
            ModelState.AddModelError("Record.Password", "Password is required.");
        }
    }
}
