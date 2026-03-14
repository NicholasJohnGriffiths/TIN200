using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using TINWeb.Models;
using TINWeb.Services;

namespace TINWeb.Pages.Questions
{
    public class CreateModel : PageModel
    {
        private readonly QuestionService _service;
        private readonly QuestionGroupService _questionGroupService;

        [BindProperty]
        public Question Record { get; set; } = new();

        public List<string> AnswerTypeOptions { get; } = Enum.GetNames<QuestionAnswerType>().ToList();
        public List<SelectListItem> QuestionGroupOptions { get; set; } = new();

        public CreateModel(QuestionService service, QuestionGroupService questionGroupService)
        {
            _service = service;
            _questionGroupService = questionGroupService;
        }

        public async Task OnGetAsync()
        {
            await LoadQuestionGroupOptionsAsync();
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

            await _service.CreateAsync(Record);
            return RedirectToPage("./Index");
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
