namespace PdfProof;

/// <summary>One decision or warning recorded while loading a PDF.</summary>
public sealed class PdfDiagnostic
{
    internal PdfDiagnostic(string code, int? page, string message)
    {
        Code = code;
        Page = page;
        Message = message;
    }

    /// <summary>What kind of entry this is; one of <see cref="PdfDiagnosticCodes"/>.</summary>
    public string Code { get; }

    /// <summary>The page the entry is about, or <see langword="null"/> for the whole document.</summary>
    public int? Page { get; }

    /// <summary>The decision or warning, in plain words.</summary>
    public string Message { get; }

    /// <summary>The entry as one line: <c>Code: Message</c> or <c>Code (page n): Message</c>.</summary>
    public override string ToString() =>
        Page is { } page ? $"{Code} (page {page}): {Message}" : $"{Code}: {Message}";
}
