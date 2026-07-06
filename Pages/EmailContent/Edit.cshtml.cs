using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Models;

namespace TINWeb.Pages.EmailContent;

[Authorize(Policy = "AdminOnly")]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;

    [BindProperty]
    public Models.EmailContent Record { get; set; } = new();

    public bool IsCreate => Record.Id == 0;

    public EditModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (!id.HasValue)
        {
            Record = new Models.EmailContent
            {
                Active = true
            };

            return Page();
        }

        var existing = await _context.EmailContent
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id.Value);

        if (existing == null)
        {
            return NotFound();
        }

        Record = existing;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Record.Name = Record.Name?.Trim() ?? string.Empty;
        Record.Subject = string.IsNullOrWhiteSpace(Record.Subject)
            ? null
            : Record.Subject.Trim();
        Record.Template = string.IsNullOrWhiteSpace(Record.Template)
            ? null
            : Record.Template.Trim();

        if (string.IsNullOrWhiteSpace(Record.Name))
        {
            ModelState.AddModelError("Record.Name", "Name is required.");
        }

        if (await _context.EmailContent
            .AsNoTracking()
            .AnyAsync(x => x.Id != Record.Id && x.Name == Record.Name))
        {
            ModelState.AddModelError("Record.Name", "An email content record with this name already exists.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var nowUtc = DateTime.UtcNow;
        var currentUser = string.IsNullOrWhiteSpace(User?.Identity?.Name)
            ? "System"
            : User.Identity!.Name!.Trim();
        if (currentUser.Length > 255)
        {
            currentUser = currentUser[..255];
        }

        if (Record.Id == 0)
        {
            Record.CreatedUtc = nowUtc;
            Record.UpdatedUtc = nowUtc;
            Record.CreatedBy = currentUser;
            Record.UpdatedBy = currentUser;
            _context.EmailContent.Add(Record);
        }
        else
        {
            var existing = await _context.EmailContent.FirstOrDefaultAsync(x => x.Id == Record.Id);
            if (existing == null)
            {
                return NotFound();
            }

            existing.Name = Record.Name;
            existing.Subject = Record.Subject;
            existing.Template = Record.Template;
            existing.Active = Record.Active;
            existing.UpdatedUtc = nowUtc;
            existing.UpdatedBy = currentUser;
        }

        await _context.SaveChangesAsync();
        return RedirectToPage("./Index");
    }
}
