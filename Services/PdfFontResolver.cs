using System.Reflection;
using PdfSharpCore.Fonts;

namespace TINWeb.Services
{
    public class PdfFontResolver : IFontResolver
    {
        private static readonly Dictionary<string, byte[]> FontDataCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object SyncLock = new();

        public string DefaultFontName => "Arial";

        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            // Map Arial, Liberation Sans, Helvetica, or any fallback font request to LiberationSans
            var suffix = (isBold, isItalic) switch
            {
                (true, true) => "BoldItalic",
                (true, false) => "Bold",
                (false, true) => "Italic",
                _ => "Regular"
            };

            var faceName = $"LiberationSans-{suffix}";
            return new FontResolverInfo(faceName);
        }

        public byte[]? GetFont(string faceName)
        {
            var normalizedFace = NormalizeFaceName(faceName);
            lock (SyncLock)
            {
                if (FontDataCache.TryGetValue(normalizedFace, out var cached))
                {
                    return cached;
                }

                var assembly = typeof(PdfFontResolver).Assembly;
                // Embedded resources in TINWeb are named: TINWeb.Fonts.{normalizedFace}.ttf
                var resourceName = $"TINWeb.Fonts.{normalizedFace}.ttf";
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var memoryStream = new MemoryStream();
                    stream.CopyTo(memoryStream);
                    var data = memoryStream.ToArray();
                    FontDataCache[normalizedFace] = data;
                    return data;
                }

                return null;
            }
        }

        private static string NormalizeFaceName(string faceName)
        {
            if (string.IsNullOrWhiteSpace(faceName))
            {
                return "LiberationSans-Regular";
            }

            if (faceName.StartsWith("LiberationSans-", StringComparison.OrdinalIgnoreCase))
            {
                return faceName;
            }

            var isBold = faceName.Contains("Bold", StringComparison.OrdinalIgnoreCase);
            var isItalic = faceName.Contains("Italic", StringComparison.OrdinalIgnoreCase) || faceName.Contains("Oblique", StringComparison.OrdinalIgnoreCase);

            return (isBold, isItalic) switch
            {
                (true, true) => "LiberationSans-BoldItalic",
                (true, false) => "LiberationSans-Bold",
                (false, true) => "LiberationSans-Italic",
                _ => "LiberationSans-Regular"
            };
        }
    }
}
