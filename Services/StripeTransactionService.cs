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
            var normalizedPageSize = pageSize > 0 ? pageSize : TransactionsPageSize;
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
            return GeneratePdfFromReport(report);
        }

        public byte[] GenerateTransactionsPdfFromRows(List<StripeTransactionRow> rows, string? searchFilter = null)
        {
            // Build a report object from pre-fetched rows
            var report = new StripeTransactionReportData
            {
                Rows = rows,
                ActiveDescriptionFilter = searchFilter,
                TotalCount = rows.Count,
                TotalAmountMinor = rows.Sum(r => r.AmountMinor),
                TotalAmountDisplay = FormatMinorAmount(rows.Sum(r => r.AmountMinor), rows.FirstOrDefault()?.Currency),
                RefundedCount = rows.Count(r => r.IsRefunded),
                DisputedCount = rows.Count(r => r.IsDisputed),
                FailedCount = rows.Count(r => r.IsFailed),
                UncapturedCount = rows.Count(r => r.IsUncaptured)
            };

            return GeneratePdfFromReport(report);
        }

        private byte[] GeneratePdfFromReport(StripeTransactionReportData report)
        {
            using var stream = new MemoryStream();
            var document = new PdfDocument();
            document.Info.Title = "Stripe Transactions";

            var titleFont = new XFont("Arial", 9, XFontStyle.Bold);
            var sectionFont = new XFont("Arial", 5, XFontStyle.Regular);
            var headerFont = new XFont("Arial", 5, XFontStyle.Bold);
            var cellFont = new XFont("Arial", 5, XFontStyle.Regular);
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
                80d, 55d, 60d, 90d, 115d, 100d, 75d, 75d, 85d, 115d
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
                var headers = new[] { "Date/Time", "Amount", "Succeeded", "Payment Method", "Description", "Customer", "Company", "Report", "Purchase For", "Refunded / Decline" };

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

            const double lineHeight = 7.5d;
            const double cellPadding = 3d;

            List<string> WrapText(string text, double maxWidth)
            {
                var lines = new List<string>();
                if (string.IsNullOrWhiteSpace(text)) { lines.Add("-"); return lines; }
                var words = text.Split(' ');
                var current = new System.Text.StringBuilder();
                foreach (var word in words)
                {
                    var candidate = current.Length == 0 ? word : current + " " + word;
                    var size = gfx.MeasureString(candidate, cellFont);
                    if (size.Width > maxWidth - cellPadding * 2 && current.Length > 0)
                    {
                        lines.Add(current.ToString());
                        current.Clear();
                        current.Append(word);
                    }
                    else
                    {
                        if (current.Length > 0) current.Append(' ');
                        current.Append(word);
                    }
                }
                if (current.Length > 0) lines.Add(current.ToString());
                return lines;
            }

            foreach (var row in report.Rows)
            {
                var nzTz = TimeZoneInfo.FindSystemTimeZoneById("New Zealand Standard Time");
                var nzTime = TimeZoneInfo.ConvertTime(row.Created, TimeZoneInfo.Utc, nzTz);
                var values = new[]
                {
                    nzTime.ToString("yyyy-MM-dd HH:mm"),
                    row.AmountDisplay,
                    row.Succeeded ? "Yes" : "No",
                    row.PaymentMethod == "-" ? "-" : row.PaymentMethod,
                    row.DescriptionDisplay,
                    row.CustomerDisplay,
                    row.MetaCompany,
                    row.MetaReport,
                    row.MetaPurchaseFor,
                    BuildRefundDeclineSummary(row)
                };

                var wrappedCells = values.Select((v, i) => WrapText(v, columns[i])).ToArray();
                var maxLines = wrappedCells.Max(l => l.Count);
                var rowHeight = Math.Max(18d, maxLines * lineHeight + cellPadding * 2);

                if (y + rowHeight > page.Height - margin)
                {
                    AddNewPage();
                }

                double x = margin;
                for (var index = 0; index < wrappedCells.Length; index++)
                {
                    var rect = new XRect(x, y, columns[index], rowHeight);
                    gfx.DrawRectangle(XPens.LightGray, rect);
                    var textY = y + cellPadding;
                    foreach (var line in wrappedCells[index])
                    {
                        gfx.DrawString(line, cellFont, brush,
                            new XRect(x + cellPadding, textY, columns[index] - cellPadding * 2, lineHeight),
                            XStringFormats.TopLeft);
                        textY += lineHeight;
                    }
                    x += columns[index];
                }

                y += rowHeight;
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
            listOptions.AddExpand("data.payment_intent");

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
                    .Where(row =>
                        row.DescriptionDisplay.Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase) ||
                        row.MetaCompany.Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase) ||
                        row.MetaReport.Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase))
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

            var description = BuildDescription(charge);

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
                MetaCompany = charge.Metadata != null && charge.Metadata.TryGetValue("Company", out var metaCompany) && !string.IsNullOrWhiteSpace(metaCompany) ? metaCompany : "-",
                MetaReport = charge.Metadata != null && charge.Metadata.TryGetValue("Report", out var metaReport) && !string.IsNullOrWhiteSpace(metaReport) ? metaReport : "-",
                MetaPurchaseFor = charge.Metadata != null && charge.Metadata.TryGetValue("Purchase For", out var metaPurchaseFor) && !string.IsNullOrWhiteSpace(metaPurchaseFor) ? metaPurchaseFor : "-",
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

        private static string BuildDescription(Charge charge)
        {
            var description = JoinNonEmpty(" | ",
                charge.Description,
                TryGetMetadataValue(charge.Metadata, "Purchase For"),
                TryGetMetadataValue(charge.Metadata, "Report"));

            if (!string.IsNullOrWhiteSpace(description))
            {
                return description;
            }

            if (!string.IsNullOrWhiteSpace(charge.PaymentIntent?.Description))
            {
                return charge.PaymentIntent.Description.Trim();
            }

            var entryId = TryGetMetadataValue(charge.Metadata, "Entry ID");
            var product = TryGetMetadataValue(charge.Metadata, "Product");

            if (!string.IsNullOrWhiteSpace(entryId) && !string.IsNullOrWhiteSpace(product))
            {
                return $"Entry ID: {entryId}, Product: {product}";
            }

            if (!string.IsNullOrWhiteSpace(product))
            {
                return $"Product: {product}";
            }

            if (!string.IsNullOrWhiteSpace(entryId))
            {
                return $"Entry ID: {entryId}";
            }

            if (!string.IsNullOrWhiteSpace(charge.CalculatedStatementDescriptor))
            {
                return charge.CalculatedStatementDescriptor.Trim();
            }

            if (!string.IsNullOrWhiteSpace(charge.StatementDescriptorSuffix))
            {
                return charge.StatementDescriptorSuffix.Trim();
            }

            return string.Empty;
        }

        private static string BuildCustomer(Charge charge)
        {
            var customerName = charge.Customer?.Name;
            var customerEmail = charge.Customer?.Email;
            var customerCompany = TryGetMetadataValue(charge.Metadata, "Company");
            var composedCustomer = JoinNonEmpty(" | ", customerName, customerCompany, customerEmail);

            if (!string.IsNullOrWhiteSpace(composedCustomer))
            {
                return composedCustomer;
            }

            if (!string.IsNullOrWhiteSpace(charge.BillingDetails?.Name) && !string.IsNullOrWhiteSpace(charge.BillingDetails?.Email))
            {
                return $"{charge.BillingDetails.Name} ({charge.BillingDetails.Email})";
            }

            if (!string.IsNullOrWhiteSpace(charge.BillingDetails?.Name))
            {
                return charge.BillingDetails.Name;
            }

            if (!string.IsNullOrWhiteSpace(charge.BillingDetails?.Email))
            {
                return charge.BillingDetails.Email;
            }

            if (!string.IsNullOrWhiteSpace(charge.ReceiptEmail))
            {
                return charge.ReceiptEmail;
            }

            if (!string.IsNullOrWhiteSpace(charge.CustomerId))
            {
                return charge.CustomerId;
            }

            return "-";
        }

        private static string JoinNonEmpty(string separator, params string?[] values)
        {
            return string.Join(separator,
                values
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!.Trim()));
        }

        private static string? TryGetMetadataValue(IDictionary<string, string>? metadata, string key)
        {
            if (metadata == null)
            {
                return null;
            }

            return metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value.Trim()
                : null;
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
        public string MetaCompany { get; set; } = "-";
        public string MetaReport { get; set; } = "-";
        public string MetaPurchaseFor { get; set; } = "-";
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