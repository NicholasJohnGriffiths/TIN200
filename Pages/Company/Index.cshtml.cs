using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Models;
using TINWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace TINWeb.Pages.Company
{
    public class IndexModel : PageModel
    {
        private readonly CompanyService _service;
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
        public int? SelectedImportYear { get; set; }

        [TempData]
        public string? StatusMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public IndexModel(CompanyService service)
        {
            _service = service;
        }

        public async Task OnGetAsync(int? lastTin200Year, int? focusId, string? companySearch, int? tinStatus, bool showTestCompanies = false, string? showImportTool = null, string? showImportTab = null)
        {
            FocusId = focusId;
            ShowImportTool = showImportTool;
            ShowImportTab = showImportTab;

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
            await LoadPageAsync(lastTin200Year, companySearch, showTestCompanies, tinStatus, hasTinStatusFilter);
        }

        public async Task<IActionResult> OnPostPreviewResetFyeValuesAsync(int? lastTin200Year, string? companySearch, bool showTestCompanies = false)
        {
            if (!User.IsInRole("1"))
            {
                ErrorMessage = "This action is only available to admin users.";
                return RedirectToPage(new { lastTin200Year, companySearch, showTestCompanies });
            }

            PreviewSummary = await _service.PreviewResetFyeValuesFromSurveyAnswersAsync();
            await LoadPageAsync(lastTin200Year, companySearch, showTestCompanies);
            return Page();
        }

        public async Task<IActionResult> OnPostResetFyeValuesAsync(int? lastTin200Year, string? companySearch, bool showTestCompanies = false)
        {
            if (!User.IsInRole("1"))
            {
                ErrorMessage = "This action is only available to admin users.";
                return RedirectToPage(new { lastTin200Year, companySearch, showTestCompanies });
            }

            var result = await _service.ResetFyeValuesFromSurveyAnswersAsync();

            if (!result.HasCurrentSurvey)
            {
                StatusMessage = "Update Company Info skipped: no current survey is configured.";
                return RedirectToPage(new { lastTin200Year, companySearch, showTestCompanies });
            }

            StatusMessage = $"Update Company Info complete (Current survey year: {result.CurrentSurveyYear}). Updated {result.UpdatedCompanyCount} of {result.TotalMatchedCompanies} matched company record(s).";
            return RedirectToPage(new { lastTin200Year, companySearch, showTestCompanies });
        }

        public async Task<IActionResult> OnPostPreviewGlobalImportAsync(IFormFile? importFile, int? lastTin200Year, int? importYear, string? companySearch, bool showTestCompanies = false, string? showImportTool = null, string? showImportTab = null)
        {
            if (!User.IsInRole("1"))
            {
                ErrorMessage = "Companies import is only available to admin users.";
                return RedirectToPage(new { lastTin200Year, companySearch, showTestCompanies, showImportTool = "import", showImportTab = "global" });
            }

            ShowImportTool = showImportTool;
            ShowImportTab = string.IsNullOrWhiteSpace(showImportTab) ? "global" : showImportTab;
            if (importFile == null || importFile.Length == 0)
            {
                ErrorMessage = "Global company import failed: please select an Excel file.";
                return RedirectToPage(new { lastTin200Year, companySearch, showTestCompanies, showImportTool, showImportTab = ShowImportTab });
            }

            if (!importYear.HasValue || importYear.Value <= 0)
            {
                ErrorMessage = "Global company import failed: please provide a valid Import Year.";
                return RedirectToPage(new { lastTin200Year, companySearch, showTestCompanies, showImportTool, showImportTab = ShowImportTab });
            }

            var fileName = importFile.FileName ?? string.Empty;
            if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = "Global company import failed: only .xlsx Excel files are supported.";
                return RedirectToPage(new { lastTin200Year, companySearch, showTestCompanies, showImportTool, showImportTab = ShowImportTab });
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

            CompanyService.CompanyGlobalImportPreviewResult preview;
            await using (var readStream = System.IO.File.OpenRead(tempFilePath))
            {
                preview = await _service.PreviewGlobalImportFromExcelAsync(readStream, importYear.Value);
            }

            PendingImports[token] = new PendingCompanyImport
            {
                Token = token,
                TempFilePath = tempFilePath,
                CreatedUtc = DateTime.UtcNow,
                LastTin200Year = lastTin200Year,
                CompanySearch = companySearch,
                ShowTestCompanies = showTestCompanies,
                ImportYear = importYear.Value,
                ShowImportTool = showImportTool,
                ShowImportTab = ShowImportTab
            };

            await LoadPageAsync(lastTin200Year, companySearch, showTestCompanies);
            GlobalImportPreview = preview;
            PendingGlobalImportToken = token;
            SelectedImportYear = importYear.Value;
            return Page();
        }

        public async Task<IActionResult> OnPostApplyGlobalImportAsync(string? previewToken)
        {
            if (!User.IsInRole("1"))
            {
                ErrorMessage = "Companies import is only available to admin users.";
                return RedirectToPage(new { showImportTool = "import", showImportTab = "global" });
            }

            CleanupExpiredPendingImports();

            if (string.IsNullOrWhiteSpace(previewToken) || !PendingImports.TryGetValue(previewToken, out var pendingImport))
            {
                ErrorMessage = "Apply global company import failed: preview session not found or expired. Please preview the file again.";
                return RedirectToPage();
            }

            try
            {
                await using var stream = System.IO.File.OpenRead(pendingImport.TempFilePath);
                var result = await _service.ImportGlobalFromExcelAsync(stream, pendingImport.ImportYear);
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

            return RedirectToPage(new { lastTin200Year = pendingImport.LastTin200Year, companySearch = pendingImport.CompanySearch, showTestCompanies = pendingImport.ShowTestCompanies, showImportTool = pendingImport.ShowImportTool, showImportTab = pendingImport.ShowImportTab });
        }

        public async Task<IActionResult> OnPostPreviewContactImportAsync(IFormFile? importFile, int? lastTin200Year, string? companySearch, bool showTestCompanies = false, string? showImportTool = null, string? showImportTab = null)
        {
            if (!User.IsInRole("1"))
            {
                ErrorMessage = "Companies import is only available to admin users.";
                return RedirectToPage(new { lastTin200Year, companySearch, showTestCompanies, showImportTool = "import", showImportTab = "contacts" });
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

            if (sourceImport == null && (importFile == null || importFile.Length == 0))
            {
                ErrorMessage = "Companies import failed: please select an Excel file.";
                return RedirectToPage(new { lastTin200Year, companySearch, showTestCompanies, showImportTool, showImportTab = ShowImportTab });
            }

            if (sourceImport == null)
            {
                var fileName = importFile?.FileName ?? string.Empty;
                if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    ErrorMessage = "Companies import failed: only .xlsx Excel files are supported.";
                    return RedirectToPage(new { lastTin200Year, companySearch, showTestCompanies, showImportTool, showImportTab = ShowImportTab });
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
                    Kind = "ContactPreview",
                    ShowImportTool = showImportTool,
                    ShowImportTab = ShowImportTab
                };
            }

            var selectedMapping = new CompanyService.CompanyContactImportMapping
            {
                CompanyNameHeader = ContactMapCompanyNameHeader,
                ContactFirstNameHeader = ContactMapFirstNameHeader,
                ContactLastNameHeader = ContactMapLastNameHeader,
                ContactEmailHeader = ContactMapEmailHeader,
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
            sourceImport.ShowImportTool = showImportTool;
            sourceImport.ShowImportTab = ShowImportTab;
            sourceImport.Kind = "ContactPreview";
            sourceImport.MappedCompanyNameHeader = preview.MatchedFields.FirstOrDefault(x => x.StartsWith("Company Name ->", StringComparison.OrdinalIgnoreCase))?.Split("->", 2).ElementAtOrDefault(1)?.Trim();
            sourceImport.MappedContactFirstNameHeader = preview.MatchedFields.FirstOrDefault(x => x.StartsWith("Contact First Name ->", StringComparison.OrdinalIgnoreCase))?.Split("->", 2).ElementAtOrDefault(1)?.Trim();
            sourceImport.MappedContactLastNameHeader = preview.MatchedFields.FirstOrDefault(x => x.StartsWith("Contact Last Name ->", StringComparison.OrdinalIgnoreCase))?.Split("->", 2).ElementAtOrDefault(1)?.Trim();
            sourceImport.MappedContactEmailHeader = preview.MatchedFields.FirstOrDefault(x => x.StartsWith("Contact Email ->", StringComparison.OrdinalIgnoreCase))?.Split("->", 2).ElementAtOrDefault(1)?.Trim();
            sourceImport.ApplyTinStatus = true;
            sourceImport.ContactImportTinStatus = ContactImportTinStatus;

            PendingImports[sourceImport.Token] = sourceImport;

            await LoadPageAsync(lastTin200Year, companySearch, showTestCompanies);
            ContactImportPreview = preview;
            PendingContactImportToken = sourceImport.Token;
            ContactImportMappingToken = sourceImport.Token;
            ContactMapCompanyNameHeader = sourceImport.MappedCompanyNameHeader;
            ContactMapFirstNameHeader = sourceImport.MappedContactFirstNameHeader;
            ContactMapLastNameHeader = sourceImport.MappedContactLastNameHeader;
            ContactMapEmailHeader = sourceImport.MappedContactEmailHeader;
            ContactImportTinStatus = sourceImport.ContactImportTinStatus;
            return Page();
        }

        public async Task<IActionResult> OnPostLoadContactImportHeadersAsync(IFormFile? importFile, int? lastTin200Year, string? companySearch, bool showTestCompanies = false, string? showImportTool = null, string? showImportTab = null)
        {
            if (!User.IsInRole("1"))
            {
                ErrorMessage = "Companies import is only available to admin users.";
                return RedirectToPage(new { lastTin200Year, companySearch, showTestCompanies, showImportTool = "import", showImportTab = "contacts" });
            }

            ShowImportTool = showImportTool;
            ShowImportTab = string.IsNullOrWhiteSpace(showImportTab) ? "contacts" : showImportTab;
            if (importFile == null || importFile.Length == 0)
            {
                ErrorMessage = "Companies import mapping failed: please select an Excel file.";
                return RedirectToPage(new { lastTin200Year, companySearch, showTestCompanies, showImportTool, showImportTab = ShowImportTab });
            }

            var fileName = importFile.FileName ?? string.Empty;
            if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = "Companies import mapping failed: only .xlsx Excel files are supported.";
                return RedirectToPage(new { lastTin200Year, companySearch, showTestCompanies, showImportTool, showImportTab = ShowImportTab });
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
                return RedirectToPage(new { lastTin200Year, companySearch, showTestCompanies, showImportTool, showImportTab = ShowImportTab });
            }

            PendingImports[token] = new PendingCompanyImport
            {
                Token = token,
                TempFilePath = tempFilePath,
                CreatedUtc = DateTime.UtcNow,
                LastTin200Year = lastTin200Year,
                CompanySearch = companySearch,
                ShowTestCompanies = showTestCompanies,
                Kind = "ContactMapping",
                ShowImportTool = showImportTool,
                ShowImportTab = ShowImportTab
            };

            await LoadPageAsync(lastTin200Year, companySearch, showTestCompanies);
            ContactImportHeaders = headers;
            ContactImportMappingToken = token;
            ContactMapCompanyNameHeader = headers.FirstOrDefault(x => string.Equals(x.Trim(), "Company Name", StringComparison.OrdinalIgnoreCase));
            ContactMapFirstNameHeader = headers.FirstOrDefault(x => string.Equals(x.Trim(), "Contact Person - First Name", StringComparison.OrdinalIgnoreCase));
            ContactMapLastNameHeader = headers.FirstOrDefault(x => string.Equals(x.Trim(), "Contact Person - Last Name", StringComparison.OrdinalIgnoreCase));
            ContactMapEmailHeader = headers.FirstOrDefault(x => string.Equals(x.Trim(), "Contact Email", StringComparison.OrdinalIgnoreCase));
            ContactImportTinStatus = (int)TinStatus.Tin200;
            StatusMessage = "Companies import file loaded. Review or adjust the header mappings, then preview the import.";
            return Page();
        }

        public async Task<IActionResult> OnPostApplyContactImportAsync(string? previewToken, string? selectedContactImportRows)
        {
            if (!User.IsInRole("1"))
            {
                ErrorMessage = "Companies import is only available to admin users.";
                return RedirectToPage(new { showImportTool = "import", showImportTab = "contacts" });
            }

            CleanupExpiredPendingImports();

            if (string.IsNullOrWhiteSpace(previewToken) || !PendingImports.TryGetValue(previewToken, out var pendingImport))
            {
                ErrorMessage = "Apply companies import failed: preview session not found or expired. Please preview the file again.";
                return RedirectToPage();
            }

            var selectedRows = ParseSelectedRowNumbers(selectedContactImportRows);
            if (selectedRows.Count == 0)
            {
                ErrorMessage = "Apply companies import failed: please select at least one row from the preview.";
                return RedirectToPage(new { lastTin200Year = pendingImport.LastTin200Year, companySearch = pendingImport.CompanySearch, showTestCompanies = pendingImport.ShowTestCompanies, showImportTool = pendingImport.ShowImportTool, showImportTab = pendingImport.ShowImportTab });
            }

            try
            {
                await using var stream = System.IO.File.OpenRead(pendingImport.TempFilePath);
                var mapping = new CompanyService.CompanyContactImportMapping
                {
                    CompanyNameHeader = pendingImport.MappedCompanyNameHeader,
                    ContactFirstNameHeader = pendingImport.MappedContactFirstNameHeader,
                    ContactLastNameHeader = pendingImport.MappedContactLastNameHeader,
                    ContactEmailHeader = pendingImport.MappedContactEmailHeader,
                    ApplyTinStatus = pendingImport.ApplyTinStatus,
                    TinStatusToApply = pendingImport.ContactImportTinStatus,
                    SelectedRowNumbers = selectedRows
                };

                var result = await _service.ImportContactsFromExcelAsync(stream, mapping);
                if (result.Errors.Any())
                {
                    StatusMessage = $"Companies import completed with warnings. Updated: {result.UpdatedCount}, Added: {result.InsertedCount}, Unchanged: {result.UnchangedCount}. Warnings: {string.Join(" ", result.Errors.Take(5))}";
                }
                else
                {
                    StatusMessage = $"Companies import completed. Updated: {result.UpdatedCount}, Added: {result.InsertedCount}, Unchanged: {result.UnchangedCount}.";
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

            return RedirectToPage(new { lastTin200Year = pendingImport.LastTin200Year, companySearch = pendingImport.CompanySearch, showTestCompanies = pendingImport.ShowTestCompanies, showImportTool = pendingImport.ShowImportTool, showImportTab = pendingImport.ShowImportTab });
        }

        private async Task LoadPageAsync(int? lastTin200Year, string? companySearch, bool showTestCompanies = false, int? tinStatus = null, bool hasTinStatusFilter = false)
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
            public int ImportYear { get; set; }
            public string Kind { get; set; } = "Global";
            public string? ShowImportTool { get; set; }
            public string? ShowImportTab { get; set; }
            public string? MappedCompanyNameHeader { get; set; }
            public string? MappedContactFirstNameHeader { get; set; }
            public string? MappedContactLastNameHeader { get; set; }
            public string? MappedContactEmailHeader { get; set; }
            public bool ApplyTinStatus { get; set; }
            public int? ContactImportTinStatus { get; set; }
        }
    }
}

