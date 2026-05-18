using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using TINWeb.Data;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.CompanySurvey
{
    public class IndexModel : PageModel
    {
        private readonly CompanySurveyService _service;
        private readonly ApplicationDbContext _context;
        private readonly ISurveyLinkTokenService _surveyLinkTokenService;
        private readonly SurveyLinkSettings _surveyLinkSettings;
        private readonly IWebHostEnvironment _environment;

        public List<CompanySurveyService.CompanySurveyListRow> Records { get; set; } = new();
        public List<int> FinancialYears { get; set; } = new();
        public bool IsAdmin => User.IsInRole("1");
        public int? SelectedFinancialYear { get; set; }
        public int TotalCompaniesWithAnswers { get; set; }
        public string CompanySearch { get; set; } = string.Empty;
        public string SurveyEmailSentFilter { get; set; } = "all";

        [BindProperty(SupportsGet = true)]
        public int SelectedTinStatus { get; set; } = (int)TinStatus.Tin200;

        public string SortBy { get; set; } = "CompanyName";
        public string SortDir { get; set; } = "asc";
        public int? PopulateYearMinusOne => SelectedFinancialYear.HasValue ? SelectedFinancialYear.Value - 1 : null;
        public int? PopulateYearMinusTwo => SelectedFinancialYear.HasValue ? SelectedFinancialYear.Value - 2 : null;
        public string PopulatePriorYearDataButtonLabel => SelectedFinancialYear.HasValue
            ? $"Populate {SelectedFinancialYear.Value - 2}, {SelectedFinancialYear.Value - 1} Data"
            : "Populate Prior Year Data";

        [BindProperty]
        public DateTime? SelectedLinkExpiryDateUtc { get; set; }

        public int LinkExpiryHours => _surveyLinkSettings.ExpiryHours;
        public int LinkExpiryDays => Math.Max(1, (int)Math.Ceiling(_surveyLinkSettings.ExpiryHours / 24d));

        [TempData]
        public string? StatusMessage { get; set; }

        public IndexModel(
            CompanySurveyService service,
            ApplicationDbContext context,
            ISurveyLinkTokenService surveyLinkTokenService,
            IOptions<SurveyLinkSettings> surveyLinkSettings,
            IWebHostEnvironment environment)
        {
            _service = service;
            _context = context;
            _surveyLinkTokenService = surveyLinkTokenService;
            _surveyLinkSettings = surveyLinkSettings.Value;
            _environment = environment;
        }

        public async Task OnGetAsync(int? financialYear, string? sortBy, string? sortDir, string? companySearch, string? surveyEmailSentFilter, int? tinStatus, DateTime? selectedLinkExpiryDateUtc)
        {
            await LoadPageDataAsync(financialYear, sortBy, sortDir, companySearch, surveyEmailSentFilter, tinStatus, selectedLinkExpiryDateUtc);
        }

        public async Task<IActionResult> OnPostBulkSubmitWithAnswersAsync(int? financialYear, string? companySearch, string? surveyEmailSentFilter, int? tinStatus = null)
        {
            await _service.BulkSubmitWithAnswersAsync(financialYear);
            return RedirectToPage(new { financialYear, companySearch, surveyEmailSentFilter, tinStatus });
        }

        public async Task<IActionResult> OnPostPreviewPopulatePriorYearDataAsync(int? financialYear, string? companySearch, string? surveyEmailSentFilter, int? tinStatus = null, List<int>? selectedRecordIds = null)
        {
            if (!User.IsInRole("1"))
            {
                return new JsonResult(new { success = false, message = "Populate data is available to admin users only." });
            }

            await LoadPageDataAsync(financialYear, sortBy: null, sortDir: null, companySearch, surveyEmailSentFilter, tinStatus, selectedLinkExpiryDateUtc: SelectedLinkExpiryDateUtc);

            if (!SelectedFinancialYear.HasValue)
            {
                return new JsonResult(new { success = false, message = "No survey financial year is selected." });
            }

            var targetRecordIds = ResolveSelectedRecordIds(selectedRecordIds);
            if (targetRecordIds.Count == 0)
            {
                return new JsonResult(new { success = false, message = "Select at least one survey record first." });
            }

            var result = await _service.PreviewPopulatePriorYearDataAsync(targetRecordIds, SelectedFinancialYear.Value);

            return new JsonResult(new
            {
                success = true,
                selectedRecords = targetRecordIds.Count,
                totalRecords = result.TotalRecords,
                affectedCompanies = result.AffectedCompanyCount,
                willUpdateFields = result.UpdatedFieldCount,
                existingValueCount = result.ExistingValueCount,
                missingSourceCount = result.MissingSourceCount,
                yearMinusOne = result.YearMinusOne,
                yearMinusTwo = result.YearMinusTwo,
                previewRows = result.PreviewRows.Select(row => new
                {
                    companyName = row.CompanyName,
                    status = row.WillUpdateCount > 0
                        ? $"Will populate {row.WillUpdateCount} field{(row.WillUpdateCount == 1 ? string.Empty : "s")}"
                        : "No blank year data to populate",
                    statusClass = row.WillUpdateCount > 0 ? "text-success" : "text-muted",
                    icon = row.WillUpdateCount > 0 ? "✓" : "•",
                    details = BuildPreviewDetails(row)
                }).ToList(),
                totalPreviewShown = result.PreviewRows.Count,
                moreRows = Math.Max(0, result.TotalRecords - result.PreviewRows.Count)
            });
        }

        public async Task<IActionResult> OnPostPopulatePriorYearDataAsync(int? financialYear, string? companySearch, string? surveyEmailSentFilter, int? tinStatus = null, List<int>? selectedRecordIds = null)
        {
            var redirectValues = new
            {
                financialYear,
                companySearch,
                surveyEmailSentFilter,
                tinStatus,
                selectedLinkExpiryDateUtc = SelectedLinkExpiryDateUtc?.ToString("yyyy-MM-dd")
            };

            if (!User.IsInRole("1"))
            {
                StatusMessage = "Error: Populate data is available to admin users only.";
                return RedirectToPage(redirectValues);
            }

            await LoadPageDataAsync(financialYear, sortBy: null, sortDir: null, companySearch, surveyEmailSentFilter, tinStatus, selectedLinkExpiryDateUtc: SelectedLinkExpiryDateUtc);

            if (!SelectedFinancialYear.HasValue)
            {
                StatusMessage = "Error: No survey financial year is selected.";
                return RedirectToPage(redirectValues);
            }

            var targetRecordIds = ResolveSelectedRecordIds(selectedRecordIds);
            if (targetRecordIds.Count == 0)
            {
                StatusMessage = "Error: Select at least one survey record first.";
                return RedirectToPage(redirectValues);
            }

            var result = await _service.PopulatePriorYearDataAsync(targetRecordIds, SelectedFinancialYear.Value);

            StatusMessage = $"Historical year data populated for {SelectedFinancialYear.Value}. Selected surveys: {targetRecordIds.Count}. Filled {result.UpdatedFieldCount} blank answers across {result.AffectedCompanyCount} companies. Already populated: {result.ExistingValueCount}. No source found: {result.MissingSourceCount}.";
            return RedirectToPage(redirectValues);
        }

        public async Task<IActionResult> OnPostPreviewPopulateSurveyLinksAsync(int? financialYear, bool overwriteExisting, string? companySearch, string? surveyEmailSentFilter, int? tinStatus = null, List<int>? selectedRecordIds = null)
        {
            if (!TryGetSelectedExpiryAtUtc(SelectedLinkExpiryDateUtc, out var selectedExpiryAtUtc, out var expiryValidationMessage))
            {
                return new JsonResult(new { success = false, message = expiryValidationMessage });
            }

            await LoadPageDataAsync(financialYear, sortBy: null, sortDir: null, companySearch, surveyEmailSentFilter, tinStatus, selectedLinkExpiryDateUtc: SelectedLinkExpiryDateUtc);

            var targetRecordIds = ResolveSelectedRecordIds(selectedRecordIds);
            if (targetRecordIds.Count == 0)
            {
                return new JsonResult(new { success = false, message = "Select at least one survey record first." });
            }

            // Get selected CompanySurvey records with company names
            var companySurveys = await _context.CompanySurvey
                .Where(cs => targetRecordIds.Contains(cs.Id))
                .Join(_context.Tin200,
                    cs => cs.CompanyId,
                    t => t.Id,
                    (cs, t) => new { cs, t })
                .OrderBy(x => x.t.CompanyName)
                .Select(x => new { x.cs, x.t.CompanyName })
                .ToListAsync();

            int blankCount = 0;
            int existingCount = 0;
            var previewRows = new List<object>();

            foreach (var item in companySurveys)
            {
                if (string.IsNullOrWhiteSpace(item.cs.SurveyLink))
                {
                    blankCount++;
                    previewRows.Add(new
                    {
                        companyName = item.CompanyName ?? "Unknown",
                        status = "Will be populated",
                        statusClass = "text-success",
                        icon = "✓"
                    });
                }
                else
                {
                    existingCount++;
                    if (overwriteExisting)
                    {
                        previewRows.Add(new
                        {
                            companyName = item.CompanyName ?? "Unknown",
                            status = "Will be overwritten",
                            statusClass = "text-warning",
                            icon = "⚠"
                        });
                    }
                }
            }

            var totalToUpdate = overwriteExisting ? blankCount + existingCount : blankCount;

            return new JsonResult(new
            {
                success = true,
                selectedRecords = targetRecordIds.Count,
                totalRecords = companySurveys.Count,
                blankCount,
                existingCount,
                willUpdate = totalToUpdate,
                willSkip = companySurveys.Count - totalToUpdate,
                overwriteExisting,
                previewRows = previewRows.Take(20).ToList(),
                totalPreviewShown = Math.Min(20, previewRows.Count),
                moreRows = previewRows.Count > 20 ? previewRows.Count - 20 : 0,
                expiryDisplay = selectedExpiryAtUtc?.ToString("dd/MM/yyyy '23:59 UTC'")
            });
        }

        public async Task<IActionResult> OnPostPopulateSurveyLinksAsync(int? financialYear, bool overwriteExisting, string? companySearch, string? surveyEmailSentFilter, int? tinStatus = null, List<int>? selectedRecordIds = null)
        {
            var redirectValues = new
            {
                financialYear,
                companySearch,
                surveyEmailSentFilter,
                tinStatus,
                selectedLinkExpiryDateUtc = SelectedLinkExpiryDateUtc?.ToString("yyyy-MM-dd")
            };

            if (!TryGetSelectedExpiryAtUtc(SelectedLinkExpiryDateUtc, out var selectedExpiryAtUtc, out var expiryValidationMessage))
            {
                StatusMessage = $"Error: {expiryValidationMessage}";
                return RedirectToPage(redirectValues);
            }

            await LoadPageDataAsync(financialYear, sortBy: null, sortDir: null, companySearch, surveyEmailSentFilter, tinStatus, selectedLinkExpiryDateUtc: SelectedLinkExpiryDateUtc);

            var targetRecordIds = ResolveSelectedRecordIds(selectedRecordIds);
            if (targetRecordIds.Count == 0)
            {
                StatusMessage = "Error: Select at least one survey record first.";
                return RedirectToPage(redirectValues);
            }

            var targetSurvey = await GetTargetSurveyAsync(financialYear);

            if (targetSurvey == null)
            {
                StatusMessage = "Error: No survey was found for the selected financial year.";
                return RedirectToPage(redirectValues);
            }

            // Get selected CompanySurvey records
            var companySurveys = await _context.CompanySurvey
                .Where(cs => targetRecordIds.Contains(cs.Id))
                .ToListAsync();

            int createdCount = 0;
            int overwrittenCount = 0;
            int skippedCount = 0;

            foreach (var companySurvey in companySurveys)
            {
                var hasExistingLink = !string.IsNullOrWhiteSpace(companySurvey.SurveyLink);

                if (!hasExistingLink || overwriteExisting)
                {
                    companySurvey.SurveyLink = BuildSurveyUrl(companySurvey.CompanyId, selectedExpiryAtUtc);
                    _context.CompanySurvey.Update(companySurvey);

                    if (hasExistingLink)
                    {
                        overwrittenCount++;
                    }
                    else
                    {
                        createdCount++;
                    }
                }
                else
                {
                    skippedCount++;
                }
            }

            if (createdCount > 0 || overwrittenCount > 0)
            {
                await _context.SaveChangesAsync();
            }

            var expiryMessage = selectedExpiryAtUtc.HasValue
                ? $" Selected expiry date: {selectedExpiryAtUtc.Value:dd/MM/yyyy HH:mm} UTC."
                : string.Empty;

            StatusMessage = $"Survey links updated for {targetSurvey.FinancialYear}. Selected surveys: {targetRecordIds.Count}. Created: {createdCount}, Overwritten: {overwrittenCount}, Skipped: {skippedCount}.{expiryMessage}";
            return RedirectToPage(redirectValues);
        }

        private List<int> ResolveSelectedRecordIds(IEnumerable<int>? selectedRecordIds)
        {
            var validRecordIds = Records.Select(r => r.Id).ToHashSet();

            return (selectedRecordIds ?? Enumerable.Empty<int>())
                .Where(id => validRecordIds.Contains(id))
                .Distinct()
                .ToList();
        }

        private async Task LoadPageDataAsync(int? financialYear, string? sortBy, string? sortDir, string? companySearch, string? surveyEmailSentFilter, int? tinStatus, DateTime? selectedLinkExpiryDateUtc)
        {
            FinancialYears = await _service.GetAvailableFinancialYearsAsync();

            SelectedFinancialYear = financialYear ?? await _service.GetCurrentSurveyFinancialYearAsync();
            CompanySearch = (companySearch ?? string.Empty).Trim();
            SurveyEmailSentFilter = NormalizeSurveyEmailSentFilter(surveyEmailSentFilter);
            SelectedTinStatus = NormalizeTinStatusFilter(tinStatus);
            SortBy = NormalizeSortBy(sortBy);
            SortDir = NormalizeSortDir(sortDir);
            SelectedLinkExpiryDateUtc = selectedLinkExpiryDateUtc?.Date ?? GetDefaultLinkExpiryDateUtc();

            Records = await _service.GetListRowsAsync(SelectedFinancialYear);
            NormalizeSurveyLinksForCurrentHost();
            Records = Records.Where(x => x.TinStatus == SelectedTinStatus).ToList();

            if (string.Equals(SurveyEmailSentFilter, "sent", StringComparison.OrdinalIgnoreCase))
            {
                Records = Records.Where(x => x.SurveyEmailSent).ToList();
            }
            else if (string.Equals(SurveyEmailSentFilter, "not-sent", StringComparison.OrdinalIgnoreCase))
            {
                Records = Records.Where(x => !x.SurveyEmailSent).ToList();
            }

            if (!string.IsNullOrWhiteSpace(CompanySearch))
            {
                Records = Records
                    .Where(x =>
                        (!string.IsNullOrWhiteSpace(x.CompanyName) && x.CompanyName.Contains(CompanySearch, StringComparison.OrdinalIgnoreCase))
                        || (!string.IsNullOrWhiteSpace(x.ExternalId) && x.ExternalId.Contains(CompanySearch, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            Records = ApplySorting(Records, SortBy, SortDir).ToList();
            TotalCompaniesWithAnswers = Records.Count(r => r.AnswerCount > 0);
        }

        private static int NormalizeTinStatusFilter(int? tinStatus)
        {
            if (!tinStatus.HasValue)
            {
                return (int)TinStatus.Tin200;
            }

            return tinStatus.Value switch
            {
                (int)TinStatus.Tin200 => (int)TinStatus.Tin200,
                (int)TinStatus.Tin200Potential => (int)TinStatus.Tin200Potential,
                (int)TinStatus.Tin1000 => (int)TinStatus.Tin1000,
                (int)TinStatus.TinTest => (int)TinStatus.TinTest,
                _ => (int)TinStatus.Tin200
            };
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

        private async Task<Models.Survey?> GetTargetSurveyAsync(int? financialYear)
        {
            var query = _context.Survey.AsQueryable();

            if (financialYear.HasValue)
            {
                query = query.Where(s => s.FinancialYear == financialYear.Value);
            }
            else
            {
                query = query.Where(s => s.CurrentSurvey);
            }

            return await query
                .OrderByDescending(s => s.CurrentSurvey)
                .ThenByDescending(s => s.FinancialYear)
                .ThenByDescending(s => s.Id)
                .FirstOrDefaultAsync();
        }

        private string BuildSurveyUrl(int companyId, DateTimeOffset? customExpiryUtc = null)
        {
            var token = _surveyLinkTokenService.GenerateToken(companyId, customExpiryUtc);
            var relativePath = Url.Page("/Company/AnswerSurvey", pageHandler: null, values: new { id = companyId, token }, protocol: null) ?? string.Empty;
            var configuredBaseUrl = (_surveyLinkSettings.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
            var shouldForceLocalHost = _environment.IsDevelopment() || IsLocalHost(Request.Host.Host);

            if (shouldForceLocalHost)
            {
                return $"{Request.Scheme}://{Request.Host}{relativePath}";
            }

            if (!string.IsNullOrWhiteSpace(configuredBaseUrl) && Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out _))
            {
                return $"{configuredBaseUrl}{relativePath}";
            }

            return Url.Page("/Company/AnswerSurvey", pageHandler: null, values: new { id = companyId, token }, protocol: Request.Scheme) ?? string.Empty;
        }

        private void NormalizeSurveyLinksForCurrentHost()
        {
            if (!(_environment.IsDevelopment() || IsLocalHost(Request.Host.Host)))
            {
                return;
            }

            var localBaseUrl = $"{Request.Scheme}://{Request.Host}";
            foreach (var record in Records)
            {
                if (string.IsNullOrWhiteSpace(record.SurveyLink))
                {
                    continue;
                }

                var link = record.SurveyLink.Trim();
                if (Uri.TryCreate(link, UriKind.Absolute, out var absoluteUri))
                {
                    record.SurveyLink = $"{localBaseUrl}{absoluteUri.PathAndQuery}{absoluteUri.Fragment}";
                    continue;
                }

                if (Uri.TryCreate(link, UriKind.Relative, out var relativeUri))
                {
                    var relativePath = relativeUri.OriginalString.StartsWith('/') ? relativeUri.OriginalString : $"/{relativeUri.OriginalString}";
                    record.SurveyLink = $"{localBaseUrl}{relativePath}";
                }
            }
        }

        private static bool IsLocalHost(string host)
        {
            return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeSurveyEmailSentFilter(string? surveyEmailSentFilter)
        {
            return surveyEmailSentFilter?.Trim().ToLowerInvariant() switch
            {
                "sent" => "sent",
                "not-sent" => "not-sent",
                _ => "all"
            };
        }

        private static string BuildPreviewDetails(CompanySurveyService.PopulatePriorYearDataPreviewRow row)
        {
            var details = new List<string>();

            if (row.ExistingValueCount > 0)
            {
                details.Add($"{row.ExistingValueCount} already populated");
            }

            if (row.MissingSourceCount > 0)
            {
                details.Add($"{row.MissingSourceCount} without source");
            }

            if (!string.IsNullOrWhiteSpace(row.FieldsSummary))
            {
                details.Add($"Fields: {row.FieldsSummary}");
            }

            return details.Count == 0 ? "No updates needed." : string.Join(" • ", details);
        }

        public string GetNextSortDirection(string column)
        {
            return string.Equals(SortBy, column, StringComparison.OrdinalIgnoreCase) && string.Equals(SortDir, "asc", StringComparison.OrdinalIgnoreCase)
                ? "desc"
                : "asc";
        }

        public string GetSortIndicator(string column)
        {
            if (!string.Equals(SortBy, column, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return string.Equals(SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? " ▲" : " ▼";
        }

        private static string NormalizeSortBy(string? sortBy)
        {
            return sortBy switch
            {
                "CompanyName" => "CompanyName",
                "Saved" => "Saved",
                "SavedDate" => "SavedDate",
                "Submitted" => "Submitted",
                "SubmittedDate" => "SubmittedDate",
                "Requested" => "Requested",
                "RequestedDate" => "RequestedDate",
                "Locked" => "Locked",
                "Estimate" => "Estimate",
                "AnswerCount" => "AnswerCount",
                _ => "CompanyName"
            };
        }

        private static string NormalizeSortDir(string? sortDir)
        {
            return string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";
        }

        private static IEnumerable<CompanySurveyService.CompanySurveyListRow> ApplySorting(
            IEnumerable<CompanySurveyService.CompanySurveyListRow> records,
            string sortBy,
            string sortDir)
        {
            var descending = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);

            return sortBy switch
            {
                "Saved" => descending ? records.OrderByDescending(r => r.Saved).ThenBy(r => r.CompanyName) : records.OrderBy(r => r.Saved).ThenBy(r => r.CompanyName),
                "SavedDate" => descending ? records.OrderByDescending(r => r.SavedDate).ThenBy(r => r.CompanyName) : records.OrderBy(r => r.SavedDate).ThenBy(r => r.CompanyName),
                "Submitted" => descending ? records.OrderByDescending(r => r.Submitted).ThenBy(r => r.CompanyName) : records.OrderBy(r => r.Submitted).ThenBy(r => r.CompanyName),
                "SubmittedDate" => descending ? records.OrderByDescending(r => r.SubmittedDate).ThenBy(r => r.CompanyName) : records.OrderBy(r => r.SubmittedDate).ThenBy(r => r.CompanyName),
                "Requested" => descending ? records.OrderByDescending(r => r.Requested).ThenBy(r => r.CompanyName) : records.OrderBy(r => r.Requested).ThenBy(r => r.CompanyName),
                "RequestedDate" => descending ? records.OrderByDescending(r => r.RequestedDate).ThenBy(r => r.CompanyName) : records.OrderBy(r => r.RequestedDate).ThenBy(r => r.CompanyName),
                "Locked" => descending ? records.OrderByDescending(r => r.Locked).ThenBy(r => r.CompanyName) : records.OrderBy(r => r.Locked).ThenBy(r => r.CompanyName),
                "Estimate" => descending ? records.OrderByDescending(r => r.Estimate).ThenBy(r => r.CompanyName) : records.OrderBy(r => r.Estimate).ThenBy(r => r.CompanyName),
                "AnswerCount" => descending ? records.OrderByDescending(r => r.AnswerCount).ThenBy(r => r.CompanyName) : records.OrderBy(r => r.AnswerCount).ThenBy(r => r.CompanyName),
                _ => descending ? records.OrderByDescending(r => r.CompanyName).ThenByDescending(r => r.Id) : records.OrderBy(r => r.CompanyName).ThenBy(r => r.Id)
            };
        }
    }
}
