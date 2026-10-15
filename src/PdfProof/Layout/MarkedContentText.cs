using System.Text.RegularExpressions;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Util;

namespace PdfProof.Layout;

// The visible text of a marked-content sequence, whether all its words are bold or italic, and where it is (default
// when it has no letters).
internal sealed record MarkedText(string Text, bool IsBold, bool IsItalic, LayoutBox Box);

// The visible text and style of each marked-content sequence on a page, by MCID. Artifacts are skipped. Letters hidden
// by clipping are already gone (PdfPig applies ClipPaths to marked content too).
internal static class MarkedContentText
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.CultureInvariant);

    public static IReadOnlyDictionary<int, MarkedText> Collect(IReadOnlyList<MarkedContentElement> elements, LayoutOptions options)
    {
        // Iterative, so deeply nested marked content can't overflow the stack.
        var texts = new Dictionary<int, MarkedText>();
        var pending = new Stack<MarkedContentElement>();
        for (var i = elements.Count - 1; i >= 0; i--)
        {
            pending.Push(elements[i]);
        }

        while (pending.Count > 0)
        {
            var element = pending.Pop();
            if (element.IsArtifact)
            {
                continue;
            }

            if (element.MarkedContentIdentifier >= 0)
            {
                var letters = Letters(element);
                var words = Words(letters, options);
                var text = element.ActualText is { } actual
                    ? Whitespace.Replace(WordFactory.Normalize(actual, options), " ").Trim()
                    : Text(letters, words, options);
                var box = letters.Count == 0 ? default : Geometry.Round(Geometry.Union(letters.Select(letter => Geometry.Bounds(letter.BoundingBox))), options.Decimals);
                var marked = new MarkedText(text, words.Count > 0 && words.All(word => word.IsBold), words.Count > 0 && words.All(word => word.IsItalic), box);
                texts[element.MarkedContentIdentifier] = texts.TryGetValue(element.MarkedContentIdentifier, out var earlier) && earlier.Text.Length > 0
                    ? new MarkedText(
                        text.Length > 0 ? earlier.Text + " " + text : earlier.Text,
                        earlier.IsBold && marked.IsBold,
                        earlier.IsItalic && marked.IsItalic,
                        earlier.Box == default ? box : box == default ? earlier.Box : Geometry.Union([earlier.Box, box]))
                    : marked;
                continue; // nested sequences are part of this MCID's content (ISO 32000 doesn't allow nested MCIDs)
            }

            for (var i = element.Children.Count - 1; i >= 0; i--)
            {
                pending.Push(element.Children[i]);
            }
        }

        return texts;
    }

    // Horizontal words, recovered from gaps like the layout; none for rotated text.
    private static List<LayoutWord> Words(List<Letter> letters, LayoutOptions options) =>
        letters.All(letter => letter.TextOrientation == TextOrientation.Horizontal)
            ? DefaultWordExtractor.Instance.GetWords(letters).Select(word => WordFactory.Create(word, options)).OfType<LayoutWord>().ToList()
            : [];

    // Horizontal text: the words joined. Rotated text: content order is the reading order within one sequence, and the
    // word extractor would scramble it.
    private static string Text(List<Letter> letters, List<LayoutWord> words, LayoutOptions options) =>
        letters.All(letter => letter.TextOrientation == TextOrientation.Horizontal)
            ? string.Join(" ", words.Select(word => word.Text))
            : Whitespace.Replace(WordFactory.Normalize(string.Concat(letters.Select(letter => letter.Value)), options), " ").Trim();

    // A sequence's letters in content order (its own, then those of nested sequences), leaving out nested
    // artifacts. Iterative, so deep nesting can't overflow the stack.
    private static List<Letter> Letters(MarkedContentElement root)
    {
        var letters = new List<Letter>();
        var pending = new Stack<MarkedContentElement>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var element = pending.Pop();
            if (element.IsArtifact)
            {
                continue;
            }

            letters.AddRange(element.Letters);
            for (var i = element.Children.Count - 1; i >= 0; i--)
            {
                pending.Push(element.Children[i]);
            }
        }

        return letters;
    }
}
