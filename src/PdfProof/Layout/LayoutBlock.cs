namespace PdfProof.Layout;

/// <summary>Lines read together: a paragraph, a table, a column fragment, or a run of rotated text.</summary>
public sealed class LayoutBlock
{
    internal LayoutBlock(LayoutBox box, IReadOnlyList<LayoutLine> lines, LayoutOrientation orientation)
    {
        Box = box;
        Lines = ReadOnly.List(lines);
        Orientation = orientation;
    }

    /// <summary>The box around the block's lines.</summary>
    public LayoutBox Box { get; }

    /// <summary>The block's lines, in reading order.</summary>
    public IReadOnlyList<LayoutLine> Lines { get; }

    /// <summary>The direction the block's text runs in.</summary>
    public LayoutOrientation Orientation { get; }
}
