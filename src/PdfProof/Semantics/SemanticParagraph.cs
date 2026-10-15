using PdfProof.Layout;

namespace PdfProof.Semantics;

/// <summary>A paragraph.</summary>
public sealed class SemanticParagraph : SemanticBlock
{
    internal SemanticParagraph(int page, SemanticSource source, IReadOnlyList<SemanticRun> runs, LayoutBox box)
        : base(page, source, SemanticText.Join(runs), box)
    {
        Runs = ReadOnly.List(runs);
    }

    /// <summary>The paragraph's text as runs.</summary>
    public IReadOnlyList<SemanticRun> Runs { get; }
}
