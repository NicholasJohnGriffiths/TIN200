using System.Text;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.Questions
{
    public class IndexModel : PageModel
    {
        private readonly QuestionService _service;

        public List<Question> Records { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int? FocusId { get; set; }

        [TempData]
        public string? StatusMessage { get; set; }

        public IndexModel(QuestionService service)
        {
            _service = service;
        }

        public async Task OnGetAsync()
        {
            Records = await _service.GetAllAsync();
        }

        public async Task<IActionResult> OnGetExportAsync()
        {
            var records = await _service.GetAllAsync();

            var orderedRecords = records
                .OrderBy(r => r.OrderNumber ?? int.MaxValue)
                .ThenBy(r => r.Id)
                .ToList();

            var groupOrderByTitle = new Dictionary<string, int>(StringComparer.Ordinal);
            var nextGroupOrder = 0;

            foreach (var record in orderedRecords)
            {
                var groupKey = (record.GroupTitle ?? string.Empty).Trim();
                if (!groupOrderByTitle.ContainsKey(groupKey))
                {
                    groupOrderByTitle[groupKey] = nextGroupOrder++;
                }
            }

            var exportRecords = orderedRecords
                .OrderBy(r => groupOrderByTitle[(r.GroupTitle ?? string.Empty).Trim()])
                .ThenBy(r => r.OrderNumber ?? int.MaxValue)
                .ThenBy(r => r.Id)
                .ToList();

            var csv = new StringBuilder();
            csv.AppendLine("OrderNumber,Title,GroupTitle,GroupDescription,Question,Description,AnswerType,Choices,ImportColumnName");

            foreach (var record in exportRecords)
            {
                var choices = string.Join("|", new[]
                {
                    record.Multi1,
                    record.Multi2,
                    record.Multi3,
                    record.Multi4,
                    record.Multi5,
                    record.Multi6,
                    record.Multi7,
                    record.Multi8,
                    record.Multi9,
                    record.Multi10
                }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));

                csv.AppendLine(string.Join(',', new[]
                {
                    EscapeCsv(record.OrderNumber?.ToString() ?? string.Empty),
                    EscapeCsv(record.Title),
                    EscapeCsv(record.GroupTitle),
                    EscapeCsv(record.GroupDescription),
                    EscapeCsv(record.QuestionText),
                    EscapeCsv(record.Description),
                    EscapeCsv(record.AnswerType),
                    EscapeCsv(choices),
                    EscapeCsv(record.ImportColumnName)
                }));
            }

            var fileName = $"questions-surveymonkey-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", fileName);
        }

        public async Task<IActionResult> OnPostMoveUpAsync(int id)
        {
            await _service.MoveUpAsync(id);
            StatusMessage = "Question moved up.";
            return RedirectToPage(new { focusId = id });
        }

        public async Task<IActionResult> OnPostMoveDownAsync(int id)
        {
            await _service.MoveDownAsync(id);
            StatusMessage = "Question moved down.";
            return RedirectToPage(new { focusId = id });
        }

        public async Task<IActionResult> OnPostReorderAsync(int? focusId)
        {
            await _service.NormalizeOrderNumbersAsync();
            StatusMessage = "Question order normalized.";
            return RedirectToPage(new { focusId });
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var escaped = value.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }
    }
}
