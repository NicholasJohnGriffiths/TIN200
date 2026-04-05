using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TINWeb.Data;
using TINWeb.Services;

namespace TINWeb.Pages.CompanySurvey
{
    public class EditModel : PageModel
    {
        private readonly CompanySurveyService _service;
        private readonly ApplicationDbContext _context;
        private readonly ISurveyLinkTokenService _surveyLinkTokenService;
        private readonly SurveyLinkSettings _surveyLinkSettings;

        [BindProperty]
        public Models.CompanySurvey Record { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int? FinancialYear { get; set; }

        [TempData]
        public string? StatusMessage { get; set; }

        [BindProperty]
        public DateTime? SelectedLinkExpiryDateUtc { get; set; }

        public string? CompanyName { get; set; }
        public int LinkExpiryHours => _surveyLinkSettings.ExpiryHours;
        public int LinkExpiryDays => Math.Max(1, (int)Math.Ceiling(_surveyLinkSettings.ExpiryHours / 24d));
        public DateTimeOffset? SurveyLinkExpiryUtc => GetSurveyLinkExpiryUtc(Record.SurveyLink);
        public bool IsSurveyLinkExpired => SurveyLinkExpiryUtc.HasValue && SurveyLinkExpiryUtc.Value <= DateTimeOffset.UtcNow;

        public EditModel(
            CompanySurveyService service,
            ApplicationDbContext context,
            ISurveyLinkTokenService surveyLinkTokenService,
            IOptions<SurveyLinkSettings> surveyLinkOptions)
        {
            _service = service;
            _context = context;
            _surveyLinkTokenService = surveyLinkTokenService;
            _surveyLinkSettings = surveyLinkOptions.Value;
        }

        public async Task<IActionResult> OnGetAsync(int? id, int? financialYear)
        {
            FinancialYear = financialYear;

            if (id == null)
            {
                return NotFound();
            }

            var record = await _service.GetByIdAsync(id.Value);
            if (record == null)
            {
                return NotFound();
            }

            Record = record;
            Record.Locked ??= false;
            Record.Estimate ??= false;
            SelectedLinkExpiryDateUtc = SurveyLinkExpiryUtc?.UtcDateTime.Date ?? GetDefaultLinkExpiryDateUtc();
            await LoadCompanyNameAsync(record.CompanyId);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadCompanyNameAsync(Record.CompanyId);
                return Page();
            }

            if (!await _service.ExistsAsync(Record.Id))
            {
                return NotFound();
            }

            Record.Locked ??= false;
            Record.Estimate ??= false;
            await _service.UpdateAsync(Record);
            return RedirectToPage("./Index", null, new { financialYear = FinancialYear }, $"record-{Record.Id}");
        }

        public async Task<IActionResult> OnPostRegenerateLinkAsync()
        {
            var existing = await _service.GetByIdAsync(Record.Id);
            if (existing == null)
            {
                return NotFound();
            }

            if (!TryGetSelectedExpiryAtUtc(SelectedLinkExpiryDateUtc, out var selectedExpiryAtUtc, out var expiryValidationMessage))
            {
                Record = existing;
                Record.Locked ??= false;
                Record.Estimate ??= false;
                await LoadCompanyNameAsync(existing.CompanyId);
                ModelState.AddModelError(nameof(SelectedLinkExpiryDateUtc), expiryValidationMessage ?? "Invalid expiry date.");
                return Page();
            }

            try
            {
                existing.SurveyLink = BuildSurveyUrl(existing.CompanyId, selectedExpiryAtUtc);
                await _service.UpdateAsync(existing);
                await AddCompanySurveyNoteAsync(
                    existing.Id,
                    User.Identity?.Name ?? "Admin",
                    "Survey link manually regenerated from the Company Survey edit page.");

                var expiryUtc = GetSurveyLinkExpiryUtc(existing.SurveyLink);
                var expiryText = expiryUtc.HasValue
                    ? $" New link expires {expiryUtc.Value:dd/MM/yyyy HH:mm} UTC."
                    : string.Empty;

                StatusMessage = $"Survey link regenerated. Token validity is currently {LinkExpiryHours} hours (~{LinkExpiryDays} days).{expiryText}";
                return RedirectToPage(new { id = existing.Id, financialYear = FinancialYear });
            }
            catch (InvalidOperationException ex)
            {
                Record = existing;
                Record.Locked ??= false;
                Record.Estimate ??= false;
                await LoadCompanyNameAsync(existing.CompanyId);
                ModelState.AddModelError(string.Empty, ex.Message);
                return Page();
            }
        }

        private DateTime GetDefaultLinkExpiryDateUtc()
        {
            return DateTime.UtcNow.AddHours(_surveyLinkSettings.ExpiryHours).Date;
        }

        private static bool TryGetSelectedExpiryAtUtc(DateTime? selectedLinkExpiryDateUtc, out DateTimeOffset? expiresAtUtc, out string? validationMessage)
        {
            expiresAtUtc = null;
            validationMessage = null;

            if (!selectedLinkExpiryDateUtc.HasValue)
            {
                return true;
            }

            var selectedDate = selectedLinkExpiryDateUtc.Value.Date;
            if (selectedDate < DateTime.UtcNow.Date)
            {
                validationMessage = "Expiry date must be today or later.";
                return false;
            }

            expiresAtUtc = new DateTimeOffset(selectedDate.AddDays(1).AddTicks(-1), TimeSpan.Zero);
            return true;
        }

        private async Task AddCompanySurveyNoteAsync(int companySurveyId, string user, string notes)
        {
            var note = new Models.CompanySurveyNote
            {
                CompanySurveyId = companySurveyId,
                NoteDateTime = DateTime.Now,
                User = user,
                Notes = notes
            };

            _context.CompanySurveyNotes.Add(note);
            await _context.SaveChangesAsync();
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

        private async Task LoadCompanyNameAsync(int companyId)
        {
            CompanyName = await _context.Tin200
                .Where(c => c.Id == companyId)
                .Select(c => c.CompanyName)
                .FirstOrDefaultAsync();
        }

        private string BuildSurveyUrl(int companyId, DateTimeOffset? customExpiryUtc = null)
        {
            var token = _surveyLinkTokenService.GenerateToken(companyId, customExpiryUtc);
            var relativePath = Url.Page("/Company/AnswerSurvey", pageHandler: null, values: new { id = companyId, token }, protocol: null) ?? string.Empty;
            var configuredBaseUrl = (_surveyLinkSettings.BaseUrl ?? string.Empty).Trim().TrimEnd('/');

            if (!string.IsNullOrWhiteSpace(configuredBaseUrl) && Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out _))
            {
                return $"{configuredBaseUrl}{relativePath}";
            }

            return Url.Page("/Company/AnswerSurvey", pageHandler: null, values: new { id = companyId, token }, protocol: Request.Scheme) ?? string.Empty;
        }
    }
}
