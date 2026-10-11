namespace PdfProof;

// Runs a call into PdfPig. Any exception from the parser means the input is not a readable PDF, so it becomes a
// PdfProofException (naming the page when known). PdfProof's own code runs outside these calls, so a bug in PdfProof
// surfaces as itself instead of being reported as a bad PDF.
internal static class PdfPigCall
{
    public static T Read<T>(Func<T> read, int? page = null)
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not PdfProofException)
        {
            Throw.UnreadablePdf(ex, page);
            throw; // unreachable: UnreadablePdf always throws
        }
    }
}
