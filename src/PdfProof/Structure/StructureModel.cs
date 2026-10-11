namespace PdfProof.Structure;

/// <summary>
/// The logical structure tree of a Tagged PDF (PDF/UA): the producer's declared headings, paragraphs, tables, lists,
/// links and figures. Never <see langword="null"/>: an untagged PDF has an empty <see cref="Root"/>.
/// </summary>
public sealed class StructureModel
{
    internal StructureModel(StructureNode root)
    {
        Root = root;
        IsTagged = root.Children.Count > 0;
    }

    internal static StructureModel Untagged { get; } =
        new(new StructureNode("StructTreeRoot", null, null, null, null, null, null, 1, 1, []));

    /// <summary>
    /// <see langword="true"/> when the document has a structure tree with at least one element (not counting
    /// artifacts). <c>/MarkInfo</c> is not used; when it disagrees, <see cref="PdfProofDocument.Diagnostics"/>
    /// notes it.
    /// </summary>
    public bool IsTagged { get; }

    /// <summary>The tree's root, role <c>StructTreeRoot</c>; its children are the top-level elements.</summary>
    public StructureNode Root { get; }
}
