namespace PdfProof.Testing;

/// <summary>
/// Marks exceptions that represent a failed assertion rather than an error. xUnit v3 reports any exception that
/// implements an interface with this name as an assertion failure, so PdfProof.Testing doesn't need to reference
/// xunit. Other frameworks fail the test on any exception.
/// </summary>
public interface IAssertionException
{
}
