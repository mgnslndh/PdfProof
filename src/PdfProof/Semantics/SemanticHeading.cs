using PdfProof.Layout;

namespace PdfProof.Semantics;

/// <summary>A heading.</summary>
public sealed class SemanticHeading : SemanticBlock
{
    internal SemanticHeading(int page, SemanticSource source, int level, IReadOnlyList<SemanticRun> runs, LayoutBox box)
        : base(page, source, SemanticText.Join(runs), box)
    {
        Level = level;
        Runs = ReadOnly.List(runs);
    }

    /// <summary>The level, 1 for the top level. Inferred levels come from font sizes, so they can differ between producers.</summary>
    public int Level { get; }

    /// <summary>The heading's text as runs.</summary>
    public IReadOnlyList<SemanticRun> Runs { get; }
}
