using System.Text.RegularExpressions;
using UglyToad.PdfPig.Content;

namespace PdfProof.Layout;

internal static class WordFactory
{
    // Subset fonts carry a random six-letter tag per generation: "ABCDEF+Lato-Bold".
    private static readonly Regex SubsetPrefix = new(@"^[A-Z]{6}\+", RegexOptions.CultureInvariant);

    // Null when the word has no visible text after normalization, e.g. only zero-width characters.
    public static LayoutWord? Create(Word word, LayoutOptions options)
    {
        if (word.Letters.Count == 0)
        {
            return null;
        }

        var text = Normalize(word.Text, options).Trim();
        if (text.Length == 0)
        {
            return null;
        }

        var first = word.Letters[0];
        var font = word.FontName ?? first.FontName;
        font = font is null ? null : SubsetPrefix.Replace(font, "");
        var details = first.FontDetails;
        return new LayoutWord(
            text,
            Geometry.Bounds(word.BoundingBox),
            first.StartBaseLine.Y,
            word.Letters.Max(letter => letter.PointSize),
            font,
            Geometry.Hex(first.Color),
            details?.IsBold == true || NameContains(font, "Bold"),
            details?.IsItalic == true || NameContains(font, "Italic") || NameContains(font, "Oblique"),
            word);
    }

    public static string Normalize(string text, LayoutOptions options)
    {
        var result = options.NormalizeText ? TextNormalizer.Normalize(text) : text;
        foreach (var key in options.TextReplacements.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            result = result.Replace(key, options.TextReplacements[key]);
        }

        return result;
    }

    private static bool NameContains(string? font, string part) =>
        font is not null && font.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
}
