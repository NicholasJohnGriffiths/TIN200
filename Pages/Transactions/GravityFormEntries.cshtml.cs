using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TINWeb.Services;

namespace TINWeb.Pages.Transactions
{
    public class GravityFormEntriesModel : PageModel
    {
        private readonly GravityFormsService _service;

        public GravityFormEntriesModel(GravityFormsService service)
        {
            _service = service;
        }

        [BindProperty(SupportsGet = true)]
        public int FormId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public GravityFormDetail? FormDetail { get; private set; }
        public GravityFormEntriesResult? Result { get; private set; }
        public decimal? FormAmountTotal { get; private set; }
        public string? ErrorMessage { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (FormId <= 0)
                return RedirectToPage("./GravityForms");

            try
            {
                var pageNum = PageNumber < 1 ? 1 : PageNumber;
                var detailTask = _service.GetFormDetailAsync(FormId);
                var entriesTask = _service.GetEntriesAsync(FormId, pageNum);
                var amountTotalTask = _service.GetFormAmountTotalAsync(FormId);
                await Task.WhenAll(detailTask, entriesTask, amountTotalTask);
                FormDetail = await detailTask;
                Result = await entriesTask;
                FormAmountTotal = await amountTotalTask;
            }
            catch (GravityFormsApiException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized || ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    ErrorMessage = $"Unable to connect to WordPress: {ex.Message} Check credentials and ensure the WordPress user has Gravity Forms API permissions (view forms/entries).";
                }
                else
                {
                    ErrorMessage = $"Unable to connect to WordPress: {ex.Message}";
                }
            }
            catch (HttpRequestException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    ErrorMessage = "Unable to connect to WordPress: 401 Unauthorized. Check WP REST API username/token env vars (WP__RESTAPI__Username and WP__RESTAPI__Token).";
                }
                else
                {
                    ErrorMessage = $"Unable to connect to WordPress: {ex.Message}";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"An error occurred: {ex.Message}";
            }

            return Page();
        }
    }
}
