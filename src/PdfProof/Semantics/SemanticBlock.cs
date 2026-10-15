using PdfProof.Layout;

namespace PdfProof.Semantics;

/// <summary>A body block in reading order: a heading, paragraph, table, list or figure. Artifacts are not blocks.</summary>
public abstract class SemanticBlock
{
    private protected SemanticBlock(int page, SemanticSource source, string text, LayoutBox box)
    {
        Page = page;
        Source = source;
        Text = text;
        Box = box;
    }

    /// <summary>The page the block starts on.</summary>
    public int Page { get; }

    /// <summary>Whether the block's kind was declared by tags or inferred from the layout.</summary>
    public SemanticSource Source { get; }

    /// <summary>
    /// The block's text: runs joined for headings and paragraphs; for a table, cells joined by <c>" | "</c> and rows
    /// by <c>\n</c>; for a list, items joined by <c>\n</c>; for a figure, its alternative text or empty.
    /// </summary>
    public string Text { get; }

    // Where the block is, for spatial queries and geometry export. Default (all zero) when the block comes from tags.
    internal LayoutBox Box { get; }
}
