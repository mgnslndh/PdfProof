using System.Text.RegularExpressions;

namespace PdfProof.Semantics;

/// <summary>
/// ArtifactDetection: text in the top or bottom band of the page that repeats at the same place on enough pages (page
/// numbers compared as <c>{n}</c> and <c>{N}</c>), or that matches a page-number pattern, is a header or footer, not
/// content.
/// </summary>
public sealed record ArtifactDetectionOptions
{
    /// <summary>The default options.</summary>
    public static ArtifactDetectionOptions Default { get; } = new();

    /// <summary>Run the pass. Default on.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>The height of the top and bottom bands, as a fraction of the page height. Default 0.1.</summary>
    public double BandRatio
    {
        get => _bandRatio;
        init => _bandRatio = Throw.IfOutOfRange(value, 0, 0.5, nameof(BandRatio));
    }

    /// <summary>The fraction of the pages a text must repeat on (and at least two pages). Default 0.5.</summary>
    public double MinPageFraction
    {
        get => _minPageFraction;
        init => _minPageFraction = Throw.IfOutOfRange(value, 0, 1, nameof(MinPageFraction));
    }

    /// <summary>
    /// Regular expressions (invariant culture) for a whole band line that is a page number, also on one-page
    /// documents. Default: <c>Page 3</c>, <c>Page 3 of 7</c> (also Swedish and German), <c>3 / 7</c>, <c>- 3 -</c>, <c>3</c>.
    /// </summary>
    public IReadOnlyList<string> PageNumberPatterns
    {
        get => _pageNumberPatterns;
        init => _pageNumberPatterns = Patterns(value);
    }

    private readonly double _bandRatio = 0.1;
    private readonly double _minPageFraction = 0.5;
    private readonly IReadOnlyList<string> _pageNumberPatterns = ReadOnly.List(
    [
        @"(?i)^(page|sida|seite)\s+\d+(\s+(of|av|von)\s+\d+)?$",
        @"^\d+\s*/\s*\d+$",
        @"^[-–]\s*\d+\s*[-–]$",
        @"^\d+$",
    ]);

    // A copy, each pattern checked once so a bad one fails here instead of while loading.
    private static IReadOnlyList<string> Patterns(IReadOnlyList<string> value)
    {
        Throw.IfNull(value, nameof(PageNumberPatterns));
        foreach (var pattern in value)
        {
            try
            {
                _ = new Regex(pattern, RegexOptions.CultureInvariant);
            }
            catch (ArgumentException ex)
            {
                Throw.ArgumentException(nameof(PageNumberPatterns), $"Not a valid regular expression: {pattern}", ex);
            }
        }

        return ReadOnly.List(value);
    }
}
