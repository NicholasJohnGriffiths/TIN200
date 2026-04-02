using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TINWeb.Data;
using TINWeb.Models;

namespace TINWeb.Pages.QuestionGroups
{
    public class SubgroupsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public SubgroupsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public QuestionGroup Group { get; private set; } = new();
        public List<QuestionSubgroup> Subgroups { get; private set; } = new();
        public List<SelectListItem> SubgroupQuestions { get; private set; } = new();
        public List<SelectListItem> AvailableQuestions { get; private set; } = new();

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SelectedSubgroupId { get; set; }

        [BindProperty]
        public string? NewSubgroupTitle { get; set; }

        [BindProperty]
        public string? EditSubgroupTitle { get; set; }

        [BindProperty]
        public List<int> SelectedQuestionIds { get; set; } = new();

        [TempData]
        public string? StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var ok = await LoadPageStateAsync();
            if (!ok)
            {
                return NotFound();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostCreateSubgroupAsync()
        {
            var group = await _context.QuestionGroup.AsNoTracking().FirstOrDefaultAsync(g => g.Id == Id);
            if (group == null)
            {
                return NotFound();
            }

            var subgroup = new QuestionSubgroup
            {
                QuestionGroupId = Id,
                Title = string.IsNullOrWhiteSpace(NewSubgroupTitle)
                    ? $"Subgroup {DateTime.UtcNow:yyyyMMddHHmmss}"
                    : NewSubgroupTitle.Trim()
            };

            _context.QuestionSubgroup.Add(subgroup);
            await _context.SaveChangesAsync();

            StatusMessage = "Subgroup created.";
            return RedirectToPage(new { id = Id, selectedSubgroupId = subgroup.Id });
        }

        public async Task<IActionResult> OnPostRenameSubgroupAsync()
        {
            if (!SelectedSubgroupId.HasValue)
            {
                StatusMessage = "Select a subgroup first.";
                return RedirectToPage(new { id = Id });
            }

            var subgroup = await _context.QuestionSubgroup
                .FirstOrDefaultAsync(s => s.Id == SelectedSubgroupId.Value && s.QuestionGroupId == Id);

            if (subgroup == null)
            {
                return NotFound();
            }

            subgroup.Title = string.IsNullOrWhiteSpace(EditSubgroupTitle)
                ? subgroup.Title
                : EditSubgroupTitle.Trim();

            await _context.SaveChangesAsync();

            StatusMessage = "Subgroup updated.";
            return RedirectToPage(new { id = Id, selectedSubgroupId = SelectedSubgroupId });
        }

        public async Task<IActionResult> OnPostDeleteSubgroupAsync()
        {
            if (!SelectedSubgroupId.HasValue)
            {
                StatusMessage = "Select a subgroup first.";
                return RedirectToPage(new { id = Id });
            }

            var subgroup = await _context.QuestionSubgroup
                .FirstOrDefaultAsync(s => s.Id == SelectedSubgroupId.Value && s.QuestionGroupId == Id);

            if (subgroup == null)
            {
                return NotFound();
            }

            var links = await _context.QuestionSubgroupQuestion
                .Where(x => x.QuestionSubgroupId == subgroup.Id)
                .ToListAsync();

            _context.QuestionSubgroupQuestion.RemoveRange(links);
            _context.QuestionSubgroup.Remove(subgroup);
            await _context.SaveChangesAsync();

            StatusMessage = "Subgroup deleted.";
            return RedirectToPage(new { id = Id });
        }

        public async Task<IActionResult> OnPostSaveAssignmentsAsync()
        {
            if (!SelectedSubgroupId.HasValue)
            {
                StatusMessage = "Select a subgroup first.";
                return RedirectToPage(new { id = Id });
            }

            var subgroup = await _context.QuestionSubgroup
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == SelectedSubgroupId.Value && s.QuestionGroupId == Id);

            if (subgroup == null)
            {
                return NotFound();
            }

            var validQuestionIds = await _context.Question
                .AsNoTracking()
                .Where(q => q.GroupId == Id)
                .Select(q => q.Id)
                .ToListAsync();

            var validSet = new HashSet<int>(validQuestionIds);
            var targetQuestionIds = (SelectedQuestionIds ?? new List<int>())
                .Where(validSet.Contains)
                .Distinct()
                .ToList();

            var existing = await _context.QuestionSubgroupQuestion
                .Where(x => x.QuestionSubgroupId == subgroup.Id)
                .ToListAsync();

            _context.QuestionSubgroupQuestion.RemoveRange(existing);

            var newLinks = targetQuestionIds
                .Select((questionId, index) => new QuestionSubgroupQuestion
                {
                    QuestionSubgroupId = subgroup.Id,
                    QuestionId = questionId,
                    OrderNumber = index + 1
                })
                .ToList();

            if (newLinks.Count > 0)
            {
                _context.QuestionSubgroupQuestion.AddRange(newLinks);
            }

            await _context.SaveChangesAsync();

            StatusMessage = "Subgroup questions saved.";
            return RedirectToPage(new { id = Id, selectedSubgroupId = SelectedSubgroupId });
        }

        private async Task<bool> LoadPageStateAsync()
        {
            var group = await _context.QuestionGroup
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == Id);

            if (group == null)
            {
                return false;
            }

            Group = group;

            Subgroups = await _context.QuestionSubgroup
                .AsNoTracking()
                .Where(s => s.QuestionGroupId == Id)
                .OrderBy(s => s.Id)
                .ToListAsync();

            if (!SelectedSubgroupId.HasValue && Subgroups.Count > 0)
            {
                SelectedSubgroupId = Subgroups[0].Id;
            }

            var groupQuestions = await _context.Question
                .AsNoTracking()
                .Where(q => q.GroupId == Id)
                .OrderBy(q => q.OrderNumber)
                .ThenBy(q => q.Id)
                .ToListAsync();

            var selectedQuestionIds = new HashSet<int>();
            if (SelectedSubgroupId.HasValue)
            {
                selectedQuestionIds = await _context.QuestionSubgroupQuestion
                    .AsNoTracking()
                    .Where(x => x.QuestionSubgroupId == SelectedSubgroupId.Value)
                    .OrderBy(x => x.OrderNumber)
                    .ThenBy(x => x.Id)
                    .Select(x => x.QuestionId)
                    .ToHashSetAsync();

                var selectedSubgroup = Subgroups.FirstOrDefault(s => s.Id == SelectedSubgroupId.Value);
                EditSubgroupTitle = selectedSubgroup?.Title;
            }

            SubgroupQuestions = groupQuestions
                .Where(q => selectedQuestionIds.Contains(q.Id))
                .Select(ToListItem)
                .ToList();

            AvailableQuestions = groupQuestions
                .Where(q => !selectedQuestionIds.Contains(q.Id))
                .Select(ToListItem)
                .ToList();

            SelectedQuestionIds = SubgroupQuestions
                .Select(x => int.Parse(x.Value))
                .ToList();

            return true;
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
    }
}
