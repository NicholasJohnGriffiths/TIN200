using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Data;
using TINWeb.Services;

namespace TINWeb.Pages.Survey
{
    public class EmailEventsModel : PageModel
    {
        private static readonly string[] UnsuccessfulEventTerms =
        {
            "bounce",
            "bounced",
            "failed",
            "failure",
            "suppressed",
            "dropped",
            "quarantined",
            "filteredspam",
            "invalid"
        };

        private static readonly string[] BouncedEventTerms =
        {
            "bounce",
            "bounced"
        };

        private readonly ApplicationDbContext _context;
        private readonly EmailEventsService _emailEventsService;

        public EmailEventsModel(ApplicationDbContext context, EmailEventsService emailEventsService)
        {
            _context = context;
            _emailEventsService = emailEventsService;
        }

        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string EventStatus { get; set; } = "all";

        public List<EmailEventRow> Events { get; private set; } = new();

        public List<LocalEmailEventRow> LocalEvents { get; private set; } = new();

        public string? ErrorMessage { get; private set; }

        public bool IsConfigured => _emailEventsService.IsEnabled;

        public DateTime EffectiveStartDate { get; private set; }

        public DateTime EffectiveEndDate { get; private set; }

        public string EffectiveEventStatus { get; private set; } = "all";

        public async Task OnGetAsync()
        {
            var utcToday = DateTime.UtcNow.Date;
            EffectiveEndDate = (EndDate ?? utcToday).Date;
            EffectiveStartDate = (StartDate ?? utcToday.AddMonths(-1)).Date;
            EffectiveEventStatus = NormalizeEventStatus(EventStatus);

            if (EffectiveStartDate > EffectiveEndDate)
            {
                ErrorMessage = "Start date must be earlier than or equal to end date.";
                return;
            }

            var startUtc = DateTime.SpecifyKind(EffectiveStartDate, DateTimeKind.Utc);
            var endUtcExclusive = DateTime.SpecifyKind(EffectiveEndDate.AddDays(1), DateTimeKind.Utc);

            var result = await _emailEventsService.QueryAsync(startUtc, endUtcExclusive);
            Events = ApplyAzureStatusFilter(result.Rows, EffectiveEventStatus);
            LocalEvents = ApplyLocalStatusFilter(
                await LoadLocalEventsAsync(EffectiveStartDate, EffectiveEndDate.AddDays(1)),
                EffectiveEventStatus);

            if (!string.IsNullOrWhiteSpace(result.Error))
            {
                ErrorMessage = result.Error;
            }
        }

        private static string NormalizeEventStatus(string? eventStatus)
        {
            return string.Equals(eventStatus, "successful", StringComparison.OrdinalIgnoreCase)
                ? "successful"
                : string.Equals(eventStatus, "unsuccessful", StringComparison.OrdinalIgnoreCase)
                    ? "unsuccessful"
                    : "all";
        }

        private static List<EmailEventRow> ApplyAzureStatusFilter(List<EmailEventRow> rows, string eventStatus)
        {
            return eventStatus switch
            {
                "successful" => rows.Where(IsAzureSuccessfulEvent).ToList(),
                "unsuccessful" => rows.Where(IsAzureUnsuccessfulEvent).ToList(),
                _ => rows
            };
        }

        private static List<LocalEmailEventRow> ApplyLocalStatusFilter(List<LocalEmailEventRow> rows, string eventStatus)
        {
            return eventStatus switch
            {
                "unsuccessful" => new List<LocalEmailEventRow>(),
                _ => rows
            };
        }

        private static bool IsAzureSuccessfulEvent(EmailEventRow row)
        {
            if (IsAzureUnsuccessfulEvent(row))
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(row.EventType)
                || !string.IsNullOrWhiteSpace(row.MessageId)
                || !string.IsNullOrWhiteSpace(row.Recipient);
        }

        private static bool IsAzureUnsuccessfulEvent(EmailEventRow row)
        {
            var combinedText = string.Join(" ", new[] { row.EventType, row.Details, row.Raw }
                .Where(value => !string.IsNullOrWhiteSpace(value)));

            return UnsuccessfulEventTerms.Any(term => combinedText.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsAzureBouncedEvent(EmailEventRow row)
        {
            var combinedText = string.Join(" ", new[] { row.EventType, row.Details, row.Raw }
                .Where(value => !string.IsNullOrWhiteSpace(value)));

            return BouncedEventTerms.Any(term => combinedText.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<List<LocalEmailEventRow>> LoadLocalEventsAsync(DateTime startLocalInclusive, DateTime endLocalExclusive)
        {
            var rows = await (
                from note in _context.CompanySurveyNotes.AsNoTracking()
                join companySurvey in _context.CompanySurvey.AsNoTracking() on note.CompanySurveyId equals companySurvey.Id
                join company in _context.Tin200.AsNoTracking() on companySurvey.CompanyId equals company.Id
                let noteText = note.Notes ?? string.Empty
                where note.NoteDateTime >= startLocalInclusive
                    && note.NoteDateTime < endLocalExclusive
                    && (noteText.StartsWith("Survey email sent to ")
                        || noteText.StartsWith("Survey reminder email sent to "))
                orderby note.NoteDateTime descending
                select new LocalEmailEventRow
                {
                    TimestampLocal = note.NoteDateTime,
                    CompanyName = company.CompanyName,
                    EventType = noteText.StartsWith("Survey reminder email sent to ")
                        ? "Survey reminder sent"
                        : "Survey email sent",
                    Recipient = ExtractRecipient(noteText),
                    Details = noteText
                })
                .ToListAsync();

            return rows;
        }

        private static string? ExtractRecipient(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
            {
                return null;
            }

            var toIndex = notes.IndexOf(" to ", StringComparison.OrdinalIgnoreCase);
            if (toIndex < 0)
            {
                return null;
            }

            var startIndex = toIndex + 4;
            var endIndex = notes.IndexOf(". Link:", startIndex, StringComparison.OrdinalIgnoreCase);
            if (endIndex < 0)
            {
                endIndex = notes.Length;
            }

            var recipient = notes[startIndex..endIndex].Trim();
            return string.IsNullOrWhiteSpace(recipient) ? null : recipient;
        }

        public class LocalEmailEventRow
        {
            public DateTime TimestampLocal { get; set; }

            public string? CompanyName { get; set; }

            public string? EventType { get; set; }

            public string? Recipient { get; set; }

            public string? Details { get; set; }
        }
    }
}