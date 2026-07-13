using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.Survey
{
    public class EditModel : PageModel
    {
        private readonly SurveyService _service;
        private readonly ApplicationDbContext _context;
        private readonly IImageStorageService _imageStorageService;

        private const int MissingCompanyPreviewLimit = 250;

        [BindProperty]
        public Models.Survey Record { get; set; } = new();

        [BindProperty]
        public IFormFile? HeaderImageFile { get; set; }

        [BindProperty]
        public int BatchSurveyYear { get; set; }

        [BindProperty]
        public List<int> SelectedCompanyIds { get; set; } = new();

        public List<SelectListItem> HeaderImageOptions { get; set; } = new();
        public string? HeaderImageThumbnailUrl { get; set; }
        public string? HeaderImageFileName { get; set; }
        public bool HeaderImageMissing { get; set; }
        public string? HeaderImageMissingMessage { get; set; }
        public int LatestSurveyYear { get; set; }
        public List<MissingCompanySurveyRow> MissingCompanySurveyRows { get; set; } = new();

        [TempData]
        public string? StatusMessage { get; set; }

        public EditModel(SurveyService service, ApplicationDbContext context, IImageStorageService imageStorageService)
        {
            _service = service;
            _context = context;
            _imageStorageService = imageStorageService;
        }

        public async Task<IActionResult> OnGetAsync(int? id, int? batchSurveyYear)
        {
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
            await LoadHeaderImageOptionsAsync();
            await LoadHeaderImagePreviewAsync();
            await LoadMissingCompanySurveyDataAsync(batchSurveyYear);
            return Page();
        }

        public async Task<IActionResult> OnGetHeaderImageAsync(int id, int imageId)
        {
            var survey = await _service.GetByIdAsync(id);
            if (survey?.HeaderImageId != imageId)
            {
                return NotFound();
            }

            var image = await _context.Image.FirstOrDefaultAsync(x => x.Id == imageId);
            if (image == null || string.IsNullOrWhiteSpace(image.FilePath))
            {
                return NotFound();
            }

            var stream = await _imageStorageService.OpenReadAsync(image.FilePath);
            if (stream == null)
            {
                return NotFound();
            }

            return File(stream, GetContentTypeFromPath(image.FilePath));
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadHeaderImageOptionsAsync();
                await LoadHeaderImagePreviewAsync();
                await LoadMissingCompanySurveyDataAsync(BatchSurveyYear > 0 ? BatchSurveyYear : null);
                return Page();
            }

            if (!await _service.ExistsAsync(Record.Id))
            {
                return NotFound();
            }

            if (HeaderImageFile != null && HeaderImageFile.Length > 0)
            {
                Record.HeaderImageId = await SaveHeaderImageAsync(Record.Id, HeaderImageFile);
            }

            await _service.UpdateAsync(Record);
            return RedirectToPage("./Index");
        }

        public async Task<IActionResult> OnPostBatchCreateMissingCompanySurveysAsync()
        {
            var survey = await _service.GetByIdAsync(Record.Id);
            if (survey == null)
            {
                return NotFound();
            }

            Record = survey;
            await LoadHeaderImageOptionsAsync();
            await LoadHeaderImagePreviewAsync();

            if (BatchSurveyYear <= 0)
            {
                ModelState.AddModelError(nameof(BatchSurveyYear), "Enter a valid survey year.");
                await LoadMissingCompanySurveyDataAsync(null);
                return Page();
            }

            var selectedCompanyIds = SelectedCompanyIds
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (selectedCompanyIds.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Select at least one company.");
                await LoadMissingCompanySurveyDataAsync(BatchSurveyYear);
                return Page();
            }

            var targetSurveyId = await _context.Survey
                .Where(x => x.FinancialYear == BatchSurveyYear)
                .OrderByDescending(x => x.CurrentSurvey)
                .ThenByDescending(x => x.Id)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync();

            if (!targetSurveyId.HasValue)
            {
                ModelState.AddModelError(nameof(BatchSurveyYear), $"No survey record exists for year {BatchSurveyYear}.");
                await LoadMissingCompanySurveyDataAsync(BatchSurveyYear);
                return Page();
            }

            var selectedSet = selectedCompanyIds.ToHashSet();

            var companyIdsWithSurveyForYear = await (
                from companySurvey in _context.CompanySurvey
                join surveyByYear in _context.Survey on companySurvey.SurveyId equals surveyByYear.Id
                where surveyByYear.FinancialYear == BatchSurveyYear
                select companySurvey.CompanyId
            ).Distinct().ToListAsync();

            var existingForYearSet = companyIdsWithSurveyForYear.ToHashSet();
            var companyIdsToCreate = selectedSet
                .Where(companyId => !existingForYearSet.Contains(companyId))
                .ToList();

            if (companyIdsToCreate.Count == 0)
            {
                StatusMessage = $"No records were created. Selected companies already have CompanySurvey rows for year {BatchSurveyYear}.";
                return RedirectToPage(new { id = Record.Id, batchSurveyYear = BatchSurveyYear });
            }

            var validCompanyIds = await _context.Tin200
                .Where(x => companyIdsToCreate.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync();

            if (validCompanyIds.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "No valid companies were selected.");
                await LoadMissingCompanySurveyDataAsync(BatchSurveyYear);
                return Page();
            }

            var newRecords = validCompanyIds.Select(companyId => new Models.CompanySurvey
            {
                CompanyId = companyId,
                SurveyId = targetSurveyId.Value,
                Saved = false,
                Submitted = false,
                Requested = false,
                Locked = false,
                Estimate = false
            }).ToList();

            _context.CompanySurvey.AddRange(newRecords);
            await _context.SaveChangesAsync();

            var skippedCount = selectedCompanyIds.Count - newRecords.Count;
            StatusMessage = $"Created {newRecords.Count} CompanySurvey record(s) for year {BatchSurveyYear}. Skipped {Math.Max(0, skippedCount)} already covered company(s).";

            return RedirectToPage(new { id = Record.Id, batchSurveyYear = BatchSurveyYear });
        }

        private async Task<int?> SaveHeaderImageAsync(int surveyId, IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".bin";
            }

            var storagePath = await _imageStorageService.SaveImageAsync(file, "survey", surveyId);

            var image = new Image
            {
                EntityType = "survey",
                EntityId = surveyId,
                FileName = file.FileName,
                FilePath = storagePath,
                FileType = extension.TrimStart('.').ToLowerInvariant(),
                FileSize = file.Length > int.MaxValue ? int.MaxValue : (int)file.Length,
                CreatedDate = DateTime.UtcNow
            };

            _context.Image.Add(image);
            await _context.SaveChangesAsync();
            return image.Id;
        }

        private async Task LoadHeaderImageOptionsAsync()
        {
            var options = await _context.Image
                .OrderBy(x => x.Id)
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = $"{x.Id} - {x.FileName}"
                })
                .ToListAsync();

            options.Insert(0, new SelectListItem
            {
                Value = string.Empty,
                Text = "-- None --"
            });

            HeaderImageOptions = options;
        }

        private async Task LoadHeaderImagePreviewAsync()
        {
            HeaderImageThumbnailUrl = null;
            HeaderImageFileName = null;
            HeaderImageMissing = false;
            HeaderImageMissingMessage = null;

            if (!Record.HeaderImageId.HasValue)
            {
                return;
            }

            var image = await _context.Image.FirstOrDefaultAsync(x => x.Id == Record.HeaderImageId.Value);
            if (image == null)
            {
                HeaderImageMissing = true;
                HeaderImageMissingMessage = "Selected header image record is missing from Image table.";
                return;
            }

            if (string.IsNullOrWhiteSpace(image.FilePath))
            {
                HeaderImageMissing = true;
                HeaderImageFileName = image.FileName;
                HeaderImageMissingMessage = "Selected header image has no file path.";
                return;
            }

            if (!await _imageStorageService.ExistsAsync(image.FilePath))
            {
                HeaderImageMissing = true;
                HeaderImageFileName = image.FileName;
                HeaderImageMissingMessage = "Selected header image file is missing from storage.";
                return;
            }

            HeaderImageFileName = image.FileName;
            HeaderImageThumbnailUrl = Url.Page("./Edit", "HeaderImage", new { id = Record.Id, imageId = image.Id });
        }

        private async Task LoadMissingCompanySurveyDataAsync(int? batchSurveyYear)
        {
            LatestSurveyYear = await _context.Survey
                .Select(x => (int?)x.FinancialYear)
                .MaxAsync() ?? 0;

            BatchSurveyYear = batchSurveyYear.HasValue && batchSurveyYear.Value > 0
                ? batchSurveyYear.Value
                : LatestSurveyYear;

            if (BatchSurveyYear <= 0)
            {
                MissingCompanySurveyRows = new List<MissingCompanySurveyRow>();
                return;
            }

            var companyIdsWithSurveyForYear =
                from companySurvey in _context.CompanySurvey
                join surveyByYear in _context.Survey on companySurvey.SurveyId equals surveyByYear.Id
                where surveyByYear.FinancialYear == BatchSurveyYear
                select companySurvey.CompanyId;

            MissingCompanySurveyRows = await _context.Tin200
                .Where(company => !companyIdsWithSurveyForYear.Contains(company.Id))
                .OrderBy(company => company.CompanyName)
                .ThenBy(company => company.Id)
                .Select(company => new MissingCompanySurveyRow
                {
                    CompanyId = company.Id,
                    CompanyName = company.CompanyName,
                    ExternalId = company.ExternalId,
                    ContactEmail = company.ContactEmail,
                    Email = company.Email
                })
                .Take(MissingCompanyPreviewLimit)
                .ToListAsync();
        }

        private static string GetContentTypeFromPath(string filePath)
        {
            var contentTypeProvider = new FileExtensionContentTypeProvider();
            var extension = Path.GetExtension(filePath);
            if (!contentTypeProvider.TryGetContentType($"file{extension}", out var contentType))
            {
                return "application/octet-stream";
            }

            return contentType;
        }

        public class MissingCompanySurveyRow
        {
            public int CompanyId { get; set; }
            public string? CompanyName { get; set; }
            public string? ExternalId { get; set; }
            public string? ContactEmail { get; set; }
            public string? Email { get; set; }
        }
    }
}
