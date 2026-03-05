namespace TINWeb.Services
{
    public interface ISurveyEmailService
    {
        Task SendSurveyLinkAsync(string recipientEmail, string surveyUrl, string? companyName, int clientId);
    }
}
