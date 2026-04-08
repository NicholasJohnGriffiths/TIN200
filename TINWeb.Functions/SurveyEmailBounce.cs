using System.Net;
using System.Text.Json;
using Azure;
using Azure.Communication.Email;
using Azure.Messaging.EventGrid;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace TINWeb.Functions;

public class SurveyEmailBounce
{
    private static readonly HashSet<string> BounceStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Bounced",
        "Failed",
        "Suppressed",
        "Dropped",
        "Quarantined",
        "FilteredSpam",
        "Invalid"
    };

    private readonly ILogger<SurveyEmailBounce> _logger;
    private readonly string _sqlConnectionString;
    private readonly string? _emailConnectionString;
    private readonly string? _fromEmail;

    public SurveyEmailBounce(ILogger<SurveyEmailBounce> logger)
    {
        _logger = logger;
        _sqlConnectionString = Environment.GetEnvironmentVariable("SqlConnectionString")
            ?? throw new InvalidOperationException("Missing app setting: SqlConnectionString");
        _emailConnectionString = Environment.GetEnvironmentVariable("AzureCommunicationEmail__ConnectionString")
            ?? Environment.GetEnvironmentVariable("AzureCommunicationConnectionString");
        _fromEmail = Environment.GetEnvironmentVariable("AzureCommunicationEmail__FromEmail")
            ?? Environment.GetEnvironmentVariable("AzureCommunicationFromEmail");
    }

    [Function("SurveyEmailBounce")]
    public async Task Run([EventGridTrigger] EventGridEvent eventGridEvent)
    {
        var eventType = eventGridEvent.EventType ?? string.Empty;

        using var json = JsonDocument.Parse(eventGridEvent.Data.ToString());
        var data = json.RootElement;

        var status = FirstNonEmpty(
            GetString(data, "status"),
            GetString(data, "deliveryStatus"),
            GetString(data, "eventType"));

        if (!IsBounceLikeEvent(eventType, status))
        {
            _logger.LogInformation("Ignoring non-bounce event. EventType={EventType}, Status={Status}", eventType, status);
            return;
        }

        var recipientEmail = FirstNonEmpty(
            GetString(data, "recipient"),
            GetString(data, "recipientAddress"),
            GetString(data, "recipientTo"),
            GetString(data, "to"),
            GetString(data, "emailAddress"),
            GetString(data, "email"));

        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            _logger.LogWarning("Bounce event received with no recipient email. EventType={EventType}, Data={EventData}", eventType, eventGridEvent.Data.ToString());
            return;
        }

        var normalizedEmail = recipientEmail.Trim().ToLowerInvariant();
        var reason = FirstNonEmpty(
            GetNestedString(data, "deliveryStatusDetails", "statusMessage"),
            GetString(data, "statusDetails"),
            GetString(data, "diagnosticCode"),
            GetString(data, "diagnosticInformation"),
            GetString(data, "errorMessage"),
            GetString(data, "smtpResponse"),
            "No additional delivery details were provided.");

        var messageId = FirstNonEmpty(
            GetString(data, "messageId"),
            GetString(data, "correlationId"));

        var eventId = string.IsNullOrWhiteSpace(eventGridEvent.Id)
            ? Guid.NewGuid().ToString("N")
            : eventGridEvent.Id;

        await using var conn = new SqlConnection(_sqlConnectionString);
        await conn.OpenAsync();

        var survey = await GetCurrentSurveyAsync(conn);
        if (survey is null)
        {
            _logger.LogWarning("No current survey found.");
            return;
        }

        var adminEmail = await GetAdminEmailAsync(conn);

        var companies = await GetCompaniesByEmailAsync(conn, normalizedEmail);
        if (companies.Count == 0)
        {
            _logger.LogWarning("No company found for bounced email {Email}", recipientEmail);
            return;
        }

        foreach (var company in companies)
        {
            var saved = false;
            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();

            try
            {
                var companySurveyId = await EnsureCompanySurveyAsync(conn, tx, company.Id, survey.Value.SurveyId);

                var duplicateNote = await BounceNoteExistsAsync(conn, tx, companySurveyId, eventId);
                if (duplicateNote)
                {
                    _logger.LogInformation("Duplicate bounce ignored for CompanySurveyId={CompanySurveyId}, EventId={EventId}", companySurveyId, eventId);
                    await tx.RollbackAsync();
                    continue;
                }

                var noteText =
                    $"Survey email bounced back for {recipientEmail}. " +
                    $"Status: {status ?? "Bounced"}. " +
                    $"Reason: {reason}. " +
                    $"{(string.IsNullOrWhiteSpace(messageId) ? string.Empty : $"Message ID: {messageId}. ")}" +
                    $"Event ID: {eventId}.";

                await InsertCompanySurveyNoteAsync(conn, tx, companySurveyId, noteText);

                var taskTitle = Truncate($"Survey email bounced - {company.CompanyName}", 255);
                var taskDescription =
                    $"The survey email to {recipientEmail} for {company.CompanyName} (Company ID {company.Id}) " +
                    $"bounced back for survey year {survey.Value.FinancialYear}. " +
                    $"Status: {status ?? "Bounced"}. Reason: {reason}. Event ID {eventId}.";

                var duplicateTask = await ActiveTaskExistsAsync(conn, tx, taskTitle, eventId);
                if (!duplicateTask)
                {
                    await InsertTaskAsync(conn, tx, taskTitle, taskDescription);
                }

                await tx.CommitAsync();
                saved = true;
            }
            catch (Exception ex)
            {
                if (tx.Connection != null)
                {
                    await tx.RollbackAsync();
                }

                _logger.LogError(ex, "Failed processing bounce for {Email} / CompanyId={CompanyId}", recipientEmail, company.Id);
            }

            if (!saved)
            {
                continue;
            }

            _logger.LogInformation("Recorded bounce for {Email} / CompanyId={CompanyId}", recipientEmail, company.Id);

            await SendAdminNotificationIfConfiguredAsync(
                adminEmail,
                company.CompanyName,
                survey.Value.FinancialYear,
                recipientEmail,
                status ?? "Bounced",
                reason,
                messageId,
                eventId);
        }
    }

    private static async Task<(int SurveyId, int FinancialYear)?> GetCurrentSurveyAsync(SqlConnection conn)
    {
        const string sql = """
            SELECT TOP 1 Id, FinancialYear
            FROM dbo.Survey
            WHERE CurrentSurvey = 1
            ORDER BY FinancialYear DESC, Id DESC;
            """;

        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return (reader.GetInt32(0), reader.GetInt32(1));
    }

    private static async Task<string?> GetAdminEmailAsync(SqlConnection conn)
    {
        const string sql = """
            SELECT TOP 1 LTRIM(RTRIM(AdminEmail))
            FROM dbo.Config
            WHERE Id = 1
              AND AdminEmail IS NOT NULL
              AND LTRIM(RTRIM(AdminEmail)) <> '';
            """;

        await using var cmd = new SqlCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync();
        return result == null || result == DBNull.Value ? null : Convert.ToString(result)?.Trim();
    }

    private static async Task<List<(int Id, string CompanyName)>> GetCompaniesByEmailAsync(SqlConnection conn, string normalizedEmail)
    {
        const string sql = """
            SELECT Id,
                   ISNULL(NULLIF(LTRIM(RTRIM(CompanyName)), ''), CONCAT('Company ', Id)) AS CompanyName
            FROM dbo.Company
            WHERE Email IS NOT NULL
              AND LOWER(LTRIM(RTRIM(Email))) = @Email;
            """;

        var results = new List<(int, string)>();

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Email", normalizedEmail);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add((reader.GetInt32(0), reader.GetString(1)));
        }

        return results;
    }

    private static async Task<int> EnsureCompanySurveyAsync(SqlConnection conn, SqlTransaction tx, int companyId, int surveyId)
    {
        const string sql = """
            DECLARE @CompanySurveyId int;

            SELECT TOP 1 @CompanySurveyId = Id
            FROM dbo.CompanySurvey
            WHERE CompanyId = @CompanyId AND SurveyId = @SurveyId;

            IF @CompanySurveyId IS NULL
            BEGIN
                INSERT INTO dbo.CompanySurvey
                    (CompanyId, SurveyId, Saved, Submitted, Requested, Locked, Estimate, RequestedDate)
                VALUES
                    (@CompanyId, @SurveyId, 0, 0, 1, 0, 0, GETDATE());

                SET @CompanySurveyId = CAST(SCOPE_IDENTITY() AS int);
            END

            SELECT @CompanySurveyId;
            """;

        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("@CompanyId", companyId);
        cmd.Parameters.AddWithValue("@SurveyId", surveyId);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private static async Task<bool> BounceNoteExistsAsync(SqlConnection conn, SqlTransaction tx, int companySurveyId, string eventId)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.CompanySurveyNotes
            WHERE CompanySurveyId = @CompanySurveyId
              AND Notes LIKE '%' + @EventId + '%';
            """;

        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("@CompanySurveyId", companySurveyId);
        cmd.Parameters.AddWithValue("@EventId", eventId);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    private static async Task InsertCompanySurveyNoteAsync(SqlConnection conn, SqlTransaction tx, int companySurveyId, string notes)
    {
        const string sql = """
            INSERT INTO dbo.CompanySurveyNotes (CompanySurveyId, NoteDateTime, [User], Notes)
            VALUES (@CompanySurveyId, GETDATE(), 'System', @Notes);
            """;

        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("@CompanySurveyId", companySurveyId);
        cmd.Parameters.AddWithValue("@Notes", notes);

        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<bool> ActiveTaskExistsAsync(SqlConnection conn, SqlTransaction tx, string title, string eventId)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.Task
            WHERE ISNULL(Status, 0) = 0
              AND Title = @Title
              AND Description LIKE '%' + @EventId + '%';
            """;

        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("@Title", title);
        cmd.Parameters.AddWithValue("@EventId", eventId);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    private static async Task InsertTaskAsync(SqlConnection conn, SqlTransaction tx, string title, string description)
    {
        const string sql = """
            INSERT INTO dbo.Task
                (CreatedBy, CreatedDatetime, Status, Title, Description, StatusChangeDatetime)
            VALUES
                ('System', GETDATE(), 0, @Title, @Description, GETDATE());
            """;

        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("@Title", title);
        cmd.Parameters.AddWithValue("@Description", description);

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task SendAdminNotificationIfConfiguredAsync(
        string? adminEmail,
        string companyName,
        int surveyYear,
        string bouncedRecipientEmail,
        string status,
        string? reason,
        string? messageId,
        string eventId)
    {
        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_emailConnectionString) || string.IsNullOrWhiteSpace(_fromEmail))
        {
            _logger.LogInformation("Bounce notification email skipped because Azure Communication Email settings are not configured in the Function App.");
            return;
        }

        var safeCompanyName = string.IsNullOrWhiteSpace(companyName) ? "Unknown company" : companyName.Trim();
        var safeReason = string.IsNullOrWhiteSpace(reason)
            ? "No additional delivery details were provided."
            : reason.Trim();
        var subject = Truncate($"TIN200 survey email bounced - {safeCompanyName}", 255);

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

        try
        {
            var recipients = adminEmail
                .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(email => email.Trim())
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(email => new EmailAddress(email))
                .ToList();

            if (recipients.Count == 0)
            {
                _logger.LogInformation("Bounce notification email skipped because Config.AdminEmail does not contain any valid recipient addresses.");
                return;
            }

            var emailClient = new EmailClient(_emailConnectionString);
            var emailMessage = new EmailMessage(
                senderAddress: _fromEmail,
                content: new EmailContent(subject)
                {
                    PlainText = plainTextBody,
                    Html = htmlBody
                },
                recipients: new EmailRecipients(recipients));

            var operation = await emailClient.SendAsync(WaitUntil.Completed, emailMessage);
            if (operation.HasCompleted && operation.Value.Status != EmailSendStatus.Succeeded)
            {
                _logger.LogWarning("Bounce notification email send completed with non-success status {Status} for admin {AdminEmail}.", operation.Value.Status, adminEmail);
            }
        }
        catch (RequestFailedException ex)
        {
            _logger.LogWarning(ex, "Failed sending bounce notification email to {AdminEmail}.", adminEmail);
        }
    }

    private static bool IsBounceLikeEvent(string? eventType, string? status)
    {
        if (!string.IsNullOrWhiteSpace(status) && BounceStatuses.Contains(status.Trim()))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(eventType) &&
               (eventType.Contains("Bounce", StringComparison.OrdinalIgnoreCase)
                || eventType.Contains("Failed", StringComparison.OrdinalIgnoreCase)
                || eventType.Contains("Suppressed", StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => prop.Value.GetRawText()
                };
            }
        }

        return null;
    }

    private static string? GetNestedString(JsonElement element, string propertyName, string nestedPropertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return GetString(prop.Value, nestedPropertyName);
            }
        }

        return null;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static string Truncate(string value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];
}
