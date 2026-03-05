using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TINWeb.Pages.Company
{
    public class SurveyLinkInvalidModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public SurveyLinkInvalidModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string RequestNewLinkUrl { get; private set; } = string.Empty;

        public bool HasSupportEmail => !string.IsNullOrWhiteSpace(RequestNewLinkUrl);

        public void OnGet()
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
    }
}

