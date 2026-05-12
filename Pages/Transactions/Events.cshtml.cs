using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Stripe;
using TINWeb.Services;

namespace TINWeb.Pages.Transactions
{
    public class EventsModel : PageModel
    {
        private readonly StripeTransactionService _stripeTransactionService;
        private readonly StripeSettings _stripeSettings;
        private readonly IConfiguration _configuration;

        public EventsModel(StripeTransactionService stripeTransactionService, IOptions<StripeSettings> stripeOptions, IConfiguration configuration)
        {
            _stripeTransactionService = stripeTransactionService;
            _stripeSettings = stripeOptions.Value;
            _configuration = configuration;
        }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public int Year { get; set; } = 0;

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public int EffectiveYear => Year > 0 ? Year : DateTime.UtcNow.Year;

        public List<int> AvailableYears { get; private set; } = new();

        public List<StripeTransactionRow> Transactions { get; private set; } = new();

        public int TotalPages { get; private set; }

        public int TotalCount { get; private set; }

        public string TotalAmountDisplay { get; private set; } = "0.00";

        public int RefundedCount { get; private set; }

        public int DisputedCount { get; private set; }

        public int FailedCount { get; private set; }

        public int UncapturedCount { get; private set; }

        public bool IsStripeTestMode => _stripeSettings.UseTestMode;

        public string StripeModeLabel => _stripeSettings.UseTestMode ? "Test" : "Live";

        public string StripeSecretKeyType => GetKeyType(_stripeSettings.SecretKey, "sk_live_", "sk_test_");

        public string StripePublishableKeyType => GetKeyType(_stripeSettings.PublishableKey, "pk_live_", "pk_test_");

        public string StripeModeVariableName => "Stripe__Testmode__TINWeb";

        public string StripeSecretVariableName => _stripeSettings.UseTestMode
            ? "Stripe__SecretKey__Test"
            : "Stripe__SecretKey";

        public string StripePublishableVariableName => _stripeSettings.UseTestMode
            ? "Stripe__PublishableKey__Test"
            : "Stripe__PublishableKey";

        public string StripeWebhookVariableName => _stripeSettings.UseTestMode
            ? "Stripe__WebhookSecret__TINWeb__Test"
            : "Stripe__WebhookSecret__TINWeb";

        public string StripeRawModeValue => string.IsNullOrWhiteSpace(_configuration["Stripe:Testmode:TINWeb"])
            ? "(empty)"
            : _configuration["Stripe:Testmode:TINWeb"]!;

        public string StripeRawLiveSecretType => GetKeyType(_configuration["Stripe:SecretKey"], "sk_live_", "sk_test_");

        public string StripeRawTestSecretType => GetKeyType(_configuration["Stripe:SecretKey:Test"], "sk_live_", "sk_test_");

        public string StripeRawLivePublishableType => GetKeyType(_configuration["Stripe:PublishableKey"], "pk_live_", "pk_test_");

        public string StripeRawTestPublishableType => GetKeyType(_configuration["Stripe:PublishableKey:Test"], "pk_live_", "pk_test_");

        public string? ErrorMessage { get; private set; }

        private static string GetKeyType(string? key, string livePrefix, string testPrefix)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return "Not configured";
            }

            if (key.StartsWith(livePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return "Live";
            }

            if (key.StartsWith(testPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return "Test";
            }

            return "Unknown format";
        }

        public async Task OnGetAsync()
        {
            var currentYear = DateTime.UtcNow.Year;
            AvailableYears = Enumerable.Range(2022, currentYear - 2022 + 1).OrderByDescending(y => y).ToList();
            try
            {
                var result = await _stripeTransactionService.GetTransactionsAsync(Search, EffectiveYear, PageNumber, 20);
                Transactions = result.Rows;
                Search = result.ActiveDescriptionFilter;
                PageNumber = result.PageNumber;
                TotalPages = result.TotalPages;
                TotalCount = result.TotalCount;
                TotalAmountDisplay = result.TotalAmountDisplay;
                RefundedCount = result.RefundedCount;
                DisputedCount = result.DisputedCount;
                FailedCount = result.FailedCount;
                UncapturedCount = result.UncapturedCount;
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

        public async Task<IActionResult> OnGetPdfAsync()
        {
            try
            {
                var pdf = await _stripeTransactionService.GenerateTransactionsPdfAsync(Search, EffectiveYear);
                return File(pdf, "application/pdf");
            }
            catch (StripeException ex)
            {
                ErrorMessage = $"Stripe API error: {ex.Message}";
                await OnGetAsync();
                return Page();
            }
            catch (InvalidOperationException ex)
            {
                ErrorMessage = ex.Message;
                await OnGetAsync();
                return Page();
            }
        }
    }
}