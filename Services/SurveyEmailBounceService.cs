using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Models;

namespace TINWeb.Services
{
    public class SurveyEmailBounceService
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

        private readonly ApplicationDbContext _context;
        private readonly TaskService _taskService;
        private readonly ISurveyEmailService _surveyEmailService;
        private readonly ILogger<SurveyEmailBounceService> _logger;

        public SurveyEmailBounceService(
            ApplicationDbContext context,
            TaskService taskService,
            ISurveyEmailService surveyEmailService,
            ILogger<SurveyEmailBounceService> logger)
        {
            _context = context;
            _taskService = taskService;
            _surveyEmailService = surveyEmailService;
            _logger = logger;
        }

        public object? TryBuildSubscriptionValidationResponse(JsonDocument payload)
        {
            if (payload.RootElement.ValueKind != JsonValueKind.Array || payload.RootElement.GetArrayLength() == 0)
            {
                return null;
            }

            var firstEvent = payload.RootElement[0];
            var eventType = GetString(firstEvent, "eventType");
            if (!string.Equals(eventType, "Microsoft.EventGrid.SubscriptionValidationEvent", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!TryGetPropertyIgnoreCase(firstEvent, "data", out var dataElement))
            {
                return null;
            }

            var validationCode = GetString(dataElement, "validationCode");
            return string.IsNullOrWhiteSpace(validationCode)
                ? null
                : new { validationResponse = validationCode };
        }

        public async Task<int> ProcessEventGridEventsAsync(JsonDocument payload)
        {
            if (payload.RootElement.ValueKind != JsonValueKind.Array)
            {
                return 0;
            }

            var handledCount = 0;

            foreach (var eventElement in payload.RootElement.EnumerateArray())
            {
                if (await TryHandleBounceEventAsync(eventElement))
                {
                    handledCount++;
                }
            }

            return handledCount;
        }

        private async Task<bool> TryHandleBounceEventAsync(JsonElement eventElement)
        {
            var eventType = GetString(eventElement, "eventType");
            if (!TryGetPropertyIgnoreCase(eventElement, "data", out var dataElement))
            {
                return false;
            }

            var status = FirstNonEmpty(
                GetString(dataElement, "status"),
                GetString(dataElement, "deliveryStatus"),
                GetString(dataElement, "eventType"));

            if (!IsBounceLikeEvent(eventType, status))
            {
                return false;
            }

            var recipientEmail = FirstNonEmpty(
                GetString(dataElement, "recipient"),
                GetString(dataElement, "recipientAddress"),
                GetString(dataElement, "recipientTo"),
                GetString(dataElement, "to"),
                GetString(dataElement, "emailAddress"),
                GetString(dataElement, "email"));

            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                _logger.LogWarning("Email bounce webhook received without a recipient address. EventType={EventType}, Data={EventData}", eventType, dataElement.GetRawText());
                return false;
            }

            var normalizedEmail = recipientEmail.Trim().ToLowerInvariant();
            var reason = FirstNonEmpty(
                GetNestedString(dataElement, "deliveryStatusDetails", "statusMessage"),
                GetString(dataElement, "statusDetails"),
                GetString(dataElement, "diagnosticCode"),
                GetString(dataElement, "diagnosticInformation"),
                GetString(dataElement, "errorMessage"),
                GetString(dataElement, "smtpResponse"),
                "No additional delivery details were provided.");
            var messageId = FirstNonEmpty(
                GetString(dataElement, "messageId"),
                GetString(dataElement, "correlationId"));
            var eventId = GetString(eventElement, "id");

            var currentSurvey = await _context.Survey
                .Where(s => s.CurrentSurvey)
                .OrderByDescending(s => s.FinancialYear)
                .ThenByDescending(s => s.Id)
                .FirstOrDefaultAsync();

            if (currentSurvey == null)
            {
                _logger.LogWarning("Email bounce received for {RecipientEmail}, but no current survey is configured.", recipientEmail);
                return false;
            }

            var adminEmail = await _context.AppConfig
                .AsNoTracking()
                .Where(c => c.Id == 1)
                .Select(c => c.AdminEmail)
                .FirstOrDefaultAsync();

            adminEmail = string.IsNullOrWhiteSpace(adminEmail) ? null : adminEmail.Trim();

            var matchingCompanies = await _context.Tin200
                .Where(c =>
                    (c.ContactEmail != null && c.ContactEmail.Trim().ToLower() == normalizedEmail)
                    || (c.Email != null && c.Email.Trim().ToLower() == normalizedEmail))
                .Select(c => new { c.Id, c.CompanyName })
                .ToListAsync();

            if (!matchingCompanies.Any())
            {
                _logger.LogWarning("Email bounce received for {RecipientEmail}, but no matching company was found.", recipientEmail);
                return false;
            }

            var recordedAny = false;

            foreach (var company in matchingCompanies)
            {
                var companySurvey = await EnsureCompanySurveyAsync(company.Id, currentSurvey.Id);

                if (await BounceNoteAlreadyExistsAsync(companySurvey.Id, eventId, recipientEmail, status ?? "Bounced"))
                {
                    _logger.LogInformation(
                        "Skipping duplicate bounce note for company survey {CompanySurveyId} and event {EventId}.",
                        companySurvey.Id,
                        eventId ?? "(none)");
                    continue;
                }

                var safeCompanyName = company.CompanyName ?? $"Company {company.Id}";

                _context.CompanySurveyNotes.Add(new CompanySurveyNote
                {
                    CompanySurveyId = companySurvey.Id,
                    NoteDateTime = DateTime.Now,
                    User = "System",
                    Notes = BuildBounceNoteText(recipientEmail, status ?? "Bounced", reason, messageId, eventId)
                });

                await _context.SaveChangesAsync();

                await _taskService.CreateSurveyEmailBounceTaskAsync(
                    company.Id,
                    safeCompanyName,
                    currentSurvey.FinancialYear,
                    recipientEmail,
                    status ?? "Bounced",
                    reason,
                    eventId,
                    "System");

                await NotifyAdminIfConfiguredAsync(
                    adminEmail,
                    safeCompanyName,
                    currentSurvey.FinancialYear,
                    recipientEmail,
                    status ?? "Bounced",
                    reason,
                    messageId,
                    eventId);

                recordedAny = true;
            }

            return recordedAny;
        }

        private async Task<CompanySurvey> EnsureCompanySurveyAsync(int companyId, int surveyId)
        {
            var companySurvey = await _context.CompanySurvey
                .FirstOrDefaultAsync(cs => cs.CompanyId == companyId && cs.SurveyId == surveyId);

            if (companySurvey != null)
            {
                return companySurvey;
            }

            companySurvey = new CompanySurvey
            {
                CompanyId = companyId,
                SurveyId = surveyId,
                Saved = false,
                Submitted = false,
                Requested = true,
                Locked = false,
                Estimate = false,
                RequestedDate = DateTime.Now
            };

            _context.CompanySurvey.Add(companySurvey);
            await _context.SaveChangesAsync();
            return companySurvey;
        }

        private async Task<bool> BounceNoteAlreadyExistsAsync(int companySurveyId, string? eventId, string recipientEmail, string status)
        {
            var noteQuery = _context.CompanySurveyNotes.Where(n => n.CompanySurveyId == companySurveyId);

            if (!string.IsNullOrWhiteSpace(eventId))
            {
                return await noteQuery.AnyAsync(n => n.Notes != null && n.Notes.Contains($"Event ID: {eventId}"));
            }

            var recentCutoff = DateTime.Now.AddDays(-1);
            return await noteQuery.AnyAsync(n =>
                n.NoteDateTime >= recentCutoff
                && n.Notes != null
                && n.Notes.Contains("Survey email bounced back")
                && n.Notes.Contains(recipientEmail)
                && n.Notes.Contains($"Status: {status}"));
        }

        private async Task NotifyAdminIfConfiguredAsync(
            string? adminEmail,
            string companyName,
            int surveyYear,
            string recipientEmail,
            string status,
            string? reason,
            string? messageId,
            string? eventId)
        {
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                return;
            }

            try
            {
                await _surveyEmailService.SendBounceNotificationAsync(
                    adminEmail,
                    companyName,
                    surveyYear,
                    recipientEmail,
                    status,
                    reason,
                    messageId,
                    eventId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed sending bounce notification email to admin {AdminEmail} for company {CompanyName}.", adminEmail, companyName);
            }
        }

        private static bool IsBounceLikeEvent(string? eventType, string? status)
        {
            if (!string.IsNullOrWhiteSpace(status) && BounceStatuses.Contains(status.Trim()))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(eventType)
                && (eventType.Contains("Bounce", StringComparison.OrdinalIgnoreCase)
                    || eventType.Contains("Failed", StringComparison.OrdinalIgnoreCase)
                    || eventType.Contains("Suppressed", StringComparison.OrdinalIgnoreCase));
        }

        private static string BuildBounceNoteText(string recipientEmail, string status, string? reason, string? messageId, string? eventId)
        {
            var note = $"Survey email bounced back for {recipientEmail}. Status: {status}. Reason: {reason}";

            if (!string.IsNullOrWhiteSpace(messageId))
            {
                note += $" Message ID: {messageId}.";
            }

            if (!string.IsNullOrWhiteSpace(eventId))
            {
                note += $" Event ID: {eventId}.";
            }

            return note;
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
        }

        private static string? GetString(JsonElement element, string propertyName)
        {
            if (!TryGetPropertyIgnoreCase(element, propertyName, out var valueElement))
            {
                return null;
            }

            return valueElement.ValueKind switch
            {
                JsonValueKind.String => valueElement.GetString(),
                JsonValueKind.Number => valueElement.GetRawText(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                JsonValueKind.Array => string.Join(", ", valueElement.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.GetRawText())),
                _ => valueElement.GetRawText()
            };
        }

        private static string? GetNestedString(JsonElement element, string propertyName, string nestedPropertyName)
        {
            if (!TryGetPropertyIgnoreCase(element, propertyName, out var nestedElement))
            {
                return null;
            }

            return GetString(nestedElement, nestedPropertyName);
        }

        private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        value = property.Value;
                        return true;
                    }
                }
            }

            value = default;
            return false;
        }
    }
}