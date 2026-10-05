using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using TINWeb.Data;
using TINWeb.Services;

namespace TINWeb.Pages.MailerLite
{
    // Physical dimensions/layout for a supported label sheet format.
    public class LabelFormat
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public int Columns { get; init; }
        public int Rows { get; init; }
        public double LabelWidthMm { get; init; }
        public double LabelHeightMm { get; init; }
        public double MarginMm { get; init; }
        public double LogoWidthTargetMm { get; init; }
        public double LogoMaxHeightMm { get; init; }
        public double NameFontSize { get; init; }
        public double CompanyFontSize { get; init; }

        public int PerPage => Columns * Rows;
    }

    public class PrintLabelsModel : PageModel
    {
        private const double MmToPt = 72.0 / 25.4;

        public static readonly List<LabelFormat> Formats = new()
        {
            new LabelFormat
            {
                Id = "L7168",
                Name = "L7168 (8 per page)",
                Columns = 2,
                Rows = 4,
                LabelWidthMm = 86.5,
                LabelHeightMm = 55.5,
                MarginMm = 5,
                LogoWidthTargetMm = 32,
                LogoMaxHeightMm = 20,
                NameFontSize = 18,
                CompanyFontSize = 16
            },
            new LabelFormat
            {
                Id = "L7163",
                Name = "L7163 (14 per page)",
                Columns = 2,
                Rows = 7,
                LabelWidthMm = 99.1,
                LabelHeightMm = 38.1,
                MarginMm = 4,
                LogoWidthTargetMm = 26,
                LogoMaxHeightMm = 12,
                NameFontSize = 18,
                CompanyFontSize = 16
            }
        };

        public const string DefaultFormatId = "L7168";

        private readonly MailerLiteService _mailerLiteService;
        private readonly ApplicationDbContext _context;
        private readonly IImageStorageService _imageStorageService;

        public PrintLabelsModel(MailerLiteService mailerLiteService, ApplicationDbContext context, IImageStorageService imageStorageService)
        {
            _mailerLiteService = mailerLiteService;
            _context = context;
            _imageStorageService = imageStorageService;
        }

        public async Task<IActionResult> OnGetAsync(string? groupId, string? labelFormat)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                return BadRequest("No MailerLite group selected.");
            }

            var format = Formats.FirstOrDefault(f => f.Id == labelFormat) ?? Formats.First(f => f.Id == DefaultFormatId);

            var groups = await _mailerLiteService.GetGroupsAsync();
            var groupName = groups.FirstOrDefault(g => g.Id == groupId)?.Name ?? groupId;
            var subscribers = await _mailerLiteService.GetGroupSubscribersAsync(groupId);
            var logoBytes = await LoadLogoBytesAsync();

            var pdfBytes = GenerateLabelsPdf(groupName, subscribers, logoBytes, format);
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

        private static byte[] GenerateLabelsPdf(string groupName, List<MailerLiteSubscriber> subscribers, byte[]? logoBytes, LabelFormat format)
        {
            using var document = new PdfDocument();
            document.Info.Title = $"Labels - {groupName}";

            var nameFont = new XFont("Arial", format.NameFontSize, XFontStyle.Bold);
            var companyFont = new XFont("Arial", format.CompanyFontSize, XFontStyle.Regular);
            var brush = XBrushes.Black;

            XImage? logoImage = logoBytes is { Length: > 0 }
                ? XImage.FromStream(() => new MemoryStream(logoBytes))
                : null;

            var pages = subscribers.Chunk(format.PerPage).ToList();
            if (pages.Count == 0)
            {
                pages.Add(Array.Empty<MailerLiteSubscriber>());
            }

            double labelWidth = format.LabelWidthMm * MmToPt;
            double labelHeight = format.LabelHeightMm * MmToPt;

            // Consistent internal margin applied identically in both label columns.
            double margin = format.MarginMm * MmToPt;
            double logoWidthTarget = format.LogoWidthTargetMm * MmToPt;
            double logoMaxHeight = format.LogoMaxHeightMm * MmToPt;
            double nameLineHeight = nameFont.Size * 1.2;
            double companyLineHeight = companyFont.Size * 1.2;
            double lineGap = MmToPt;
            double textWidth = labelWidth - (margin * 2);

            foreach (var pageSubscribers in pages)
            {
                var page = document.AddPage();
                page.Size = PdfSharpCore.PageSize.A4;
                page.Orientation = PdfSharpCore.PageOrientation.Portrait;
                using var gfx = XGraphics.FromPdfPage(page);

                double contentWidth = labelWidth * format.Columns;
                double contentHeight = labelHeight * format.Rows;
                double marginX = (page.Width - contentWidth) / 2;
                double marginY = (page.Height - contentHeight) / 2;

                for (var i = 0; i < pageSubscribers.Length; i++)
                {
                    var subscriber = pageSubscribers[i];
                    var col = i % format.Columns;
                    var row = i / format.Columns;
                    double x = marginX + (col * labelWidth);
                    double y = marginY + (row * labelHeight);

                    var nameText = $"{subscriber.FirstName} {subscriber.LastName}".Trim();
                    var companyLines = WrapText(gfx, subscriber.CompanyName ?? "", companyFont, textWidth);
                    double textBlockHeight = nameLineHeight
                        + (companyLines.Count > 0 ? lineGap + (companyLines.Count * companyLineHeight) : 0);

                    double availableLogoHeight = labelHeight - (2 * margin) - textBlockHeight - (companyLines.Count > 0 ? lineGap : 0);
                    double logoHeight = 0;
                    double logoWidth = 0;
                    if (logoImage != null)
                    {
                        var imageAspectRatio = logoImage.PixelHeight / (double)logoImage.PixelWidth;
                        logoWidth = Math.Min(logoWidthTarget, Math.Min(logoMaxHeight / imageAspectRatio, Math.Max(0, availableLogoHeight) / imageAspectRatio));
                        logoHeight = logoWidth * imageAspectRatio;
                        if (logoHeight > 0)
                        {
                            // Keep the logo inset at the top-right while making room for wrapped text.
                            gfx.DrawImage(logoImage, x + labelWidth - margin - logoWidth, y + margin, logoWidth, logoHeight);
                        }
                    }

                    double lowerRegionTop = y + margin + logoHeight + (logoHeight > 0 && textBlockHeight > 0 ? lineGap : 0);
                    double lowerRegionBottom = y + labelHeight - margin;
                    double blockTop = lowerRegionTop + ((lowerRegionBottom - lowerRegionTop - textBlockHeight) / 2);

                    var nameRect = new XRect(x + margin, blockTop, textWidth, nameLineHeight);
                    gfx.DrawString(nameText, nameFont, brush, nameRect, XStringFormats.TopCenter);

                    for (var lineIndex = 0; lineIndex < companyLines.Count; lineIndex++)
                    {
                        var companyRect = new XRect(
                            x + margin,
                            blockTop + nameLineHeight + lineGap + (lineIndex * companyLineHeight),
                            textWidth,
                            companyLineHeight);
                        gfx.DrawString(companyLines[lineIndex], companyFont, brush, companyRect, XStringFormats.TopCenter);
                    }
                }
            }

            using var outputStream = new MemoryStream();
            document.Save(outputStream, false);
            return outputStream.ToArray();
        }

        private static List<string> WrapText(XGraphics graphics, string text, XFont font, double width)
        {
            var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var lines = new List<string>();
            var currentLine = string.Empty;

            foreach (var word in words)
            {
                var candidate = currentLine.Length == 0 ? word : $"{currentLine} {word}";
                if (graphics.MeasureString(candidate, font).Width <= width)
                {
                    currentLine = candidate;
                    continue;
                }

                if (currentLine.Length > 0)
                {
                    lines.Add(currentLine);
                    currentLine = string.Empty;
                }

                if (graphics.MeasureString(word, font).Width <= width)
                {
                    currentLine = word;
                    continue;
                }

                var wordPart = string.Empty;
                foreach (var character in word)
                {
                    var nextPart = wordPart + character;
                    if (wordPart.Length > 0 && graphics.MeasureString(nextPart, font).Width > width)
                    {
                        lines.Add(wordPart);
                        wordPart = character.ToString();
                    }
                    else
                    {
                        wordPart = nextPart;
                    }
                }

                currentLine = wordPart;
            }

            if (currentLine.Length > 0)
            {
                lines.Add(currentLine);
            }

            return lines;
        }
    }
}
