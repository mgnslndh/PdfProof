namespace PdfProof.Semantics;

/// <summary>Linked text and the block it is in, as listed by <see cref="SemanticModel.Links"/>.</summary>
public sealed class SemanticLinkedText
{
    internal SemanticLinkedText(SemanticBlock block, string text, SemanticLink link)
    {
        Block = block;
        Text = text;
        Link = link;
    }

    /// <summary>The block the linked text is in.</summary>
    public SemanticBlock Block { get; }

    /// <summary>The linked text.</summary>
    public string Text { get; }

    /// <summary>Where the link goes.</summary>
    public SemanticLink Link { get; }
}
