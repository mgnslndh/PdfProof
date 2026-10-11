namespace PdfProof.Layout;

internal static class LineFactory
{
    // Consecutive words with the same font, size, color and style merge into one span.
    public static LayoutLine Line(IReadOnlyList<LayoutWord> words, LayoutOptions options)
    {
        var spans = new List<LayoutSpan>();
        foreach (var word in words)
        {
            var size = Geometry.Round(word.Size, options.Decimals);
            if (spans.Count > 0)
            {
                var last = spans[spans.Count - 1];
                if (last.Font == word.Font && last.Size == size && last.Color == word.Color && last.IsBold == word.IsBold && last.IsItalic == word.IsItalic)
                {
                    spans[spans.Count - 1] = new LayoutSpan(last.Text + " " + word.Text, last.Font, size, last.Color, last.IsBold, last.IsItalic);
                    continue;
                }
            }

            spans.Add(new LayoutSpan(word.Text, word.Font, size, word.Color, word.IsBold, word.IsItalic));
        }

        var box = Geometry.Round(Geometry.Union(words.Select(word => word.Box)), options.Decimals);
        return new LayoutLine(string.Join(" ", spans.Select(span => span.Text)), box, spans);
    }

    public static LayoutBlock Block(IReadOnlyList<LayoutLine> lines, LayoutOrientation orientation) =>
        new(Geometry.Union(lines.Select(line => line.Box)), lines, orientation);
}
