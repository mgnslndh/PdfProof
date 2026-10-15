using System.Text.RegularExpressions;

namespace PdfProof.Semantics;

/// <summary>
/// ListDetection (untagged documents): consecutive blocks at the same indentation that start with a bullet, a number,
/// or a small marker shape just left of the text (how HTML-to-PDF converters draw CSS bullets) are a list. The bullet
/// or number is left out of the item text.
/// </summary>
public sealed record ListDetectionOptions
{
    /// <summary>The default options.</summary>
    public static ListDetectionOptions Default { get; } = new();

    /// <summary>Run the pass. Default on.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Text bullets. A <c>-</c> followed by a digit is a minus sign, not a bullet. Default <c>• ◦ ‣ ▪ – * -</c> (<c>‣</c> is Typst's nested bullet).</summary>
    public IReadOnlyList<string> BulletGlyphs
    {
        get => _bulletGlyphs;
        init => _bulletGlyphs = Glyphs(value);
    }

    /// <summary>Regular expressions for item numbers at the start of a line. Default <c>1.</c>, <c>1)</c>, <c>a.</c>, <c>a)</c>, <c>iv.</c>.</summary>
    public IReadOnlyList<string> NumberingPatterns
    {
        get => _numberingPatterns;
        init => _numberingPatterns = Patterns(value);
    }

    /// <summary>Read small filled shapes just left of a line as bullets. Default on.</summary>
    public bool UseMarkerShapes { get; init; } = true;

    /// <summary>How far apart, in points, item starts may be and still belong to one list. Default 3.</summary>
    public double IndentTolerance
    {
        get => _indentTolerance;
        init => _indentTolerance = Throw.IfOutOfRange(value, 0, 100, nameof(IndentTolerance));
    }

    private readonly IReadOnlyList<string> _bulletGlyphs = ReadOnly.List(["•", "◦", "‣", "▪", "–", "*", "-"]);
    private readonly IReadOnlyList<string> _numberingPatterns = ReadOnly.List([@"^\d{1,3}[.)]", @"^[a-z][.)]", @"^(?i:[ivxlc]{1,6})[.)]"]);
    private readonly double _indentTolerance = 3;

    private static IReadOnlyList<string> Glyphs(IReadOnlyList<string> value)
    {
        Throw.IfNull(value, nameof(BulletGlyphs));
        if (value.Any(string.IsNullOrWhiteSpace))
        {
            Throw.ArgumentException(nameof(BulletGlyphs), "A bullet glyph must not be empty or whitespace.");
        }

        return ReadOnly.List(value);
    }

    private static IReadOnlyList<string> Patterns(IReadOnlyList<string> value)
    {
        Throw.IfNull(value, nameof(NumberingPatterns));
        foreach (var pattern in value)
        {
            try
            {
                _ = new Regex(pattern, RegexOptions.CultureInvariant);
            }
            catch (ArgumentException ex)
            {
                Throw.ArgumentException(nameof(NumberingPatterns), $"Not a valid regular expression: {pattern}", ex);
            }
        }

        return ReadOnly.List(value);
    }
}
