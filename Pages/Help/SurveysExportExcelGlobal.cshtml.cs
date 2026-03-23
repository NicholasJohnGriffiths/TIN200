using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;

namespace TINWeb.Pages.Help
{
    public class SurveysExportExcelGlobalModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public SurveysExportExcelGlobalModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<QuestionMapping> QuestionMappings { get; set; } = new();

        public async Task OnGetAsync()
        {
            QuestionMappings = await _context.Question
                .AsNoTracking()
                .Where(q => !string.IsNullOrWhiteSpace(q.ImportColumnNameAlt))
                .OrderBy(q => q.OrderNumber)
                .ThenBy(q => q.Id)
                .Select(q => new QuestionMapping
                {
                    Id = q.Id,
                    Title = q.Title ?? string.Empty,
                    ImportColumnNameAlt = q.ImportColumnNameAlt!
                })
                .ToListAsync();
        }

        public class QuestionMapping
        {
            public int Id { get; set; }
            public string Title { get; set; } = string.Empty;
            public string ImportColumnNameAlt { get; set; } = string.Empty;
        }
    }
}
