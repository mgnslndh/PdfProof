namespace PdfProof.Layout;

/// <summary>A bookmark (outline item) with its children.</summary>
public sealed class LayoutBookmark
{
    internal LayoutBookmark(string title, int? pageNumber, string? uri, IReadOnlyList<LayoutBookmark> children)
    {
        Title = title;
        PageNumber = pageNumber;
        Uri = uri;
        Children = ReadOnly.List(children);
    }

    /// <summary>The bookmark's title.</summary>
    public string Title { get; }

    /// <summary>The page the bookmark points to, for document bookmarks.</summary>
    public int? PageNumber { get; }

    /// <summary>The URI the bookmark points to, for URI bookmarks.</summary>
    public string? Uri { get; }

    /// <summary>Nested bookmarks, in order.</summary>
    public IReadOnlyList<LayoutBookmark> Children { get; }
}
