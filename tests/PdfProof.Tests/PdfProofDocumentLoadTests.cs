using System.Globalization;

namespace PdfProof.Tests;

public sealed class PdfProofDocumentLoadTests
{
    [Fact]
    public void Load_reads_the_page_count_and_the_info_dictionary()
    {
        var doc = PdfProofDocument.Load(TestPdfs.Create(pages: 2, title: "Invoice", author: "Accounts"));

        Assert.Equal(2, doc.Metadata.PageCount);
        Assert.Equal("Invoice", doc.Metadata.Title);
        Assert.Equal("Accounts", doc.Metadata.Author);
        Assert.Equal("PdfPig", doc.Metadata.Producer);
    }

    [Fact]
    public void Pdf_version_uses_a_dot_in_every_culture()
    {
        var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("sv-SE");
        try
        {
            var doc = PdfProofDocument.Load(TestPdfs.Create());

            // PdfPig's writer produces PDF 1.7; sv-SE would format the number as "1,7".
            Assert.Equal("1.7", doc.Metadata.PdfVersion);
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous;
        }
    }

    [Fact]
    public void A_pdf_without_an_info_dictionary_loads_with_null_entries()
    {
        var doc = PdfProofDocument.Load(TestPdfs.Create(includeInfo: false));

        Assert.Equal(1, doc.Metadata.PageCount);
        Assert.Null(doc.Metadata.Title);
        Assert.Null(doc.Metadata.Author);
        Assert.Null(doc.Metadata.CreationDate);
    }

    [Fact]
    public void Load_reads_a_non_seekable_stream()
    {
        using var stream = new NonSeekableStream(TestPdfs.Create(pages: 3));

        var doc = PdfProofDocument.Load(stream);

        Assert.Equal(3, doc.Metadata.PageCount);
    }

    [Fact]
    public void Load_reads_from_the_current_position_and_leaves_the_stream_open()
    {
        var pdf = TestPdfs.Create(title: "Offset");
        using var stream = new MemoryStream();
        stream.Write([0x42, 0x42, 0x42], 0, 3);
        stream.Write(pdf, 0, pdf.Length);
        stream.Position = 3;

        var doc = PdfProofDocument.Load(stream);

        Assert.Equal("Offset", doc.Metadata.Title);
        Assert.True(stream.CanRead, "Load must not dispose the caller's stream.");
    }

    [Fact]
    public void Load_reads_a_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pdfproof-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, TestPdfs.Create(title: "From disk"));
        try
        {
            Assert.Equal("From disk", PdfProofDocument.Load(path).Metadata.Title);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Bytes_that_are_not_a_pdf_throw_PdfProofException()
    {
        var notAPdf = "<html><body>Not a PDF</body></html>"u8.ToArray();

        var ex = Assert.Throws<PdfProofException>(() => PdfProofDocument.Load(notAPdf));

        Assert.StartsWith("The input could not be read as a PDF", ex.Message);
        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public void Empty_input_throws_PdfProofException()
    {
        var ex = Assert.Throws<PdfProofException>(() => PdfProofDocument.Load(Array.Empty<byte>()));

        Assert.StartsWith("The input could not be read as a PDF", ex.Message);
    }

    [Fact]
    public void A_missing_file_throws_FileNotFoundException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.pdf");

        var ex = Assert.Throws<FileNotFoundException>(() => PdfProofDocument.Load(path));

        Assert.Equal(path, ex.FileName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_path_throws_ArgumentException(string path)
    {
        var ex = Assert.Throws<ArgumentException>(() => PdfProofDocument.Load(path));

        Assert.Equal("path", ex.ParamName);
    }

    [Fact]
    public void A_directory_path_throws_ArgumentException_that_says_so()
    {
        var ex = Assert.Throws<ArgumentException>(() => PdfProofDocument.Load(Path.GetTempPath()));

        Assert.Equal("path", ex.ParamName);
        Assert.StartsWith("The path is a directory, not a PDF file.", ex.Message);
    }

    [Fact]
    public void Null_input_throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PdfProofDocument.Load((byte[])null!));
        Assert.Throws<ArgumentNullException>(() => PdfProofDocument.Load((Stream)null!));
        Assert.Throws<ArgumentNullException>(() => PdfProofDocument.Load((string)null!));
    }

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }
}
