using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.Company
{
    public class SurveyUpdateModel : PageModel
    {
        private readonly CompanyService _companyService;
        private readonly ISurveyLinkTokenService _surveyLinkTokenService;

        public SurveyUpdateModel(CompanyService companyService, ISurveyLinkTokenService surveyLinkTokenService)
        {
            _companyService = companyService;
            _surveyLinkTokenService = surveyLinkTokenService;
        }

        [BindProperty]
        public Models.Tin200 Record { get; set; } = new();

        [BindProperty]
        public string Token { get; set; } = string.Empty;

        [BindProperty]
        public string FormAction { get; set; } = string.Empty;

        public bool Submitted { get; set; }
        public bool Saved { get; set; }

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
    }
}

