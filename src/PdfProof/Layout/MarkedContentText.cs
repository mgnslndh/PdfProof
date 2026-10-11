using System.Text.RegularExpressions;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Util;

namespace PdfProof.Layout;

// The visible text of each marked-content sequence on a page, by MCID. Artifacts are skipped. Letters hidden by
// clipping are already gone (PdfPig applies ClipPaths to marked content too).
internal static class MarkedContentText
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.CultureInvariant);

    public static IReadOnlyDictionary<int, string> Collect(IReadOnlyList<MarkedContentElement> elements, LayoutOptions options)
    {
        var texts = new Dictionary<int, string>();
        foreach (var element in elements)
        {
            Collect(element, options, texts);
        }

        return texts;
    }

    private static void Collect(MarkedContentElement element, LayoutOptions options, Dictionary<int, string> texts)
    {
        if (element.IsArtifact)
        {
            return;
        }

        if (element.MarkedContentIdentifier >= 0)
        {
            var text = element.ActualText is { } actual ? Whitespace.Replace(WordFactory.Normalize(actual, options), " ").Trim() : Text(Letters(element).ToList(), options);
            texts[element.MarkedContentIdentifier] = texts.TryGetValue(element.MarkedContentIdentifier, out var earlier) && earlier.Length > 0
                ? (text.Length > 0 ? earlier + " " + text : earlier)
                : text;
            return; // nested sequences are part of this MCID's content
        }

        foreach (var child in element.Children)
        {
            Collect(child, options, texts);
        }
    }

    // Horizontal text: the word extractor recovers spaces from gaps, like the layout. Rotated text: content order is
    // the reading order within one sequence, and the word extractor would scramble it.
    private static string Text(List<Letter> letters, LayoutOptions options)
    {
        if (letters.All(letter => letter.TextOrientation == TextOrientation.Horizontal))
        {
            var words = DefaultWordExtractor.Instance.GetWords(letters)
                .Select(word => WordFactory.Create(word, options))
                .OfType<LayoutWord>()
                .Select(word => word.Text);
            return string.Join(" ", words);
        }

        return Whitespace.Replace(WordFactory.Normalize(string.Concat(letters.Select(letter => letter.Value)), options), " ").Trim();
    }

    private static IEnumerable<Letter> Letters(MarkedContentElement element) =>
        element.Letters.Concat(element.Children.SelectMany(Letters));
}
