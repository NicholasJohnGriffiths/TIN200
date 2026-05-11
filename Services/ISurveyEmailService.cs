namespace TINWeb.Services
{
    public interface ISurveyEmailService
    {
        Task SendSurveyLinkAsync(string recipientEmail, string surveyUrl, string? companyName, int clientId);

        Task SendSurveyReminderLinkAsync(string recipientEmail, string surveyUrl, string? companyName, int clientId);

        Task SendBounceNotificationAsync(
            string adminEmail,
            string companyName,
            int surveyYear,
            string bouncedRecipientEmail,
            string status,
            string? reason,
            string? messageId,
            string? eventId);
    }
}
