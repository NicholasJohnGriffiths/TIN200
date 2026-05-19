using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.Config;

[Authorize(Policy = "AdminOnly")]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IImageStorageService _imageStorageService;

    [BindProperty]
    public AppConfig Record { get; set; } = new();

    [BindProperty]
    public IFormFile? EmailHeaderImageFile { get; set; }

    public List<SelectListItem> EmailHeaderImageOptions { get; set; } = new();
    public string? EmailHeaderImageThumbnailUrl { get; set; }
    public string? EmailHeaderImageFileName { get; set; }
    public bool EmailHeaderImageMissing { get; set; }
    public string? EmailHeaderImageMissingMessage { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public EditModel(ApplicationDbContext context, IImageStorageService imageStorageService)
    {
        _context = context;
        _imageStorageService = imageStorageService;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var record = await _context.AppConfig.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (record == null)
        {
            return NotFound();
        }

        Record = record;
        await LoadEmailHeaderImageOptionsAsync();
        await LoadEmailHeaderImagePreviewAsync();
        return Page();
    }

    public async Task<IActionResult> OnGetEmailHeaderImageAsync(int id, int imageId)
    {
        var config = await _context.AppConfig.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (config?.EmailHeaderImageId != imageId)
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
        Record.AdminEmail = Record.AdminEmail?.Trim() ?? string.Empty;
        Record.SurveyEmailSubject = string.IsNullOrWhiteSpace(Record.SurveyEmailSubject)
            ? null
            : Record.SurveyEmailSubject.Trim();
        Record.SurveyEmailTemplate = string.IsNullOrWhiteSpace(Record.SurveyEmailTemplate)
            ? null
            : Record.SurveyEmailTemplate.Trim();
        Record.SurveyReminderEmailSubject = string.IsNullOrWhiteSpace(Record.SurveyReminderEmailSubject)
            ? null
            : Record.SurveyReminderEmailSubject.Trim();
        Record.SurveyReminderEmailTemplate = string.IsNullOrWhiteSpace(Record.SurveyReminderEmailTemplate)
            ? null
            : Record.SurveyReminderEmailTemplate.Trim();

        if (string.IsNullOrWhiteSpace(Record.AdminEmail))
        {
            ModelState.AddModelError("Record.AdminEmail", "Admin email is required.");
        }

        if (!ModelState.IsValid)
        {
            await LoadEmailHeaderImageOptionsAsync();
            await LoadEmailHeaderImagePreviewAsync();
            return Page();
        }

        var existing = await _context.AppConfig.FirstOrDefaultAsync(c => c.Id == Record.Id);
        if (existing == null)
        {
            return NotFound();
        }

        if (EmailHeaderImageFile != null && EmailHeaderImageFile.Length > 0)
        {
            Record.EmailHeaderImageId = await SaveEmailHeaderImageAsync(Record.Id, EmailHeaderImageFile);
        }

        existing.AdminEmail = Record.AdminEmail;
        existing.SurveyEmailSubject = Record.SurveyEmailSubject;
        existing.SurveyEmailTemplate = Record.SurveyEmailTemplate;
        existing.SurveyReminderEmailSubject = Record.SurveyReminderEmailSubject;
        existing.EmailHeaderImageId = Record.EmailHeaderImageId;
        existing.SurveyReminderEmailTemplate = Record.SurveyReminderEmailTemplate;
        await _context.SaveChangesAsync();

        StatusMessage = "Config updated successfully.";
        return RedirectToPage("./Index");
    }

    private async Task<int?> SaveEmailHeaderImageAsync(int configId, IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".bin";
        }

        var storagePath = await _imageStorageService.SaveImageAsync(file, "config", configId);

        var image = new Image
        {
            EntityType = "config",
            EntityId = configId,
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

    private async Task LoadEmailHeaderImageOptionsAsync()
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

        EmailHeaderImageOptions = options;
    }

    private async Task LoadEmailHeaderImagePreviewAsync()
    {
        EmailHeaderImageThumbnailUrl = null;
        EmailHeaderImageFileName = null;
        EmailHeaderImageMissing = false;
        EmailHeaderImageMissingMessage = null;

        if (!Record.EmailHeaderImageId.HasValue)
        {
            return;
        }

        var image = await _context.Image.FirstOrDefaultAsync(x => x.Id == Record.EmailHeaderImageId.Value);
        if (image == null)
        {
            EmailHeaderImageMissing = true;
            EmailHeaderImageMissingMessage = "Selected email header image record is missing from Image table.";
            return;
        }

        if (string.IsNullOrWhiteSpace(image.FilePath))
        {
            EmailHeaderImageMissing = true;
            EmailHeaderImageFileName = image.FileName;
            EmailHeaderImageMissingMessage = "Selected email header image has no file path.";
            return;
        }

        if (!await _imageStorageService.ExistsAsync(image.FilePath))
        {
            EmailHeaderImageMissing = true;
            EmailHeaderImageFileName = image.FileName;
            EmailHeaderImageMissingMessage = "Selected email header image file is missing from storage.";
            return;
        }

        EmailHeaderImageFileName = image.FileName;
        EmailHeaderImageThumbnailUrl = Url.Page("./Edit", "EmailHeaderImage", new { id = Record.Id, imageId = image.Id });
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
