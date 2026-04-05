using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
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

        public List<SurveyClientRow> AvailableClients { get; set; } = new();

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
            await LoadAvailableClientsAsync();

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
            var currentSurveyId = await _context.Survey
                .Where(s => s.CurrentSurvey)
                .OrderByDescending(s => s.FinancialYear)
                .ThenByDescending(s => s.Id)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync();

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

                var surveyUrl = BuildSurveyUrl(clientRow.Id);

                try
                {
                    await _surveyEmailService.SendSurveyLinkAsync(clientRow.Email, surveyUrl, clientRow.CompanyName, clientRow.Id);
                    sentCount++;

                    // Update CompanySurvey with survey link if current survey exists
                    if (currentSurveyId.HasValue)
                    {
                        var companySurvey = await _context.CompanySurvey
                            .FirstOrDefaultAsync(cs => cs.CompanyId == clientRow.Id && cs.SurveyId == currentSurveyId.Value);
                        if (companySurvey != null)
                        {
                            companySurvey.SurveyLink = surveyUrl;
                            companySurvey.SurveyEmailSent = true;
                            companySurvey.SurveyEmailSentLastDate = DateTime.UtcNow.Date;
                            _context.CompanySurvey.Update(companySurvey);
                        }
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;
                    firstFailureReason ??= ex.Message;
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

            return RedirectToPage();
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
            var clients = await _companyService.GetAllCompaniesAsync();
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
                        Email = c.Email,
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

        private async Task<Dictionary<int, SurveyEmailStatus>> GetSurveyEmailStatusByCompanyIdForCurrentSurveyAsync()
        {
            var currentSurveyId = await _context.Survey
                .Where(s => s.CurrentSurvey)
                .OrderByDescending(s => s.FinancialYear)
                .ThenByDescending(s => s.Id)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync();

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
            var currentSurveyId = await _context.Survey
                .Where(s => s.CurrentSurvey)
                .OrderByDescending(s => s.FinancialYear)
                .ThenByDescending(s => s.Id)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync();

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
    }
}

