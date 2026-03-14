using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.Questions
{
    public class EditModel : PageModel
    {
        private readonly QuestionService _service;
        private readonly QuestionGroupService _questionGroupService;

        [BindProperty]
        public Question Record { get; set; } = new();

        public List<string> AnswerTypeOptions { get; } = Enum.GetNames<QuestionAnswerType>().ToList();
        public List<SelectListItem> QuestionGroupOptions { get; set; } = new();

        public EditModel(QuestionService service, QuestionGroupService questionGroupService)
        {
            _service = service;
            _questionGroupService = questionGroupService;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var record = await _service.GetByIdAsync(id);
            if (record == null)
            {
                return NotFound();
            }

            Record = record;
            await LoadQuestionGroupOptionsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!Enum.TryParse<QuestionAnswerType>(Record.AnswerType, out _))
            {
                ModelState.AddModelError("Record.AnswerType", "Answer Type must be one of: Text, Currency, Number, SingleChoice, Multichoice.");
            }

            if (!ModelState.IsValid)
            {
                await LoadQuestionGroupOptionsAsync();
                return Page();
            }

            if (!await _service.ExistsAsync(Record.Id))
            {
                return NotFound();
            }

            var existing = await _service.GetByIdAsync(Record.Id);
            if (existing == null)
            {
                return NotFound();
            }

            Record.GroupTitle = existing.GroupTitle;
            Record.GroupDescription = existing.GroupDescription;

            await _service.UpdateAsync(Record);
            return RedirectToPage("./Index", new { focusId = Record.Id });
        }

        private async Task LoadQuestionGroupOptionsAsync()
        {
            var groups = await _questionGroupService.GetAllAsync();
            QuestionGroupOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = string.Empty, Text = "None" }
            };

            QuestionGroupOptions.AddRange(groups.Select(g => new SelectListItem
            {
                Value = g.Id.ToString(),
                Text = g.Title ?? $"Group {g.Id}"
            }));
        }
    }
}
