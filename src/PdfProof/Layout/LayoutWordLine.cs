namespace PdfProof.Layout;

/// <summary>A line as returned by a layout strategy: words in reading order.</summary>
public sealed class LayoutWordLine
{
    /// <summary>Creates a line from words in reading order.</summary>
    /// <param name="words">The line's words; must not be empty.</param>
    public LayoutWordLine(IReadOnlyList<LayoutWord> words)
    {
        Throw.IfNull(words);
        Words = words;
    }

    /// <summary>The line's words, in reading order.</summary>
    public IReadOnlyList<LayoutWord> Words { get; }
}
