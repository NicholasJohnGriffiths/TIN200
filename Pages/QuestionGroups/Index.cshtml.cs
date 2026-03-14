using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.QuestionGroups
{
    public class IndexModel : PageModel
    {
        private readonly QuestionGroupService _service;

        public List<QuestionGroup> Records { get; set; } = new();
        public Dictionary<int, ImageDetailsViewModel> ImageDetailsById { get; set; } = new();

        public IndexModel(QuestionGroupService service)
        {
            _service = service;
        }

        public async Task OnGetAsync()
        {
            Records = await _service.GetAllAsync();

            var imageIds = Records
                .SelectMany(r => new[] { r.ImageId1, r.ImageId2, r.ImageId3 })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var images = await _service.GetImagesByIdsAsync(imageIds);

            ImageDetailsById = images.ToDictionary(
                pair => pair.Key,
                pair => new ImageDetailsViewModel
                {
                    Id = pair.Value.Id,
                    FileName = pair.Value.FileName
                });
        }

        public class ImageDetailsViewModel
        {
            public int Id { get; set; }
            public string FileName { get; set; } = string.Empty;
        }
    }
}
