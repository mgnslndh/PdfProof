using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace PdfProof.Layout;

// Rotated text is grouped in its own frame. The frame comes from the baselines (start to end of each word), not from
// the orientation label or the boxes: on real PDFs PdfPig labels bottom-to-top text Rotate270, and rotated boxes
// have Bottom > Top. Works for any angle. Each run becomes a one-line block.
internal static class RotatedText
{
    public static IEnumerable<LayoutBlock> Blocks(IReadOnlyList<LayoutWord> words, LayoutOptions options)
    {
        foreach (var group in words.GroupBy(word => word.Source.TextOrientation).OrderBy(group => group.Key))
        {
            var (dx, dy) = Direction(group);
            double Along(PdfPoint point) => (point.X * dx) + (point.Y * dy);
            double Across(PdfPoint point) => (point.Y * dx) - (point.X * dy);
            var orientation = ToOrientation(group.Key);

            var rows = new List<List<LayoutWord>>();
            foreach (var word in group.OrderByDescending(w => Across(Start(w))).ThenBy(w => Along(Start(w))))
            {
                var row = rows.Count > 0 ? rows[rows.Count - 1] : null;
                if (row is not null && Math.Abs(Across(Start(row[0])) - Across(Start(word))) <= Math.Max(1.0, 0.3 * word.Size))
                {
                    row.Add(word);
                }
                else
                {
                    rows.Add([word]);
                }
            }

            foreach (var row in rows)
            {
                var ordered = row.OrderBy(w => Along(Start(w))).ToList();
                var run = new List<LayoutWord> { ordered[0] };
                foreach (var word in ordered.Skip(1))
                {
                    if (Along(Start(word)) - Along(End(run[run.Count - 1])) > options.ColumnGapFactor * word.Size)
                    {
                        yield return LineFactory.Block([LineFactory.Line(run, options)], orientation);
                        run = [];
                    }

                    run.Add(word);
                }

                yield return LineFactory.Block([LineFactory.Line(run, options)], orientation);
            }
        }
    }

    private static PdfPoint Start(LayoutWord word) => word.Source.Letters[0].StartBaseLine;

    private static PdfPoint End(LayoutWord word) => word.Source.Letters[word.Source.Letters.Count - 1].EndBaseLine;

    // The average reading direction of a group of words, as a unit vector.
    private static (double Dx, double Dy) Direction(IEnumerable<LayoutWord> words)
    {
        double x = 0;
        double y = 0;
        foreach (var word in words)
        {
            x += End(word).X - Start(word).X;
            y += End(word).Y - Start(word).Y;
        }

        var length = Math.Sqrt((x * x) + (y * y));
        return length == 0 ? (1, 0) : (x / length, y / length);
    }

    private static LayoutOrientation ToOrientation(TextOrientation orientation) => orientation switch
    {
        TextOrientation.Horizontal => LayoutOrientation.Horizontal,
        TextOrientation.Rotate90 => LayoutOrientation.Rotate90,
        TextOrientation.Rotate180 => LayoutOrientation.Rotate180,
        TextOrientation.Rotate270 => LayoutOrientation.Rotate270,
        _ => LayoutOrientation.Other,
    };
}
