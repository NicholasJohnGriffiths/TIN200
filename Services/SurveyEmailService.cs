using Azure;
using Azure.Communication.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.RegularExpressions;
using TINWeb.Data;

namespace TINWeb.Services
{
    public class SurveyEmailService : ISurveyEmailService
    {
        private readonly ApplicationDbContext _context;
        private readonly AzureCommunicationEmailSettings _emailSettings;
        private readonly SurveyLinkSettings _surveyLinkSettings;

        public SurveyEmailService(
            ApplicationDbContext context,
            IOptions<AzureCommunicationEmailSettings> emailOptions,
            IOptions<SurveyLinkSettings> surveyLinkOptions)
        {
            _context = context;
            _emailSettings = emailOptions.Value;
            _surveyLinkSettings = surveyLinkOptions.Value;
        }

        public async Task SendSurveyLinkAsync(string recipientEmail, string surveyUrl, string? companyName, int clientId)
        {
            EnsureEmailConfigured();

            var recipientName = string.IsNullOrWhiteSpace(companyName) ? "there" : companyName.Trim();
            var senderDisplayName = GetSenderDisplayName();
            var supportEmail = "tin100@tinetwork.com";
            var defaultSubject = "TIN200 survey request: please review your company details";

            var unsubscribeToken = GenerateUnsubscribeToken(clientId);
            var baseUrl = _surveyLinkSettings.BaseUrl ?? string.Empty;
            baseUrl = baseUrl.Trim().TrimEnd('/');
            var unsubscribeUrl = $"{baseUrl}/Company/Unsubscribe?id={clientId}&token={Uri.EscapeDataString(unsubscribeToken)}";

            var configuredEmailOptions = await _context.AppConfig
                .AsNoTracking()
                .OrderBy(c => c.Id)
                .Select(c => new { c.SurveyEmailSubject, c.SurveyEmailTemplate, c.EmailHeaderImageId })
                .FirstOrDefaultAsync();

            var emailHeaderImageUrl = BuildEmailHeaderImageUrl(configuredEmailOptions?.EmailHeaderImageId);

            var subject = string.IsNullOrWhiteSpace(configuredEmailOptions?.SurveyEmailSubject)
                ? defaultSubject
                : configuredEmailOptions.SurveyEmailSubject.Trim();

            var (plainTextBody, htmlBody) = BuildSurveyEmailBodies(
                configuredEmailOptions?.SurveyEmailTemplate,
                emailHeaderImageUrl,
                recipientName,
                surveyUrl,
                unsubscribeUrl,
                supportEmail,
                senderDisplayName);

            await SendEmailAsync(new[] { recipientEmail }, subject, plainTextBody, htmlBody);
        }

        public async Task SendBounceNotificationAsync(
            string adminEmail,
            string companyName,
            int surveyYear,
            string bouncedRecipientEmail,
            string status,
            string? reason,
            string? messageId,
            string? eventId)
        {
            EnsureEmailConfigured();

            var safeCompanyName = string.IsNullOrWhiteSpace(companyName) ? "Unknown company" : companyName.Trim();
            var safeReason = string.IsNullOrWhiteSpace(reason)
                ? "No additional delivery details were provided."
                : reason.Trim();

            var subject = $"TIN200 survey email bounced - {safeCompanyName}";

            var plainTextBody = $@"A TIN200 survey email has bounced back.

Company: {safeCompanyName}
Survey year: {surveyYear}
Recipient: {bouncedRecipientEmail}
Status: {status}
Reason: {safeReason}
{(string.IsNullOrWhiteSpace(messageId) ? string.Empty : $"Message ID: {messageId}\r\n")}{(string.IsNullOrWhiteSpace(eventId) ? string.Empty : $"Event ID: {eventId}\r\n")}
Please review the contact email for this company before resending the survey.";

            var htmlBody = $@"<p>A <strong>TIN200</strong> survey email has bounced back.</p>
<ul>
    <li><strong>Company:</strong> {WebUtility.HtmlEncode(safeCompanyName)}</li>
    <li><strong>Survey year:</strong> {surveyYear}</li>
    <li><strong>Recipient:</strong> {WebUtility.HtmlEncode(bouncedRecipientEmail)}</li>
    <li><strong>Status:</strong> {WebUtility.HtmlEncode(status)}</li>
    <li><strong>Reason:</strong> {WebUtility.HtmlEncode(safeReason)}</li>
    {(string.IsNullOrWhiteSpace(messageId) ? string.Empty : $"<li><strong>Message ID:</strong> {WebUtility.HtmlEncode(messageId)}</li>")}
    {(string.IsNullOrWhiteSpace(eventId) ? string.Empty : $"<li><strong>Event ID:</strong> {WebUtility.HtmlEncode(eventId)}</li>")}
</ul>
<p>Please review the contact email for this company before resending the survey.</p>";

            await SendEmailAsync(new[] { adminEmail }, subject, plainTextBody, htmlBody);
        }

        private (string PlainTextBody, string HtmlBody) BuildSurveyEmailBodies(
            string? configuredTemplate,
            string? emailHeaderImageUrl,
            string recipientName,
            string surveyUrl,
            string unsubscribeUrl,
            string supportEmail,
            string senderDisplayName)
        {
            var unsubscribePlainText = $@"To unsubscribe from future TIN200 surveys:
{unsubscribeUrl}";

            var unsubscribeHtml = $@"<p><small><a href=""{WebUtility.HtmlEncode(unsubscribeUrl)}"" style=""color: #999; font-size: 12px;"">Unsubscribe from future surveys</a></small></p>";

            var footerPlainText = $@"If you did not expect this email, you can safely ignore it.

Need help? Contact {supportEmail}.

{unsubscribePlainText}

Regards,
{senderDisplayName}";

            var footerHtml = $@"<p>If you did not expect this email, you can safely ignore it.</p>
<p>Need help? Contact <a href=""mailto:{WebUtility.HtmlEncode(supportEmail)}"">{WebUtility.HtmlEncode(supportEmail)}</a>.</p>
{unsubscribeHtml}
<p>Regards,<br/>{WebUtility.HtmlEncode(senderDisplayName)}</p>";

            var emailHeaderHtml = BuildEmailHeaderImageHtml(emailHeaderImageUrl);

            if (!string.IsNullOrWhiteSpace(configuredTemplate))
            {
                var htmlTemplate = ApplySurveyTemplate(configuredTemplate, recipientName, surveyUrl, encodeForHtml: true);
                var plainTextTemplate = ConvertHtmlToPlainText(ApplySurveyTemplate(configuredTemplate, recipientName, surveyUrl, encodeForHtml: false));

                var plainTextBody = string.IsNullOrWhiteSpace(plainTextTemplate)
                    ? unsubscribePlainText
                    : $"{plainTextTemplate}\r\n\r\n{unsubscribePlainText}";

                var htmlBody = string.IsNullOrWhiteSpace(htmlTemplate)
                    ? $"{emailHeaderHtml}{unsubscribeHtml}"
                    : $"{emailHeaderHtml}{htmlTemplate}\n{unsubscribeHtml}";

                return (plainTextBody, htmlBody);
            }

            var fallbackPlainTextBody = $@"Hello {recipientName},

You have been invited to review and update your company details for TIN200.

Open your secure survey link
{surveyUrl}

{footerPlainText}";

            var fallbackHtmlBody = $@"<p>Hello {WebUtility.HtmlEncode(recipientName)},</p>
<p>You have been invited to review and update your company details for <strong>TIN200</strong>.</p>
<p><a href=""{WebUtility.HtmlEncode(surveyUrl)}"">Open your secure survey link</a></p>
{footerHtml}";

            fallbackHtmlBody = $"{emailHeaderHtml}{fallbackHtmlBody}";

            return (fallbackPlainTextBody, fallbackHtmlBody);
        }

        private string? BuildEmailHeaderImageUrl(int? emailHeaderImageId)
        {
            if (!emailHeaderImageId.HasValue)
            {
                return null;
            }

            var baseUrl = (_surveyLinkSettings.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return null;
            }

            return $"{baseUrl}/api/config/email-header-image/{emailHeaderImageId.Value}";
        }

        private static string BuildEmailHeaderImageHtml(string? emailHeaderImageUrl)
        {
            if (string.IsNullOrWhiteSpace(emailHeaderImageUrl))
            {
                return string.Empty;
            }

            return $@"<div style=""margin: 0 0 16px 0; text-align: left;"">
<img src=""{WebUtility.HtmlEncode(emailHeaderImageUrl)}"" alt=""TIN200"" style=""max-width: 100%; height: auto; display: block;"" />
</div>
";
        }

        private static string ApplySurveyTemplate(string template, string recipientName, string surveyUrl, bool encodeForHtml)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return string.Empty;
            }

            var companyReplacement = encodeForHtml
                ? WebUtility.HtmlEncode(recipientName)
                : recipientName;

            var surveyLinkReplacement = encodeForHtml
                ? $@"<a id=""surveylink"" href=""{WebUtility.HtmlEncode(surveyUrl)}"">{WebUtility.HtmlEncode(surveyUrl)}</a>"
                : surveyUrl;

            var result = template
                .Replace("(Company Name)", companyReplacement, StringComparison.OrdinalIgnoreCase)
                .Replace("(Name)", companyReplacement, StringComparison.OrdinalIgnoreCase)
                .Replace("(Survey link)", surveyLinkReplacement, StringComparison.OrdinalIgnoreCase);

            if (encodeForHtml)
            {
                result = Regex.Replace(
                    result,
                    @"<a\b(?=[^>]*\bid\s*=\s*['""]surveylink['""])([^>]*)>(.*?)</a>",
                    match =>
                    {
                        var attributes = Regex.Replace(
                            match.Groups[1].Value,
                            @"\s+href\s*=\s*(['""]).*?\1",
                            string.Empty,
                            RegexOptions.IgnoreCase | RegexOptions.Singleline).Trim();

                        var attributePrefix = string.IsNullOrWhiteSpace(attributes)
                            ? string.Empty
                            : $" {attributes}";

                        var linkText = match.Groups[2].Value;
                        return $@"<a{attributePrefix} href=""{WebUtility.HtmlEncode(surveyUrl)}"">{linkText}</a>";
                    },
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
            }
            else
            {
                result = Regex.Replace(
                    result,
                    @"<a\b(?=[^>]*\bid\s*=\s*['""]surveylink['""])([^>]*)>(.*?)</a>",
                    surveyUrl,
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
            }

            return result;
        }

        private static string ConvertHtmlToPlainText(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            var text = Regex.Replace(html, @"<\s*br\s*/?>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<\s*/p\s*>", "\n\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<[^>]+>", string.Empty);
            text = WebUtility.HtmlDecode(text);

            return Regex.Replace(text, @"\n{3,}", "\n\n").Trim();
        }

        private void EnsureEmailConfigured()
        {
            if (string.IsNullOrWhiteSpace(_emailSettings.ConnectionString)
                || string.IsNullOrWhiteSpace(_emailSettings.FromEmail))
            {
                throw new InvalidOperationException("Azure Communication Email settings are not configured. Please configure AzureCommunicationEmail in appsettings or environment settings.");
            }
        }

        private async Task SendEmailAsync(IEnumerable<string> recipientEmails, string subject, string plainTextBody, string htmlBody)
        {
            var recipients = ParseRecipientEmails(recipientEmails).ToList();
            if (recipients.Count == 0)
            {
                throw new InvalidOperationException("No valid recipient email addresses were provided.");
            }

            var emailClient = new EmailClient(_emailSettings.ConnectionString);

            var emailMessage = new EmailMessage(
                senderAddress: BuildSenderAddress(_emailSettings.FromEmail, _emailSettings.FromName),
                content: new EmailContent(subject)
                {
                    PlainText = plainTextBody,
                    Html = htmlBody
                },
                recipients: new EmailRecipients(recipients.Select(email => new EmailAddress(email)).ToList()));

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await emailClient.SendAsync(WaitUntil.Started, emailMessage, cts.Token);
            }
            catch (OperationCanceledException ex)
            {
                throw new InvalidOperationException(
                    "Azure Communication Email send did not receive an acceptance response within 30 seconds. The request was cancelled to avoid the survey send page hanging.",
                    ex);
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
        }

        private string GetSenderDisplayName()
        {
            return string.IsNullOrWhiteSpace(_emailSettings.FromName)
                ? "TIN Survey"
                : _emailSettings.FromName.Trim();
        }

        private static string BuildSenderAddress(string fromEmail, string? fromName)
        {
            // Azure Communication Email expects senderAddress to be only the email address.
            // The configured FromName is still used throughout the survey email content and should
            // also match the sender identity configured in Azure for the mailbox.
            // return "tin@tin100.com"; // Temporary override previously used.
            return fromEmail.Trim();
        }

        private static IEnumerable<string> ParseRecipientEmails(IEnumerable<string> recipientEmails)
        {
            return recipientEmails
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .SelectMany(email => email.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(email => email.Trim())
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private string GenerateUnsubscribeToken(int clientId)
        {
            using (var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes("unsubscribe-token-key")))
            {
                var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes($"{clientId}:{DateTime.UtcNow:yyyyMMdd}"));
                return Convert.ToBase64String(hash);
            }
        }
    }
}
