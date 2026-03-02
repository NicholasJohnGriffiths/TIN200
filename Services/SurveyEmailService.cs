using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace TINWorkspaceTemp.Services
{
    public class SurveyEmailService : ISurveyEmailService
    {
        private readonly SmtpSettings _smtpSettings;

        public SurveyEmailService(IOptions<SmtpSettings> smtpOptions)
        {
            _smtpSettings = smtpOptions.Value;
        }

        public async Task SendSurveyLinkAsync(string recipientEmail, string surveyUrl, string? companyName, int clientId)
        {
            if (string.IsNullOrWhiteSpace(_smtpSettings.Host)
                || _smtpSettings.Port <= 0
                || string.IsNullOrWhiteSpace(_smtpSettings.FromEmail))
            {
                throw new InvalidOperationException("SMTP settings are not configured. Please configure SmtpSettings in appsettings.json.");
            }

            var subject = $"Please update your company details (Client ID: {clientId})";
            var body = $@"Hello{(string.IsNullOrWhiteSpace(companyName) ? string.Empty : $" {companyName}")},

Please use the link below to review and update your company details:
{surveyUrl}

If you did not expect this email, you can ignore it.

Thank you.";

            using var message = new MailMessage
            {
                From = new MailAddress(_smtpSettings.FromEmail, _smtpSettings.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };

            message.To.Add(recipientEmail);

            using var smtpClient = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port)
            {
                EnableSsl = _smtpSettings.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            if (!string.IsNullOrWhiteSpace(_smtpSettings.Username))
            {
                smtpClient.Credentials = new NetworkCredential(_smtpSettings.Username, _smtpSettings.Password);
            }

            await smtpClient.SendMailAsync(message);
        }
    }
}
