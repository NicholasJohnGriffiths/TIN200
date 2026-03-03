using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Options;

namespace TINWorkspaceTemp.Services
{
    public class SurveyEmailService : ISurveyEmailService
    {
        private readonly AzureCommunicationEmailSettings _emailSettings;

        public SurveyEmailService(IOptions<AzureCommunicationEmailSettings> emailOptions)
        {
            _emailSettings = emailOptions.Value;
        }

        public async Task SendSurveyLinkAsync(string recipientEmail, string surveyUrl, string? companyName, int clientId)
        {
            if (string.IsNullOrWhiteSpace(_emailSettings.ConnectionString)
                || string.IsNullOrWhiteSpace(_emailSettings.FromEmail))
            {
                throw new InvalidOperationException("Azure Communication Email settings are not configured. Please configure AzureCommunicationEmail in appsettings or environment settings.");
            }

            var subject = $"Please update your company details (Client ID: {clientId})";
            var body = $@"Hello{(string.IsNullOrWhiteSpace(companyName) ? string.Empty : $" {companyName}")},

Please use the link below to review and update your company details:
{surveyUrl}

If you did not expect this email, you can ignore it.

Thank you.";

            var emailClient = new EmailClient(_emailSettings.ConnectionString);

            var emailMessage = new EmailMessage(
                senderAddress: _emailSettings.FromEmail,
                content: new EmailContent(subject)
                {
                    PlainText = body
                },
                recipients: new EmailRecipients(new List<EmailAddress>
                {
                    new(recipientEmail)
                }));

            EmailSendOperation operation = await emailClient.SendAsync(WaitUntil.Completed, emailMessage);

            if (operation.HasCompleted && operation.Value.Status != EmailSendStatus.Succeeded)
            {
                throw new InvalidOperationException($"Email send failed with status: {operation.Value.Status}");
            }
        }
    }
}
