using PdfProof.Layout;

namespace PdfProof.Semantics;

/// <summary>A figure (an image or drawing) declared by tags.</summary>
public sealed class SemanticFigure : SemanticBlock
{
    internal SemanticFigure(int page, SemanticSource source, string? alt, LayoutBox box)
        : base(page, source, alt ?? "", box)
    {
        Alt = alt;
    }

    /// <summary>The figure's alternative text, or <see langword="null"/> when it has none.</summary>
    public string? Alt { get; }
}
