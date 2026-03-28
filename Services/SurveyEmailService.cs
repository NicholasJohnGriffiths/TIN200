using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Options;
using System.Net;

namespace TINWeb.Services
{
    public class SurveyEmailService : ISurveyEmailService
    {
        private readonly AzureCommunicationEmailSettings _emailSettings;
        private readonly SurveyLinkSettings _surveyLinkSettings;

        public SurveyEmailService(
            IOptions<AzureCommunicationEmailSettings> emailOptions,
            IOptions<SurveyLinkSettings> surveyLinkOptions)
        {
            _emailSettings = emailOptions.Value;
            _surveyLinkSettings = surveyLinkOptions.Value;
        }

        public async Task SendSurveyLinkAsync(string recipientEmail, string surveyUrl, string? companyName, int clientId)
        {
            if (string.IsNullOrWhiteSpace(_emailSettings.ConnectionString)
                || string.IsNullOrWhiteSpace(_emailSettings.FromEmail))
            {
                throw new InvalidOperationException("Azure Communication Email settings are not configured. Please configure AzureCommunicationEmail in appsettings or environment settings.");
            }

            var recipientName = string.IsNullOrWhiteSpace(companyName) ? "there" : companyName.Trim();
            var supportEmail = "tin100@tinetwork.com";
            var subject = "TIN200 survey request: please review your company details";
            var plainTextBody = $@"Hello {recipientName},

You have been invited to review and update your company details for TIN200.

Open your secure survey link
{surveyUrl}

If you did not expect this email, you can safely ignore it.

Need help? Contact {supportEmail}.

Regards,
TIN200 Team";

            var htmlBody = $@"<p>Hello {WebUtility.HtmlEncode(recipientName)},</p>
<p>You have been invited to review and update your company details for <strong>TIN200</strong>.</p>
<p><a href=""{WebUtility.HtmlEncode(surveyUrl)}"">Open your secure survey link</a></p>
<p>If you did not expect this email, you can safely ignore it.</p>
<p>Need help? Contact <a href=""mailto:{WebUtility.HtmlEncode(supportEmail)}"">{WebUtility.HtmlEncode(supportEmail)}</a>.</p>
<p>Regards,<br/>TIN200 Team</p>";

            var emailClient = new EmailClient(_emailSettings.ConnectionString);

            var emailMessage = new EmailMessage(
                senderAddress: _emailSettings.FromEmail,
                content: new EmailContent(subject)
                {
                    PlainText = plainTextBody,
                    Html = htmlBody
                },
                recipients: new EmailRecipients(new List<EmailAddress>
                {
                    new(recipientEmail)
                }));

            EmailSendOperation operation;
            try
            {
                operation = await emailClient.SendAsync(WaitUntil.Completed, emailMessage);
            }
            catch (RequestFailedException ex) when (ex.Status == 401)
            {
                throw new InvalidOperationException(
                    "Azure Communication Email authorization failed (401). Verify AzureCommunicationEmail__ConnectionString is from the correct Communication Services resource and AzureCommunicationEmail__FromEmail is a sender address from a verified/connected domain for that resource.",
                    ex);
            }
            catch (RequestFailedException ex)
            {
                throw new InvalidOperationException(
                    $"Azure Communication Email send failed. Status: {ex.Status}, Code: {ex.ErrorCode}, Message: {ex.Message}",
                    ex);
            }

            if (operation.HasCompleted && operation.Value.Status != EmailSendStatus.Succeeded)
            {
                throw new InvalidOperationException($"Email send failed with status: {operation.Value.Status}");
            }
        }
    }
}
