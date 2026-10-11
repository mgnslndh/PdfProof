namespace PdfProof.Layout;

/// <summary>Consecutive words in a line that share font, size, color and style.</summary>
public sealed class LayoutSpan
{
    internal LayoutSpan(string text, string? font, double size, string color, bool isBold, bool isItalic)
    {
        Text = text;
        Font = font;
        Size = size;
        Color = color;
        IsBold = isBold;
        IsItalic = isItalic;
    }

    /// <summary>The span's words, joined by single spaces.</summary>
    public string Text { get; }

    /// <summary>The font name without its subset prefix (<c>ABCDEF+Lato-Bold</c> becomes <c>Lato-Bold</c>).</summary>
    public string? Font { get; }

    /// <summary>The font size in points, rounded to <see cref="LayoutOptions.Decimals"/>.</summary>
    public double Size { get; }

    /// <summary>The fill color as <c>#RRGGBB</c>.</summary>
    public string Color { get; }

    /// <summary>Bold, from the font's flags or, failing that, its name.</summary>
    public bool IsBold { get; }

    /// <summary>Italic or oblique, from the font's flags or, failing that, its name.</summary>
    public bool IsItalic { get; }
}
