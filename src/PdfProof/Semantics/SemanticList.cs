using PdfProof.Layout;

namespace PdfProof.Semantics;

/// <summary>A bulleted or numbered list.</summary>
public sealed class SemanticList : SemanticBlock
{
    internal SemanticList(int page, SemanticSource source, IReadOnlyList<SemanticListItem> items, LayoutBox box)
        : base(page, source, string.Join("\n", items.Select(item => item.Text)), box)
    {
        Items = ReadOnly.List(items);
    }

    /// <summary>The items, in order.</summary>
    public IReadOnlyList<SemanticListItem> Items { get; }
}
