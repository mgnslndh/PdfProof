using PdfProof.Layout;

namespace PdfProof.Semantics;

// Spec §6, pass 4 (untagged documents). Consecutive candidates on one page made only of rows with two or more cells
// are a table when there are MinRows rows and at least two columns hold a cell in every row. Columns are the merged
// horizontal extents of all cells (ColumnTolerance apart), so left-, right- and centre-aligned cells line up; two cells
// of one row in the same column means the rows don't align. The first row is the header when its boldness differs
// from the second row's.
internal static class TableDetector
{
    public static void Run(SemanticContext context)
    {
        var options = context.Options.Tables;
        var candidates = context.Candidates;
        for (var i = 0; i < candidates.Count; i++)
        {
            if (!IsRows(candidates[i]))
            {
                continue;
            }

            var j = i;
            var rows = new List<CandidateLine>();
            while (j < candidates.Count && IsRows(candidates[j]) && candidates[j].Page == candidates[i].Page)
            {
                rows.AddRange(candidates[j].Lines);
                j++;
            }

            var (aligned, reason) = rows.Count >= options.MinRows ? Align(rows, options.ColumnTolerance) : (null, null);
            if (aligned is not null)
            {
                var header = rows[0].IsBold != rows[1].IsBold ? 1 : 0;
                var table = new Candidate(candidates[i].Page, aligned, declared: false) { Kind = CandidateKind.Table, HeaderRows = header };
                candidates.RemoveRange(i, j - i);
                candidates.Insert(i, table);
                context.Decide(
                    SemanticPass.TableDetection,
                    table.Page,
                    table,
                    $"{rows.Count} rows × {aligned[0].Cells.Count} columns" + (header == 1 ? ", header row by style" : ""));
            }
            else
            {
                if (reason is not null)
                {
                    context.Decide(SemanticPass.TableDetection, candidates[i].Page, null, $"{rows.Count} rows of cells are not a table: {reason}");
                }

                i = j - 1;
            }
        }
    }

    private static bool IsRows(Candidate candidate) =>
        candidate.Kind == CandidateKind.Unknown && candidate.Lines.Count > 0 && candidate.Lines.All(line => line.Cells.Count >= 2);

    // The rows with one cell per column, or why they don't line up.
    private static (List<CandidateLine>? Aligned, string? Reason) Align(List<CandidateLine> rows, double tolerance)
    {
        var columns = new List<(double Left, double Right)>();
        foreach (var cell in rows.SelectMany(row => row.Cells).OrderBy(cell => cell.Box.Left))
        {
            if (columns.Count > 0 && cell.Box.Left <= columns[columns.Count - 1].Right + tolerance)
            {
                var last = columns[columns.Count - 1];
                columns[columns.Count - 1] = (last.Left, Math.Max(last.Right, cell.Box.Right));
            }
            else
            {
                columns.Add((cell.Box.Left, cell.Box.Right));
            }
        }

        var aligned = new List<CandidateLine>();
        var filled = new int[columns.Count];
        foreach (var row in rows)
        {
            var cells = new CandidateCell?[columns.Count];
            foreach (var cell in row.Cells)
            {
                var column = columns.FindIndex(c => cell.Box.Left >= c.Left - tolerance && cell.Box.Right <= c.Right + tolerance);
                if (column < 0 || cells[column] is not null)
                {
                    return (null, "two cells of one row fall in the same column");
                }

                cells[column] = cell;
                filled[column]++;
            }

            aligned.Add(row with { Cells = cells.Select(cell => cell ?? new CandidateCell([], default)).ToList() });
        }

        return filled.Count(count => count == rows.Count) >= 2 ? (aligned, null) : (null, "fewer than two columns have a cell in every row");
    }
}
