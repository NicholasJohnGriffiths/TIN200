using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.AppUsers;

[Authorize(Policy = "AdminOnly")]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;

    [BindProperty]
    public AppUser Record { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public EditModel(ApplicationDbContext context)
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
        NormalizeRecord();
        ValidateRecord();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var existing = await _context.AppUsers.FirstOrDefaultAsync(u => u.Id == Record.Id);
        if (existing == null)
        {
            return NotFound();
        }

        var normalizedEmail = Record.Email.ToLowerInvariant();
        var normalizedUserName = Record.UserName.ToLowerInvariant();

        var duplicateExists = await _context.AppUsers.AnyAsync(u =>
            u.Id != Record.Id &&
            (u.Email.ToLower() == normalizedEmail || u.UserName.ToLower() == normalizedUserName));

        if (duplicateExists)
        {
            ModelState.AddModelError(string.Empty, "A user with that email or username already exists.");
            return Page();
        }

        existing.Name = Record.Name;
        existing.Email = Record.Email;
        existing.UserName = Record.UserName;
        existing.Password = Record.Password;
        existing.UserType = Record.UserType;
        existing.TransactionsVisible = Record.TransactionsVisible;

        await _context.SaveChangesAsync();

        StatusMessage = "App user updated successfully.";
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

        if (Record.UserType != UserTypes.StandardUser
            && Record.UserType != UserTypes.Admin
            && Record.UserType != UserTypes.StandardUserReadOnly
            && Record.UserType != UserTypes.SurveyEstimations)
        {
            ModelState.AddModelError("Record.UserType", "Invalid user type.");
        }
    }
}
