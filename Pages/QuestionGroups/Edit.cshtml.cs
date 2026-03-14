using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.StaticFiles;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.QuestionGroups
{
    public class EditModel : PageModel
    {
        private readonly QuestionGroupService _service;
        private readonly QuestionService _questionService;

        [BindProperty]
        public QuestionGroup Record { get; set; } = new();

        [BindProperty]
        public IFormFile? Image1File { get; set; }

        [BindProperty]
        public IFormFile? Image2File { get; set; }

        [BindProperty]
        public IFormFile? Image3File { get; set; }

        [BindProperty]
        public bool ClearImage1 { get; set; }

        [BindProperty]
        public bool ClearImage2 { get; set; }

        [BindProperty]
        public bool ClearImage3 { get; set; }

        [BindProperty]
        public List<int> SelectedQuestionIds { get; set; } = new();

        public ImageDetailsViewModel? Image1Details { get; set; }
        public ImageDetailsViewModel? Image2Details { get; set; }
        public ImageDetailsViewModel? Image3Details { get; set; }

        public List<SelectListItem> GroupQuestions { get; set; } = new();
        public List<SelectListItem> AvailableQuestions { get; set; } = new();

        public EditModel(QuestionGroupService service, QuestionService questionService)
        {
            _service = service;
            _questionService = questionService;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var record = await _service.GetByIdAsync(id);
            if (record == null)
            {
                return NotFound();
            }

            Record = record;
            await LoadQuestionListsAsync(record.Id);
            await LoadImageDetailsAsync(record);
            return Page();
        }

        public async Task<IActionResult> OnGetImageAsync(int id, int imageId)
        {
            if (!await _service.ExistsAsync(id))
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

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                var existing = await _service.GetByIdAsync(Record.Id);
                if (existing != null)
                {
                    Record.ImageId1 = existing.ImageId1;
                    Record.ImageId2 = existing.ImageId2;
                    Record.ImageId3 = existing.ImageId3;
                    await LoadImageDetailsAsync(existing);
                }

                await LoadQuestionListsAsync(Record.Id);
                return Page();
            }

            if (!await _service.ExistsAsync(Record.Id))
            {
                return NotFound();
            }

            await _service.UpdateAsync(Record, Image1File, Image2File, Image3File, ClearImage1, ClearImage2, ClearImage3);
            await _questionService.SetGroupMembershipAsync(Record.Id, SelectedQuestionIds);
            return RedirectToPage("./Index");
        }

        private async Task LoadQuestionListsAsync(int groupId)
        {
            var groupedQuestions = await _questionService.GetByGroupIdAsync(groupId);
            var availableQuestions = await _questionService.GetUngroupedAsync();

            GroupQuestions = groupedQuestions
                .Select(ToListItem)
                .ToList();

            AvailableQuestions = availableQuestions
                .Select(ToListItem)
                .ToList();

            SelectedQuestionIds = groupedQuestions.Select(q => q.Id).ToList();
        }

        private static SelectListItem ToListItem(Question question)
        {
            var labelCore = string.IsNullOrWhiteSpace(question.Title)
                ? question.QuestionText
                : question.Title;

            var label = string.IsNullOrWhiteSpace(labelCore)
                ? $"Question {question.Id}"
                : labelCore.Trim();

            var orderPrefix = question.OrderNumber.HasValue ? $"{question.OrderNumber.Value}. " : string.Empty;
            return new SelectListItem
            {
                Value = question.Id.ToString(),
                Text = $"{orderPrefix}{label}"
            };
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
                return null;
            }

            return new ImageDetailsViewModel
            {
                Id = image.Id,
                FileName = image.FileName,
                FilePath = image.FilePath,
                ThumbnailUrl = Url.Page("./Edit", "Image", new { id = groupId, imageId = image.Id }) ?? string.Empty
            };
        }

        public class ImageDetailsViewModel
        {
            public int Id { get; set; }
            public string FileName { get; set; } = string.Empty;
            public string FilePath { get; set; } = string.Empty;
            public string ThumbnailUrl { get; set; } = string.Empty;
        }
    }
}
