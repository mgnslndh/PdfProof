namespace PdfProof.Semantics;

/// <summary>A table row: one cell per column.</summary>
public sealed class SemanticRow
{
    internal SemanticRow(IReadOnlyList<SemanticCell> cells)
    {
        Cells = ReadOnly.List(cells);
    }

    /// <summary>The cells, left to right.</summary>
    public IReadOnlyList<SemanticCell> Cells { get; }
}
