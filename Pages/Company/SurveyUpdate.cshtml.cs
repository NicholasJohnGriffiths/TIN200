using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.Company
{
    public class SurveyUpdateModel : PageModel
    {
        private readonly CompanyService _companyService;
        private readonly ISurveyLinkTokenService _surveyLinkTokenService;
        private readonly ApplicationDbContext _context;

        public SurveyUpdateModel(CompanyService companyService, ISurveyLinkTokenService surveyLinkTokenService, ApplicationDbContext context)
        {
            _companyService = companyService;
            _surveyLinkTokenService = surveyLinkTokenService;
            _context = context;
        }

        [BindProperty]
        public Models.Tin200 Record { get; set; } = new();

        [BindProperty]
        public string Token { get; set; } = string.Empty;

        [BindProperty]
        public string FormAction { get; set; } = string.Empty;

        public bool Submitted { get; set; }
        public bool Saved { get; set; }
        public bool IsLocked { get; set; }

        public async Task<IActionResult> OnGetAsync(int id, string token, bool submitted = false, bool saved = false)
        {
            if (!_surveyLinkTokenService.IsTokenValid(id, token))
            {
                return RedirectToPage("/Company/SurveyLinkInvalid", new { id, reason = "invalid-token" });
            }

            var record = await _companyService.GetCompanyByIdAsync(id);
            if (record == null)
            {
                return NotFound();
            }

            Record = record;
            Token = token;
            Submitted = submitted;
            Saved = saved;
            IsLocked = await IsLockedForCurrentSurveyAsync(id);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            if (!_surveyLinkTokenService.IsTokenValid(id, Token))
            {
                return RedirectToPage("/Company/SurveyLinkInvalid", new { id, reason = "invalid-token" });
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (await IsLockedForCurrentSurveyAsync(id))
            {
                var lockedRecord = await _companyService.GetCompanyByIdAsync(id);
                if (lockedRecord == null)
                {
                    return NotFound();
                }

                Record = lockedRecord;
                IsLocked = true;
                ModelState.AddModelError(string.Empty, "This survey record is locked. Please contact the Technology Investment Network.");
                return Page();
            }

            var existing = await _companyService.GetCompanyByIdAsync(id);
            if (existing == null)
            {
                return NotFound();
            }

            existing.CeoFirstName = Record.CeoFirstName;
            existing.CeoLastName = Record.CeoLastName;
            existing.CompanyName = Record.CompanyName;
            existing.CompanyDescription = Record.CompanyDescription;
            existing.Fye2025 = Record.Fye2025;
            existing.Fye2024 = Record.Fye2024;
            existing.Fye2023 = Record.Fye2023;

            await _companyService.UpdateCompanyAsync(existing);

            if (string.Equals(FormAction, "save", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToPage(new { id, token = Token, saved = true });
            }

            return RedirectToPage(new { id, token = Token, submitted = true });
        }

        private async Task<bool> IsLockedForCurrentSurveyAsync(int companyId)
        {
            var currentSurveyId = await _context.Survey
                .Where(s => s.CurrentSurvey)
                .OrderByDescending(s => s.FinancialYear)
                .ThenByDescending(s => s.Id)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync();

            if (!currentSurveyId.HasValue)
            {
                return false;
            }

            var companySurvey = await _context.CompanySurvey
                .FirstOrDefaultAsync(cs => cs.CompanyId == companyId && cs.SurveyId == currentSurveyId.Value);

            return (companySurvey?.Locked).GetValueOrDefault();
        }
    }
}

