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
        private readonly IWebHostEnvironment _environment;

        [BindProperty]
        public Models.Survey Record { get; set; } = new();

        [BindProperty]
        public IFormFile? HeaderImageFile { get; set; }

        public List<SelectListItem> HeaderImageOptions { get; set; } = new();
        public string? HeaderImageThumbnailUrl { get; set; }
        public string? HeaderImageFileName { get; set; }
        public bool HeaderImageMissing { get; set; }
        public string? HeaderImageMissingMessage { get; set; }

        public EditModel(SurveyService service, ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _service = service;
            _context = context;
            _environment = environment;
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

            var normalizedRelativePath = image.FilePath
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);

            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), normalizedRelativePath);
            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound();
            }

            var contentTypeProvider = new FileExtensionContentTypeProvider();
            var extension = Path.GetExtension(fullPath);
            if (!contentTypeProvider.TryGetContentType($"file{extension}", out var contentType))
            {
                contentType = "application/octet-stream";
            }

            return PhysicalFile(fullPath, contentType);
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

            var generatedFileName = $"survey_{surveyId}_{Guid.NewGuid():N}{extension}";
            var imagesFolderPath = Path.Combine(_environment.ContentRootPath, "Images");
            Directory.CreateDirectory(imagesFolderPath);

            var physicalFilePath = Path.Combine(imagesFolderPath, generatedFileName);
            await using (var stream = System.IO.File.Create(physicalFilePath))
            {
                await file.CopyToAsync(stream);
            }

            var image = new Image
            {
                EntityType = "survey",
                EntityId = surveyId,
                FileName = file.FileName,
                FilePath = Path.Combine("Images", generatedFileName).Replace("\\", "/"),
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

            var normalizedRelativePath = image.FilePath
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), normalizedRelativePath);
            if (!System.IO.File.Exists(fullPath))
            {
                HeaderImageMissing = true;
                HeaderImageFileName = image.FileName;
                HeaderImageMissingMessage = "Selected header image file is missing on disk.";
                return;
            }

            HeaderImageFileName = image.FileName;
            HeaderImageThumbnailUrl = Url.Page("./Edit", "HeaderImage", new { id = Record.Id, imageId = image.Id });
        }
    }
}
