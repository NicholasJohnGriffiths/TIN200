using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Models;

namespace TINWeb.Pages.Company
{
    public class SurveyLinkInvalidModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;

        public SurveyLinkInvalidModel(IConfiguration configuration, ApplicationDbContext context)
        {
            _configuration = configuration;
            _context = context;
        }

        public string RequestNewLinkUrl { get; private set; } = string.Empty;
        public string? Reason { get; private set; }
        public int? CompanyId { get; private set; }
        public bool RequestRecorded { get; private set; }
        public bool IsCompanySurveyLocked { get; private set; }

        public bool HasSupportEmail => !string.IsNullOrWhiteSpace(RequestNewLinkUrl);

        public async Task OnGetAsync(int? id, string? reason, bool requested = false)
        {
            CompanyId = id;
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            RequestRecorded = requested;

            if (id.HasValue && id.Value > 0)
            {
                IsCompanySurveyLocked = await IsLockedForCurrentSurveyAsync(id.Value);
            }

            BuildRequestEmailUrl();
        }

        public async Task<IActionResult> OnPostRequestAsync(int? id, string? reason)
        {
            if (!id.HasValue || id.Value <= 0)
            {
                return RedirectToPage(new { reason, requested = false });
            }

            var currentSurvey = await _context.Survey
                .Where(s => s.CurrentSurvey)
                .OrderByDescending(s => s.FinancialYear)
                .ThenByDescending(s => s.Id)
                .FirstOrDefaultAsync();

            if (currentSurvey != null)
            {
                var companySurvey = await _context.CompanySurvey
                    .FirstOrDefaultAsync(cs => cs.CompanyId == id.Value && cs.SurveyId == currentSurvey.Id);

                if ((companySurvey?.Locked).GetValueOrDefault())
                {
                    return RedirectToPage(new { id, reason = "survey-locked", requested = false });
                }

                if (companySurvey == null)
                {
                    companySurvey = new Models.CompanySurvey
                    {
                        CompanyId = id.Value,
                        SurveyId = currentSurvey.Id,
                        Saved = false,
                        Submitted = false,
                        Requested = true,
                        Locked = false,
                        Estimate = false,
                        SavedDate = null,
                        SubmittedDate = null,
                        RequestedDate = DateTime.Now
                    };

                    _context.CompanySurvey.Add(companySurvey);
                }
                else
                {
                    companySurvey.Requested = true;
                    companySurvey.RequestedDate = DateTime.Now;
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToPage(new { id, reason, requested = true });
        }

        private void BuildRequestEmailUrl()
        {

            var supportEmail = _configuration["SurveyLinkSettings:SupportEmail"];
            if (string.IsNullOrWhiteSpace(supportEmail))
            {
                supportEmail = _configuration["SmtpSettings:FromEmail"];
            }

            if (string.IsNullOrWhiteSpace(supportEmail))
            {
                RequestNewLinkUrl = string.Empty;
                return;
            }

            var subject = Uri.EscapeDataString("Request new survey link");
            var body = Uri.EscapeDataString("Hello,\n\nMy survey link is invalid or expired. Please send me a new link.\n\nThank you.");
            RequestNewLinkUrl = $"mailto:{supportEmail}?subject={subject}&body={body}";
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

