using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Models;

namespace TINWeb.Pages.EmailContent;

[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public List<Models.EmailContent> Records { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task OnGetAsync()
    {
        Records = await _context.EmailContent
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var record = await _context.EmailContent.FirstOrDefaultAsync(x => x.Id == id);
        if (record == null)
        {
            StatusMessage = "Email content record not found.";
            return RedirectToPage();
        }

        _context.EmailContent.Remove(record);
        await _context.SaveChangesAsync();

        StatusMessage = $"Deleted email content: {record.Name}.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDuplicateAsync(int id)
    {
        var source = await _context.EmailContent
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (source == null)
        {
            StatusMessage = "Email content record not found.";
            return RedirectToPage();
        }

        var baseName = source.Name.Trim();
        var candidateName = baseName + " (Copy)";
        var copyIndex = 2;

        while (await _context.EmailContent.AsNoTracking().AnyAsync(x => x.Name == candidateName))
        {
            candidateName = $"{baseName} (Copy {copyIndex})";
            copyIndex++;
        }

        var currentUser = string.IsNullOrWhiteSpace(User?.Identity?.Name)
            ? "System"
            : User.Identity!.Name!.Trim();
        if (currentUser.Length > 255)
        {
            currentUser = currentUser[..255];
        }

        var nowUtc = DateTime.UtcNow;

        var duplicate = new Models.EmailContent
        {
            Name = candidateName,
            Subject = source.Subject,
            Template = source.Template,
            Active = source.Active,
            CreatedUtc = nowUtc,
            UpdatedUtc = nowUtc,
            CreatedBy = currentUser,
            UpdatedBy = currentUser
        };

        _context.EmailContent.Add(duplicate);
        await _context.SaveChangesAsync();

        StatusMessage = $"Created duplicate email content: {duplicate.Name}.";
        return RedirectToPage();
    }
}
