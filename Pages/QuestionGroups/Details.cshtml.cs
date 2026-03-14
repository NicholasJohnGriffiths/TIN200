using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.StaticFiles;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.QuestionGroups
{
    public class DetailsModel : PageModel
    {
        private readonly QuestionGroupService _service;

        public QuestionGroup Record { get; set; } = new();
        public ImageDetailsViewModel? Image1Details { get; set; }
        public ImageDetailsViewModel? Image2Details { get; set; }
        public ImageDetailsViewModel? Image3Details { get; set; }

        public DetailsModel(QuestionGroupService service)
        {
            _service = service;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var record = await _service.GetByIdAsync(id);
            if (record == null)
            {
                return NotFound();
            }

            Record = record;
            await LoadImageDetailsAsync(record);
            return Page();
        }

        public async Task<IActionResult> OnGetImageAsync(int id, int imageId)
        {
            var record = await _service.GetByIdAsync(id);
            if (record == null)
            {
                return NotFound();
            }

            if (record.ImageId1 != imageId && record.ImageId2 != imageId && record.ImageId3 != imageId)
            {
                return NotFound();
            }

            var image = await _service.GetImageByIdAsync(imageId);
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

        private async Task LoadImageDetailsAsync(QuestionGroup record)
        {
            Image1Details = await BuildImageDetailsAsync(record.Id, record.ImageId1);
            Image2Details = await BuildImageDetailsAsync(record.Id, record.ImageId2);
            Image3Details = await BuildImageDetailsAsync(record.Id, record.ImageId3);
        }

        private async Task<ImageDetailsViewModel?> BuildImageDetailsAsync(int groupId, int? imageId)
        {
            if (!imageId.HasValue)
            {
                return null;
            }

            var image = await _service.GetImageByIdAsync(imageId.Value);
            if (image == null)
            {
                return new ImageDetailsViewModel
                {
                    Id = imageId.Value,
                    IsMissing = true,
                    MissingMessage = "Image record not found in Image table."
                };
            }

            if (string.IsNullOrWhiteSpace(image.FilePath))
            {
                return new ImageDetailsViewModel
                {
                    Id = image.Id,
                    FileName = image.FileName,
                    IsMissing = true,
                    MissingMessage = "Image path is empty."
                };
            }

            var normalizedRelativePath = image.FilePath
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), normalizedRelativePath);
            if (!System.IO.File.Exists(fullPath))
            {
                return new ImageDetailsViewModel
                {
                    Id = image.Id,
                    FileName = image.FileName,
                    FilePath = image.FilePath,
                    IsMissing = true,
                    MissingMessage = "Image file not found on disk."
                };
            }

            return new ImageDetailsViewModel
            {
                Id = image.Id,
                FileName = image.FileName,
                FilePath = image.FilePath,
                ThumbnailUrl = Url.Page("./Details", "Image", new { id = groupId, imageId = image.Id }) ?? string.Empty
            };
        }

        public class ImageDetailsViewModel
        {
            public int Id { get; set; }
            public string FileName { get; set; } = string.Empty;
            public string FilePath { get; set; } = string.Empty;
            public string ThumbnailUrl { get; set; } = string.Empty;
            public bool IsMissing { get; set; }
            public string MissingMessage { get; set; } = string.Empty;
        }
    }
}
