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
        // Iterative, so deeply nested marked content can't overflow the stack.
        var texts = new Dictionary<int, string>();
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
                var text = element.ActualText is { } actual ? Whitespace.Replace(WordFactory.Normalize(actual, options), " ").Trim() : Text(Letters(element), options);
                texts[element.MarkedContentIdentifier] = texts.TryGetValue(element.MarkedContentIdentifier, out var earlier) && earlier.Length > 0
                    ? (text.Length > 0 ? earlier + " " + text : earlier)
                    : text;
                continue; // nested sequences are part of this MCID's content (ISO 32000 doesn't allow nested MCIDs)
            }

            for (var i = element.Children.Count - 1; i >= 0; i--)
            {
                pending.Push(element.Children[i]);
            }
        }

        return texts;
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
