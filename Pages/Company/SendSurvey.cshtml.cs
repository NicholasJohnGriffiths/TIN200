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
            var failedCount = 0;
            string? firstFailureReason = null;
            var lockedCompanyIds = await GetLockedCompanyIdsForCurrentSurveyAsync();

            foreach (var clientRow in selected)
            {
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
                }
                catch (Exception ex)
                {
                    failedCount++;
                    firstFailureReason ??= ex.Message;
                }
            }

            StatusMessage = $"Bulk send complete. Sent: {sentCount}, Skipped (no email): {skippedNoEmailCount}, Skipped (locked): {skippedLockedCount}, Failed: {failedCount}.";
            BulkSentCount = sentCount;
            BulkSkippedCount = skippedNoEmailCount;
            BulkFailedCount = failedCount;
            BulkLastRunAt = DateTime.Now.ToString("MMM d, yyyy h:mm tt");

            if (failedCount > 0)
            {
                var detail = string.IsNullOrWhiteSpace(firstFailureReason)
                    ? string.Empty
                    : $" First error: {firstFailureReason}";
                ModelState.AddModelError(string.Empty, $"Some emails could not be sent. Please check SMTP settings and retry.{detail}");
                return Page();
            }

            if (skippedLockedCount > 0)
            {
                ModelState.AddModelError(string.Empty, "Locked survey records were skipped and no survey email was sent for them.");
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

        private async Task LoadAvailableClientsAsync()
        {
            var clients = await _companyService.GetAllCompaniesAsync();
            var lockedCompanyIds = await GetLockedCompanyIdsForCurrentSurveyAsync();

            AvailableClients = clients
                .OrderBy(c => c.CompanyName)
                .ThenBy(c => c.Id)
                .Select(c => new SurveyClientRow
                {
                    Id = c.Id,
                    CompanyName = c.CompanyName,
                    Email = c.Email,
                    IsLocked = lockedCompanyIds.Contains(c.Id)
                })
                .ToList();
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
        }
    }
}

