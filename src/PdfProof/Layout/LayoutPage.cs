namespace PdfProof.Layout;

/// <summary>One page of the layout model.</summary>
public sealed class LayoutPage
{
    internal LayoutPage(int number, double width, double height, int rotation, IReadOnlyList<LayoutBlock> blocks, IReadOnlyList<LayoutLink> links, IReadOnlyList<LayoutImage> images, IReadOnlyList<LayoutAnnotation> annotations)
    {
        Number = number;
        Width = width;
        Height = height;
        Rotation = rotation;
        Blocks = ReadOnly.List(blocks);
        Lines = ReadOnly.List(blocks.SelectMany(block => block.Lines));
        Text = string.Join("\n", Lines.Select(line => line.Text));
        Links = ReadOnly.List(links);
        Images = ReadOnly.List(images);
        Annotations = ReadOnly.List(annotations);
    }

    /// <summary>The page number, starting at 1.</summary>
    public int Number { get; }

    /// <summary>The page width in points.</summary>
    public double Width { get; }

    /// <summary>The page height in points.</summary>
    public double Height { get; }

    /// <summary>The page's <c>/Rotate</c> value in degrees (0, 90, 180 or 270).</summary>
    public int Rotation { get; }

    /// <summary>The page's blocks: horizontal text in layout order, then rotated text.</summary>
    public IReadOnlyList<LayoutBlock> Blocks { get; }

    /// <summary>All lines of all blocks, in block order.</summary>
    public IReadOnlyList<LayoutLine> Lines { get; }

    /// <summary>All line texts, joined by <c>\n</c>.</summary>
    public string Text { get; }

    /// <summary>The page's links, top to bottom, then left to right.</summary>
    public IReadOnlyList<LayoutLink> Links { get; }

    /// <summary>The page's images, top to bottom, then left to right.</summary>
    public IReadOnlyList<LayoutImage> Images { get; }

    /// <summary>The page's other annotations (not links or form widgets), top to bottom, then left to right.</summary>
    public IReadOnlyList<LayoutAnnotation> Annotations { get; }
}
