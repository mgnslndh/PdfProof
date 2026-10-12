using PdfSharp.Fonts;

namespace PdfProof.FixtureGenerator;

// PDFsharp 6 (the Core build) finds no fonts on its own. On Windows it may use the installed Windows fonts; elsewhere a
// resolver serves Liberation Sans or DejaVu Sans for every family MigraDoc asks for, its error font included. PDFsharp's
// font settings can be set only once per process, so the outcome is remembered.
internal static class MigraDocFonts
{
    private static readonly object Gate = new();
    private static bool _configured;
    private static string? _problem;

    // Null when fonts are set up; otherwise why not.
    public static string? Configure()
    {
        lock (Gate)
        {
            if (!_configured)
            {
                _configured = true;
                if (OperatingSystem.IsWindows())
                {
                    GlobalFontSettings.UseWindowsFontsUnderWindows = true;
                }
                else if (FileFontResolver.TryCreate() is { } resolver)
                {
                    GlobalFontSettings.FontResolver = resolver;
                }
                else
                {
                    _problem = "no Liberation Sans or DejaVu Sans font found under /usr/share/fonts/truetype";
                }
            }

            return _problem;
        }
    }

    private sealed class FileFontResolver(IReadOnlyDictionary<string, string> faces) : IFontResolver
    {
        public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) =>
            new((bold, italic) switch
            {
                (true, true) => "bolditalic",
                (true, false) => "bold",
                (false, true) => "italic",
                _ => "regular",
            });

        public byte[]? GetFont(string faceName) => File.ReadAllBytes(faces[faceName]);

        public static FileFontResolver? TryCreate()
        {
            string[][] families =
            [
                [
                    "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
                    "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf",
                    "/usr/share/fonts/truetype/liberation/LiberationSans-Italic.ttf",
                    "/usr/share/fonts/truetype/liberation/LiberationSans-BoldItalic.ttf",
                ],
                [
                    "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
                    "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
                    "/usr/share/fonts/truetype/dejavu/DejaVuSans-Oblique.ttf",
                    "/usr/share/fonts/truetype/dejavu/DejaVuSans-BoldOblique.ttf",
                ],
            ];
            var files = families.FirstOrDefault(family => family.All(File.Exists));
            return files is null
                ? null
                : new FileFontResolver(new Dictionary<string, string>
                {
                    ["regular"] = files[0],
                    ["bold"] = files[1],
                    ["italic"] = files[2],
                    ["bolditalic"] = files[3],
                });
        }
    }
}
