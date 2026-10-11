using Microsoft.Extensions.Logging;

namespace PdfProof;

/// <summary>
/// How to load one PDF: which profile to read it with and where to log. Producer-specific settings live in
/// <see cref="PdfProfile"/>; these options only choose one.
/// </summary>
public sealed record PdfProofOptions
{
    /// <summary>No profile (it is detected), no logging.</summary>
    public static PdfProofOptions Default { get; } = new();

    /// <summary>
    /// The profile to read the PDF with, exactly as given. <see langword="null"/> (the default) detects it from the
    /// PDF's <c>/Producer</c> and <c>/Creator</c> entries.
    /// </summary>
    public PdfProfile? Profile { get; init; }

    /// <summary>Receives every diagnostic entry as it is recorded; <see langword="null"/> for no logging.</summary>
    public ILogger? Logger { get; init; }
}
