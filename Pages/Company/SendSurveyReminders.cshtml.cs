using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TINWeb.Data;
using TINWeb.Services;

namespace TINWeb.Pages.Company
{
    public class SendSurveyRemindersModel : PageModel
    {
        private readonly CompanyService _companyService;
        private readonly ISurveyEmailService _surveyEmailService;
        private readonly ISurveyLinkTokenService _surveyLinkTokenService;
        private readonly SurveyLinkSettings _surveyLinkSettings;
        private readonly ApplicationDbContext _context;

        public SendSurveyRemindersModel(
            CompanyService companyService,
            ISurveyEmailService surveyEmailService,
            ISurveyLinkTokenService surveyLinkTokenService,
            IOptions<SurveyLinkSettings> surveyLinkSettings,
            ApplicationDbContext context)
        {
            _companyService = companyService;
            _surveyEmailService = surveyEmailService;
            _surveyLinkTokenService = surveyLinkTokenService;
            _surveyLinkSettings = surveyLinkSettings.Value;
            _context = context;
        }

        [BindProperty]
        public List<int> SelectedClientIds { get; set; } = new();

        [BindProperty]
        public bool SendToAllClients { get; set; }

        [BindProperty]
        public bool EnableScheduleSettings { get; set; }

        [BindProperty]
        [DataType(DataType.DateTime)]
        public DateTime? SendStartTime { get; set; }

        [BindProperty]
        [Range(1, 10000)]
        public int SplitBulkIntoBatchesOf { get; set; } = 50;

        [BindProperty]
        [Range(typeof(decimal), "0", "168")]
        public decimal BreakBetweenSendingGroupsHours { get; set; }

        [BindProperty]
        [Range(0, 3600)]
        public int IntervalBetweenEachEmailSendSeconds { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool IncludeTestCompanies { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SelectedSentYear { get; set; }

        public List<int> AvailableSentYears { get; set; } = new();
        public List<ReminderClientRow> AvailableClients { get; set; } = new();

        [TempData]
        public string? StatusMessage { get; set; }

        [TempData]
        public bool BulkSendSucceeded { get; set; }

        [TempData]
        public int? BulkSentCount { get; set; }

        [TempData]
        public int? BulkSkippedCount { get; set; }

        [TempData]
        public int? BulkFailedCount { get; set; }

        [TempData]
        public string? BulkLastRunAt { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadAvailableClientsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostBulkAsync()
        {
            await LoadAvailableClientsAsync();

            if (EnableScheduleSettings)
            {
                if (!SendStartTime.HasValue)
                    ModelState.AddModelError(nameof(SendStartTime), "Send Start Time is required when Schedule Settings is enabled.");
                if (SplitBulkIntoBatchesOf < 1)
                    ModelState.AddModelError(nameof(SplitBulkIntoBatchesOf), "Split bulk into batches of must be at least 1.");
                if (BreakBetweenSendingGroupsHours < 0)
                    ModelState.AddModelError(nameof(BreakBetweenSendingGroupsHours), "Break between groups must be 0 or greater.");
                if (IntervalBetweenEachEmailSendSeconds < 0)
                    ModelState.AddModelError(nameof(IntervalBetweenEachEmailSendSeconds), "Interval between each email send must be 0 or greater.");
                if (!ModelState.IsValid)
                    return Page();
            }

            var selected = SendToAllClients
                ? AvailableClients
                : AvailableClients.Where(c => SelectedClientIds.Contains(c.Id)).ToList();

            if (!selected.Any())
            {
                ModelState.AddModelError(string.Empty, "Select at least one client, or choose Send to all clients.");
                return Page();
            }

            var sentCount = 0;
            var skippedNoEmailCount = 0;
            var skippedLockedCount = 0;
            var skippedUnsubscribedCount = 0;
            var failedCount = 0;
            string? firstFailureReason = null;
            var lockedCompanyIds = await GetLockedCompanyIdsForCurrentSurveyAsync();
            var currentSurveyId = await GetCurrentOrLatestSurveyIdAsync();
            var sendQueue = new List<ReminderClientRow>();

            if (!currentSurveyId.HasValue)
            {
                ModelState.AddModelError(string.Empty, "No survey record exists. Unable to send reminder emails.");
                return Page();
            }

            foreach (var clientRow in selected)
            {
                if (clientRow.Unsubscribed) { skippedUnsubscribedCount++; continue; }
                if (string.IsNullOrWhiteSpace(clientRow.Email)) { skippedNoEmailCount++; continue; }
                if (lockedCompanyIds.Contains(clientRow.Id)) { skippedLockedCount++; continue; }
                sendQueue.Add(clientRow);
            }

            if (EnableScheduleSettings && SendStartTime.HasValue)
            {
                try
                {
                    await DelayUntilStartAsync(SendStartTime.Value, HttpContext.RequestAborted);
                }
                catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
                {
                    StatusMessage = "Bulk send was cancelled before sending started.";
                    BulkSentCount = 0;
                    BulkSkippedCount = skippedNoEmailCount + skippedLockedCount + skippedUnsubscribedCount;
                    BulkFailedCount = 0;
                    BulkLastRunAt = DateTime.Now.ToString("MMM d, yyyy h:mm tt");
                    BulkSendSucceeded = false;
                    return RedirectToPage(new { IncludeTestCompanies, SelectedSentYear });
                }
            }

            for (var index = 0; index < sendQueue.Count; index++)
            {
                var clientRow = sendQueue[index];
                var recipientEmail = clientRow.Email!;
                var surveyUrl = BuildSurveyUrl(clientRow.Id);

                try
                {
                    await _surveyEmailService.SendSurveyReminderLinkAsync(recipientEmail, surveyUrl, clientRow.CompanyName, clientRow.Id);
                    sentCount++;

                    var companySurvey = await EnsureCompanySurveyAsync(clientRow.Id, currentSurveyId.Value);
                    if (companySurvey != null)
                    {
                        var sentAtLocal = DateTime.Now;
                        var sentByUser = string.IsNullOrWhiteSpace(User?.Identity?.Name) ? "System" : User.Identity!.Name!;

                        companySurvey.SurveyLink = surveyUrl;
                        companySurvey.SurveyEmailSent = true;
                        companySurvey.SurveyEmailSentLastDate = sentAtLocal;
                        companySurvey.SurveyReminderEmailSent = true;
                        companySurvey.SurveyReminderEmailSentLastDate = sentAtLocal;
                        _context.CompanySurvey.Update(companySurvey);

                        await AddCompanySurveyNoteAsync(companySurvey.Id, sentAtLocal, sentByUser, recipientEmail, surveyUrl, isReminder: true);
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;
                    firstFailureReason ??= ex.Message;
                }

                if (EnableScheduleSettings && index < sendQueue.Count - 1)
                {
                    try
                    {
                        await ApplyScheduledDelayBetweenSendsAsync(index + 1, HttpContext.RequestAborted);
                    }
                    catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
                    {
                        if (sentCount > 0) await _context.SaveChangesAsync();

                        StatusMessage = $"Bulk send was cancelled. Sent before cancellation: {sentCount}, Skipped (no email): {skippedNoEmailCount}, Skipped (locked): {skippedLockedCount}, Skipped (unsubscribed): {skippedUnsubscribedCount}, Failed: {failedCount}.";
                        BulkSentCount = sentCount;
                        BulkSkippedCount = skippedNoEmailCount + skippedLockedCount + skippedUnsubscribedCount;
                        BulkFailedCount = failedCount;
                        BulkLastRunAt = DateTime.Now.ToString("MMM d, yyyy h:mm tt");
                        BulkSendSucceeded = false;
                        return RedirectToPage(new { IncludeTestCompanies, SelectedSentYear });
                    }
                }
            }

            if (sentCount > 0) await _context.SaveChangesAsync();

            StatusMessage = $"Bulk send complete. Sent: {sentCount}, Skipped (no email): {skippedNoEmailCount}, Skipped (locked): {skippedLockedCount}, Skipped (unsubscribed): {skippedUnsubscribedCount}, Failed: {failedCount}.";
            BulkSentCount = sentCount;
            BulkSkippedCount = skippedNoEmailCount + skippedLockedCount + skippedUnsubscribedCount;
            BulkFailedCount = failedCount;
            BulkLastRunAt = DateTime.Now.ToString("MMM d, yyyy h:mm tt");

            if (failedCount > 0)
            {
                var detail = string.IsNullOrWhiteSpace(firstFailureReason) ? string.Empty : $" First error: {firstFailureReason}";
                ModelState.AddModelError(string.Empty, $"Some emails could not be sent. Please check Azure Communication Email settings and retry.{detail}");
                return Page();
            }

            if (skippedLockedCount > 0)
            {
                ModelState.AddModelError(string.Empty, "Locked survey records were skipped.");
                return Page();
            }

            if (skippedUnsubscribedCount > 0)
            {
                ModelState.AddModelError(string.Empty, "Unsubscribed records were skipped.");
                return Page();
            }

            BulkSendSucceeded = true;
            return RedirectToPage(new { IncludeTestCompanies, SelectedSentYear });
        }

        private async Task LoadAvailableClientsAsync()
        {
            // Distinct years from SurveyEmailSentLastDate where a survey email was sent
            AvailableSentYears = await _context.CompanySurvey
                .Where(cs => (cs.SurveyEmailSent ?? false)
                    && !cs.Saved
                    && !cs.Submitted
                    && cs.SurveyEmailSentLastDate.HasValue)
                .Select(cs => cs.SurveyEmailSentLastDate!.Value.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();

            // Default to the latest year if no year explicitly selected
            if (!SelectedSentYear.HasValue && AvailableSentYears.Any())
            {
                SelectedSentYear = AvailableSentYears.First();
            }

            // Load all companies with a survey email sent in the selected year
            var surveyEmailStatusByCompanyId = await _context.CompanySurvey
                .Where(cs => (cs.SurveyEmailSent ?? false)
                    && !cs.Saved
                    && !cs.Submitted
                    && cs.SurveyEmailSentLastDate.HasValue
                    && (!SelectedSentYear.HasValue || cs.SurveyEmailSentLastDate!.Value.Year == SelectedSentYear.Value))
                .GroupBy(cs => cs.CompanyId)
                .Select(g => g.OrderByDescending(cs => cs.SurveyEmailSentLastDate).First())
                .ToDictionaryAsync(cs => cs.CompanyId, cs => cs);

            var companyIds = surveyEmailStatusByCompanyId.Keys.ToList();

            var companies = await _companyService.GetAllCompaniesAsync(null);
            companies = companies.Where(c => companyIds.Contains(c.Id)).ToList();

            if (!IncludeTestCompanies)
            {
                companies = companies.Where(c => c.Test != true).ToList();
            }

            var lockedCompanyIds = await GetLockedCompanyIdsForCurrentSurveyAsync();

            AvailableClients = companies
                .OrderBy(c => c.CompanyName)
                .ThenBy(c => c.Id)
                .Select(c =>
                {
                    surveyEmailStatusByCompanyId.TryGetValue(c.Id, out var cs);
                    var surveyLink = cs?.SurveyLink;
                    var surveyLinkExpiryUtc = GetSurveyLinkExpiryUtc(surveyLink);

                    return new ReminderClientRow
                    {
                        Id = c.Id,
                        CompanyName = c.CompanyName,
                        Email = c.ContactEmail,
                        IsLocked = lockedCompanyIds.Contains(c.Id),
                        SurveyEmailSentLastDate = cs?.SurveyEmailSentLastDate,
                        SurveyReminderEmailSent = cs?.SurveyReminderEmailSent ?? false,
                        SurveyReminderEmailSentLastDate = cs?.SurveyReminderEmailSentLastDate,
                        Unsubscribed = cs?.Unsubscribed ?? false,
                        SurveyLink = surveyLink,
                        SurveyLinkExpiryUtc = surveyLinkExpiryUtc,
                        IsSurveyLinkExpired = surveyLinkExpiryUtc.HasValue && surveyLinkExpiryUtc.Value <= DateTimeOffset.UtcNow
                    };
                })
                .ToList();
        }

        private async Task<HashSet<int>> GetLockedCompanyIdsForCurrentSurveyAsync()
        {
            var currentSurveyId = await GetCurrentOrLatestSurveyIdAsync();
            if (!currentSurveyId.HasValue) return new HashSet<int>();

            var lockedIds = await _context.CompanySurvey
                .Where(cs => cs.SurveyId == currentSurveyId.Value && (cs.Locked ?? false))
                .Select(cs => cs.CompanyId)
                .ToListAsync();

            return lockedIds.ToHashSet();
        }

        private async Task<Models.CompanySurvey?> EnsureCompanySurveyAsync(int companyId, int surveyId)
        {
            var companySurvey = await _context.CompanySurvey
                .OrderByDescending(cs => cs.Id)
                .FirstOrDefaultAsync(cs => cs.CompanyId == companyId && cs.SurveyId == surveyId);

            if (companySurvey != null) return companySurvey;

            companySurvey = new Models.CompanySurvey
            {
                CompanyId = companyId,
                SurveyId = surveyId,
                Saved = false,
                Submitted = false,
                Requested = false,
                Locked = false,
                Estimate = false
            };

            _context.CompanySurvey.Add(companySurvey);
            await _context.SaveChangesAsync();
            return companySurvey;
        }

        private async Task<int?> GetCurrentOrLatestSurveyIdAsync()
        {
            var currentSurveyId = await _context.Survey
                .Where(s => s.CurrentSurvey)
                .OrderByDescending(s => s.FinancialYear)
                .ThenByDescending(s => s.Id)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync();

            if (currentSurveyId.HasValue) return currentSurveyId;

            return await _context.Survey
                .OrderByDescending(s => s.FinancialYear)
                .ThenByDescending(s => s.Id)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync();
        }

        private string BuildSurveyUrl(int id)
        {
            var token = _surveyLinkTokenService.GenerateToken(id);
            var relativePath = Url.Page("/Company/AnswerSurvey", pageHandler: null, values: new { id, token }, protocol: null) ?? string.Empty;
            var configuredBaseUrl = (_surveyLinkSettings.BaseUrl ?? string.Empty).Trim().TrimEnd('/');

            if (!string.IsNullOrWhiteSpace(configuredBaseUrl) && Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out _))
            {
                return $"{configuredBaseUrl}{relativePath}";
            }

            return Url.Page("/Company/AnswerSurvey", pageHandler: null, values: new { id, token }, protocol: Request.Scheme) ?? string.Empty;
        }

        private DateTimeOffset? GetSurveyLinkExpiryUtc(string? surveyLink)
        {
            if (string.IsNullOrWhiteSpace(surveyLink) || !Uri.TryCreate(surveyLink.Trim(), UriKind.Absolute, out var linkUri))
                return null;

            var token = linkUri.Segments.LastOrDefault()?.Trim('/');
            return string.IsNullOrWhiteSpace(token) ? null : _surveyLinkTokenService.GetTokenExpiryUtc(Uri.UnescapeDataString(token));
        }

        private async Task DelayUntilStartAsync(DateTime sendStartTimeLocal, CancellationToken cancellationToken)
        {
            var initialDelay = sendStartTimeLocal - DateTime.Now;
            if (initialDelay > TimeSpan.Zero)
                await Task.Delay(initialDelay, cancellationToken);
        }

        private async Task ApplyScheduledDelayBetweenSendsAsync(int completedSends, CancellationToken cancellationToken)
        {
            var isBatchBoundary = SplitBulkIntoBatchesOf > 0 && completedSends % SplitBulkIntoBatchesOf == 0;
            if (isBatchBoundary && BreakBetweenSendingGroupsHours > 0)
            {
                await Task.Delay(TimeSpan.FromHours((double)BreakBetweenSendingGroupsHours), cancellationToken);
                return;
            }

            if (IntervalBetweenEachEmailSendSeconds > 0)
                await Task.Delay(TimeSpan.FromSeconds(IntervalBetweenEachEmailSendSeconds), cancellationToken);
        }

        private async Task AddCompanySurveyNoteAsync(int companySurveyId, DateTime noteDateTimeLocal, string userName, string recipientEmail, string surveyUrl, bool isReminder)
        {
            var safeUser = string.IsNullOrWhiteSpace(userName) ? "System" : userName.Trim();
            if (safeUser.Length > 255) safeUser = safeUser[..255];

            var label = isReminder ? "Survey reminder email sent" : "Survey email sent";
            var notes = $"{label} to {recipientEmail}. Link: {surveyUrl}";

            _context.CompanySurveyNotes.Add(new Models.CompanySurveyNote
            {
                CompanySurveyId = companySurveyId,
                NoteDateTime = noteDateTimeLocal,
                User = safeUser,
                Notes = notes
            });

            await _context.SaveChangesAsync();
        }

        public class ReminderClientRow
        {
            public int Id { get; set; }
            public string? CompanyName { get; set; }
            public string? Email { get; set; }
            public bool IsLocked { get; set; }
            public DateTime? SurveyEmailSentLastDate { get; set; }
            public bool SurveyReminderEmailSent { get; set; }
            public DateTime? SurveyReminderEmailSentLastDate { get; set; }
            public bool Unsubscribed { get; set; }
            public string? SurveyLink { get; set; }
            public DateTimeOffset? SurveyLinkExpiryUtc { get; set; }
            public bool IsSurveyLinkExpired { get; set; }
        }
    }
}
