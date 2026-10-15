namespace PdfProof.Semantics;

/// <summary>A list item, without its bullet or number.</summary>
public sealed class SemanticListItem
{
    internal SemanticListItem(IReadOnlyList<SemanticRun> runs)
    {
        Runs = ReadOnly.List(runs);
        Text = SemanticText.Join(runs);
    }

    /// <summary>The item's text.</summary>
    public string Text { get; }

    /// <summary>The item's text as runs.</summary>
    public IReadOnlyList<SemanticRun> Runs { get; }
}
