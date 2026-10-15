namespace PdfProof.Semantics;

/// <summary>A table cell. An empty cell has no runs and empty text.</summary>
public sealed class SemanticCell
{
    internal SemanticCell(IReadOnlyList<SemanticRun> runs)
    {
        Runs = ReadOnly.List(runs);
        Text = SemanticText.Join(runs);
    }

    /// <summary>The cell's text.</summary>
    public string Text { get; }

    /// <summary>The cell's text as runs.</summary>
    public IReadOnlyList<SemanticRun> Runs { get; }
}
