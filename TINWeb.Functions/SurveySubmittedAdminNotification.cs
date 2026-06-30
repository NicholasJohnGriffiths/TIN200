using System.Net;
using System.Text.Json;
using Azure;
using Azure.Communication.Email;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TINWeb.Functions;

public class SurveySubmittedAdminNotification
{
    private readonly ILogger<SurveySubmittedAdminNotification> _logger;
    private readonly string? _emailConnectionString;
    private readonly string? _fromEmail;
    private readonly string? _fromName;

    public SurveySubmittedAdminNotification(ILogger<SurveySubmittedAdminNotification> logger)
    {
        _logger = logger;
        _emailConnectionString = Environment.GetEnvironmentVariable("AzureCommunicationEmail__ConnectionString")
            ?? Environment.GetEnvironmentVariable("AzureCommunicationConnectionString");
        _fromEmail = Environment.GetEnvironmentVariable("AzureCommunicationEmail__FromEmail")
            ?? Environment.GetEnvironmentVariable("AzureCommunicationFromEmail");
        _fromName = Environment.GetEnvironmentVariable("AzureCommunicationEmail__FromName")
            ?? Environment.GetEnvironmentVariable("AzureCommunicationFromName")
            ?? "TIN Survey";
    }

    [Function("SurveySubmittedAdminNotification")]
    public async Task Run(
        [QueueTrigger("%SurveySubmittedNotificationQueueName%", Connection = "SurveySubmittedNotificationQueueConnectionString")]
        string queueMessage)
    {
        SurveySubmittedNotificationMessage? message;

        try
        {
            message = JsonSerializer.Deserialize<SurveySubmittedNotificationMessage>(
                queueMessage,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ignoring invalid survey submitted notification message payload.");
            return;
        }

        if (message == null || string.IsNullOrWhiteSpace(message.AdminEmail))
        {
            _logger.LogWarning("Ignoring survey submitted notification message with missing admin email.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_emailConnectionString) || string.IsNullOrWhiteSpace(_fromEmail))
        {
            _logger.LogWarning("Survey submitted admin notification skipped because Azure Communication Email settings are not configured.");
            return;
        }

        var safeCompanyName = string.IsNullOrWhiteSpace(message.CompanyName)
            ? "Unknown company"
            : message.CompanyName.Trim();
        var safeSubmitterEmail = string.IsNullOrWhiteSpace(message.SubmitterEmail)
            ? "Not available"
            : message.SubmitterEmail.Trim();
        var submittedAtUtc = message.SubmittedAtUtc;
        if (submittedAtUtc == default)
        {
            submittedAtUtc = DateTime.UtcNow;
        }

        var subject = Truncate($"TIN200 survey submitted - {safeCompanyName}", 255);

        var plainTextBody = $@"A TIN200 survey has been submitted.

Company: {safeCompanyName}
Survey year: {message.SurveyYear}
Submitted at (UTC): {submittedAtUtc:yyyy-MM-dd HH:mm:ss}
Submitter email: {safeSubmitterEmail}

Please review the submitted survey and follow up if needed.";

        var htmlBody = $@"<p>A <strong>TIN200</strong> survey has been submitted.</p>
<ul>
    <li><strong>Company:</strong> {WebUtility.HtmlEncode(safeCompanyName)}</li>
    <li><strong>Survey year:</strong> {message.SurveyYear}</li>
    <li><strong>Submitted at (UTC):</strong> {WebUtility.HtmlEncode(submittedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"))}</li>
    <li><strong>Submitter email:</strong> {WebUtility.HtmlEncode(safeSubmitterEmail)}</li>
</ul>
<p>Please review the submitted survey and follow up if needed.</p>";

        var recipients = message.AdminEmail
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(email => email.Trim())
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(email => new EmailAddress(email))
            .ToList();

        if (recipients.Count == 0)
        {
            _logger.LogWarning("Survey submitted notification message contains no valid admin recipients.");
            return;
        }

        var emailClient = new EmailClient(_emailConnectionString);
        var emailMessage = new EmailMessage(
            senderAddress: BuildSenderAddress(_fromEmail, _fromName),
            content: new EmailContent(subject)
            {
                PlainText = plainTextBody,
                Html = htmlBody
            },
            recipients: new EmailRecipients(recipients));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            var operation = await emailClient.SendAsync(WaitUntil.Started, emailMessage, cts.Token);
            _logger.LogInformation("Survey submitted admin notification accepted for delivery. OperationId {OperationId}.", operation.Id);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogWarning(ex, "Survey submitted admin notification send failed.");
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Timed out waiting for ACS to accept survey submitted admin notification.");
            throw;
        }
    }

    private static string BuildSenderAddress(string? fromEmail, string? fromName)
    {
        return (fromEmail ?? string.Empty).Trim();
    }

    private static string Truncate(string value, int maxLength)
    {
        return string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];
    }

    private sealed class SurveySubmittedNotificationMessage
    {
        public string AdminEmail { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        public int SurveyYear { get; set; }

        public DateTime SubmittedAtUtc { get; set; }

        public string? SubmitterEmail { get; set; }
    }
}