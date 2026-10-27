using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Services;

namespace TINWeb.Pages.MailerLite
{
    public class LabelsModel : PageModel
    {
        private readonly MailerLiteService _mailerLiteService;

        public LabelsModel(MailerLiteService mailerLiteService)
        {
            _mailerLiteService = mailerLiteService;
        }

        public List<MailerLiteGroup> Groups { get; set; } = new();

        public List<MailerLiteSubscriber> Subscribers { get; set; } = new();

        public string? SelectedGroupId { get; set; }

        public string SelectedLabelFormat { get; set; } = PrintLabelsModel.DefaultFormatId;

        public MailerLiteSubscriberOrder SelectedOrderBy { get; set; } = MailerLiteSubscriberOrder.Date;

        public List<LabelFormat> LabelFormats => PrintLabelsModel.Formats;

        public string? ErrorMessage { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateOnly? DateAddedFrom { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateOnly? DateAddedTo { get; set; }

        public async Task OnGetAsync(string? groupId, string? labelFormat, MailerLiteSubscriberOrder orderBy = MailerLiteSubscriberOrder.Date)
        {
            SelectedGroupId = groupId;
            SelectedLabelFormat = PrintLabelsModel.Formats.Any(f => f.Id == labelFormat) ? labelFormat! : PrintLabelsModel.DefaultFormatId;
            SelectedOrderBy = orderBy;

            if (DateAddedFrom.HasValue && DateAddedTo.HasValue && DateAddedFrom.Value > DateAddedTo.Value)
            {
                ModelState.AddModelError(nameof(DateAddedTo), "Date Added To must be on or after Date Added From.");
            }

            try
            {
                Groups = await _mailerLiteService.GetGroupsAsync();

                if (ModelState.IsValid && !string.IsNullOrWhiteSpace(groupId))
                {
                    Subscribers = await _mailerLiteService.GetGroupSubscribersAsync(groupId, SelectedOrderBy, DateAddedFrom, DateAddedTo);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Unable to load MailerLite data: {ex.Message}";
            }
        }
    }
}
