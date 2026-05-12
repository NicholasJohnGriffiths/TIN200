using System.Globalization;
using Microsoft.Extensions.Options;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using Stripe;

namespace TINWeb.Services
{
    public sealed class StripeTransactionService
    {
        private readonly StripeSettings _stripeSettings;
        private const int TransactionsPageSize = 20;

        public StripeTransactionService(IOptions<StripeSettings> stripeOptions)
        {
            _stripeSettings = stripeOptions.Value;
        }

        public async Task<StripeTransactionResult> GetTransactionsAsync(string? descriptionFilter, int year = 0, int pageNumber = 1, int pageSize = TransactionsPageSize)
        {
            var report = await BuildTransactionsReportAsync(descriptionFilter, year);
            var normalizedPageSize = Math.Clamp(pageSize, 1, TransactionsPageSize);
            var totalPages = Math.Max(1, (int)Math.Ceiling(report.Rows.Count / (double)normalizedPageSize));
            var normalizedPageNumber = Math.Clamp(pageNumber, 1, totalPages);

            return new StripeTransactionResult
            {
                Rows = report.Rows
                    .Skip((normalizedPageNumber - 1) * normalizedPageSize)
                    .Take(normalizedPageSize)
                    .ToList(),
                ActiveDescriptionFilter = report.ActiveDescriptionFilter,
                PageNumber = normalizedPageNumber,
                PageSize = normalizedPageSize,
                TotalPages = totalPages,
                TotalCount = report.TotalCount,
                TotalAmountMinor = report.TotalAmountMinor,
                TotalAmountDisplay = report.TotalAmountDisplay,
                RefundedCount = report.RefundedCount,
                DisputedCount = report.DisputedCount,
                FailedCount = report.FailedCount,
                UncapturedCount = report.UncapturedCount
            };
        }

        public async Task<byte[]> GenerateTransactionsPdfAsync(string? descriptionFilter, int year = 0)
        {
            var report = await BuildTransactionsReportAsync(descriptionFilter, year);

            using var stream = new MemoryStream();
            var document = new PdfDocument();
            document.Info.Title = "Stripe Transactions";

            var titleFont = new XFont("Arial", 16, XFontStyle.Bold);
            var sectionFont = new XFont("Arial", 9, XFontStyle.Regular);
            var headerFont = new XFont("Arial", 8, XFontStyle.Bold);
            var cellFont = new XFont("Arial", 8, XFontStyle.Regular);
            var brush = XBrushes.Black;
            var headerBrush = XBrushes.White;
            var headerBackground = new XSolidBrush(XColor.FromArgb(33, 37, 41));

            PdfPage page = document.AddPage();
            page.Size = PdfSharpCore.PageSize.A4;
            page.Orientation = PdfSharpCore.PageOrientation.Landscape;
            XGraphics gfx = XGraphics.FromPdfPage(page);

            double margin = 24;
            double y = margin;
            double contentWidth = page.Width - (margin * 2);
            var columns = new[]
            {
                85d, 65d, 95d, 170d, 120d, 85d, 150d
            };

            void AddNewPage()
            {
                page = document.AddPage();
                page.Size = PdfSharpCore.PageSize.A4;
                page.Orientation = PdfSharpCore.PageOrientation.Landscape;
                gfx = XGraphics.FromPdfPage(page);
                y = margin;
                DrawTableHeader();
            }

            void DrawTableHeader()
            {
                double x = margin;
                var headers = new[] { "Date/Time", "Amount", "Succeeded", "Payment Method", "Description", "Customer", "Refunded / Decline" };

                for (var index = 0; index < headers.Length; index++)
                {
                    var rect = new XRect(x, y, columns[index], 20);
                    gfx.DrawRectangle(headerBackground, rect);
                    gfx.DrawString(headers[index], headerFont, headerBrush, rect, XStringFormats.Center);
                    x += columns[index];
                }

                y += 20;
            }

            gfx.DrawString("Stripe Transactions", titleFont, brush, new XRect(margin, y, contentWidth, 20), XStringFormats.TopLeft);
            y += 24;
            gfx.DrawString(
                $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC    Search: {(string.IsNullOrWhiteSpace(report.ActiveDescriptionFilter) ? "All" : report.ActiveDescriptionFilter)}",
                sectionFont,
                brush,
                new XRect(margin, y, contentWidth, 16),
                XStringFormats.TopLeft);
            y += 18;
            gfx.DrawString(
                $"Transactions: {report.TotalCount}    Total amount: {report.TotalAmountDisplay}    Refunded: {report.RefundedCount}    Disputed: {report.DisputedCount}    Failed: {report.FailedCount}    Uncaptured: {report.UncapturedCount}",
                sectionFont,
                brush,
                new XRect(margin, y, contentWidth, 16),
                XStringFormats.TopLeft);
            y += 22;

            DrawTableHeader();

            foreach (var row in report.Rows)
            {
                if (y > page.Height - margin - 18)
                {
                    AddNewPage();
                }

                double x = margin;
                var values = new[]
                {
                    row.Created.ToUniversalTime().ToString("yyyy-MM-dd HH:mm"),
                    row.AmountDisplay,
                    row.Succeeded ? "Yes" : "No",
                    Truncate(row.PaymentMethod, 24),
                    Truncate(row.DescriptionDisplay, 42),
                    Truncate(row.CustomerDisplay, 30),
                    Truncate(BuildRefundDeclineSummary(row), 40)
                };

                for (var index = 0; index < values.Length; index++)
                {
                    var rect = new XRect(x, y, columns[index], 18);
                    gfx.DrawRectangle(XPens.LightGray, rect);
                    gfx.DrawString(values[index], cellFont, brush, new XRect(rect.X + 3, rect.Y + 2, rect.Width - 6, rect.Height - 4), XStringFormats.TopLeft);
                    x += columns[index];
                }

                y += 18;
            }

            document.Save(stream, false);
            return stream.ToArray();
        }

        private async Task<StripeTransactionReportData> BuildTransactionsReportAsync(string? descriptionFilter, int year = 0)
        {
            EnsureConfigured();

            StripeConfiguration.ApiKey = _stripeSettings.SecretKey;

            var service = new ChargeService();
            var effectiveYear = year > 0 ? year : DateTime.UtcNow.Year;
            var listOptions = new ChargeListOptions
            {
                Limit = 100,
                Created = new DateRangeOptions
                {
                    GreaterThanOrEqual = new DateTime(effectiveYear, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    LessThan = new DateTime(effectiveYear + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            };

            listOptions.AddExpand("data.customer");
            listOptions.AddExpand("data.refunds.data");

            var rows = new List<StripeTransactionRow>();

            await foreach (var charge in service.ListAutoPagingAsync(listOptions))
            {
                rows.Add(MapCharge(charge));
            }

            var normalizedFilter = string.IsNullOrWhiteSpace(descriptionFilter)
                ? null
                : descriptionFilter.Trim();

            if (!string.IsNullOrWhiteSpace(normalizedFilter))
            {
                rows = rows
                    .Where(row => row.DescriptionDisplay.Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            rows = rows
                .OrderByDescending(row => row.Created)
                .ToList();

            return new StripeTransactionReportData
            {
                Rows = rows,
                ActiveDescriptionFilter = normalizedFilter,
                TotalCount = rows.Count,
                TotalAmountMinor = rows.Sum(row => row.AmountMinor),
                TotalAmountDisplay = FormatMinorAmount(rows.Sum(row => row.AmountMinor), rows.Select(row => row.Currency).FirstOrDefault()),
                RefundedCount = rows.Count(row => row.IsRefunded),
                DisputedCount = rows.Count(row => row.IsDisputed),
                FailedCount = rows.Count(row => row.IsFailed),
                UncapturedCount = rows.Count(row => row.IsUncaptured)
            };
        }

        private static StripeTransactionRow MapCharge(Charge charge)
        {
            var latestRefund = charge.Refunds?.Data?
                .OrderByDescending(refund => refund.Created)
                .FirstOrDefault();

            var description = string.IsNullOrWhiteSpace(charge.Description)
                ? string.Empty
                : charge.Description.Trim();

            return new StripeTransactionRow
            {
                Id = charge.Id,
                Created = charge.Created,
                AmountMinor = charge.Amount,
                AmountDisplay = FormatMinorAmount(charge.Amount, charge.Currency),
                Currency = charge.Currency,
                Description = description,
                DescriptionDisplay = string.IsNullOrWhiteSpace(description) ? "-" : description,
                Succeeded = string.Equals(charge.Status, "succeeded", StringComparison.OrdinalIgnoreCase),
                PaymentMethod = BuildPaymentMethod(charge),
                CustomerDisplay = BuildCustomer(charge),
                RefundedDate = latestRefund?.Created,
                DeclineReason = BuildDeclineReason(charge),
                IsRefunded = charge.Refunded || charge.AmountRefunded > 0,
                IsDisputed = charge.Disputed,
                IsFailed = string.Equals(charge.Status, "failed", StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrWhiteSpace(charge.FailureCode)
                    || !string.IsNullOrWhiteSpace(charge.Outcome?.Reason),
                IsUncaptured = charge.Paid && !charge.Captured
            };
        }

        private static string BuildPaymentMethod(Charge charge)
        {
            var paymentMethodType = charge.PaymentMethodDetails?.Type;
            var card = charge.PaymentMethodDetails?.Card;

            if (card != null)
            {
                var brand = string.IsNullOrWhiteSpace(card.Brand) ? "Card" : card.Brand.ToUpperInvariant();
                var last4 = string.IsNullOrWhiteSpace(card.Last4) ? string.Empty : $" ****{card.Last4}";
                return $"{brand}{last4}".Trim();
            }

            if (!string.IsNullOrWhiteSpace(paymentMethodType))
            {
                return paymentMethodType.Replace("_", " ", StringComparison.OrdinalIgnoreCase);
            }

            return "-";
        }

        private static string BuildCustomer(Charge charge)
        {
            var customerName = charge.Customer?.Name;
            var customerEmail = charge.Customer?.Email;

            if (!string.IsNullOrWhiteSpace(customerName) && !string.IsNullOrWhiteSpace(customerEmail))
            {
                return $"{customerName} ({customerEmail})";
            }

            if (!string.IsNullOrWhiteSpace(customerName))
            {
                return customerName;
            }

            if (!string.IsNullOrWhiteSpace(customerEmail))
            {
                return customerEmail;
            }

            if (!string.IsNullOrWhiteSpace(charge.BillingDetails?.Name) && !string.IsNullOrWhiteSpace(charge.BillingDetails?.Email))
            {
                return $"{charge.BillingDetails.Name} ({charge.BillingDetails.Email})";
            }

            if (!string.IsNullOrWhiteSpace(charge.BillingDetails?.Email))
            {
                return charge.BillingDetails.Email;
            }

            if (!string.IsNullOrWhiteSpace(charge.CustomerId))
            {
                return charge.CustomerId;
            }

            return "-";
        }

        private static string? BuildDeclineReason(Charge charge)
        {
            if (!string.IsNullOrWhiteSpace(charge.FailureMessage))
            {
                return charge.FailureMessage;
            }

            if (!string.IsNullOrWhiteSpace(charge.Outcome?.SellerMessage))
            {
                return charge.Outcome.SellerMessage;
            }

            if (!string.IsNullOrWhiteSpace(charge.Outcome?.Reason))
            {
                return charge.Outcome.Reason;
            }

            return null;
        }

        private static string FormatMinorAmount(long amountMinor, string? currency)
        {
            var normalizedCurrency = string.IsNullOrWhiteSpace(currency)
                ? null
                : currency.Trim().ToUpperInvariant();

            decimal amount = amountMinor / 100m;
            return normalizedCurrency == null
                ? amount.ToString("N2", CultureInfo.InvariantCulture)
                : $"{amount.ToString("N2", CultureInfo.InvariantCulture)} {normalizedCurrency}";
        }

        private static string Truncate(string? value, int length)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "-";
            }

            var trimmed = value.Trim();
            return trimmed.Length <= length ? trimmed : $"{trimmed[..(length - 3)]}...";
        }

        private static string BuildRefundDeclineSummary(StripeTransactionRow row)
        {
            var refunded = row.RefundedDate.HasValue
                ? $"Refunded: {row.RefundedDate.Value.ToUniversalTime():yyyy-MM-dd}"
                : "Refunded: -";
            var decline = string.IsNullOrWhiteSpace(row.DeclineReason)
                ? "Decline: -"
                : $"Decline: {row.DeclineReason}";

            return $"{refunded}; {decline}";
        }

        private void EnsureConfigured()
        {
            if (string.IsNullOrWhiteSpace(_stripeSettings.SecretKey))
            {
                throw new InvalidOperationException(
                    _stripeSettings.UseTestMode
                        ? "Stripe is in test mode but no secret key is configured. Set Stripe__SecretKey__Test (or Stripe:SecretKey:Test)."
                        : "Stripe secret key is not configured. Set Stripe__SecretKey (or Stripe:SecretKey)."
                );
            }
        }
    }

    public sealed class StripeTransactionResult
    {
        public List<StripeTransactionRow> Rows { get; set; } = new();
        public string? ActiveDescriptionFilter { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
        public long TotalAmountMinor { get; set; }
        public string TotalAmountDisplay { get; set; } = "0.00";
        public int RefundedCount { get; set; }
        public int DisputedCount { get; set; }
        public int FailedCount { get; set; }
        public int UncapturedCount { get; set; }
    }

    public sealed class StripeTransactionRow
    {
        public string Id { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DescriptionDisplay { get; set; } = "-";
        public DateTime Created { get; set; }
        public long AmountMinor { get; set; }
        public string AmountDisplay { get; set; } = string.Empty;
        public string? Currency { get; set; }
        public bool Succeeded { get; set; }
        public string PaymentMethod { get; set; } = "-";
        public string CustomerDisplay { get; set; } = "-";
        public DateTime? RefundedDate { get; set; }
        public string? DeclineReason { get; set; }
        public bool IsRefunded { get; set; }
        public bool IsDisputed { get; set; }
        public bool IsFailed { get; set; }
        public bool IsUncaptured { get; set; }
    }

    internal sealed class StripeTransactionReportData
    {
        public List<StripeTransactionRow> Rows { get; set; } = new();
        public string? ActiveDescriptionFilter { get; set; }
        public int TotalCount { get; set; }
        public long TotalAmountMinor { get; set; }
        public string TotalAmountDisplay { get; set; } = "0.00";
        public int RefundedCount { get; set; }
        public int DisputedCount { get; set; }
        public int FailedCount { get; set; }
        public int UncapturedCount { get; set; }
    }
}