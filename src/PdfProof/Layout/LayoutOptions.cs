namespace PdfProof.Layout;

/// <summary>
/// How text is extracted and grouped into the layout model. Start from <see cref="Default"/> and adjust with
/// <c>with</c>.
/// </summary>
public sealed record LayoutOptions
{
    /// <summary>The default layout options.</summary>
    public static LayoutOptions Default { get; } = new();
}
