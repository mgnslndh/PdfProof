namespace PdfProof.Layout;

/// <summary>The geometry of a PDF: its pages, with text grouped into blocks, lines and spans.</summary>
public sealed class LayoutModel
{
    internal LayoutModel(IReadOnlyList<LayoutPage> pages)
    {
        Pages = pages;
    }

    /// <summary>The pages, in order.</summary>
    public IReadOnlyList<LayoutPage> Pages { get; }
}
