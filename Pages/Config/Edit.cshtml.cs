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
public class EditModel : PageModel
{
    private const string RevenueForecastMethodLogLinear = "LogLinear";
    private const string RevenueForecastMethodRecencyWeighted = "RecencyWeighted";

    private readonly ApplicationDbContext _context;
    private readonly IImageStorageService _imageStorageService;

    [BindProperty]
    public AppConfig Record { get; set; } = new();

    [BindProperty]
    public IFormFile? EmailHeaderImageFile { get; set; }

    [BindProperty]
    public IFormFile? TinLogoLabelsImageFile { get; set; }

    public List<SelectListItem> EmailHeaderImageOptions { get; set; } = new();
    public List<SelectListItem> RevenueForecastMethodOptions { get; set; } = new();
    public string? EmailHeaderImageThumbnailUrl { get; set; }
    public string? EmailHeaderImageFileName { get; set; }
    public bool EmailHeaderImageMissing { get; set; }
    public string? EmailHeaderImageMissingMessage { get; set; }

    public List<SelectListItem> TinLogoLabelsImageOptions { get; set; } = new();
    public string? TinLogoLabelsImageThumbnailUrl { get; set; }
    public string? TinLogoLabelsImageFileName { get; set; }
    public bool TinLogoLabelsImageMissing { get; set; }
    public string? TinLogoLabelsImageMissingMessage { get; set; }

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

        record.RevenueForecastMethod = NormalizeRevenueForecastMethod(record.RevenueForecastMethod);
        Record = record;
        await LoadEmailHeaderImageOptionsAsync();
        await LoadTinLogoLabelsImageOptionsAsync();
        LoadRevenueForecastMethodOptions();
        await LoadEmailHeaderImagePreviewAsync();
        await LoadTinLogoLabelsImagePreviewAsync();
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

    public async Task<IActionResult> OnGetTinLogoLabelsImageAsync(int id, int imageId)
    {
        var config = await _context.AppConfig.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (config?.TinLogoLabelsImageId != imageId)
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
            await LoadTinLogoLabelsImageOptionsAsync();
            LoadRevenueForecastMethodOptions();
            await LoadEmailHeaderImagePreviewAsync();
            await LoadTinLogoLabelsImagePreviewAsync();
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

        if (TinLogoLabelsImageFile != null && TinLogoLabelsImageFile.Length > 0)
        {
            Record.TinLogoLabelsImageId = await SaveTinLogoLabelsImageAsync(Record.Id, TinLogoLabelsImageFile);
        }

        existing.AdminEmail = Record.AdminEmail;
        existing.EmailHeaderImageId = Record.EmailHeaderImageId;
        existing.TinLogoLabelsImageId = Record.TinLogoLabelsImageId;
        existing.RevenueForecastMethod = NormalizeRevenueForecastMethod(Record.RevenueForecastMethod);
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

    private async Task<int?> SaveTinLogoLabelsImageAsync(int configId, IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".bin";
        }

        var storagePath = await _imageStorageService.SaveImageAsync(file, "config-tin-logo-labels", configId);

        var image = new Image
        {
            EntityType = "config-tin-logo-labels",
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

    private async Task LoadTinLogoLabelsImageOptionsAsync()
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

        TinLogoLabelsImageOptions = options;
    }

    private void LoadRevenueForecastMethodOptions()
    {
        RevenueForecastMethodOptions = new List<SelectListItem>
        {
            new() { Value = RevenueForecastMethodLogLinear, Text = "Log-linear" },
            new() { Value = RevenueForecastMethodRecencyWeighted, Text = "Recency weighted" }
        };
    }

    private static string NormalizeRevenueForecastMethod(string? method)
    {
        return string.Equals(method, RevenueForecastMethodRecencyWeighted, StringComparison.OrdinalIgnoreCase)
            ? RevenueForecastMethodRecencyWeighted
            : RevenueForecastMethodLogLinear;
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

    private async Task LoadTinLogoLabelsImagePreviewAsync()
    {
        TinLogoLabelsImageThumbnailUrl = null;
        TinLogoLabelsImageFileName = null;
        TinLogoLabelsImageMissing = false;
        TinLogoLabelsImageMissingMessage = null;

        if (!Record.TinLogoLabelsImageId.HasValue)
        {
            return;
        }

        var image = await _context.Image.FirstOrDefaultAsync(x => x.Id == Record.TinLogoLabelsImageId.Value);
        if (image == null)
        {
            TinLogoLabelsImageMissing = true;
            TinLogoLabelsImageMissingMessage = "Selected TIN Logo - Labels image record is missing from Image table.";
            return;
        }

        if (string.IsNullOrWhiteSpace(image.FilePath))
        {
            TinLogoLabelsImageMissing = true;
            TinLogoLabelsImageFileName = image.FileName;
            TinLogoLabelsImageMissingMessage = "Selected TIN Logo - Labels image has no file path.";
            return;
        }

        if (!await _imageStorageService.ExistsAsync(image.FilePath))
        {
            TinLogoLabelsImageMissing = true;
            TinLogoLabelsImageFileName = image.FileName;
            TinLogoLabelsImageMissingMessage = "Selected TIN Logo - Labels image file is missing from storage.";
            return;
        }

        TinLogoLabelsImageFileName = image.FileName;
        TinLogoLabelsImageThumbnailUrl = Url.Page("./Edit", "TinLogoLabelsImage", new { id = Record.Id, imageId = image.Id });
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
