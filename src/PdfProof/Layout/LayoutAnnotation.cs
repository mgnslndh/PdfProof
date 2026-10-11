namespace PdfProof.Layout;

/// <summary>An annotation other than a link or a form widget, e.g. a note or a highlight.</summary>
public sealed class LayoutAnnotation
{
    internal LayoutAnnotation(string type, string? content, LayoutBox box)
    {
        Type = type;
        Content = content;
        Box = box;
    }

    /// <summary>The annotation subtype, e.g. <c>Text</c> or <c>Highlight</c>.</summary>
    public string Type { get; }

    /// <summary>The annotation's <c>/Contents</c>.</summary>
    public string? Content { get; }

    /// <summary>The annotation rectangle.</summary>
    public LayoutBox Box { get; }
}
