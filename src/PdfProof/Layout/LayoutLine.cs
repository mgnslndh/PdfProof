namespace PdfProof.Layout;

/// <summary>Words on one baseline, up to a column gap.</summary>
public sealed class LayoutLine
{
    internal LayoutLine(string text, LayoutBox box, IReadOnlyList<LayoutSpan> spans)
    {
        Text = text;
        Box = box;
        Spans = ReadOnly.List(spans);
    }

    /// <summary>The line's text: its spans joined by single spaces.</summary>
    public string Text { get; }

    /// <summary>The axis-aligned box around the line's words.</summary>
    public LayoutBox Box { get; }

    /// <summary>The line's spans, left to right (in reading direction for rotated text).</summary>
    public IReadOnlyList<LayoutSpan> Spans { get; }
}
