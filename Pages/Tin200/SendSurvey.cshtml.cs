using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using TINWorkspaceTemp.Models;
using TINWorkspaceTemp.Services;

namespace TINWorkspaceTemp.Pages.Tin200
{
    public class SendSurveyModel : PageModel
    {
        private readonly Tin200Service _tin200Service;
        private readonly ISurveyEmailService _surveyEmailService;
        private readonly ISurveyLinkTokenService _surveyLinkTokenService;

        public SendSurveyModel(
            Tin200Service tin200Service,
            ISurveyEmailService surveyEmailService,
            ISurveyLinkTokenService surveyLinkTokenService)
        {
            _tin200Service = tin200Service;
            _surveyEmailService = surveyEmailService;
            _surveyLinkTokenService = surveyLinkTokenService;
        }

        [BindProperty]
        public int ClientId { get; set; }

        [BindProperty]
        [EmailAddress]
        public string? RecipientEmail { get; set; }

        [BindProperty]
        public List<int> SelectedClientIds { get; set; } = new();

        [BindProperty]
        public bool SendToAllClients { get; set; }

        public Models.Tin200? Client { get; set; }

        public List<SurveyClientRow> AvailableClients { get; set; } = new();

        public string? SurveyUrl { get; set; }

        [TempData]
        public string? StatusMessage { get; set; }

        [TempData]
        public bool BulkSendSucceeded { get; set; }

        [TempData]
        public int? BulkSentCount { get; set; }

        [TempData]
        public int? BulkSkippedCount { get; set; }

        [TempData]
        public int? BulkFailedCount { get; set; }

        [TempData]
        public string? BulkLastRunAt { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            await LoadAvailableClientsAsync();

            if (!id.HasValue)
            {
                return Page();
            }

            ClientId = id.Value;
            Client = await _tin200Service.GetTin200ByIdAsync(ClientId);
            if (Client == null)
            {
                ModelState.AddModelError(string.Empty, "Client not found.");
                return Page();
            }

            RecipientEmail = Client.Email;
            SurveyUrl = BuildSurveyUrl(Client.Id);

            return Page();
        }

        public async Task<IActionResult> OnPostSingleAsync()
        {
            await LoadAvailableClientsAsync();

            if (ClientId <= 0)
            {
                ModelState.AddModelError(nameof(ClientId), "Please enter a valid client ID.");
                return Page();
            }

            Client = await _tin200Service.GetTin200ByIdAsync(ClientId);
            if (Client == null)
            {
                ModelState.AddModelError(string.Empty, "Client not found.");
                return Page();
            }

            RecipientEmail = string.IsNullOrWhiteSpace(RecipientEmail)
                ? Client.Email
                : RecipientEmail;

            if (string.IsNullOrWhiteSpace(RecipientEmail))
            {
                ModelState.AddModelError(nameof(RecipientEmail), "No recipient email is available. Enter an email address.");
                SurveyUrl = BuildSurveyUrl(ClientId);
                return Page();
            }

            SurveyUrl = BuildSurveyUrl(ClientId);

            try
            {
                await _surveyEmailService.SendSurveyLinkAsync(RecipientEmail, SurveyUrl, Client.CompanyName, Client.Id);
                StatusMessage = $"Survey email sent to {RecipientEmail}.";
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Could not send email: {ex.Message}");
                return Page();
            }

            return RedirectToPage(new { id = ClientId });
        }

        public async Task<IActionResult> OnPostBulkAsync()
        {
            await LoadAvailableClientsAsync();

            var selected = SendToAllClients
                ? AvailableClients
                : AvailableClients.Where(c => SelectedClientIds.Contains(c.Id)).ToList();

            if (!selected.Any())
            {
                ModelState.AddModelError(string.Empty, "Select at least one client, or choose Send to all clients.");
                return Page();
            }

            var sentCount = 0;
            var skippedNoEmailCount = 0;
            var failedCount = 0;

            foreach (var clientRow in selected)
            {
                if (string.IsNullOrWhiteSpace(clientRow.Email))
                {
                    skippedNoEmailCount++;
                    continue;
                }

                var surveyUrl = BuildSurveyUrl(clientRow.Id);

                try
                {
                    await _surveyEmailService.SendSurveyLinkAsync(clientRow.Email, surveyUrl, clientRow.CompanyName, clientRow.Id);
                    sentCount++;
                }
                catch
                {
                    failedCount++;
                }
            }

            StatusMessage = $"Bulk send complete. Sent: {sentCount}, Skipped (no email): {skippedNoEmailCount}, Failed: {failedCount}.";
            BulkSentCount = sentCount;
            BulkSkippedCount = skippedNoEmailCount;
            BulkFailedCount = failedCount;
            BulkLastRunAt = DateTime.Now.ToString("MMM d, yyyy h:mm tt");

            if (failedCount > 0)
            {
                ModelState.AddModelError(string.Empty, "Some emails could not be sent. Please check SMTP settings and retry.");
                return Page();
            }

            BulkSendSucceeded = true;

            return RedirectToPage();
        }

        private string BuildSurveyUrl(int id)
        {
            var token = _surveyLinkTokenService.GenerateToken(id);
            return Url.Page("/Tin200/SurveyUpdate", pageHandler: null, values: new { id, token }, protocol: Request.Scheme) ?? string.Empty;
        }

        private async Task LoadAvailableClientsAsync()
        {
            var clients = await _tin200Service.GetAllTin200Async();
            AvailableClients = clients
                .OrderBy(c => c.CompanyName)
                .ThenBy(c => c.Id)
                .Select(c => new SurveyClientRow
                {
                    Id = c.Id,
                    CompanyName = c.CompanyName,
                    Email = c.Email
                })
                .ToList();
        }

        public class SurveyClientRow
        {
            public int Id { get; set; }
            public string? CompanyName { get; set; }
            public string? Email { get; set; }
        }
    }
}
