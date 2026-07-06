using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TINWeb.Data;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.Company
{
    public class SendSurveyModel : PageModel
    {
        private readonly CompanyService _companyService;
        private readonly ISurveyEmailService _surveyEmailService;
        private readonly ISurveyLinkTokenService _surveyLinkTokenService;
        private readonly SurveyLinkSettings _surveyLinkSettings;
        private readonly ApplicationDbContext _context;

        public SendSurveyModel(
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
        public int SelectedTinStatus { get; set; } = (int)TinStatus.Tin200;

        [BindProperty(SupportsGet = true)]
        public int? SelectedLastTin200Year { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SelectedEmailContentId { get; set; }

        public List<int> AvailableLastTin200Years { get; set; } = new();
        public List<SurveyClientRow> AvailableClients { get; set; } = new();
        public List<SelectListItem> EmailContentOptions { get; set; } = new();
        public SurveyEmailPreviewResult? EmailPreview { get; set; }

        public bool HasQueryPreselection { get; set; }

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

        public int LinkExpiryHours => _surveyLinkSettings.ExpiryHours;
        public int LinkExpiryDays => Math.Max(1, (int)Math.Ceiling(_surveyLinkSettings.ExpiryHours / 24d));

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            SelectedTinStatus = NormalizeTinStatusFilter(SelectedTinStatus);
            await LoadAvailableClientsAsync();
            await LoadEmailContentOptionsAsync();
            SelectDefaultEmailContentIfMissing();
            await LoadEmailPreviewAsync();

            if (id.HasValue)
            {
                var selectedClient = AvailableClients.FirstOrDefault(c => c.Id == id.Value);
                if (selectedClient != null && !string.IsNullOrWhiteSpace(selectedClient.Email))
                {
                    SelectedClientIds = new List<int> { id.Value };
                    HasQueryPreselection = true;
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostBulkAsync()
        {
            await LoadAvailableClientsAsync();
            await LoadEmailContentOptionsAsync();
            SelectDefaultEmailContentIfMissing();
            await LoadEmailPreviewAsync();

            if (!SelectedEmailContentId.HasValue)
            {
                ModelState.AddModelError(nameof(SelectedEmailContentId), "Select an email content before sending surveys.");
            }

            if (EnableScheduleSettings)
            {
                if (!SendStartTime.HasValue)
                {
                    ModelState.AddModelError(nameof(SendStartTime), "Send Start Time is required when Schedule Settings is enabled.");
                }

                if (SplitBulkIntoBatchesOf < 1)
                {
                    ModelState.AddModelError(nameof(SplitBulkIntoBatchesOf), "Split bulk into batches of must be at least 1.");
                }

                if (BreakBetweenSendingGroupsHours < 0)
                {
                    ModelState.AddModelError(nameof(BreakBetweenSendingGroupsHours), "Break Between sending Groups must be 0 or greater.");
                }

                if (IntervalBetweenEachEmailSendSeconds < 0)
                {
                    ModelState.AddModelError(nameof(IntervalBetweenEachEmailSendSeconds), "Interval Between Each Email Send must be 0 or greater.");
                }

                if (!ModelState.IsValid)
                {
                    return Page();
                }
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
            var sendQueue = new List<SurveyClientRow>();

            if (!currentSurveyId.HasValue)
            {
                ModelState.AddModelError(string.Empty, "No survey record exists. Unable to send survey emails.");
                return Page();
            }

            foreach (var clientRow in selected)
            {
                if (clientRow.Unsubscribed)
                {
                    skippedUnsubscribedCount++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(clientRow.Email))
                {
                    skippedNoEmailCount++;
                    continue;
                }

                if (lockedCompanyIds.Contains(clientRow.Id))
                {
                    skippedLockedCount++;
                    continue;
                }

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
                    return RedirectToPage(new { SelectedTinStatus, SelectedLastTin200Year, SelectedEmailContentId });
                }
            }

            for (var index = 0; index < sendQueue.Count; index++)
            {
                var clientRow = sendQueue[index];
                var recipientEmail = clientRow.Email!;

                var surveyUrl = BuildSurveyUrl(clientRow.Id);

                try
                {
                    await _surveyEmailService.SendSurveyLinkAsync(recipientEmail, surveyUrl, clientRow.CompanyName, clientRow.Id, SelectedEmailContentId!.Value);
                    sentCount++;

                    if (currentSurveyId.HasValue)
                    {
                        var companySurvey = await EnsureCompanySurveyAsync(clientRow.Id, currentSurveyId.Value);

                        if (companySurvey != null)
                        {
                            var sentAtLocal = DateTime.Now;
                            var sentByUser = string.IsNullOrWhiteSpace(User?.Identity?.Name) ? "System" : User.Identity!.Name!;

                            companySurvey.SurveyLink = surveyUrl;
                            companySurvey.SurveyEmailSent = true;
                            companySurvey.SurveyEmailSentLastDate = sentAtLocal; // local datetime
                            _context.CompanySurvey.Update(companySurvey);

                            await AddCompanySurveyNoteAsync(
                                companySurvey.Id,
                                sentAtLocal,
                                sentByUser,
                                recipientEmail,
                                surveyUrl);
                        }
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
                        if (sentCount > 0)
                        {
                            await _context.SaveChangesAsync();
                        }

                        StatusMessage = $"Bulk send was cancelled. Sent before cancellation: {sentCount}, Skipped (no email): {skippedNoEmailCount}, Skipped (locked): {skippedLockedCount}, Skipped (unsubscribed): {skippedUnsubscribedCount}, Failed: {failedCount}.";
                        BulkSentCount = sentCount;
                        BulkSkippedCount = skippedNoEmailCount + skippedLockedCount + skippedUnsubscribedCount;
                        BulkFailedCount = failedCount;
                        BulkLastRunAt = DateTime.Now.ToString("MMM d, yyyy h:mm tt");
                        BulkSendSucceeded = false;
                        return RedirectToPage(new { SelectedTinStatus, SelectedLastTin200Year, SelectedEmailContentId });
                    }
                }
            }

            // Save all SurveyLink updates at once
            if (sentCount > 0)
            {
                await _context.SaveChangesAsync();
            }

            StatusMessage = $"Bulk send complete. Sent: {sentCount}, Skipped (no email): {skippedNoEmailCount}, Skipped (locked): {skippedLockedCount}, Skipped (unsubscribed): {skippedUnsubscribedCount}, Failed: {failedCount}.";
            BulkSentCount = sentCount;
            BulkSkippedCount = skippedNoEmailCount + skippedLockedCount + skippedUnsubscribedCount;
            BulkFailedCount = failedCount;
            BulkLastRunAt = DateTime.Now.ToString("MMM d, yyyy h:mm tt");

            if (failedCount > 0)
            {
                var detail = string.IsNullOrWhiteSpace(firstFailureReason)
                    ? string.Empty
                    : $" First error: {firstFailureReason}";
                ModelState.AddModelError(string.Empty, $"Some emails could not be sent. Please check Azure Communication Email settings and retry.{detail}");
                return Page();
            }

            if (skippedLockedCount > 0)
            {
                ModelState.AddModelError(string.Empty, "Locked survey records were skipped and no survey email was sent for them.");
                return Page();
            }

            if (skippedUnsubscribedCount > 0)
            {
                ModelState.AddModelError(string.Empty, "Unsubscribed records were skipped and no survey email was sent for them.");
                return Page();
            }

            BulkSendSucceeded = true;

            return RedirectToPage(new { SelectedTinStatus, SelectedLastTin200Year, SelectedEmailContentId });
        }

        private async Task DelayUntilStartAsync(DateTime sendStartTimeLocal, CancellationToken cancellationToken)
        {
            var initialDelay = sendStartTimeLocal - DateTime.Now;
            if (initialDelay > TimeSpan.Zero)
            {
                await Task.Delay(initialDelay, cancellationToken);
            }
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
            {
                await Task.Delay(TimeSpan.FromSeconds(IntervalBetweenEachEmailSendSeconds), cancellationToken);
            }
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
            var token = ExtractTokenFromSurveyLink(surveyLink);
            return string.IsNullOrWhiteSpace(token)
                ? null
                : _surveyLinkTokenService.GetTokenExpiryUtc(token);
        }

        private static string? ExtractTokenFromSurveyLink(string? surveyLink)
        {
            if (string.IsNullOrWhiteSpace(surveyLink) || !Uri.TryCreate(surveyLink.Trim(), UriKind.Absolute, out var linkUri))
            {
                return null;
            }

            var token = linkUri.Segments.LastOrDefault()?.Trim('/');
            return string.IsNullOrWhiteSpace(token) ? null : Uri.UnescapeDataString(token);
        }

        private async Task LoadAvailableClientsAsync()
        {
            AvailableLastTin200Years = await _companyService.GetAvailableLastTin200YearsAsync();

            var clients = await _companyService.GetAllCompaniesAsync(SelectedLastTin200Year);
            clients = clients.Where(c => c.TinStatus == SelectedTinStatus).ToList();

            var lockedCompanyIds = await GetLockedCompanyIdsForCurrentSurveyAsync();
            var surveyEmailStatusByCompanyId = await GetSurveyEmailStatusByCompanyIdForCurrentSurveyAsync();

            AvailableClients = clients
                .OrderBy(c => c.CompanyName)
                .ThenBy(c => c.Id)
                .Select(c =>
                {
                    surveyEmailStatusByCompanyId.TryGetValue(c.Id, out var status);
                    var surveyLink = status?.SurveyLink;
                    var surveyLinkExpiryUtc = GetSurveyLinkExpiryUtc(surveyLink);

                    return new SurveyClientRow
                    {
                        Id = c.Id,
                        CompanyName = c.CompanyName,
                        Email = c.ContactEmail,
                        IsLocked = lockedCompanyIds.Contains(c.Id),
                        SurveyEmailSent = status?.SurveyEmailSent ?? false,
                        SurveyEmailSentLastDate = status?.SurveyEmailSentLastDate,
                        Unsubscribed = status?.Unsubscribed ?? false,
                        UnsubscribedDate = status?.UnsubscribedDate,
                        SurveyLink = surveyLink,
                        SurveyLinkExpiryUtc = surveyLinkExpiryUtc,
                        IsSurveyLinkExpired = surveyLinkExpiryUtc.HasValue && surveyLinkExpiryUtc.Value <= DateTimeOffset.UtcNow
                    };
                })
                .ToList();
        }

        private async Task LoadEmailContentOptionsAsync()
        {
            EmailContentOptions = await _context.EmailContent
                .AsNoTracking()
                .Where(x => x.Active)
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = x.Name
                })
                .ToListAsync();
        }

        private void SelectDefaultEmailContentIfMissing()
        {
            if (SelectedEmailContentId.HasValue
                && EmailContentOptions.Any(x => string.Equals(x.Value, SelectedEmailContentId.Value.ToString(), StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            SelectedEmailContentId = EmailContentOptions
                .Select(x => int.TryParse(x.Value, out var parsed) ? parsed : (int?)null)
                .FirstOrDefault();
        }

        private async Task LoadEmailPreviewAsync()
        {
            EmailPreview = null;

            if (!SelectedEmailContentId.HasValue)
            {
                return;
            }

            var previewClient = AvailableClients.FirstOrDefault();
            var previewClientId = previewClient?.Id ?? 1;
            var previewCompanyName = previewClient?.CompanyName ?? "Example Company";
            var previewSurveyUrl = BuildSurveyUrl(previewClientId);

            EmailPreview = await _surveyEmailService.BuildSurveyEmailPreviewAsync(
                SelectedEmailContentId.Value,
                previewSurveyUrl,
                previewCompanyName,
                previewClientId);
        }

        private static int NormalizeTinStatusFilter(int tinStatus)
        {
            return tinStatus switch
            {
                (int)TinStatus.Tin200 => (int)TinStatus.Tin200,
                (int)TinStatus.Tin200Potential => (int)TinStatus.Tin200Potential,
                (int)TinStatus.Tin1000 => (int)TinStatus.Tin1000,
                (int)TinStatus.TinTest => (int)TinStatus.TinTest,
                _ => (int)TinStatus.Tin200
            };
        }

        private async Task<Dictionary<int, SurveyEmailStatus>> GetSurveyEmailStatusByCompanyIdForCurrentSurveyAsync()
        {
            var currentSurveyId = await GetCurrentOrLatestSurveyIdAsync();

            if (!currentSurveyId.HasValue)
            {
                return new Dictionary<int, SurveyEmailStatus>();
            }

            return await _context.CompanySurvey
                .Where(cs => cs.SurveyId == currentSurveyId.Value)
                .GroupBy(cs => cs.CompanyId)
                .Select(g => g
                    .OrderByDescending(cs => cs.Id)
                    .Select(cs => new
                    {
                        cs.CompanyId,
                        cs.SurveyLink,
                        cs.SurveyEmailSent,
                        cs.SurveyEmailSentLastDate,
                        cs.Unsubscribed,
                        cs.UnsubscribedDate
                    })
                    .First())
                .ToDictionaryAsync(
                    x => x.CompanyId,
                    x => new SurveyEmailStatus
                    {
                        SurveyLink = x.SurveyLink,
                        SurveyEmailSent = x.SurveyEmailSent ?? false,
                        SurveyEmailSentLastDate = x.SurveyEmailSentLastDate,
                        Unsubscribed = x.Unsubscribed ?? false,
                        UnsubscribedDate = x.UnsubscribedDate
                    });
        }

        private async Task<HashSet<int>> GetLockedCompanyIdsForCurrentSurveyAsync()
        {
            var currentSurveyId = await GetCurrentOrLatestSurveyIdAsync();

            if (!currentSurveyId.HasValue)
            {
                return new HashSet<int>();
            }

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

            if (companySurvey != null)
            {
                return companySurvey;
            }

            companySurvey = new Models.CompanySurvey
            {
                CompanyId = companyId,
                SurveyId = surveyId,
                Saved = false,
                Submitted = false,
                Requested = false,
                Locked = false,
                Estimate = false,
                SavedDate = null,
                SubmittedDate = null,
                RequestedDate = null
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

            if (currentSurveyId.HasValue)
            {
                return currentSurveyId;
            }

            return await _context.Survey
                .OrderByDescending(s => s.FinancialYear)
                .ThenByDescending(s => s.Id)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync();
        }

        public class SurveyClientRow
        {
            public int Id { get; set; }
            public string? CompanyName { get; set; }
            public string? Email { get; set; }
            public bool IsLocked { get; set; }
            public bool SurveyEmailSent { get; set; }
            public DateTime? SurveyEmailSentLastDate { get; set; }
            public bool Unsubscribed { get; set; }
            public DateTime? UnsubscribedDate { get; set; }
            public string? SurveyLink { get; set; }
            public DateTimeOffset? SurveyLinkExpiryUtc { get; set; }
            public bool IsSurveyLinkExpired { get; set; }
        }

        private class SurveyEmailStatus
        {
            public string? SurveyLink { get; set; }
            public bool SurveyEmailSent { get; set; }
            public DateTime? SurveyEmailSentLastDate { get; set; }
            public bool Unsubscribed { get; set; }
            public DateTime? UnsubscribedDate { get; set; }
        }

        private async Task AddCompanySurveyNoteAsync(
            int companySurveyId,
            DateTime noteDateTimeLocal,
            string userName,
            string recipientEmail,
            string surveyUrl)
        {
            var safeUser = string.IsNullOrWhiteSpace(userName) ? "System" : userName.Trim();
            if (safeUser.Length > 255) safeUser = safeUser[..255];

            var notes = $"Survey email sent to {recipientEmail}. Link: {surveyUrl}";

            _context.CompanySurveyNotes.Add(new Models.CompanySurveyNote
            {
                CompanySurveyId = companySurveyId,
                NoteDateTime = noteDateTimeLocal,
                User = safeUser,
                Notes = notes
            });

            await _context.SaveChangesAsync();
        }
    }
}

