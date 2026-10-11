namespace PdfProof.Layout;

/// <summary>
/// How text is extracted and grouped into the layout model. Start from <see cref="Default"/> and adjust with
/// <c>with</c>.
/// </summary>
public sealed record LayoutOptions
{
    /// <summary>The default layout options.</summary>
    public static LayoutOptions Default { get; } = new();

    /// <summary>Decimals kept for coordinates and font sizes; absorbs floating-point noise. Default 1.</summary>
    public int Decimals { get; init; } = 1;

    /// <summary>
    /// Words on the same baseline further apart than this many font sizes start a new line (a column or table
    /// cell). Default 1.0.
    /// </summary>
    public double ColumnGapFactor { get; init; } = 1.0;

    /// <summary>
    /// <see cref="LayoutStrategies.Rows"/>: a vertical gap larger than this many font sizes (of the smaller row)
    /// starts a new block. Default 0.8.
    /// </summary>
    public double BlockGapFactor { get; init; } = 0.8;

    /// <summary>
    /// Normalize producer-specific characters: soft and Unicode hyphens become <c>-</c>, special spaces a space,
    /// ligatures are expanded and zero-width characters removed. Default on.
    /// </summary>
    public bool NormalizeText { get; init; } = true;

    /// <summary>
    /// Extra text replacements applied to every word after normalization, in ordinal key order. Default none.
    /// </summary>
    public IReadOnlyDictionary<string, string> TextReplacements { get; init; } = new Dictionary<string, string>();
}
