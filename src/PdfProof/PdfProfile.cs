using PdfProof.Layout;

namespace PdfProof;

/// <summary>
/// How to read PDFs from one producer: a named, complete set of settings. Use a preset such as
/// <see cref="SelectPdf"/> or <see cref="Typst"/>, adjusted with <c>with</c> when needed, or create your own. Choose a
/// profile with <see cref="PdfProofOptions.Profile"/>; without one, <see cref="PdfProofDocument"/> detects it from the
/// PDF's <c>/Producer</c> and <c>/Creator</c> entries. Settings are never merged: a document is read with exactly the
/// settings of one profile.
/// </summary>
public sealed record PdfProfile
{
    /// <summary>Creates a profile with default settings.</summary>
    /// <param name="name">The profile's name, shown in diagnostics.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or whitespace.</exception>
    public PdfProfile(string name)
    {
        Name = Throw.IfNullOrWhitespace(name);
    }

    /// <summary>No producer-specific settings. Used when no other profile is detected.</summary>
    public static PdfProfile Default { get; } = new("Default");

    /// <summary>SelectPdf (HTML to PDF). Needs clipping and text normalization, which are on by default.</summary>
    public static PdfProfile SelectPdf { get; } = new("SelectPdf");

    /// <summary>
    /// QuestPDF. QuestPDF writes no <c>/Producer</c> or <c>/Creator</c> entry, so this profile is never detected:
    /// choose it with <see cref="PdfProofOptions.Profile"/>.
    /// </summary>
    public static PdfProfile QuestPdf { get; } = new("QuestPdf");

    /// <summary>Typst. Detected from the <c>/Creator</c> entry, which is the only one Typst writes.</summary>
    public static PdfProfile Typst { get; } = new("Typst");

    /// <summary>Chromium print-to-PDF (Chrome, Edge, Playwright, Puppeteer).</summary>
    public static PdfProfile Chromium { get; } = new("Chromium");

    // Declared after the presets: static initializers run in order.
    internal static IReadOnlyList<PdfProfile> Presets { get; } = ReadOnly.List([Default, SelectPdf, QuestPdf, Typst, Chromium]);

    /// <summary>The profile's name, shown in diagnostics. A preset adjusted with <c>with</c> keeps its name.</summary>
    public string Name
    {
        get => _name;
        init => _name = Throw.IfNullOrWhitespace(value, nameof(Name));
    }

    /// <summary>How text is extracted and grouped into the layout model.</summary>
    public LayoutOptions Layout
    {
        get => _layout;
        init => _layout = Throw.IfNull(value, nameof(Layout));
    }

    /// <summary>The profile's name.</summary>
    public override string ToString() => Name;

    private readonly string _name = "";
    private readonly LayoutOptions _layout = LayoutOptions.Default;
}
