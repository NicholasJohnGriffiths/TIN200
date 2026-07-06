using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using TINWeb.Data;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.Config;

[Authorize(Policy = "AdminOnly")]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IImageStorageService _imageStorageService;

    [BindProperty]
    public AppConfig Record { get; set; } = new() { Id = 1 };

    [BindProperty]
    public IFormFile? EmailHeaderImageFile { get; set; }

    public List<SelectListItem> EmailHeaderImageOptions { get; set; } = new();
    public string? EmailHeaderImageThumbnailUrl { get; set; }
    public string? EmailHeaderImageFileName { get; set; }
    public bool EmailHeaderImageMissing { get; set; }
    public string? EmailHeaderImageMissingMessage { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public CreateModel(ApplicationDbContext context, IImageStorageService imageStorageService)
    {
        _context = context;
        _imageStorageService = imageStorageService;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadEmailHeaderImageOptionsAsync();
        return Page();
    }

    public async Task<IActionResult> OnGetEmailHeaderImageAsync(int imageId)
    {
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
        Record.Id = 1;
        Record.AdminEmail = Record.AdminEmail?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(Record.AdminEmail))
        {
            ModelState.AddModelError("Record.AdminEmail", "Admin email is required.");
        }
        else if (!TryValidateAdminEmailList(Record.AdminEmail, out var invalidEmail))
        {
            ModelState.AddModelError("Record.AdminEmail", $"Invalid admin email address: {invalidEmail}");
        }

        if (!ModelState.IsValid)
        {
            await LoadEmailHeaderImageOptionsAsync();
            await LoadEmailHeaderImagePreviewAsync();
            return Page();
        }

        var exists = await _context.AppConfig.AnyAsync();
        if (exists)
        {
            ModelState.AddModelError(string.Empty, "A config row already exists. Edit the existing row instead.");
            await LoadEmailHeaderImageOptionsAsync();
            await LoadEmailHeaderImagePreviewAsync();
            return Page();
        }

        if (EmailHeaderImageFile != null && EmailHeaderImageFile.Length > 0)
        {
            Record.EmailHeaderImageId = await SaveEmailHeaderImageAsync(Record.Id, EmailHeaderImageFile);
        }

        _context.AppConfig.Add(Record);
        await _context.SaveChangesAsync();

        StatusMessage = "Config created successfully.";
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
        EmailHeaderImageThumbnailUrl = Url.Page("./Create", "EmailHeaderImage", new { imageId = image.Id });
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

    private static bool TryValidateAdminEmailList(string adminEmailList, out string invalidEmail)
    {
        invalidEmail = string.Empty;

        var tokens = adminEmailList
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim())
            .Where(token => !string.IsNullOrWhiteSpace(token));

        foreach (var token in tokens)
        {
            try
            {
                _ = new MailAddress(token);
            }
            catch
            {
                invalidEmail = token;
                return false;
            }
        }

        return true;
    }
}
