using System.Collections.ObjectModel;

namespace PdfProof.Layout;

/// <summary>
/// How text is extracted and grouped into the layout model. Start from <see cref="Default"/> and adjust with
/// <c>with</c>.
/// </summary>
public sealed record LayoutOptions
{
    // Declared first: static initializers run in order, and Default (below) needs it.
    private static readonly IReadOnlyDictionary<string, string> NoReplacements =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

    /// <summary>The default layout options.</summary>
    public static LayoutOptions Default { get; } = new();

    /// <summary>How horizontal text is grouped into blocks and ordered. Default <see cref="LayoutStrategies.Rows"/>.</summary>
    public ILayoutStrategy Strategy
    {
        get => _strategy;
        init => _strategy = Throw.IfNull(value, nameof(Strategy));
    }

    /// <summary>
    /// Leave out text hidden by clipping paths, and images drawn entirely outside the page. Essential for
    /// HTML-to-PDF converters such as SelectPdf, which draw the whole document on every page and clip each page to
    /// its slice. Default on; turn it off only to diagnose.
    /// </summary>
    public bool ApplyClipping { get; init; } = true;

    /// <summary>Decimals kept for coordinates and font sizes; absorbs floating-point noise. Default 1.</summary>
    public int Decimals
    {
        get => _decimals;
        init => _decimals = Throw.IfOutOfRange(value, 0, 15, nameof(Decimals));
    }

    /// <summary>
    /// Words on the same baseline further apart than this many font sizes start a new line (a column or table
    /// cell). Default 1.0.
    /// </summary>
    public double ColumnGapFactor
    {
        get => _columnGapFactor;
        init => _columnGapFactor = Factor(value, nameof(ColumnGapFactor));
    }

    /// <summary>
    /// <see cref="LayoutStrategies.Rows"/>: a vertical gap larger than this many font sizes (of the smaller row)
    /// starts a new block. Default 0.8.
    /// </summary>
    public double BlockGapFactor
    {
        get => _blockGapFactor;
        init => _blockGapFactor = Factor(value, nameof(BlockGapFactor));
    }

    /// <summary>
    /// Normalize producer-specific characters: soft and Unicode hyphens become <c>-</c>, special spaces a space,
    /// ligatures are expanded and zero-width characters removed. Default on.
    /// </summary>
    public bool NormalizeText { get; init; } = true;

    /// <summary>
    /// Extra text replacements applied to every word (and to link text, which is built from words) after
    /// normalization, in ordinal key order. Replacements apply within single words, so a key must not be empty or
    /// contain whitespace. The dictionary is copied when set. Default none.
    /// </summary>
    public IReadOnlyDictionary<string, string> TextReplacements
    {
        get => _textReplacements;
        init => _textReplacements = Replacements(value);
    }

    private readonly ILayoutStrategy _strategy = LayoutStrategies.Rows;
    private readonly int _decimals = 1;
    private readonly double _columnGapFactor = 1.0;
    private readonly double _blockGapFactor = 0.8;
    private readonly IReadOnlyDictionary<string, string> _textReplacements = NoReplacements;

    private static double Factor(double value, string name)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
        {
            Throw.ArgumentOutOfRangeException(name, value, "The factor must be a finite number of zero or more.");
        }

        return value;
    }

    // A sorted, read-only copy: later changes to the caller's dictionary don't affect the options.
    private static IReadOnlyDictionary<string, string> Replacements(IReadOnlyDictionary<string, string> value)
    {
        Throw.IfNull(value, nameof(TextReplacements));
        var copy = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in value)
        {
            if (string.IsNullOrEmpty(entry.Key))
            {
                Throw.ArgumentException(nameof(TextReplacements), "A replacement key must not be empty.");
            }

            if (entry.Key.Any(char.IsWhiteSpace))
            {
                Throw.ArgumentException(nameof(TextReplacements), "Replacements apply to single words; a key must not contain whitespace.");
            }

            copy[entry.Key] = entry.Value ?? "";
        }

        return new ReadOnlyDictionary<string, string>(copy);
    }
}
