using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Services;

namespace TINWeb.Pages.Company
{
    [IgnoreAntiforgeryToken]
    public class UnsubscribeModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ISurveyLinkTokenService _surveyLinkTokenService;

        public UnsubscribeModel(ApplicationDbContext context, ISurveyLinkTokenService surveyLinkTokenService)
        {
            _context = context;
            _surveyLinkTokenService = surveyLinkTokenService;
        }

        public string? Message { get; set; }
        public bool Success { get; set; }
        public string? CompanyName { get; set; }

        public async Task<IActionResult> OnGetAsync(int id, string token)
        {
            // Validate token
            if (!_surveyLinkTokenService.IsTokenValid(id, token))
            {
                Success = false;
                Message = "Invalid or expired unsubscribe link.";
                return Page();
            }

            try
            {
                // Get the company to display name
                var company = await _context.Tin200.FirstOrDefaultAsync(t => t.Id == id);
                CompanyName = company?.CompanyName ?? "Your Company";

                // Update all CompanySurvey records for this company to mark as unsubscribed
                var companySurveys = await _context.CompanySurvey
                    .Where(cs => cs.CompanyId == id)
                    .ToListAsync();

                if (companySurveys.Count == 0)
                {
                    Success = false;
                    Message = $"No survey records found for {CompanyName}.";
                    return Page();
                }

                // Mark all records as unsubscribed
                foreach (var companySurvey in companySurveys)
                {
                    companySurvey.Unsubscribed = true;
                    companySurvey.UnsubscribedDate = DateTime.UtcNow.Date;
                    _context.CompanySurvey.Update(companySurvey);
                }

                await _context.SaveChangesAsync();

                Success = true;
                Message = $"You have been unsubscribed from TIN200 surveys. If you change your mind, please contact us.";
                return Page();
            }
            catch (Exception ex)
            {
                Success = false;
                Message = $"An error occurred while processing your request: {ex.Message}";
                return Page();
            }
        }
    }
}
