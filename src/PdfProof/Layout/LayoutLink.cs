namespace PdfProof.Layout;

/// <summary>A link annotation. Normally exactly one of <see cref="Uri"/> and <see cref="TargetPage"/> is set.</summary>
public sealed class LayoutLink
{
    internal LayoutLink(string text, string? uri, int? targetPage, LayoutBox box, IReadOnlyList<double>? sourceRectangle)
    {
        SourceRectangle = sourceRectangle;
        Text = text;
        Uri = uri;
        TargetPage = targetPage;
        Box = box;
    }

    /// <summary>The text under the link: letters whose centre lies inside the link rectangle.</summary>
    public string Text { get; }

    /// <summary>The target of an external (URI) link.</summary>
    public string? Uri { get; }

    /// <summary>The target page of an internal (GoTo) link.</summary>
    public int? TargetPage { get; }

    /// <summary>The link rectangle.</summary>
    public LayoutBox Box { get; }

    // The annotation's /Rect exactly as written, before PdfPig applies the page's crop box and rotation. The structure
    // tree's annotation references are matched on it.
    internal IReadOnlyList<double>? SourceRectangle { get; }
}
