namespace PdfProof.Tests;

public sealed class PdfPigCallTests
{
    [Fact]
    public void A_parser_error_becomes_PdfProofException_with_the_original_as_inner_exception()
    {
        var parserError = new InvalidOperationException("Unexpected token.");

        var ex = Assert.Throws<PdfProofException>(() => PdfPigCall.Read<int>(() => throw parserError));

        Assert.Equal("The input could not be read as a PDF: Unexpected token.", ex.Message);
        Assert.Same(parserError, ex.InnerException);
    }

    [Fact]
    public void The_page_number_is_named_when_known()
    {
        var ex = Assert.Throws<PdfProofException>(() =>
            PdfPigCall.Read<int>(() => throw new FormatException("Bad operator."), page: 3));

        Assert.Equal("The input could not be read as a PDF (page 3): Bad operator.", ex.Message);
    }

    [Fact]
    public void A_PdfProofException_is_not_wrapped_twice()
    {
        var original = new PdfProofException("Already explained.");

        var ex = Assert.Throws<PdfProofException>(() => PdfPigCall.Read<int>(() => throw original));

        Assert.Same(original, ex);
    }

    [Fact]
    public void Out_of_memory_is_not_wrapped()
    {
        Assert.Throws<OutOfMemoryException>(() => PdfPigCall.Read<int>(() => throw new OutOfMemoryException()));
    }

    [Fact(Skip = "Enabled in Task 5, when page content is read")]
    public void A_corrupt_page_content_stream_names_the_page()
    {
        // Page 2's /Contents points at an object that is not a stream: the parser fails when the page is read.
        var pdf = new RawPdf();
        pdf.AddPage(RawPdf.Text(50, 750, "Fine"));
        var broken = pdf.AddPage(RawPdf.Text(50, 750, "Broken"));
        var notAStream = pdf.Add("<< /Not (a stream) >>");
        var bytes = pdf.Build();
        var text = System.Text.Encoding.GetEncoding("ISO-8859-1").GetString(bytes);
        var fixedUp = text.Replace($"/Contents {broken + 1} 0 R", $"/Contents {notAStream} 0 R");

        var ex = Assert.Throws<PdfProofException>(() =>
            PdfProofDocument.Load(System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(fixedUp)));

        Assert.Contains("(page 2)", ex.Message);
    }
}
