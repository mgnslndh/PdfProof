using PdfProof.Layout;

namespace PdfProof.Structure;

/// <summary>A reference from a structure element to an annotation (an OBJR), e.g. a link or a form widget.</summary>
public sealed class StructureObjectReference : StructureContent
{
    internal StructureObjectReference(string type, int? page, LayoutLink? link)
        : base(page)
    {
        Type = type;
        Link = link;
    }

    /// <summary>The annotation's subtype, e.g. <c>Link</c> or <c>Widget</c>; <c>Unknown</c> when it has none.</summary>
    public string Type { get; }

    /// <summary>
    /// For a link annotation: the same link as in <see cref="LayoutPage.Links"/> on its page (matched by rectangle),
    /// with its text and target. Otherwise <see langword="null"/>.
    /// </summary>
    public LayoutLink? Link { get; }
}
