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
        public CompanyService.ResetFyeValuesResult? PreviewSummary { get; set; }
        public CompanyService.CompanyGlobalImportPreviewResult? GlobalImportPreview { get; set; }
        public string? PendingGlobalImportToken { get; set; }
        public int? SelectedImportYear { get; set; }

        [TempData]
        public string? StatusMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public IndexModel(CompanyService service)
        {
            _service = service;
        }

        public async Task OnGetAsync(int? lastTin200Year, int? focusId)
        {
            FocusId = focusId;
            await LoadPageAsync(lastTin200Year);
        }

        public async Task<IActionResult> OnPostPreviewResetFyeValuesAsync(int? lastTin200Year)
        {
            PreviewSummary = await _service.PreviewResetFyeValuesFromSurveyAnswersAsync();
            await LoadPageAsync(lastTin200Year);
            return Page();
        }

        public async Task<IActionResult> OnPostResetFyeValuesAsync(int? lastTin200Year)
        {
            var result = await _service.ResetFyeValuesFromSurveyAnswersAsync();

            if (!result.HasCurrentSurvey)
            {
                StatusMessage = "Update Company Info skipped: no current survey is configured.";
                return RedirectToPage(new { lastTin200Year });
            }

            StatusMessage = $"Update Company Info complete (Current survey year: {result.CurrentSurveyYear}). Updated {result.UpdatedCompanyCount} of {result.TotalMatchedCompanies} matched company record(s).";
            return RedirectToPage(new { lastTin200Year });
        }

        public async Task<IActionResult> OnPostPreviewGlobalImportAsync(IFormFile? importFile, int? lastTin200Year, int? importYear)
        {
            if (importFile == null || importFile.Length == 0)
            {
                ErrorMessage = "Global company import failed: please select an Excel file.";
                return RedirectToPage(new { lastTin200Year });
            }

            if (!importYear.HasValue || importYear.Value <= 0)
            {
                ErrorMessage = "Global company import failed: please provide a valid Import Year.";
                return RedirectToPage(new { lastTin200Year });
            }

            var fileName = importFile.FileName ?? string.Empty;
            if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = "Global company import failed: only .xlsx Excel files are supported.";
                return RedirectToPage(new { lastTin200Year });
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
                ImportYear = importYear.Value
            };

            await LoadPageAsync(lastTin200Year);
            GlobalImportPreview = preview;
            PendingGlobalImportToken = token;
            SelectedImportYear = importYear.Value;
            return Page();
        }

        public async Task<IActionResult> OnPostApplyGlobalImportAsync(string? previewToken)
        {
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

            return RedirectToPage(new { lastTin200Year = pendingImport.LastTin200Year });
        }

        private async Task LoadPageAsync(int? lastTin200Year)
        {
            AvailableLastTin200Years = await _service.GetAvailableLastTin200YearsAsync();
            SelectedImportYear ??= SelectedLastTin200Year;
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
            public int ImportYear { get; set; }
        }
    }
}

