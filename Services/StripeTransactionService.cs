using Microsoft.Extensions.Options;
using Stripe;

namespace TINWeb.Services
{
    public sealed class StripeTransactionService
    {
        private readonly StripeSettings _stripeSettings;

        public StripeTransactionService(IOptions<StripeSettings> stripeOptions)
        {
            _stripeSettings = stripeOptions.Value;
        }

        public async Task<StripeTransactionResult> GetTransactionsAsync(string? descriptionFilter, int limit = 100)
        {
            EnsureConfigured();

            StripeConfiguration.ApiKey = _stripeSettings.SecretKey;

            var listOptions = new BalanceTransactionListOptions
            {
                Limit = Math.Clamp(limit, 1, 100)
            };

            var service = new BalanceTransactionService();
            var transactions = await service.ListAsync(listOptions);

            var rows = transactions.Data
                .OrderByDescending(t => t.Created)
                .Select(t => new StripeTransactionRow
                {
                    Id = t.Id,
                    Type = t.Type,
                    Description = t.Description,
                    Created = t.Created,
                    Amount = t.Amount,
                    Fee = t.Fee,
                    Net = t.Net,
                    Currency = t.Currency,
                    Status = t.Status,
                    SourceId = t.SourceId
                })
                .ToList();

            var descriptionOptions = rows
                .Select(r => r.Description)
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(d => d)
                .ToList();

            string? normalizedFilter = string.IsNullOrWhiteSpace(descriptionFilter)
                ? null
                : descriptionFilter.Trim();

            if (!string.IsNullOrWhiteSpace(normalizedFilter))
            {
                rows = rows
                    .Where(r => string.Equals(r.Description?.Trim(), normalizedFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return new StripeTransactionResult
            {
                Rows = rows,
                DescriptionOptions = descriptionOptions,
                ActiveDescriptionFilter = normalizedFilter
            };
        }

        private void EnsureConfigured()
        {
            if (string.IsNullOrWhiteSpace(_stripeSettings.SecretKey))
            {
                throw new InvalidOperationException(
                    "Stripe secret key is not configured. Set Stripe__SecretKey in environment settings.");
            }
        }
    }

    public sealed class StripeTransactionResult
    {
        public List<StripeTransactionRow> Rows { get; set; } = new();
        public List<string> DescriptionOptions { get; set; } = new();
        public string? ActiveDescriptionFilter { get; set; }
    }

    public sealed class StripeTransactionRow
    {
        public string Id { get; set; } = string.Empty;
        public string? Type { get; set; }
        public string? Description { get; set; }
        public DateTime Created { get; set; }
        public long Amount { get; set; }
        public long Fee { get; set; }
        public long Net { get; set; }
        public string? Currency { get; set; }
        public string? Status { get; set; }
        public string? SourceId { get; set; }
    }
}