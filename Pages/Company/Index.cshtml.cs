using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Models;
using TINWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;

namespace TINWeb.Pages.Company
{
    public class IndexModel : PageModel
    {
        private readonly CompanyService _service;
        private readonly ApplicationDbContext _context;
        private static readonly ConcurrentDictionary<string, PendingCompanyImport> PendingImports = new();

        public List<Models.Tin200> Records { get; set; } = new();
        public List<int> AvailableLastTin200Years { get; set; } = new();
        public int? SelectedLastTin200Year { get; set; }
        public int? FocusId { get; set; }
        public string CompanySearch { get; set; } = string.Empty;
        public int? SelectedTinStatus { get; set; } = (int)TinStatus.Tin200;
        public bool ShowTestCompanies { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? ShowImportTool { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? ShowImportTab { get; set; }
        public CompanyService.ResetFyeValuesResult? PreviewSummary { get; set; }
        public CompanyService.CompanyGlobalImportPreviewResult? GlobalImportPreview { get; set; }
        public CompanyService.CompanyContactImportPreviewResult? ContactImportPreview { get; set; }
        public bool IsAdmin => User.IsInRole("1");
        public string? PendingGlobalImportToken { get; set; }
        public string? PendingContactImportToken { get; set; }
        [BindProperty]
        public string? GlobalImportMappingToken { get; set; }
        public List<string> GlobalImportHeaders { get; set; } = new();
        [BindProperty]
        public string? GlobalMapCeoFirstNameHeader { get; set; }
        [BindProperty]
        public string? GlobalMapCeoLastNameHeader { get; set; }
        [BindProperty]
        public string? GlobalMapContactFirstNameHeader { get; set; }
        [BindProperty]
        public string? GlobalMapContactLastNameHeader { get; set; }
        [BindProperty]
        public string? GlobalMapContactEmailHeader { get; set; }
        [BindProperty]
        public string? GlobalMapEmailHeader { get; set; }
        [BindProperty]
        public string? GlobalMapExternalIdHeader { get; set; }
        [BindProperty]
        public string? GlobalMapCompanyNameHeader { get; set; }
        [BindProperty]
        public string? GlobalMapCompanyDescriptionHeader { get; set; }
        [BindProperty]
        public string? GlobalMapStreetHeader { get; set; }
        [BindProperty]
        public string? GlobalMapSuburbHeader { get; set; }
        [BindProperty]
        public string? GlobalMapCityHeader { get; set; }
        [BindProperty]
        public string? GlobalMapPostcodeHeader { get; set; }
        [BindProperty]
        public string? GlobalMapPhoneHeader { get; set; }
        [BindProperty]
        public string? GlobalMapWebsiteHeader { get; set; }
        [BindProperty]
        public string? GlobalMapFye2025Header { get; set; }
        [BindProperty]
        public string? GlobalMapFye2024Header { get; set; }
        [BindProperty]
        public string? GlobalMapFye2023Header { get; set; }
        [BindProperty]
        public string? GlobalMapFinancialYearHeader { get; set; }
        [BindProperty]
        public string? GlobalMapLastTin200YearHeader { get; set; }
        [BindProperty]
        public string? GlobalMapTinStatusHeader { get; set; }
        [BindProperty]
        public string? ContactImportMappingToken { get; set; }
        public List<string> ContactImportHeaders { get; set; } = new();
        [BindProperty]
        public string? ContactMapCompanyNameHeader { get; set; }
        [BindProperty]
        public string? ContactMapFirstNameHeader { get; set; }
        [BindProperty]
        public string? ContactMapLastNameHeader { get; set; }
        [BindProperty]
        public string? ContactMapEmailHeader { get; set; }
        [BindProperty]
        public int? ContactImportTinStatus { get; set; } = (int)TinStatus.Tin200;
        [BindProperty]
        public int? ContactImportSurveyYear { get; set; }
        public int? SelectedImportYear { get; set; }
        public int? LatestSurveyYear { get; set; }
        public int MissingCompanySurveyCount { get; set; }
        public HashSet<int> MissingCompanySurveyCompanyIds { get; set; } = new();
        public bool ShowMissingCompanySurveyOnly { get; set; }

        [TempData]
        public string? StatusMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public IndexModel(CompanyService service, ApplicationDbContext context)
        {
            _service = service;
            _context = context;
        }

        public async Task OnGetAsync(int? lastTin200Year, int? focusId, string? companySearch, int? tinStatus, bool showTestCompanies = false, bool showMissingCompanySurveyOnly = false, string? showImportTool = null, string? showImportTab = null)
        {
            FocusId = focusId;
            ShowImportTool = showImportTool;
            ShowImportTab = showImportTab;
            ShowMissingCompanySurveyOnly = showMissingCompanySurveyOnly;

            if (string.Equals(ShowImportTool, "contacts", StringComparison.OrdinalIgnoreCase))
            {
                ShowImportTool = "import";
                ShowImportTab = "contacts";
            }
            else if (string.Equals(ShowImportTool, "global", StringComparison.OrdinalIgnoreCase))
            {
                ShowImportTool = "import";
                ShowImportTab = "global";
            }

            var hasTinStatusFilter = Request.Query.ContainsKey("tinStatus");
            await LoadPageAsync(lastTin200Year, companySearch, showTestCompanies, tinStatus, hasTinStatusFilter, showMissingCompanySurveyOnly);
        }

        public async Task<IActionResult> OnPostPreviewResetFyeValuesAsync(int? lastTin200Year, string? companySearch, bool showTestCompanies = false, bool showMissingCompanySurveyOnly = false)
        {
            if (!User.IsInRole("1"))
            {
                ErrorMessage = "This action is only available to admin users.";
                return RedirectToPage(new { lastTin200Year, companySearch, showTestCompanies, showMissingCompanySurveyOnly });
            }

            PreviewSummary = await _service.PreviewResetFyeValuesFromSurveyAnswersAsync();
            await LoadPageAsync(lastTin200Year, companySearch, showTestCompanies, showMissingCompanySurveyOnly: showMissingCompanySurveyOnly);
            return Page();
        }

        public async Task<IActionResult> OnPostResetFyeValuesAsync(int? lastTin200Year, string? companySearch, bool showTestCompanies = false, bool showMissingCompanySurveyOnly = false)
        {
            if (!User.IsInRole("1"))
            {
                ErrorMessage = "This action is only available to admin users.";
                return RedirectToPage(new { lastTin200Year, companySearch, showTestCompanies, showMissingCompanySurveyOnly });
            }

            var result = await _service.ResetFyeValuesFromSurveyAnswersAsync();

            if (!result.HasCurrentSurvey)
            {
                StatusMessage = "Update Company Info skipped: no current survey is configured.";
                return RedirectToPage(new { lastTin200Year, companySearch, showTestCompanies, showMissingCompanySurveyOnly });
            }

            StatusMessage = $"Update Company Info complete (Current survey year: {result.CurrentSurveyYear}). Updated {result.UpdatedCompanyCount} of {result.TotalMatchedCompanies} matched company record(s).";
            return RedirectToPage(new { lastTin200Year, companySearch, showTestCompanies, showMissingCompanySurveyOnly });
        }

        public async Task<IActionResult> OnPostLoadGlobalImportHeadersAsync(IFormFile? importFile, int? lastTin200Year, int? tinStatus, int? importYear, string? companySearch, bool showTestCompanies = false, bool showMissingCompanySurveyOnly = false, string? showImportTool = null, string? showImportTab = null)
        {
            if (!User.IsInRole("1"))
            {
                ErrorMessage = "Companies import is only available to admin users.";
                return RedirectToPage(new { lastTin200Year, tinStatus, companySearch, showTestCompanies, showMissingCompanySurveyOnly, showImportTool = "import", showImportTab = "global" });
            }

            ShowImportTool = showImportTool;
            ShowImportTab = string.IsNullOrWhiteSpace(showImportTab) ? "global" : showImportTab;

            if (!importYear.HasValue || importYear.Value <= 0)
            {
                ErrorMessage = "Global company import failed: please provide a valid Import Year.";
                return RedirectToPage(new { lastTin200Year, tinStatus, companySearch, showTestCompanies, showMissingCompanySurveyOnly, showImportTool, showImportTab = ShowImportTab });
            }

            if (importFile == null || importFile.Length == 0)
            {
                ErrorMessage = "Global company import failed: please select an Excel file.";
                return RedirectToPage(new { lastTin200Year, tinStatus, companySearch, showTestCompanies, showMissingCompanySurveyOnly, showImportTool, showImportTab = ShowImportTab });
            }

            var fileName = importFile.FileName ?? string.Empty;
            if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = "Global company import failed: only .xlsx Excel files are supported.";
                return RedirectToPage(new { lastTin200Year, tinStatus, companySearch, showTestCompanies, showMissingCompanySurveyOnly, showImportTool, showImportTab = ShowImportTab });
            }

            CleanupExpiredPendingImports();

            var token = Guid.NewGuid().ToString("N");
            var tempDir = Path.Combine(Path.GetTempPath(), "tinweb-company-global-import");
            Directory.CreateDirectory(tempDir);
            var tempFilePath = Path.Combine(tempDir, $"{token}.xlsx");

            await using (var tempFileStream = System.IO.File.Create(tempFilePath))
            {
                await importFile.CopyToAsync(tempFileStream);
            }

            List<string> headers;
            await using (var readStream = System.IO.File.OpenRead(tempFilePath))
            {
                headers = await _service.GetExcelHeadersAsync(readStream);
            }

            if (!headers.Any())
            {
                if (System.IO.File.Exists(tempFilePath))
                {
                    System.IO.File.Delete(tempFilePath);
                }

                ErrorMessage = "Global company import mapping failed: no header row was detected in the selected file.";
                return RedirectToPage(new { lastTin200Year, tinStatus, companySearch, showTestCompanies, showMissingCompanySurveyOnly, showImportTool, showImportTab = ShowImportTab });
            }

            PendingImports[token] = new PendingCompanyImport
            {
                Token = token,
                TempFilePath = tempFilePath,
                CreatedUtc = DateTime.UtcNow,
                LastTin200Year = lastTin200Year,
                CompanySearch = companySearch,
                ShowTestCompanies = showTestCompanies,
                ShowMissingCompanySurveyOnly = showMissingCompanySurveyOnly,
                ImportYear = importYear.Value,
                ImportTinStatus = ResolveDefaultContactImportTinStatus(tinStatus),
                Kind = "GlobalMapping",
                ShowImportTool = showImportTool,
                ShowImportTab = ShowImportTab,
                GlobalMapping = BuildGlobalImportMapping()
            };

            await LoadPageAsync(lastTin200Year, companySearch, showTestCompanies, tinStatus, hasTinStatusFilter: true, showMissingCompanySurveyOnly: showMissingCompanySurveyOnly);
            GlobalImportHeaders = headers;
            GlobalImportMappingToken = token;
            SelectedImportYear = importYear.Value;
            ApplyDefaultGlobalHeaders(headers);
            StatusMessage = "Global import file loaded. Map the columns to Company fields, then preview the import.";
            return Page();
        }

        public async Task<IActionResult> OnPostPreviewGlobalImportAsync(IFormFile? importFile, int? lastTin200Year, int? tinStatus, int? importYear, string? companySearch, bool showTestCompanies = false, bool showMissingCompanySurveyOnly = false, string? showImportTool = null, string? showImportTab = null)
        {
            if (!User.IsInRole("1"))
            {
                ErrorMessage = "Companies import is only available to admin users.";
                return RedirectToPage(new { lastTin200Year, tinStatus, companySearch, showTestCompanies, showMissingCompanySurveyOnly, showImportTool = "import", showImportTab = "global" });
            }

            ShowImportTool = showImportTool;
            ShowImportTab = string.IsNullOrWhiteSpace(showImportTab) ? "global" : showImportTab;

            PendingCompanyImport? sourceImport = null;
            if (!string.IsNullOrWhiteSpace(GlobalImportMappingToken)
                && PendingImports.TryGetValue(GlobalImportMappingToken, out var mappedPending)
                && string.Equals(mappedPending.Kind, "GlobalMapping", StringComparison.OrdinalIgnoreCase))
            {
                sourceImport = mappedPending;
            }

            var effectiveImportYear = importYear ?? sourceImport?.ImportYear;
            if (!effectiveImportYear.HasValue || effectiveImportYear.Value <= 0)
            {
                ErrorMessage = "Global company import failed: please provide a valid Import Year.";
                return RedirectToPage(new { lastTin200Year, tinStatus, companySearch, showTestCompanies, showMissingCompanySurveyOnly, showImportTool, showImportTab = ShowImportTab });
            }

            if (sourceImport == null && (importFile == null || importFile.Length == 0))
            {
                ErrorMessage = "Global company import failed: please select an Excel file.";
                return RedirectToPage(new { lastTin200Year, tinStatus, companySearch, showTestCompanies, showMissingCompanySurveyOnly, showImportTool, showImportTab = ShowImportTab });
            }

            if (sourceImport == null)
            {
                var fileName = importFile?.FileName ?? string.Empty;
                if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    ErrorMessage = "Global company import failed: only .xlsx Excel files are supported.";
                    return RedirectToPage(new { lastTin200Year, tinStatus, companySearch, showTestCompanies, showMissingCompanySurveyOnly, showImportTool, showImportTab = ShowImportTab });
                }

                CleanupExpiredPendingImports();

                var token = Guid.NewGuid().ToString("N");
                var tempDir = Path.Combine(Path.GetTempPath(), "tinweb-company-global-import");
                Directory.CreateDirectory(tempDir);
                var tempFilePath = Path.Combine(tempDir, $"{token}.xlsx");

                await using (var tempFileStream = System.IO.File.Create(tempFilePath))
                {
                    await importFile!.CopyToAsync(tempFileStream);
                }

                sourceImport = new PendingCompanyImport
                {
                    Token = token,
                    TempFilePath = tempFilePath,
                    CreatedUtc = DateTime.UtcNow,
                    LastTin200Year = lastTin200Year,
                    CompanySearch = companySearch,
                    ShowTestCompanies = showTestCompanies,
                    ShowMissingCompanySurveyOnly = showMissingCompanySurveyOnly,
                    ImportYear = effectiveImportYear.Value,
                    ImportTinStatus = ResolveDefaultContactImportTinStatus(tinStatus),
                    Kind = "GlobalPreview",
                    ShowImportTool = showImportTool,
                    ShowImportTab = ShowImportTab
                };
            }

            var selectedMapping = BuildGlobalImportMapping();

            CompanyService.CompanyGlobalImportPreviewResult preview;
            await using (var readStream = System.IO.File.OpenRead(sourceImport.TempFilePath))
            {
                preview = await _service.PreviewGlobalImportFromExcelAsync(readStream, effectiveImportYear.Value, selectedMapping);
            }

            await using (var headerStream = System.IO.File.OpenRead(sourceImport.TempFilePath))
            {
                GlobalImportHeaders = await _service.GetExcelHeadersAsync(headerStream);
            }

            sourceImport.CreatedUtc = DateTime.UtcNow;
            sourceImport.LastTin200Year = lastTin200Year;
            sourceImport.CompanySearch = companySearch;
            sourceImport.ShowTestCompanies = showTestCompanies;
            sourceImport.ShowMissingCompanySurveyOnly = showMissingCompanySurveyOnly;
            sourceImport.ImportYear = effectiveImportYear.Value;
            sourceImport.ImportTinStatus = ResolveDefaultContactImportTinStatus(tinStatus);
            sourceImport.Kind = "GlobalPreview";
            sourceImport.ShowImportTool = showImportTool;
            sourceImport.ShowImportTab = ShowImportTab;
            sourceImport.GlobalMapping = selectedMapping;
            PendingImports[sourceImport.Token] = sourceImport;

            await LoadPageAsync(lastTin200Year, companySearch, showTestCompanies, tinStatus, hasTinStatusFilter: true, showMissingCompanySurveyOnly: showMissingCompanySurveyOnly);
            GlobalImportPreview = preview;
            PendingGlobalImportToken = sourceImport.Token;
            GlobalImportMappingToken = sourceImport.Token;
            SelectedImportYear = effectiveImportYear.Value;
            ApplyGlobalMappingToPage(selectedMapping);
            return Page();
        }

        public async Task<IActionResult> OnPostApplyGlobalImportAsync(string? previewToken, int? tinStatus, bool showMissingCompanySurveyOnly = false)
        {
            if (!User.IsInRole("1"))
            {
                ErrorMessage = "Companies import is only available to admin users.";
                return RedirectToPage(new { tinStatus, showMissingCompanySurveyOnly, showImportTool = "import", showImportTab = "global" });
            }

            CleanupExpiredPendingImports();

            if (string.IsNullOrWhiteSpace(previewToken) || !PendingImports.TryGetValue(previewToken, out var pendingImport))
            {
                ErrorMessage = "Apply global company import failed: preview session not found or expired. Please preview the file again.";
                return RedirectToPage(new { tinStatus, showMissingCompanySurveyOnly });
            }

            try
            {
                await using var stream = System.IO.File.OpenRead(pendingImport.TempFilePath);
                var result = await _service.ImportGlobalFromExcelAsync(stream, pendingImport.ImportYear, pendingImport.ImportTinStatus, pendingImport.GlobalMapping);
                if (result.Errors.Any())
                {
                    StatusMessage = $"Global company import completed with warnings. Import Year: {result.ImportYear}. Added: {result.InsertedCount}, Updated: {result.UpdatedCount}, Unchanged: {result.UnchangedCount}, CompanySurvey created: {result.CompanySurveyCreatedCount}. Warnings: {string.Join(" ", result.Errors.Take(5))}";
                }
                else
                {
                    StatusMessage = $"Global company import completed. Import Year: {result.ImportYear}. Added: {result.InsertedCount}, Updated: {result.UpdatedCount}, Unchanged: {result.UnchangedCount}, CompanySurvey created: {result.CompanySurveyCreatedCount}.";
                }
            }
            finally
            {
                PendingImports.TryRemove(previewToken, out _);
                if (System.IO.File.Exists(pendingImport.TempFilePath))
                {
                    System.IO.File.Delete(pendingImport.TempFilePath);
                }
            }

            return RedirectToPage(new { lastTin200Year = pendingImport.LastTin200Year, tinStatus, companySearch = pendingImport.CompanySearch, showTestCompanies = pendingImport.ShowTestCompanies, showMissingCompanySurveyOnly = pendingImport.ShowMissingCompanySurveyOnly, showImportTool = pendingImport.ShowImportTool, showImportTab = pendingImport.ShowImportTab });
        }

        public async Task<IActionResult> OnPostPreviewContactImportAsync(IFormFile? importFile, int? lastTin200Year, int? tinStatus, int? contactImportSurveyYear, string? companySearch, bool showTestCompanies = false, bool showMissingCompanySurveyOnly = false, string? showImportTool = null, string? showImportTab = null)
        {
            if (!User.IsInRole("1"))
            {
                ErrorMessage = "Companies import is only available to admin users.";
                return RedirectToPage(new { lastTin200Year, tinStatus, companySearch, showTestCompanies, showMissingCompanySurveyOnly, showImportTool = "import", showImportTab = "contacts" });
            }

            ShowImportTool = showImportTool;
            ShowImportTab = string.IsNullOrWhiteSpace(showImportTab) ? "contacts" : showImportTab;
            CleanupExpiredPendingImports();

            PendingCompanyImport? sourceImport = null;
            if (!string.IsNullOrWhiteSpace(ContactImportMappingToken)
                && PendingImports.TryGetValue(ContactImportMappingToken, out var mappedPending)
                && string.Equals(mappedPending.Kind, "ContactMapping", StringComparison.OrdinalIgnoreCase))
            {
                sourceImport = mappedPending;
            }

            var effectiveSurveyYear = contactImportSurveyYear ?? sourceImport?.ContactImportSurveyYear;
            if (!effectiveSurveyYear.HasValue || effectiveSurveyYear.Value <= 0)
            {
                ErrorMessage = "Companies import failed: please select a Survey Year.";
                return RedirectToPage(new { lastTin200Year, tinStatus, companySearch, showTestCompanies, showMissingCompanySurveyOnly, showImportTool, showImportTab = ShowImportTab });
            }

            if (sourceImport == null && (importFile == null || importFile.Length == 0))
            {
                ErrorMessage = "Companies import failed: please select an Excel file.";
                return RedirectToPage(new { lastTin200Year, tinStatus, companySearch, showTestCompanies, showMissingCompanySurveyOnly, showImportTool, showImportTab = ShowImportTab });
            }

            if (sourceImport == null)
            {
                var fileName = importFile?.FileName ?? string.Empty;
                if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    ErrorMessage = "Companies import failed: only .xlsx Excel files are supported.";
                    return RedirectToPage(new { lastTin200Year, tinStatus, companySearch, showTestCompanies, showMissingCompanySurveyOnly, showImportTool, showImportTab = ShowImportTab });
                }

                var token = Guid.NewGuid().ToString("N");
                var tempDir = Path.Combine(Path.GetTempPath(), "tinweb-company-contact-import");
                Directory.CreateDirectory(tempDir);
                var tempFilePath = Path.Combine(tempDir, $"{token}.xlsx");

                await using (var tempFileStream = System.IO.File.Create(tempFilePath))
                {
                    await importFile!.CopyToAsync(tempFileStream);
                }

                sourceImport = new PendingCompanyImport
                {
                    Token = token,
                    TempFilePath = tempFilePath,
                    CreatedUtc = DateTime.UtcNow,
                    LastTin200Year = lastTin200Year,
                    CompanySearch = companySearch,
                    ShowTestCompanies = showTestCompanies,
                    ShowMissingCompanySurveyOnly = showMissingCompanySurveyOnly,
                    Kind = "ContactPreview",
                    ShowImportTool = showImportTool,
                    ShowImportTab = ShowImportTab,
                    ContactImportSurveyYear = effectiveSurveyYear
                };
            }

            ContactImportTinStatus ??= ResolveDefaultContactImportTinStatus(tinStatus);

            var selectedMapping = new CompanyService.CompanyContactImportMapping
            {
                CompanyNameHeader = ContactMapCompanyNameHeader,
                ExternalIdHeader = GlobalMapExternalIdHeader,
                CeoFirstNameHeader = GlobalMapCeoFirstNameHeader,
                CeoLastNameHeader = GlobalMapCeoLastNameHeader,
                ContactFirstNameHeader = ContactMapFirstNameHeader,
                ContactLastNameHeader = ContactMapLastNameHeader,
                ContactEmailHeader = ContactMapEmailHeader,
                EmailHeader = GlobalMapEmailHeader,
                CompanyDescriptionHeader = GlobalMapCompanyDescriptionHeader,
                StreetHeader = GlobalMapStreetHeader,
                SuburbHeader = GlobalMapSuburbHeader,
                CityHeader = GlobalMapCityHeader,
                PostcodeHeader = GlobalMapPostcodeHeader,
                PhoneHeader = GlobalMapPhoneHeader,
                WebsiteHeader = GlobalMapWebsiteHeader,
                Fye2025Header = GlobalMapFye2025Header,
                Fye2024Header = GlobalMapFye2024Header,
                Fye2023Header = GlobalMapFye2023Header,
                FinancialYearHeader = GlobalMapFinancialYearHeader,
                LastTin200YearHeader = GlobalMapLastTin200YearHeader,
                TinStatusHeader = GlobalMapTinStatusHeader,
                ApplyTinStatus = true,
                TinStatusToApply = ContactImportTinStatus
            };

            CompanyService.CompanyContactImportPreviewResult preview;
            await using (var readStream = System.IO.File.OpenRead(sourceImport.TempFilePath))
            {
                preview = await _service.PreviewContactImportFromExcelAsync(readStream, selectedMapping);
            }

            await using (var headerStream = System.IO.File.OpenRead(sourceImport.TempFilePath))
            {
                ContactImportHeaders = await _service.GetExcelHeadersAsync(headerStream);
            }

            sourceImport.CreatedUtc = DateTime.UtcNow;
            sourceImport.LastTin200Year = lastTin200Year;
            sourceImport.CompanySearch = companySearch;
            sourceImport.ShowTestCompanies = showTestCompanies;
            sourceImport.ShowMissingCompanySurveyOnly = showMissingCompanySurveyOnly;
            sourceImport.ShowImportTool = showImportTool;
            sourceImport.ShowImportTab = ShowImportTab;
            sourceImport.Kind = "ContactPreview";
            sourceImport.MappedCompanyNameHeader = preview.MatchedFields.FirstOrDefault(x => x.StartsWith("Company Name ->", StringComparison.OrdinalIgnoreCase))?.Split("->", 2).ElementAtOrDefault(1)?.Trim();
            sourceImport.MappedExternalIdHeader = preview.MatchedFields.FirstOrDefault(x => x.StartsWith("External ID ->", StringComparison.OrdinalIgnoreCase))?.Split("->", 2).ElementAtOrDefault(1)?.Trim();
            sourceImport.MappedCeoFirstNameHeader = preview.MatchedFields.FirstOrDefault(x => x.StartsWith("CEO First Name ->", StringComparison.OrdinalIgnoreCase))?.Split("->", 2).ElementAtOrDefault(1)?.Trim();
            sourceImport.MappedCeoLastNameHeader = preview.MatchedFields.FirstOrDefault(x => x.StartsWith("CEO Last Name ->", StringComparison.OrdinalIgnoreCase))?.Split("->", 2).ElementAtOrDefault(1)?.Trim();
            sourceImport.MappedContactFirstNameHeader = preview.MatchedFields.FirstOrDefault(x => x.StartsWith("Contact First Name ->", StringComparison.OrdinalIgnoreCase))?.Split("->", 2).ElementAtOrDefault(1)?.Trim();
            sourceImport.MappedContactLastNameHeader = preview.MatchedFields.FirstOrDefault(x => x.StartsWith("Contact Last Name ->", StringComparison.OrdinalIgnoreCase))?.Split("->", 2).ElementAtOrDefault(1)?.Trim();
            sourceImport.MappedContactEmailHeader = preview.MatchedFields.FirstOrDefault(x => x.StartsWith("Contact Email ->", StringComparison.OrdinalIgnoreCase))?.Split("->", 2).ElementAtOrDefault(1)?.Trim();
            sourceImport.MappedEmailHeader = preview.MatchedFields.FirstOrDefault(x => x.StartsWith("Email ->", StringComparison.OrdinalIgnoreCase))?.Split("->", 2).ElementAtOrDefault(1)?.Trim();
            sourceImport.MappedCompanyDescriptionHeader = preview.MatchedFields.FirstOrDefault(x => x.StartsWith("Company Description ->", StringComparison.OrdinalIgnoreCase))?.Split("->", 2).ElementAtOrDefault(1)?.Trim();
            sourceImport.MappedStreetHeader = preview.MatchedFields.FirstOrDefault(x => x.StartsWith("Street ->", StringComparison.OrdinalIgnoreCase))?.Split("->", 2).ElementAtOrDefault(1)?.Trim();
            sourceImport.MappedSuburbHeader = preview.MatchedFields.FirstOrDefault(x => x.StartsWith("Suburb ->", StringComparison.OrdinalIgnoreCase))?.Split("->", 2).ElementAtOrDefault(1)?.Trim();
            sourceImport.MappedCityHeader = preview.MatchedFields.FirstOrDefault(x => x.StartsWith("City ->", StringComparison.OrdinalIgnoreCase))?.Split("->", 2).ElementAtOrDefault(1)?.Trim();
            sourceImport.MappedPostcodeHeader = preview.MatchedFields.FirstOrDefault(x => x.StartsWith("Postcode ->", StringComparison.OrdinalIgnoreCase))?.Split("->", 2).ElementAtOrDefault(1)?.Trim();
            sourceImport.MappedPhoneHeader = preview.MatchedFields.FirstOrDefault(x => x.StartsWith("Phone ->", StringComparison.OrdinalIgnoreCase))?.Split("->", 2).ElementAtOrDefault(1)?.Trim();
            sourceImport.MappedWebsiteHeader = preview.MatchedFields.FirstOrDefault(x => x.StartsWith("Website ->", StringComparison.OrdinalIgnoreCase))?.Split("->", 2).ElementAtOrDefault(1)?.Trim();
            sourceImport.MappedFye2025Header = preview.MatchedFields.FirstOrDefault(x => x.StartsWith("FYE Last Financial Year ->", StringComparison.OrdinalIgnoreCase))?.Split("->", 2).ElementAtOrDefault(1)?.Trim();
            sourceImport.MappedFye2024Header = preview.MatchedFields.FirstOrDefault(x => x.StartsWith("FYE Year-1 ->", StringComparison.OrdinalIgnoreCase))?.Split("->", 2).ElementAtOrDefault(1)?.Trim();
            sourceImport.MappedFye2023Header = preview.MatchedFields.FirstOrDefault(x => x.StartsWith("FYE Year-2 ->", StringComparison.OrdinalIgnoreCase))?.Split("->", 2).ElementAtOrDefault(1)?.Trim();
            sourceImport.MappedFinancialYearHeader = preview.MatchedFields.FirstOrDefault(x => x.StartsWith("Financial Year ->", StringComparison.OrdinalIgnoreCase))?.Split("->", 2).ElementAtOrDefault(1)?.Trim();
            sourceImport.MappedLastTin200YearHeader = preview.MatchedFields.FirstOrDefault(x => x.StartsWith("Last TIN200 Year ->", StringComparison.OrdinalIgnoreCase))?.Split("->", 2).ElementAtOrDefault(1)?.Trim();
            sourceImport.MappedTinStatusHeader = preview.MatchedFields.FirstOrDefault(x => x.StartsWith("TIN Status ->", StringComparison.OrdinalIgnoreCase))?.Split("->", 2).ElementAtOrDefault(1)?.Trim();
            sourceImport.ApplyTinStatus = true;
            sourceImport.ContactImportTinStatus = ContactImportTinStatus;
            sourceImport.ContactImportSurveyYear = effectiveSurveyYear;

            PendingImports[sourceImport.Token] = sourceImport;

            await LoadPageAsync(lastTin200Year, companySearch, showTestCompanies, tinStatus, hasTinStatusFilter: true, showMissingCompanySurveyOnly: showMissingCompanySurveyOnly);
            ContactImportPreview = preview;
            PendingContactImportToken = sourceImport.Token;
            ContactImportMappingToken = sourceImport.Token;
            ContactMapCompanyNameHeader = sourceImport.MappedCompanyNameHeader;
            ContactMapFirstNameHeader = sourceImport.MappedContactFirstNameHeader;
            ContactMapLastNameHeader = sourceImport.MappedContactLastNameHeader;
            ContactMapEmailHeader = sourceImport.MappedContactEmailHeader;
            GlobalMapExternalIdHeader = sourceImport.MappedExternalIdHeader;
            GlobalMapCeoFirstNameHeader = sourceImport.MappedCeoFirstNameHeader;
            GlobalMapCeoLastNameHeader = sourceImport.MappedCeoLastNameHeader;
            GlobalMapEmailHeader = sourceImport.MappedEmailHeader;
            GlobalMapCompanyDescriptionHeader = sourceImport.MappedCompanyDescriptionHeader;
            GlobalMapStreetHeader = sourceImport.MappedStreetHeader;
            GlobalMapSuburbHeader = sourceImport.MappedSuburbHeader;
            GlobalMapCityHeader = sourceImport.MappedCityHeader;
            GlobalMapPostcodeHeader = sourceImport.MappedPostcodeHeader;
            GlobalMapPhoneHeader = sourceImport.MappedPhoneHeader;
            GlobalMapWebsiteHeader = sourceImport.MappedWebsiteHeader;
            GlobalMapFye2025Header = sourceImport.MappedFye2025Header;
            GlobalMapFye2024Header = sourceImport.MappedFye2024Header;
            GlobalMapFye2023Header = sourceImport.MappedFye2023Header;
            GlobalMapFinancialYearHeader = sourceImport.MappedFinancialYearHeader;
            GlobalMapLastTin200YearHeader = sourceImport.MappedLastTin200YearHeader;
            GlobalMapTinStatusHeader = sourceImport.MappedTinStatusHeader;
            ContactImportTinStatus = sourceImport.ContactImportTinStatus;
            ContactImportSurveyYear = sourceImport.ContactImportSurveyYear;
            return Page();
        }

        public async Task<IActionResult> OnPostLoadContactImportHeadersAsync(IFormFile? importFile, int? lastTin200Year, int? tinStatus, int? contactImportSurveyYear, string? companySearch, bool showTestCompanies = false, bool showMissingCompanySurveyOnly = false, string? showImportTool = null, string? showImportTab = null)
        {
            if (!User.IsInRole("1"))
            {
                ErrorMessage = "Companies import is only available to admin users.";
                return RedirectToPage(new { lastTin200Year, tinStatus, companySearch, showTestCompanies, showMissingCompanySurveyOnly, showImportTool = "import", showImportTab = "contacts" });
            }

            ShowImportTool = showImportTool;
            ShowImportTab = string.IsNullOrWhiteSpace(showImportTab) ? "contacts" : showImportTab;
            if (importFile == null || importFile.Length == 0)
            {
                ErrorMessage = "Companies import mapping failed: please select an Excel file.";
                return RedirectToPage(new { lastTin200Year, tinStatus, companySearch, showTestCompanies, showMissingCompanySurveyOnly, showImportTool, showImportTab = ShowImportTab });
            }

            var fileName = importFile.FileName ?? string.Empty;
            if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = "Companies import mapping failed: only .xlsx Excel files are supported.";
                return RedirectToPage(new { lastTin200Year, tinStatus, companySearch, showTestCompanies, showMissingCompanySurveyOnly, showImportTool, showImportTab = ShowImportTab });
            }

            CleanupExpiredPendingImports();

            var token = Guid.NewGuid().ToString("N");
            var tempDir = Path.Combine(Path.GetTempPath(), "tinweb-company-contact-import");
            Directory.CreateDirectory(tempDir);
            var tempFilePath = Path.Combine(tempDir, $"{token}.xlsx");

            await using (var tempFileStream = System.IO.File.Create(tempFilePath))
            {
                await importFile.CopyToAsync(tempFileStream);
            }

            List<string> headers;
            await using (var readStream = System.IO.File.OpenRead(tempFilePath))
            {
                headers = await _service.GetExcelHeadersAsync(readStream);
            }

            if (!headers.Any())
            {
                if (System.IO.File.Exists(tempFilePath))
                {
                    System.IO.File.Delete(tempFilePath);
                }

                ErrorMessage = "Companies import mapping failed: no header row was detected in the selected file.";
                return RedirectToPage(new { lastTin200Year, tinStatus, companySearch, showTestCompanies, showMissingCompanySurveyOnly, showImportTool, showImportTab = ShowImportTab });
            }

            PendingImports[token] = new PendingCompanyImport
            {
                Token = token,
                TempFilePath = tempFilePath,
                CreatedUtc = DateTime.UtcNow,
                LastTin200Year = lastTin200Year,
                CompanySearch = companySearch,
                ShowTestCompanies = showTestCompanies,
                ShowMissingCompanySurveyOnly = showMissingCompanySurveyOnly,
                Kind = "ContactMapping",
                ShowImportTool = showImportTool,
                ShowImportTab = ShowImportTab,
                ContactImportSurveyYear = contactImportSurveyYear
            };

            await LoadPageAsync(lastTin200Year, companySearch, showTestCompanies, tinStatus, hasTinStatusFilter: true, showMissingCompanySurveyOnly: showMissingCompanySurveyOnly);
            ContactImportHeaders = headers;
            ContactImportMappingToken = token;
            ContactMapCompanyNameHeader = headers.FirstOrDefault(x => string.Equals(x.Trim(), "Company Name", StringComparison.OrdinalIgnoreCase));
            ContactMapFirstNameHeader = headers.FirstOrDefault(x => string.Equals(x.Trim(), "Contact Person - First Name", StringComparison.OrdinalIgnoreCase));
            ContactMapLastNameHeader = headers.FirstOrDefault(x => string.Equals(x.Trim(), "Contact Person - Last Name", StringComparison.OrdinalIgnoreCase));
            ContactMapEmailHeader = headers.FirstOrDefault(x => string.Equals(x.Trim(), "Contact Email", StringComparison.OrdinalIgnoreCase));
            GlobalMapExternalIdHeader = headers.FirstOrDefault(x => string.Equals(x.Trim(), "External ID", StringComparison.OrdinalIgnoreCase) || string.Equals(x.Trim(), "ID", StringComparison.OrdinalIgnoreCase));
            GlobalMapCeoFirstNameHeader = headers.FirstOrDefault(x => string.Equals(x.Trim(), "CEO First Name", StringComparison.OrdinalIgnoreCase));
            GlobalMapCeoLastNameHeader = headers.FirstOrDefault(x => string.Equals(x.Trim(), "CEO Last Name", StringComparison.OrdinalIgnoreCase));
            GlobalMapEmailHeader = headers.FirstOrDefault(x => string.Equals(x.Trim(), "Email", StringComparison.OrdinalIgnoreCase));
            GlobalMapCompanyDescriptionHeader = headers.FirstOrDefault(x => string.Equals(x.Trim(), "Company Description", StringComparison.OrdinalIgnoreCase) || string.Equals(x.Trim(), "Description", StringComparison.OrdinalIgnoreCase));
            GlobalMapStreetHeader = headers.FirstOrDefault(x => string.Equals(x.Trim(), "Street", StringComparison.OrdinalIgnoreCase));
            GlobalMapSuburbHeader = headers.FirstOrDefault(x => string.Equals(x.Trim(), "Suburb", StringComparison.OrdinalIgnoreCase));
            GlobalMapCityHeader = headers.FirstOrDefault(x => string.Equals(x.Trim(), "City", StringComparison.OrdinalIgnoreCase));
            GlobalMapPostcodeHeader = headers.FirstOrDefault(x => string.Equals(x.Trim(), "Postcode", StringComparison.OrdinalIgnoreCase));
            GlobalMapPhoneHeader = headers.FirstOrDefault(x => string.Equals(x.Trim(), "Phone", StringComparison.OrdinalIgnoreCase));
            GlobalMapWebsiteHeader = headers.FirstOrDefault(x => string.Equals(x.Trim(), "Website", StringComparison.OrdinalIgnoreCase));
            GlobalMapFye2025Header = headers.FirstOrDefault(x => string.Equals(x.Trim(), "FYE Last Financial Year", StringComparison.OrdinalIgnoreCase) || string.Equals(x.Trim(), "FYE2025", StringComparison.OrdinalIgnoreCase));
            GlobalMapFye2024Header = headers.FirstOrDefault(x => string.Equals(x.Trim(), "FYE Year-1", StringComparison.OrdinalIgnoreCase) || string.Equals(x.Trim(), "FYE2024", StringComparison.OrdinalIgnoreCase));
            GlobalMapFye2023Header = headers.FirstOrDefault(x => string.Equals(x.Trim(), "FYE Year-2", StringComparison.OrdinalIgnoreCase) || string.Equals(x.Trim(), "FYE2023", StringComparison.OrdinalIgnoreCase));
            GlobalMapFinancialYearHeader = headers.FirstOrDefault(x => string.Equals(x.Trim(), "Financial Year", StringComparison.OrdinalIgnoreCase));
            GlobalMapLastTin200YearHeader = headers.FirstOrDefault(x => string.Equals(x.Trim(), "Last TIN200 Year", StringComparison.OrdinalIgnoreCase));
            GlobalMapTinStatusHeader = headers.FirstOrDefault(x => string.Equals(x.Trim(), "TIN Status", StringComparison.OrdinalIgnoreCase) || string.Equals(x.Trim(), "TIN200", StringComparison.OrdinalIgnoreCase));
            ContactImportTinStatus = ResolveDefaultContactImportTinStatus(tinStatus);
            ContactImportSurveyYear = contactImportSurveyYear;
            StatusMessage = "Companies import file loaded. Review or adjust the header mappings, then preview the import.";
            return Page();
        }

        public async Task<IActionResult> OnPostApplyContactImportAsync(string? previewToken, string? selectedContactImportRows, int? tinStatus, int? contactImportSurveyYear, bool showMissingCompanySurveyOnly = false)
        {
            if (!User.IsInRole("1"))
            {
                ErrorMessage = "Companies import is only available to admin users.";
                return RedirectToPage(new { showMissingCompanySurveyOnly, showImportTool = "import", showImportTab = "contacts" });
            }

            CleanupExpiredPendingImports();

            if (string.IsNullOrWhiteSpace(previewToken) || !PendingImports.TryGetValue(previewToken, out var pendingImport))
            {
                ErrorMessage = "Apply companies import failed: preview session not found or expired. Please preview the file again.";
                return RedirectToPage(new { tinStatus, showMissingCompanySurveyOnly });
            }

            var selectedRows = ParseSelectedRowNumbers(selectedContactImportRows);
            var effectiveTinStatus = pendingImport.ContactImportTinStatus ?? ResolveDefaultContactImportTinStatus(tinStatus);
            var effectiveSurveyYear = pendingImport.ContactImportSurveyYear ?? contactImportSurveyYear;

            if (!effectiveSurveyYear.HasValue || effectiveSurveyYear.Value <= 0)
            {
                ErrorMessage = "Apply companies import failed: please select a Survey Year during preview.";
                return RedirectToPage(new { tinStatus, showMissingCompanySurveyOnly, showImportTool = "import", showImportTab = "contacts" });
            }

            try
            {
                await using var stream = System.IO.File.OpenRead(pendingImport.TempFilePath);
                var mapping = new CompanyService.CompanyContactImportMapping
                {
                    CompanyNameHeader = pendingImport.MappedCompanyNameHeader,
                    ExternalIdHeader = pendingImport.MappedExternalIdHeader,
                    CeoFirstNameHeader = pendingImport.MappedCeoFirstNameHeader,
                    CeoLastNameHeader = pendingImport.MappedCeoLastNameHeader,
                    ContactFirstNameHeader = pendingImport.MappedContactFirstNameHeader,
                    ContactLastNameHeader = pendingImport.MappedContactLastNameHeader,
                    ContactEmailHeader = pendingImport.MappedContactEmailHeader,
                    EmailHeader = pendingImport.MappedEmailHeader,
                    CompanyDescriptionHeader = pendingImport.MappedCompanyDescriptionHeader,
                    StreetHeader = pendingImport.MappedStreetHeader,
                    SuburbHeader = pendingImport.MappedSuburbHeader,
                    CityHeader = pendingImport.MappedCityHeader,
                    PostcodeHeader = pendingImport.MappedPostcodeHeader,
                    PhoneHeader = pendingImport.MappedPhoneHeader,
                    WebsiteHeader = pendingImport.MappedWebsiteHeader,
                    Fye2025Header = pendingImport.MappedFye2025Header,
                    Fye2024Header = pendingImport.MappedFye2024Header,
                    Fye2023Header = pendingImport.MappedFye2023Header,
                    FinancialYearHeader = pendingImport.MappedFinancialYearHeader,
                    LastTin200YearHeader = pendingImport.MappedLastTin200YearHeader,
                    TinStatusHeader = pendingImport.MappedTinStatusHeader,
                    ApplyTinStatus = pendingImport.ApplyTinStatus,
                    TinStatusToApply = effectiveTinStatus,
                    SelectedRowNumbers = selectedRows.Count > 0 ? selectedRows : null
                };

                var result = await _service.ImportContactsFromExcelAsync(stream, mapping, effectiveSurveyYear.Value);
                if (result.Errors.Any())
                {
                    StatusMessage = $"Companies import completed with warnings. Survey Year: {effectiveSurveyYear.Value}. Updated: {result.UpdatedCount}, Added: {result.InsertedCount}, Unchanged: {result.UnchangedCount}, CompanySurvey created: {result.CompanySurveyCreatedCount}. Warnings: {string.Join(" ", result.Errors.Take(5))}";
                }
                else
                {
                    StatusMessage = $"Companies import completed. Survey Year: {effectiveSurveyYear.Value}. Updated: {result.UpdatedCount}, Added: {result.InsertedCount}, Unchanged: {result.UnchangedCount}, CompanySurvey created: {result.CompanySurveyCreatedCount}.";
                }
            }
            finally
            {
                PendingImports.TryRemove(previewToken, out _);
                if (System.IO.File.Exists(pendingImport.TempFilePath))
                {
                    System.IO.File.Delete(pendingImport.TempFilePath);
                }
            }

            return RedirectToPage(new { lastTin200Year = pendingImport.LastTin200Year, tinStatus, companySearch = pendingImport.CompanySearch, showTestCompanies = pendingImport.ShowTestCompanies, showMissingCompanySurveyOnly = pendingImport.ShowMissingCompanySurveyOnly, showImportTool = pendingImport.ShowImportTool, showImportTab = pendingImport.ShowImportTab });
        }

        private async Task LoadPageAsync(int? lastTin200Year, string? companySearch, bool showTestCompanies = false, int? tinStatus = null, bool hasTinStatusFilter = false, bool showMissingCompanySurveyOnly = false)
        {
            AvailableLastTin200Years = await _service.GetAvailableLastTin200YearsAsync();
            SelectedImportYear ??= SelectedLastTin200Year;
            CompanySearch = (companySearch ?? string.Empty).Trim();
            ShowTestCompanies = showTestCompanies;
            SelectedTinStatus = NormalizeTinStatusFilter(tinStatus, hasTinStatusFilter);
            if (lastTin200Year.HasValue)
            {
                SelectedLastTin200Year = lastTin200Year.Value;
            }
            else
            {
                // default to all records when no filter is provided
                SelectedLastTin200Year = null;
            }

            Records = await _service.GetAllCompaniesAsync(SelectedLastTin200Year);
            if (SelectedTinStatus.HasValue)
            {
                Records = Records.Where(x => x.TinStatus == SelectedTinStatus.Value).ToList();
            }
            else
            {
                Records = Records.Where(x => !x.TinStatus.HasValue || x.TinStatus.Value == (int)TinStatus.Blank).ToList();
            }

            if (!ShowTestCompanies && SelectedTinStatus != (int)TinStatus.TinTest)
            {
                Records = Records.Where(x => !TinStatusHelper.IsTestCompany(x.TinStatus)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(CompanySearch))
            {
                Records = Records
                    .Where(x =>
                        (!string.IsNullOrWhiteSpace(x.CompanyName) && x.CompanyName.Contains(CompanySearch, StringComparison.OrdinalIgnoreCase))
                        || (!string.IsNullOrWhiteSpace(x.ExternalId) && x.ExternalId.Contains(CompanySearch, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            await LoadMissingCompanySurveyIndicatorsAsync();

            ShowMissingCompanySurveyOnly = showMissingCompanySurveyOnly;
            if (ShowMissingCompanySurveyOnly && LatestSurveyYear.HasValue)
            {
                Records = Records
                    .Where(x => MissingCompanySurveyCompanyIds.Contains(x.Id))
                    .ToList();

                MissingCompanySurveyCompanyIds = Records.Select(x => x.Id).ToHashSet();
                MissingCompanySurveyCount = MissingCompanySurveyCompanyIds.Count;
            }
        }

        private async Task LoadMissingCompanySurveyIndicatorsAsync()
        {
            LatestSurveyYear = await _context.Survey
                .Select(x => (int?)x.FinancialYear)
                .MaxAsync();

            MissingCompanySurveyCompanyIds = new HashSet<int>();
            MissingCompanySurveyCount = 0;

            if (!LatestSurveyYear.HasValue || Records.Count == 0)
            {
                return;
            }

            var visibleCompanyIds = Records.Select(x => x.Id).ToList();

            var existingCompanyIds = await (
                from companySurvey in _context.CompanySurvey
                join survey in _context.Survey on companySurvey.SurveyId equals survey.Id
                where survey.FinancialYear == LatestSurveyYear.Value && visibleCompanyIds.Contains(companySurvey.CompanyId)
                select companySurvey.CompanyId
            )
            .Distinct()
            .ToListAsync();

            var existingSet = existingCompanyIds.ToHashSet();
            MissingCompanySurveyCompanyIds = visibleCompanyIds
                .Where(id => !existingSet.Contains(id))
                .ToHashSet();

            MissingCompanySurveyCount = MissingCompanySurveyCompanyIds.Count;
        }

        private static int? NormalizeTinStatusFilter(int? tinStatus, bool hasTinStatusFilter)
        {
            if (!hasTinStatusFilter)
            {
                return (int)TinStatus.Tin200;
            }

            if (!tinStatus.HasValue || tinStatus.Value == (int)TinStatus.Blank)
            {
                return null;
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

        private static int? ResolveDefaultContactImportTinStatus(int? tinStatus)
        {
            if (!TinStatusHelper.IsValidSelection(tinStatus))
            {
                return (int)TinStatus.Tin200;
            }

            if (!tinStatus.HasValue || tinStatus.Value == (int)TinStatus.Blank)
            {
                return null;
            }

            return tinStatus.Value;
        }

        private static HashSet<int> ParseSelectedRowNumbers(string? selectedContactImportRows)
        {
            if (string.IsNullOrWhiteSpace(selectedContactImportRows))
            {
                return new HashSet<int>();
            }

            return selectedContactImportRows
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => int.TryParse(x, out var rowNumber) ? rowNumber : 0)
                .Where(x => x > 0)
                .ToHashSet();
        }

        private CompanyService.CompanyGlobalImportMapping BuildGlobalImportMapping()
        {
            return new CompanyService.CompanyGlobalImportMapping
            {
                CeoFirstNameHeader = GlobalMapCeoFirstNameHeader,
                CeoLastNameHeader = GlobalMapCeoLastNameHeader,
                ContactFirstNameHeader = GlobalMapContactFirstNameHeader,
                ContactLastNameHeader = GlobalMapContactLastNameHeader,
                ContactEmailHeader = GlobalMapContactEmailHeader,
                EmailHeader = GlobalMapEmailHeader,
                ExternalIdHeader = GlobalMapExternalIdHeader,
                CompanyNameHeader = GlobalMapCompanyNameHeader,
                CompanyDescriptionHeader = GlobalMapCompanyDescriptionHeader,
                StreetHeader = GlobalMapStreetHeader,
                SuburbHeader = GlobalMapSuburbHeader,
                CityHeader = GlobalMapCityHeader,
                PostcodeHeader = GlobalMapPostcodeHeader,
                PhoneHeader = GlobalMapPhoneHeader,
                WebsiteHeader = GlobalMapWebsiteHeader,
                Fye2025Header = GlobalMapFye2025Header,
                Fye2024Header = GlobalMapFye2024Header,
                Fye2023Header = GlobalMapFye2023Header,
                FinancialYearHeader = GlobalMapFinancialYearHeader,
                LastTin200YearHeader = GlobalMapLastTin200YearHeader,
                TinStatusHeader = GlobalMapTinStatusHeader
            };
        }

        private void ApplyGlobalMappingToPage(CompanyService.CompanyGlobalImportMapping? mapping)
        {
            if (mapping == null)
            {
                return;
            }

            GlobalMapCeoFirstNameHeader = mapping.CeoFirstNameHeader;
            GlobalMapCeoLastNameHeader = mapping.CeoLastNameHeader;
            GlobalMapContactFirstNameHeader = mapping.ContactFirstNameHeader;
            GlobalMapContactLastNameHeader = mapping.ContactLastNameHeader;
            GlobalMapContactEmailHeader = mapping.ContactEmailHeader;
            GlobalMapEmailHeader = mapping.EmailHeader;
            GlobalMapExternalIdHeader = mapping.ExternalIdHeader;
            GlobalMapCompanyNameHeader = mapping.CompanyNameHeader;
            GlobalMapCompanyDescriptionHeader = mapping.CompanyDescriptionHeader;
            GlobalMapStreetHeader = mapping.StreetHeader;
            GlobalMapSuburbHeader = mapping.SuburbHeader;
            GlobalMapCityHeader = mapping.CityHeader;
            GlobalMapPostcodeHeader = mapping.PostcodeHeader;
            GlobalMapPhoneHeader = mapping.PhoneHeader;
            GlobalMapWebsiteHeader = mapping.WebsiteHeader;
            GlobalMapFye2025Header = mapping.Fye2025Header;
            GlobalMapFye2024Header = mapping.Fye2024Header;
            GlobalMapFye2023Header = mapping.Fye2023Header;
            GlobalMapFinancialYearHeader = mapping.FinancialYearHeader;
            GlobalMapLastTin200YearHeader = mapping.LastTin200YearHeader;
            GlobalMapTinStatusHeader = mapping.TinStatusHeader;
        }

        private void ApplyDefaultGlobalHeaders(List<string> headers)
        {
            string? Find(string expected)
            {
                return headers.FirstOrDefault(x => string.Equals(x.Trim(), expected, StringComparison.OrdinalIgnoreCase));
            }

            GlobalMapCeoFirstNameHeader ??= Find("CEO First Name") ?? Find("CEOFirstName");
            GlobalMapCeoLastNameHeader ??= Find("CEO Last Name") ?? Find("CEOLastName");
            GlobalMapContactFirstNameHeader ??= Find("Contact Person - First Name") ?? Find("Contact First Name");
            GlobalMapContactLastNameHeader ??= Find("Contact Person - Last Name") ?? Find("Contact Last Name");
            GlobalMapContactEmailHeader ??= Find("Contact Email");
            GlobalMapEmailHeader ??= Find("Email");
            GlobalMapExternalIdHeader ??= Find("ID") ?? Find("External ID");
            GlobalMapCompanyNameHeader ??= Find("Company Name") ?? Find("Name");
            GlobalMapCompanyDescriptionHeader ??= Find("Company Description") ?? Find("Description");
            GlobalMapStreetHeader ??= Find("Street");
            GlobalMapSuburbHeader ??= Find("Suburb");
            GlobalMapCityHeader ??= Find("City");
            GlobalMapPostcodeHeader ??= Find("Postcode");
            GlobalMapPhoneHeader ??= Find("Phone");
            GlobalMapWebsiteHeader ??= Find("Website");
            GlobalMapFye2025Header ??= Find("FYE Last Financial Year") ?? Find("FYE2025");
            GlobalMapFye2024Header ??= Find("FYE Year-1") ?? Find("FYE2024");
            GlobalMapFye2023Header ??= Find("FYE Year-2") ?? Find("FYE2023");
            GlobalMapFinancialYearHeader ??= Find("Financial Year");
            GlobalMapLastTin200YearHeader ??= Find("Last TIN200 Year");
            GlobalMapTinStatusHeader ??= Find("TIN Status") ?? Find("TIN200");
        }

        private static void CleanupExpiredPendingImports()
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-30);
            foreach (var key in PendingImports.Keys)
            {
                if (PendingImports.TryGetValue(key, out var pendingImport) && pendingImport.CreatedUtc < cutoff)
                {
                    PendingImports.TryRemove(key, out _);
                    if (System.IO.File.Exists(pendingImport.TempFilePath))
                    {
                        System.IO.File.Delete(pendingImport.TempFilePath);
                    }
                }
            }
        }

        private sealed class PendingCompanyImport
        {
            public string Token { get; set; } = string.Empty;
            public string TempFilePath { get; set; } = string.Empty;
            public DateTime CreatedUtc { get; set; }
            public int? LastTin200Year { get; set; }
            public string? CompanySearch { get; set; }
            public bool ShowTestCompanies { get; set; }
            public bool ShowMissingCompanySurveyOnly { get; set; }
            public int ImportYear { get; set; }
            public int? ImportTinStatus { get; set; }
            public string Kind { get; set; } = "Global";
            public string? ShowImportTool { get; set; }
            public string? ShowImportTab { get; set; }
            public string? MappedCompanyNameHeader { get; set; }
            public string? MappedExternalIdHeader { get; set; }
            public string? MappedCeoFirstNameHeader { get; set; }
            public string? MappedCeoLastNameHeader { get; set; }
            public string? MappedContactFirstNameHeader { get; set; }
            public string? MappedContactLastNameHeader { get; set; }
            public string? MappedContactEmailHeader { get; set; }
            public string? MappedEmailHeader { get; set; }
            public string? MappedCompanyDescriptionHeader { get; set; }
            public string? MappedStreetHeader { get; set; }
            public string? MappedSuburbHeader { get; set; }
            public string? MappedCityHeader { get; set; }
            public string? MappedPostcodeHeader { get; set; }
            public string? MappedPhoneHeader { get; set; }
            public string? MappedWebsiteHeader { get; set; }
            public string? MappedFye2025Header { get; set; }
            public string? MappedFye2024Header { get; set; }
            public string? MappedFye2023Header { get; set; }
            public string? MappedFinancialYearHeader { get; set; }
            public string? MappedLastTin200YearHeader { get; set; }
            public string? MappedTinStatusHeader { get; set; }
            public bool ApplyTinStatus { get; set; }
            public int? ContactImportTinStatus { get; set; }
            public int? ContactImportSurveyYear { get; set; }
            public CompanyService.CompanyGlobalImportMapping? GlobalMapping { get; set; }
        }
    }
}

