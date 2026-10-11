using Microsoft.Extensions.Logging;

namespace PdfProof;

// Collects diagnostic entries while loading, and logs each one as it is recorded.
internal sealed class DiagnosticsCollector(ILogger? logger)
{
    private readonly List<PdfDiagnostic> _entries = [];

    public void Add(string code, int? page, string message)
    {
        var entry = new PdfDiagnostic(code, page, message);
        _entries.Add(entry);
        logger?.Log(IsWarning(code) ? LogLevel.Warning : LogLevel.Debug, "{Diagnostic}", entry);
    }

    private static bool IsWarning(string code) =>
        code is PdfDiagnosticCodes.NoTextLayer or PdfDiagnosticCodes.BookmarksUnreadable or PdfDiagnosticCodes.StructureUnreadable;

    public PdfDiagnostics ToDiagnostics(PdfProfile profile, bool profileDetected) =>
        new(profile, profileDetected, _entries.ToArray());
}
