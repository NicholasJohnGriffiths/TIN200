using Azure.Storage.Queues;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TINWeb.Data;
using TINWeb.Services;

namespace TINWeb.Pages.Config;

[Authorize(Policy = "AdminOnly")]
public class NotificationStatusModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly SurveySubmittedNotificationQueueSettings _queueSettings;

    public NotificationStatusModel(
        ApplicationDbContext context,
        IOptions<SurveySubmittedNotificationQueueSettings> queueOptions)
    {
        _context = context;
        _queueSettings = queueOptions.Value;
    }

    public string AdminEmail { get; private set; } = string.Empty;

    public bool QueueEnabled => _queueSettings.Enabled;

    public string QueueName => _queueSettings.QueueName ?? string.Empty;

    public int? PendingQueueCount { get; private set; }

    public int? PoisonQueueCount { get; private set; }

    public DateTime? LastSubmitAt { get; private set; }

    public string LastSubmitNote { get; private set; } = string.Empty;

    public string QueueStatusMessage { get; private set; } = string.Empty;

    public async Task OnGetAsync()
    {
        AdminEmail = await _context.AppConfig
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .Select(c => c.AdminEmail)
            .FirstOrDefaultAsync() ?? string.Empty;

        var latestSubmitNote = await _context.CompanySurveyNotes
            .AsNoTracking()
            .Where(n => n.User == "Survey Receiver" && n.Notes != null && n.Notes.Contains("Submit Final"))
            .OrderByDescending(n => n.NoteDateTime)
            .Select(n => new { n.NoteDateTime, n.Notes })
            .FirstOrDefaultAsync();

        if (latestSubmitNote != null)
        {
            LastSubmitAt = latestSubmitNote.NoteDateTime;
            LastSubmitNote = latestSubmitNote.Notes ?? string.Empty;
        }

        if (!QueueEnabled)
        {
            QueueStatusMessage = "Survey submitted queue is disabled in app settings.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_queueSettings.ConnectionString)
            || string.IsNullOrWhiteSpace(_queueSettings.QueueName))
        {
            QueueStatusMessage = "Queue is enabled but connection string or queue name is missing.";
            return;
        }

        try
        {
            var queueClient = new QueueClient(_queueSettings.ConnectionString, _queueSettings.QueueName);
            var queueProps = await queueClient.GetPropertiesAsync();
            PendingQueueCount = queueProps.Value.ApproximateMessagesCount;

            var poisonQueueClient = new QueueClient(_queueSettings.ConnectionString, _queueSettings.QueueName + "-poison");
            var poisonExists = await poisonQueueClient.ExistsAsync();
            if (poisonExists.Value)
            {
                var poisonProps = await poisonQueueClient.GetPropertiesAsync();
                PoisonQueueCount = poisonProps.Value.ApproximateMessagesCount;
            }
            else
            {
                PoisonQueueCount = 0;
            }

            QueueStatusMessage = "Queue status loaded successfully.";
        }
        catch (Exception ex)
        {
            QueueStatusMessage = $"Failed to read queue status: {ex.Message}";
        }
    }
}
