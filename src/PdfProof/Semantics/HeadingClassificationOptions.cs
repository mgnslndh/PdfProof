namespace PdfProof.Semantics;

/// <summary>
/// HeadingClassification (untagged documents): a short block clearly larger than the body text, or bold, short and
/// followed by body text, is a heading. Levels come from size clusters, the largest being level 1.
/// </summary>
public sealed record HeadingClassificationOptions
{
    /// <summary>The default options.</summary>
    public static HeadingClassificationOptions Default { get; } = new();

    /// <summary>Run the pass. Default on.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>How much larger than the body text a heading must be. Default 1.15.</summary>
    public double MinSizeRatio
    {
        get => _minSizeRatio;
        init => _minSizeRatio = Throw.IfOutOfRange(value, 1, 10, nameof(MinSizeRatio));
    }

    /// <summary>The most lines a heading can have. Default 2.</summary>
    public int MaxLines
    {
        get => _maxLines;
        init => _maxLines = Throw.IfOutOfRange(value, 1, 20, nameof(MaxLines));
    }

    /// <summary>A bold, short block at body size that is followed by body text is a heading. Default on.</summary>
    public bool BoldShortLineIsHeading { get; init; } = true;

    private readonly double _minSizeRatio = 1.15;
    private readonly int _maxLines = 2;
}
