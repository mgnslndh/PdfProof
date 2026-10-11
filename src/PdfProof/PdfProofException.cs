namespace PdfProof;

/// <summary>
/// Thrown when a PDF can't be loaded: the input isn't a PDF, it is corrupt, or it is encrypted and no valid password
/// was given. The inner exception, when present, is the underlying parser's error.
/// </summary>
public sealed class PdfProofException : Exception
{
    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong, in terms of the PDF.</param>
    public PdfProofException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the underlying error.</summary>
    /// <param name="message">What went wrong, in terms of the PDF.</param>
    /// <param name="innerException">The underlying error, usually from the PDF parser.</param>
    public PdfProofException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
