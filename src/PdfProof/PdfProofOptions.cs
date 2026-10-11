using Microsoft.Extensions.Logging;
using PdfProof.Layout;

namespace PdfProof;

/// <summary>
/// Options for loading a PDF. Options are complete values: what you pass is exactly what is used. Profiles are
/// presets (<see cref="ForProfile"/>) that you adjust with <c>with</c>.
/// </summary>
public sealed record PdfProofOptions
{
    /// <summary>The default options: profile <see cref="PdfProfile.Default"/>, default layout, no logger.</summary>
    public static PdfProofOptions Default { get; } = new();

    /// <summary>The profile these options were made for. Recorded in <see cref="PdfProofDocument.Diagnostics"/>.</summary>
    public PdfProfile Profile { get; init; } = PdfProfile.Default;

    /// <summary>How text is extracted and grouped into the layout model.</summary>
    public LayoutOptions Layout
    {
        get => _layout;
        init => _layout = Throw.IfNull(value, nameof(Layout));
    }

    /// <summary>Receives every diagnostic entry as it is recorded; <see langword="null"/> for no logging.</summary>
    public ILogger? Logger { get; init; }

    /// <summary>Complete options for a producer profile.</summary>
    /// <param name="profile">The producer profile.</param>
    public static PdfProofOptions ForProfile(PdfProfile profile) => new() { Profile = profile };

    private readonly LayoutOptions _layout = LayoutOptions.Default;
}
