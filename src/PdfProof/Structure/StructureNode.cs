namespace PdfProof.Structure;

/// <summary>A structure element of a Tagged PDF, e.g. a heading, paragraph, table cell or link.</summary>
public sealed class StructureNode : StructureContent
{
    internal StructureNode(
        string role,
        string? customRole,
        int? page,
        string? alt,
        string? actualText,
        string? language,
        string? scope,
        int rowSpan,
        int columnSpan,
        IReadOnlyList<StructureContent> content)
        : base(page)
    {
        Role = role;
        CustomRole = customRole;
        Alt = alt;
        ActualText = actualText;
        Language = language;
        Scope = scope;
        RowSpan = rowSpan;
        ColumnSpan = columnSpan;
        Content = ReadOnly.List(content);
        Children = ReadOnly.List(content.OfType<StructureNode>());
        Text = actualText ?? string.Join(" ", content.Select(TextOf).Where(text => text.Length > 0));
    }

    /// <summary>
    /// The standard structure type after <c>/RoleMap</c>, e.g. <c>Document</c>, <c>H1</c>, <c>P</c>, <c>Table</c>,
    /// <c>TR</c>, <c>TH</c>, <c>TD</c>, <c>L</c>, <c>LI</c>, <c>Link</c> or <c>Figure</c>. The root is
    /// <c>StructTreeRoot</c>.
    /// </summary>
    public string Role { get; }

    /// <summary>The role as written in the PDF, when <c>/RoleMap</c> mapped it to another role; otherwise <see langword="null"/>.</summary>
    public string? CustomRole { get; }

    /// <summary>The alternative description (<c>/Alt</c>), e.g. of a figure.</summary>
    public string? Alt { get; }

    /// <summary>The replacement text (<c>/ActualText</c>). When set, it is also <see cref="Text"/>.</summary>
    public string? ActualText { get; }

    /// <summary>The element's language (<c>/Lang</c>), e.g. <c>sv-SE</c>.</summary>
    public string? Language { get; }

    /// <summary>For a table header cell: <c>Row</c>, <c>Column</c> or <c>Both</c> (table attribute <c>/Scope</c>).</summary>
    public string? Scope { get; }

    /// <summary>For a table cell: the number of rows it spans (table attribute <c>/RowSpan</c>); 1 by default.</summary>
    public int RowSpan { get; }

    /// <summary>For a table cell: the number of columns it spans (table attribute <c>/ColSpan</c>); 1 by default.</summary>
    public int ColumnSpan { get; }

    /// <summary>The element's content in document order: child elements, marked text and annotation references.</summary>
    public IReadOnlyList<StructureContent> Content { get; }

    /// <summary>The child elements, in document order.</summary>
    public IReadOnlyList<StructureNode> Children { get; }

    /// <summary>
    /// The element's text: <see cref="ActualText"/> when set, otherwise the text of its content, in order, joined
    /// by single spaces.
    /// </summary>
    public string Text { get; }

    /// <summary>All elements below this one, depth first in document order, not including this element.</summary>
    public IEnumerable<StructureNode> Descendants()
    {
        // An explicit stack keeps this linear in the number of elements, whatever the depth.
        var pending = new Stack<StructureNode>();
        for (var i = Children.Count - 1; i >= 0; i--)
        {
            pending.Push(Children[i]);
        }

        while (pending.Count > 0)
        {
            var node = pending.Pop();
            yield return node;
            for (var i = node.Children.Count - 1; i >= 0; i--)
            {
                pending.Push(node.Children[i]);
            }
        }
    }

    private static string TextOf(StructureContent content) => content switch
    {
        StructureText text => text.Text,
        StructureNode node => node.Text,
        _ => "",
    };
}
