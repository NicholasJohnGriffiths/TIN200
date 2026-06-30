namespace TINWeb.Services
{
    public class SurveySubmittedNotificationMessage
    {
        public string AdminEmail { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        public int SurveyYear { get; set; }

        public DateTime SubmittedAtUtc { get; set; }

        public string? SubmitterEmail { get; set; }
    }
}