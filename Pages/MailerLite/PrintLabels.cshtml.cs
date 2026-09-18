using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using TINWeb.Data;
using TINWeb.Services;

namespace TINWeb.Pages.MailerLite
{
    public class PrintLabelsModel : PageModel
    {
        private const double MmToPt = 72.0 / 25.4;
        private const double LabelWidthMm = 86.5;
        private const double LabelHeightMm = 55.5;
        private const int Columns = 2;
        private const int Rows = 4;

        private readonly MailerLiteService _mailerLiteService;
        private readonly ApplicationDbContext _context;
        private readonly IImageStorageService _imageStorageService;

        public PrintLabelsModel(MailerLiteService mailerLiteService, ApplicationDbContext context, IImageStorageService imageStorageService)
        {
            _mailerLiteService = mailerLiteService;
            _context = context;
            _imageStorageService = imageStorageService;
        }

        public async Task<IActionResult> OnGetAsync(string? groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                return BadRequest("No MailerLite group selected.");
            }

            var groups = await _mailerLiteService.GetGroupsAsync();
            var groupName = groups.FirstOrDefault(g => g.Id == groupId)?.Name ?? groupId;
            var subscribers = await _mailerLiteService.GetGroupSubscribersAsync(groupId);
            var logoBytes = await LoadLogoBytesAsync();

            var pdfBytes = GenerateLabelsPdf(groupName, subscribers, logoBytes);
            return File(pdfBytes, "application/pdf");
        }

        private async Task<byte[]?> LoadLogoBytesAsync()
        {
            var config = await _context.AppConfig.AsNoTracking().OrderBy(c => c.Id).FirstOrDefaultAsync();
            if (config?.TinLogoLabelsImageId == null)
            {
                return null;
            }

            var image = await _context.Image.AsNoTracking().FirstOrDefaultAsync(i => i.Id == config.TinLogoLabelsImageId.Value);
            if (image == null || string.IsNullOrWhiteSpace(image.FilePath))
            {
                return null;
            }

            using var stream = await _imageStorageService.OpenReadAsync(image.FilePath);
            if (stream == null)
            {
                return null;
            }

            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }

        private static byte[] GenerateLabelsPdf(string groupName, List<MailerLiteSubscriber> subscribers, byte[]? logoBytes)
        {
            using var document = new PdfDocument();
            document.Info.Title = $"Labels - {groupName}";

            var nameFont = new XFont("Arial", 13, XFontStyle.Bold);
            var companyFont = new XFont("Arial", 10, XFontStyle.Regular);
            var brush = XBrushes.Black;

            XImage? logoImage = logoBytes is { Length: > 0 }
                ? XImage.FromStream(() => new MemoryStream(logoBytes))
                : null;

            const int perPage = Columns * Rows;
            var pages = subscribers.Chunk(perPage).ToList();
            if (pages.Count == 0)
            {
                pages.Add(Array.Empty<MailerLiteSubscriber>());
            }

            double labelWidth = LabelWidthMm * MmToPt;
            double labelHeight = LabelHeightMm * MmToPt;

            // Consistent internal margin applied identically in both label columns.
            double margin = 5 * MmToPt;
            double logoWidthTarget = 32 * MmToPt;
            double logoMaxHeight = 20 * MmToPt;
            double nameLineHeight = nameFont.Size * 1.2;
            double companyLineHeight = companyFont.Size * 1.2;
            double lineGap = 2 * MmToPt;
            double textBlockHeight = nameLineHeight + lineGap + companyLineHeight;

            foreach (var pageSubscribers in pages)
            {
                var page = document.AddPage();
                page.Size = PdfSharpCore.PageSize.A4;
                page.Orientation = PdfSharpCore.PageOrientation.Portrait;
                using var gfx = XGraphics.FromPdfPage(page);

                double contentWidth = labelWidth * Columns;
                double contentHeight = labelHeight * Rows;
                double marginX = (page.Width - contentWidth) / 2;
                double marginY = (page.Height - contentHeight) / 2;

                for (var i = 0; i < pageSubscribers.Length; i++)
                {
                    var subscriber = pageSubscribers[i];
                    var col = i % Columns;
                    var row = i / Columns;
                    double x = marginX + (col * labelWidth);
                    double y = marginY + (row * labelHeight);

                    double logoBottom = y + margin;
                    if (logoImage != null)
                    {
                        double logoWidth = logoWidthTarget;
                        double logoHeight = logoWidth * (logoImage.PixelHeight / (double)logoImage.PixelWidth);
                        if (logoHeight > logoMaxHeight)
                        {
                            logoHeight = logoMaxHeight;
                            logoWidth = logoMaxHeight * (logoImage.PixelWidth / (double)logoImage.PixelHeight);
                        }

                        // Top-right, inset by the same margin used elsewhere so it isn't flush with the edge.
                        gfx.DrawImage(logoImage, x + labelWidth - margin - logoWidth, y + margin, logoWidth, logoHeight);
                        logoBottom = y + margin + logoHeight;
                    }

                    // Name/company block is centred within the lower portion of the label, below the logo.
                    double lowerRegionTop = Math.Max(y + (LabelHeightMm * MmToPt * 0.4), logoBottom + (2 * MmToPt));
                    double lowerRegionBottom = y + labelHeight - margin;
                    double blockTop = lowerRegionTop + ((lowerRegionBottom - lowerRegionTop - textBlockHeight) / 2);

                    var textWidth = labelWidth - (margin * 2);

                    var nameText = $"{subscriber.FirstName} {subscriber.LastName}".Trim();
                    var nameRect = new XRect(x + margin, blockTop, textWidth, nameLineHeight);
                    gfx.DrawString(nameText, nameFont, brush, nameRect, XStringFormats.TopLeft);

                    var companyRect = new XRect(x + margin, blockTop + nameLineHeight + lineGap, textWidth, companyLineHeight);
                    gfx.DrawString(subscriber.CompanyName ?? "", companyFont, brush, companyRect, XStringFormats.TopLeft);
                }
            }

            using var outputStream = new MemoryStream();
            document.Save(outputStream, false);
            return outputStream.ToArray();
        }
    }
}

