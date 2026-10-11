namespace PdfProof.Testing;

/// <summary>
/// Thrown when a PDF assertion fails. The message states the expectation, what was found instead, and an excerpt
/// of the document.
/// </summary>
public sealed class PdfAssertionException : Exception, IAssertionException
{
    /// <summary>Creates the exception with a complete failure message.</summary>
    /// <param name="message">The failure message, shown as is by the test runner.</param>
    public PdfAssertionException(string message)
        : base(message)
    {
    }
}
