namespace PdfProof.Layout;

// Rows by baseline from top to bottom, split into cells at column-sized gaps, and into blocks at paragraph-sized
// vertical gaps.
internal sealed class RowsLayoutStrategy : ILayoutStrategy
{
    public string Name => "Rows";

    public IReadOnlyList<LayoutWordBlock> Arrange(IReadOnlyList<LayoutWord> words, LayoutOptions options)
    {
        var blocks = new List<LayoutWordBlock>();
        var current = new List<LayoutWordLine>();
        List<LayoutWord>? previousRow = null;
        foreach (var row in Rows(words))
        {
            if (previousRow is not null)
            {
                var gap = current.SelectMany(line => line.Words).Min(word => word.Box.Bottom) - row.Max(word => word.Box.Top);
                if (gap > options.BlockGapFactor * Math.Min(MaxSize(row), MaxSize(previousRow)))
                {
                    blocks.Add(new LayoutWordBlock(current));
                    current = [];
                }
            }

            current.AddRange(Cells(row, options));
            previousRow = row;
        }

        if (current.Count > 0)
        {
            blocks.Add(new LayoutWordBlock(current));
        }

        return blocks;
    }

    // Words on (nearly) the same baseline, rows from top to bottom.
    private static List<List<LayoutWord>> Rows(IEnumerable<LayoutWord> words)
    {
        var rows = new List<List<LayoutWord>>();
        foreach (var word in words.OrderByDescending(w => w.Baseline).ThenBy(w => w.Box.Left))
        {
            var row = rows.Count > 0 ? rows[rows.Count - 1] : null;
            if (row is not null && Math.Abs(row[0].Baseline - word.Baseline) <= Math.Max(1.0, 0.3 * word.Size))
            {
                row.Add(word);
            }
            else
            {
                rows.Add([word]);
            }
        }

        return rows;
    }

    // A row split into cells where the horizontal gap is wider than ColumnGapFactor font sizes.
    private static IEnumerable<LayoutWordLine> Cells(List<LayoutWord> row, LayoutOptions options)
    {
        var ordered = row.OrderBy(word => word.Box.Left).ToList();
        var cell = new List<LayoutWord> { ordered[0] };
        for (var i = 1; i < ordered.Count; i++)
        {
            var previous = cell[cell.Count - 1];
            if (ordered[i].Box.Left - previous.Box.Right > options.ColumnGapFactor * Math.Max(ordered[i].Size, previous.Size))
            {
                yield return new LayoutWordLine(cell);
                cell = [];
            }

            cell.Add(ordered[i]);
        }

        yield return new LayoutWordLine(cell);
    }

    private static double MaxSize(List<LayoutWord> row) => row.Max(word => word.Size);
}
