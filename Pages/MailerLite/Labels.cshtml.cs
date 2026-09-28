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

        public List<LabelFormat> LabelFormats => PrintLabelsModel.Formats;

        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync(string? groupId, string? labelFormat)
        {
            SelectedGroupId = groupId;
            SelectedLabelFormat = PrintLabelsModel.Formats.Any(f => f.Id == labelFormat) ? labelFormat! : PrintLabelsModel.DefaultFormatId;

            try
            {
                Groups = await _mailerLiteService.GetGroupsAsync();

                if (!string.IsNullOrWhiteSpace(groupId))
                {
                    Subscribers = await _mailerLiteService.GetGroupSubscribersAsync(groupId);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Unable to load MailerLite data: {ex.Message}";
            }
        }
    }
}
