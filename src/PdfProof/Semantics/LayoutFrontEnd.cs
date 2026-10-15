using PdfProof.Layout;

namespace PdfProof.Semantics;

// Untagged documents (spec §6, row 0a): each layout block becomes one or more candidates. Lines on one baseline form a
// row of cells. A block is split where font size, whole-line boldness or cell structure changes, where the gap between
// rows is larger than ParagraphGapFactor font sizes, or after a short line (a one-line paragraph followed by another):
// a line ending before ShortLineRatio of its own block's width, so a ragged paragraph isn't measured against a wider
// block elsewhere on the page.
internal static class LayoutFrontEnd
{
    public static List<Candidate> Build(SemanticContext context)
    {
        var candidates = new List<Candidate>();
        foreach (var page in context.Layout.Pages)
        {
            for (var b = 0; b < page.Blocks.Count; b++)
            {
                var block = page.Blocks[b];
                var rows = Rows(block.Lines);
                var current = new List<CandidateLine>();
                foreach (var row in rows)
                {
                    if (current.Count > 0 && SplitReason(current[current.Count - 1], row, block.Box.Left, block.Box.Right, context.Options) is { } reason)
                    {
                        candidates.Add(new Candidate(page.Number, current, declared: false));
                        context.Decide(SemanticPass.LayoutFrontEnd, page.Number, null, $"block {b + 1}: split before \"{SemanticText.Excerpt(row.Text)}\": {reason}");
                        current = [];
                    }

                    current.Add(row);
                }

                if (current.Count > 0)
                {
                    candidates.Add(new Candidate(page.Number, current, declared: false));
                }
            }
        }

        return candidates;
    }

    // Lines whose vertical centres are within half a line height of each other form one row; cells left to right.
    public static List<CandidateLine> Rows(IReadOnlyList<LayoutLine> lines)
    {
        var rows = new List<List<LayoutLine>>();
        foreach (var line in lines)
        {
            var row = rows.Count > 0 ? rows[rows.Count - 1] : null;
            if (row is not null && SameRow(row[0], line))
            {
                row.Add(line);
            }
            else
            {
                rows.Add([line]);
            }
        }

        return rows.Select(Row).ToList();
    }

    private static bool SameRow(LayoutLine a, LayoutLine b) =>
        Math.Abs((a.Box.Top + a.Box.Bottom) / 2 - (b.Box.Top + b.Box.Bottom) / 2) <= Math.Min(a.Box.Height, b.Box.Height) / 2;

    private static CandidateLine Row(List<LayoutLine> lines)
    {
        var ordered = lines.OrderBy(line => line.Box.Left).ToList();
        var cells = ordered.Select(line => new CandidateCell(Runs(line), line.Box)).ToList();
        var spans = ordered.SelectMany(line => line.Spans).ToList();
        var size = spans.GroupBy(span => span.Size).OrderByDescending(group => group.Sum(span => span.Text.Length)).ThenByDescending(group => group.Key).First().Key;
        return new CandidateLine(cells, Geometry.Union(ordered.Select(line => line.Box)), size, spans.All(span => span.IsBold));
    }

    private static IReadOnlyList<SemanticRun> Runs(LayoutLine line) =>
        SemanticText.Concat(line.Spans.Select(span => (IReadOnlyList<SemanticRun>)[new SemanticRun(span.Text, span.IsBold, span.IsItalic, null)]));

    private static string? SplitReason(CandidateLine previous, CandidateLine next, double blockLeft, double textRight, SemanticOptions options)
    {
        if ((previous.Cells.Count > 1) != (next.Cells.Count > 1))
        {
            return $"cells {previous.Cells.Count} → {next.Cells.Count}";
        }

        if (Math.Abs(previous.Size - next.Size) > 0.5)
        {
            return $"size {SemanticText.Number(previous.Size)} → {SemanticText.Number(next.Size)}";
        }

        if (previous.IsBold != next.IsBold)
        {
            return previous.IsBold ? "bold → regular" : "regular → bold";
        }

        var gap = previous.Box.Bottom - next.Box.Top;
        if (gap > options.ParagraphGapFactor * Math.Min(previous.Size, next.Size))
        {
            return $"gap {SemanticText.Number(gap)} pt";
        }

        if (previous.Cells.Count == 1 && previous.Box.Right < blockLeft + options.ShortLineRatio * (textRight - blockLeft))
        {
            return "a short line ends a paragraph";
        }

        return null;
    }
}
