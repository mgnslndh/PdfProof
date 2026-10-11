using UglyToad.PdfPig.Content;

namespace PdfProof.Layout;

/// <summary>
/// A word as a layout strategy sees it: normalized text, geometry and style. Words are never empty.
/// </summary>
public sealed class LayoutWord
{
    internal LayoutWord(string text, LayoutBox box, double baseline, double size, string? font, string color, bool isBold, bool isItalic, Word source)
    {
        Text = text;
        Box = box;
        Baseline = baseline;
        Size = size;
        Font = font;
        Color = color;
        IsBold = isBold;
        IsItalic = isItalic;
        Source = source;
    }

    /// <summary>The word's text after normalization and replacements.</summary>
    public string Text { get; }

    /// <summary>The axis-aligned box around the word, not rounded.</summary>
    public LayoutBox Box { get; }

    /// <summary>The y coordinate of the word's baseline at its first letter.</summary>
    public double Baseline { get; }

    /// <summary>The largest font size among the word's letters, in points, not rounded.</summary>
    public double Size { get; }

    /// <summary>The font name without its subset prefix.</summary>
    public string? Font { get; }

    /// <summary>The fill color of the first letter as <c>#RRGGBB</c>.</summary>
    public string Color { get; }

    /// <summary>Bold, from the font's flags or, failing that, its name.</summary>
    public bool IsBold { get; }

    /// <summary>Italic or oblique, from the font's flags or, failing that, its name.</summary>
    public bool IsItalic { get; }

    internal Word Source { get; }
}
