using System.Diagnostics.CodeAnalysis;

namespace PdfProof.Shared;

// PdfProof-specific throw helpers. BCL helpers live in Throw.cs, which is kept close to its upstream copy so it
// can be shared with other repositories.
internal static partial class Throw
{
    /// <summary>
    /// Throws a <see cref="PdfProofException"/> for input the PDF parser could not read, naming the page when known.
    /// </summary>
    /// <param name="parserError">The parser's exception; kept as the inner exception.</param>
    /// <param name="page">The page being read, when known.</param>
    [DoesNotReturn]
    public static void UnreadablePdf(Exception parserError, int? page = null)
    {
        var where = page is { } number ? $" (page {number})" : "";
        throw new PdfProofException($"The input could not be read as a PDF{where}: {parserError.Message}", parserError);
    }
}
