namespace PdfProof.Layout;

/// <summary>A block as returned by a layout strategy: lines in reading order.</summary>
public sealed class LayoutWordBlock
{
    /// <summary>Creates a block from lines in reading order.</summary>
    /// <param name="lines">The block's lines; must not be empty.</param>
    public LayoutWordBlock(IReadOnlyList<LayoutWordLine> lines)
    {
        Throw.IfNull(lines);
        Lines = lines;
    }

    /// <summary>The block's lines, in reading order.</summary>
    public IReadOnlyList<LayoutWordLine> Lines { get; }
}
