namespace PdfProof.Layout;

/// <summary>The geometry of a PDF: its pages, with text grouped into blocks, lines and spans.</summary>
public sealed class LayoutModel
{
    internal LayoutModel(IReadOnlyList<LayoutPage> pages, IReadOnlyList<LayoutBookmark> bookmarks)
    {
        Pages = ReadOnly.List(pages);
        Bookmarks = ReadOnly.List(bookmarks);
    }

    /// <summary>The pages, in order.</summary>
    public IReadOnlyList<LayoutPage> Pages { get; }

    /// <summary>The document's bookmarks (outline), as a tree.</summary>
    public IReadOnlyList<LayoutBookmark> Bookmarks { get; }
}
