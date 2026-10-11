namespace PdfProof;

/// <summary>What PdfProof decided while loading a PDF: the profile used and every recorded entry, in order.</summary>
public sealed class PdfDiagnostics
{
    internal PdfDiagnostics(PdfProfile profile, bool profileDetected, IReadOnlyList<PdfDiagnostic> entries)
    {
        Profile = profile;
        ProfileDetected = profileDetected;
        Entries = entries;
    }

    /// <summary>The profile the document was loaded with.</summary>
    public PdfProfile Profile { get; }

    /// <summary><see langword="true"/> when the profile was detected from the producer, not given in options.</summary>
    public bool ProfileDetected { get; }

    /// <summary>Every decision and warning, in the order they were recorded.</summary>
    public IReadOnlyList<PdfDiagnostic> Entries { get; }
}
