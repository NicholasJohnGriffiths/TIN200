using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Models;

namespace TINWeb.Pages.Config;

[Authorize(Policy = "AdminOnly")]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;

    [BindProperty]
    public AppConfig Record { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public EditModel(ApplicationDbContext context)
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

    public async Task<IActionResult> OnPostAsync()
    {
        Record.AdminEmail = Record.AdminEmail?.Trim() ?? string.Empty;
        Record.SurveyEmailSubject = string.IsNullOrWhiteSpace(Record.SurveyEmailSubject)
            ? null
            : Record.SurveyEmailSubject.Trim();
        Record.SurveyEmailTemplate = string.IsNullOrWhiteSpace(Record.SurveyEmailTemplate)
            ? null
            : Record.SurveyEmailTemplate.Trim();

        if (string.IsNullOrWhiteSpace(Record.AdminEmail))
        {
            ModelState.AddModelError("Record.AdminEmail", "Admin email is required.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var existing = await _context.AppConfig.FirstOrDefaultAsync(c => c.Id == Record.Id);
        if (existing == null)
        {
            return NotFound();
        }

        existing.AdminEmail = Record.AdminEmail;
        existing.SurveyEmailSubject = Record.SurveyEmailSubject;
        existing.SurveyEmailTemplate = Record.SurveyEmailTemplate;
        await _context.SaveChangesAsync();

        StatusMessage = "Config updated successfully.";
        return RedirectToPage("./Index");
    }
}
