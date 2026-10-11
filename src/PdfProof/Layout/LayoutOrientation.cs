namespace PdfProof.Layout;

/// <summary>The direction a block's text runs in.</summary>
public enum LayoutOrientation
{
    /// <summary>Normal left-to-right text.</summary>
    Horizontal,

    /// <summary>Text rotated 90 degrees.</summary>
    Rotate90,

    /// <summary>Upside-down text.</summary>
    Rotate180,

    /// <summary>Text rotated 270 degrees (on real documents this is often text reading bottom to top).</summary>
    Rotate270,

    /// <summary>Text at another angle.</summary>
    Other,
}
