using PdfProof.Layout;

namespace PdfProof.Semantics;

/// <summary>A table: an optional header row and the body rows.</summary>
public sealed class SemanticTable : SemanticBlock
{
    internal SemanticTable(int page, SemanticSource source, SemanticRow? header, IReadOnlyList<SemanticRow> rows, LayoutBox box)
        : base(page, source, Join(header, rows), box)
    {
        Header = header;
        Rows = ReadOnly.List(rows);
    }

    /// <summary>The header row, or <see langword="null"/> when the table has none.</summary>
    public SemanticRow? Header { get; }

    /// <summary>The body rows, top to bottom.</summary>
    public IReadOnlyList<SemanticRow> Rows { get; }

    private static string Join(SemanticRow? header, IReadOnlyList<SemanticRow> rows) =>
        string.Join("\n", (header is null ? rows : new[] { header }.Concat(rows)).Select(row => string.Join(" | ", row.Cells.Select(cell => cell.Text))));
}
