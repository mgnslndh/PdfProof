using System.Text;
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

    /// <summary>
    /// The user or owner password of an encrypted PDF; <see langword="null"/> (the default) for none. A password for
    /// a PDF that isn't encrypted is ignored. The password is never written to diagnostics, logs or
    /// <see cref="ToString"/>.
    /// </summary>
    public string? Password { get; init; }

    /// <summary>Receives every diagnostic entry as it is recorded; <see langword="null"/> for no logging.</summary>
    public ILogger? Logger { get; init; }

    // Shapes the record's ToString: the password is masked, so options can be logged safely.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Profile = ").Append(Profile?.Name ?? "(detect)");
        builder.Append(", Password = ").Append(Password is null ? "(none)" : "***");
        builder.Append(", Logger = ").Append(Logger is null ? "(none)" : Logger.GetType().Name);
        return true;
    }
}
