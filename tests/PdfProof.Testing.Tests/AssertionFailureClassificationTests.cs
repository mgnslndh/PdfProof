using Xunit.Sdk;

namespace PdfProof.Testing.Tests;

// Spec §8.7 / OQ-1: PdfProof.Testing must not depend on xunit, yet its failures must show up in xUnit v3 as
// assertion failures, not errors. xUnit v3 recognizes any exception implementing an interface named
// IAssertionException, whatever its namespace.
public sealed class AssertionFailureClassificationTests
{
    [Fact]
    public void Xunit_reports_PdfAssertionException_as_an_assertion_failure()
    {
        var metadata = ExceptionUtility.ExtractMetadata(new PdfAssertionException("Expected a heading \"Terms\"."));

        Assert.Equal(FailureCause.Assertion, metadata.Cause);
    }

    [Fact]
    public void Xunit_reports_an_ordinary_exception_as_an_error()
    {
        var metadata = ExceptionUtility.ExtractMetadata(new InvalidOperationException("Boom."));

        Assert.Equal(FailureCause.Exception, metadata.Cause);
    }

    [Fact]
    public void The_message_is_kept_as_written()
    {
        var ex = new PdfAssertionException("Expected a heading \"Terms\" on page 2.");

        Assert.Equal("Expected a heading \"Terms\" on page 2.", ex.Message);
    }
}
