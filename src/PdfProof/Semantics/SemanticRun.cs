namespace PdfProof.Semantics;

/// <summary>A piece of inline text with one style and at most one link.</summary>
public sealed class SemanticRun
{
    internal SemanticRun(string text, bool isBold, bool isItalic, SemanticLink? link)
    {
        Text = text;
        IsBold = isBold;
        IsItalic = isItalic;
        Link = link;
    }

    /// <summary>The run's text, spaces included: a block's text is its runs joined.</summary>
    public string Text { get; }

    /// <summary>The text is bold.</summary>
    public bool IsBold { get; }

    /// <summary>The text is italic.</summary>
    public bool IsItalic { get; }

    /// <summary>The link the text belongs to, or <see langword="null"/>.</summary>
    public SemanticLink? Link { get; }
}
