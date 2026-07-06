namespace TINWeb.Services
{
    public interface ISurveyEmailService
    {
        Task SendSurveyLinkAsync(string recipientEmail, string surveyUrl, string? companyName, int clientId, int emailContentId);

        Task SendSurveyReminderLinkAsync(string recipientEmail, string surveyUrl, string? companyName, int clientId, int emailContentId);

        Task<SurveyEmailPreviewResult> BuildSurveyEmailPreviewAsync(int emailContentId, string surveyUrl, string? companyName, int clientId);

        Task SendSurveySubmittedNotificationAsync(
            string adminEmail,
            string companyName,
            int surveyYear,
            DateTime submittedAt,
            string? submitterEmail);

        Task SendSurveySavedNotificationAsync(
            string adminEmail,
            string companyName,
            int surveyYear,
            DateTime savedAt,
            string? submitterEmail);

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
