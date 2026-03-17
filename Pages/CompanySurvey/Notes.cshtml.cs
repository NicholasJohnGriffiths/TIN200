using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Models;

namespace TINWeb.Pages.CompanySurvey
{
    public class NotesModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public int CompanySurveyId { get; set; }
        public int? FinancialYear { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public List<CompanySurveyNote> Notes { get; set; } = new();

        [BindProperty]
        [Required(ErrorMessage = "Please enter a note before saving.")]
        public string NewNoteText { get; set; } = string.Empty;

        public NotesModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> OnGetAsync(int? companySurveyId, int? financialYear)
        {
            if (!companySurveyId.HasValue)
            {
                return NotFound();
            }

            CompanySurveyId = companySurveyId.Value;
            FinancialYear = financialYear;

            var companySurveyInfo = await (
                from companySurvey in _context.CompanySurvey
                join company in _context.Tin200 on companySurvey.CompanyId equals company.Id into companyJoin
                from company in companyJoin.DefaultIfEmpty()
                where companySurvey.Id == CompanySurveyId
                select new
                {
                    CompanyName = company.CompanyName
                })
                .FirstOrDefaultAsync();

            if (companySurveyInfo == null)
            {
                return NotFound();
            }

            CompanyName = companySurveyInfo.CompanyName ?? string.Empty;

            Notes = await _context.CompanySurveyNotes
                .Where(n => n.CompanySurveyId == CompanySurveyId)
                .OrderByDescending(n => n.NoteDateTime)
                .ThenByDescending(n => n.Id)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int companySurveyId, int? financialYear)
        {
            CompanySurveyId = companySurveyId;
            FinancialYear = financialYear;

            var companyExists = await _context.CompanySurvey.AnyAsync(cs => cs.Id == companySurveyId);
            if (!companyExists)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                await LoadPageDataAsync();
                return Page();
            }

            var note = new CompanySurveyNote
            {
                CompanySurveyId = companySurveyId,
                NoteDateTime = DateTime.Now,
                User = User.Identity?.Name ?? "Unknown",
                Notes = NewNoteText
            };

            _context.CompanySurveyNotes.Add(note);
            await _context.SaveChangesAsync();

            return RedirectToPage(new { companySurveyId, financialYear });
        }

        private async Task LoadPageDataAsync()
        {
            var companySurveyInfo = await (
                from companySurvey in _context.CompanySurvey
                join company in _context.Tin200 on companySurvey.CompanyId equals company.Id into companyJoin
                from company in companyJoin.DefaultIfEmpty()
                where companySurvey.Id == CompanySurveyId
                select new
                {
                    CompanyName = company.CompanyName
                })
                .FirstOrDefaultAsync();

            CompanyName = companySurveyInfo?.CompanyName ?? string.Empty;

            Notes = await _context.CompanySurveyNotes
                .Where(n => n.CompanySurveyId == CompanySurveyId)
                .OrderByDescending(n => n.NoteDateTime)
                .ThenByDescending(n => n.Id)
                .ToListAsync();
        }
    }
}
