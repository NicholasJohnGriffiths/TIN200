using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using TINWeb.Data;
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
        public int? SelectedFinancialYear { get; set; }
        public int TotalCompaniesWithAnswers { get; set; }
        public string CompanySearch { get; set; } = string.Empty;
        public string SurveyEmailSentFilter { get; set; } = "all";
        public string SortBy { get; set; } = "CompanyName";
        public string SortDir { get; set; } = "asc";

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

        public async Task OnGetAsync(int? financialYear, string? sortBy, string? sortDir, string? companySearch, string? surveyEmailSentFilter)
        {
            FinancialYears = await _service.GetAvailableFinancialYearsAsync();

            SelectedFinancialYear = financialYear ?? await _service.GetCurrentSurveyFinancialYearAsync();
            CompanySearch = (companySearch ?? string.Empty).Trim();
            SurveyEmailSentFilter = NormalizeSurveyEmailSentFilter(surveyEmailSentFilter);
            SortBy = NormalizeSortBy(sortBy);
            SortDir = NormalizeSortDir(sortDir);

            Records = await _service.GetListRowsAsync(SelectedFinancialYear);

            NormalizeSurveyLinksForCurrentHost();

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

        public async Task<IActionResult> OnPostBulkSubmitWithAnswersAsync(int? financialYear, string? companySearch, string? surveyEmailSentFilter)
        {
            await _service.BulkSubmitWithAnswersAsync(financialYear);
            return RedirectToPage(new { financialYear, companySearch, surveyEmailSentFilter });
        }

        public async Task<IActionResult> OnPostPreviewPopulateSurveyLinksAsync(int? financialYear, bool overwriteExisting)
        {
            var targetSurvey = await GetTargetSurveyAsync(financialYear);

            if (targetSurvey == null)
            {
                return new JsonResult(new { success = false, message = "No survey was found for the selected financial year." });
            }

            // Get all CompanySurvey records for the selected survey with company names
            var companySurveys = await _context.CompanySurvey
                .Where(cs => cs.SurveyId == targetSurvey.Id)
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
                totalRecords = companySurveys.Count,
                blankCount,
                existingCount,
                willUpdate = totalToUpdate,
                willSkip = companySurveys.Count - totalToUpdate,
                overwriteExisting,
                previewRows = previewRows.Take(20).ToList(),
                totalPreviewShown = Math.Min(20, previewRows.Count),
                moreRows = previewRows.Count > 20 ? previewRows.Count - 20 : 0
            });
        }

        public async Task<IActionResult> OnPostPopulateSurveyLinksAsync(int? financialYear, bool overwriteExisting, string? companySearch, string? surveyEmailSentFilter)
        {
            var targetSurvey = await GetTargetSurveyAsync(financialYear);

            if (targetSurvey == null)
            {
                StatusMessage = "Error: No survey was found for the selected financial year.";
                return RedirectToPage(new { financialYear, companySearch, surveyEmailSentFilter });
            }

            // Get all CompanySurvey records for the selected survey
            var companySurveys = await _context.CompanySurvey
                .Where(cs => cs.SurveyId == targetSurvey.Id)
                .ToListAsync();

            int createdCount = 0;
            int overwrittenCount = 0;
            int skippedCount = 0;

            foreach (var companySurvey in companySurveys)
            {
                var hasExistingLink = !string.IsNullOrWhiteSpace(companySurvey.SurveyLink);

                if (!hasExistingLink || overwriteExisting)
                {
                    companySurvey.SurveyLink = BuildSurveyUrl(companySurvey.CompanyId);
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

            StatusMessage = $"Survey links updated for {targetSurvey.FinancialYear}. Created: {createdCount}, Overwritten: {overwrittenCount}, Skipped: {skippedCount}.";
            return RedirectToPage(new { financialYear, companySearch, surveyEmailSentFilter });
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

        private string BuildSurveyUrl(int companyId)
        {
            var token = _surveyLinkTokenService.GenerateToken(companyId);
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

        public DateTimeOffset? GetSurveyLinkExpiryUtc(string? surveyLink)
        {
            var token = ExtractTokenFromSurveyLink(surveyLink);
            return string.IsNullOrWhiteSpace(token)
                ? null
                : _surveyLinkTokenService.GetTokenExpiryUtc(token);
        }

        public bool IsSurveyLinkExpired(string? surveyLink)
        {
            var expiryUtc = GetSurveyLinkExpiryUtc(surveyLink);
            return expiryUtc.HasValue && expiryUtc.Value <= DateTimeOffset.UtcNow;
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
