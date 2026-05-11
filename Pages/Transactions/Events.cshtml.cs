using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Stripe;
using TINWeb.Services;

namespace TINWeb.Pages.Transactions
{
    public class EventsModel : PageModel
    {
        private readonly StripeTransactionService _stripeTransactionService;

        public EventsModel(StripeTransactionService stripeTransactionService)
        {
            _stripeTransactionService = stripeTransactionService;
        }

        [BindProperty(SupportsGet = true)]
        public string? Description { get; set; }

        public List<StripeTransactionRow> Transactions { get; private set; } = new();

        public List<string> DescriptionOptions { get; private set; } = new();

        public string? ErrorMessage { get; private set; }

        public async Task OnGetAsync()
        {
            try
            {
                var result = await _stripeTransactionService.GetTransactionsAsync(Description, 100);
                Transactions = result.Rows;
                DescriptionOptions = result.DescriptionOptions;
                Description = result.ActiveDescriptionFilter;
            }
            catch (StripeException ex)
            {
                ErrorMessage = $"Stripe API error: {ex.Message}";
            }
            catch (InvalidOperationException ex)
            {
                ErrorMessage = ex.Message;
            }
        }
    }
}