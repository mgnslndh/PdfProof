namespace PdfProof.Structure;

/// <summary>
/// One item of a structure element's content, in document order: a child element (<see cref="StructureNode"/>), a
/// run of marked text (<see cref="StructureText"/>) or a reference to an annotation
/// (<see cref="StructureObjectReference"/>).
/// </summary>
public abstract class StructureContent
{
    private protected StructureContent(int? page)
    {
        Page = page;
    }

    /// <summary>The page the content is on, when known.</summary>
    public int? Page { get; }
}
