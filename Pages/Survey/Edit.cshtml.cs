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

        [BindProperty]
        public Models.Survey Record { get; set; } = new();

        [BindProperty]
        public IFormFile? HeaderImageFile { get; set; }

        public List<SelectListItem> HeaderImageOptions { get; set; } = new();
        public string? HeaderImageThumbnailUrl { get; set; }
        public string? HeaderImageFileName { get; set; }
        public bool HeaderImageMissing { get; set; }
        public string? HeaderImageMissingMessage { get; set; }

        public EditModel(SurveyService service, ApplicationDbContext context, IImageStorageService imageStorageService)
        {
            _service = service;
            _context = context;
            _imageStorageService = imageStorageService;
        }

        public async Task<IActionResult> OnGetAsync(int? id)
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
    }
}
