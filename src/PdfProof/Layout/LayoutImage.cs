namespace PdfProof.Layout;

/// <summary>An image drawn on the page.</summary>
public sealed class LayoutImage
{
    internal LayoutImage(LayoutBox box, int pixelWidth, int pixelHeight, string sha256)
    {
        Box = box;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        Sha256 = sha256;
    }

    /// <summary>Where the image is drawn.</summary>
    public LayoutBox Box { get; }

    /// <summary>The image's width in pixels.</summary>
    public int PixelWidth { get; }

    /// <summary>The image's height in pixels.</summary>
    public int PixelHeight { get; }

    /// <summary>The first 16 lowercase hex digits of the SHA-256 of the image's raw data.</summary>
    public string Sha256 { get; }
}
