using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Services;

namespace TINWeb.Pages.CompanySurvey
{
    public class DetailsModel : PageModel
    {
        private readonly CompanySurveyService _service;
        private readonly ISurveyLinkTokenService _surveyLinkTokenService;

        public Models.CompanySurvey Record { get; set; } = new();
        public DateTimeOffset? SurveyLinkExpiryUtc => GetSurveyLinkExpiryUtc(Record.SurveyLink);
        public bool IsSurveyLinkExpired => SurveyLinkExpiryUtc.HasValue && SurveyLinkExpiryUtc.Value <= DateTimeOffset.UtcNow;

        public DetailsModel(CompanySurveyService service, ISurveyLinkTokenService surveyLinkTokenService)
        {
            _service = service;
            _surveyLinkTokenService = surveyLinkTokenService;
        }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var record = await _service.GetByIdAsync(id.Value);
            if (record == null)
            {
                return NotFound();
            }

            Record = record;
            return Page();
        }

        private DateTimeOffset? GetSurveyLinkExpiryUtc(string? surveyLink)
        {
            var token = ExtractTokenFromSurveyLink(surveyLink);
            return string.IsNullOrWhiteSpace(token)
                ? null
                : _surveyLinkTokenService.GetTokenExpiryUtc(token);
        }

        private static string? ExtractTokenFromSurveyLink(string? surveyLink)
        {
            if (string.IsNullOrWhiteSpace(surveyLink) || !Uri.TryCreate(surveyLink.Trim(), UriKind.Absolute, out var linkUri))
            {
                return null;
            }

            var token = linkUri.Segments.LastOrDefault()?.Trim('/');
            return string.IsNullOrWhiteSpace(token) ? null : Uri.UnescapeDataString(token);
        }
    }
}
