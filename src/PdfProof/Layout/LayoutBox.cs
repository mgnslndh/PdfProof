namespace PdfProof.Layout;

/// <summary>A rectangle in PDF user space: points, origin at the bottom left of the page.</summary>
/// <param name="Left">The left edge.</param>
/// <param name="Bottom">The bottom edge.</param>
/// <param name="Right">The right edge.</param>
/// <param name="Top">The top edge.</param>
public readonly record struct LayoutBox(double Left, double Bottom, double Right, double Top)
{
    /// <summary>The width, <c>Right - Left</c>.</summary>
    public double Width => Right - Left;

    /// <summary>The height, <c>Top - Bottom</c>.</summary>
    public double Height => Top - Bottom;
}
