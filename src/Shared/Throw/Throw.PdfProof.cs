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

    /// <summary>
    /// Throws a <see cref="PdfProofException"/> for an encrypted PDF that couldn't be opened: no password, or one that
    /// is neither the user nor the owner password. The password itself is never part of the message.
    /// </summary>
    /// <param name="parserError">The parser's exception; kept as the inner exception.</param>
    /// <param name="passwordGiven">Whether the options held a password.</param>
    [DoesNotReturn]
    public static void EncryptedPdf(Exception parserError, bool passwordGiven) =>
        throw new PdfProofException(
            passwordGiven
                ? "The PDF is encrypted, and the password in the options is neither its user password nor its owner password."
                : "The PDF is encrypted and needs a password. Pass it in the options: new PdfProofOptions { Password = \"...\" }.",
            parserError);
}
