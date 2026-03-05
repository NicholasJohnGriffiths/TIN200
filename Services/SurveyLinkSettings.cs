namespace TINWeb.Services
{
    public class SurveyLinkSettings
    {
        public string SecretKey { get; set; } = string.Empty;
        public int ExpiryHours { get; set; } = 72;
        public string SupportEmail { get; set; } = string.Empty;
    }
}
