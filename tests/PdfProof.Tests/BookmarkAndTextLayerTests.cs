using Microsoft.Extensions.Logging;

namespace PdfProof.Tests;

public sealed class BookmarkAndTextLayerTests
{
    [Fact]
    public void Bookmarks_are_read_as_a_tree_with_their_target_pages()
    {
        var pdf = new RawPdf();
        var first = pdf.AddPage(RawPdf.Text(50, 750, "Invoice"));
        var second = pdf.AddPage(RawPdf.Text(50, 750, "Terms"));
        var child = pdf.Reserve();
        var top = pdf.Reserve();
        var outlines = pdf.Reserve();
        pdf.Set(child, $"<< /Title (Terms and conditions) /Parent {top} 0 R /Dest [{second} 0 R /Fit] >>");
        pdf.Set(top, $"<< /Title (Invoice) /Parent {outlines} 0 R /First {child} 0 R /Last {child} 0 R /Count 1 /Dest [{first} 0 R /Fit] >>");
        pdf.Set(outlines, $"<< /Type /Outlines /First {top} 0 R /Last {top} 0 R /Count 2 >>");
        pdf.CatalogEntries = $"/Outlines {outlines} 0 R";

        var bookmark = Assert.Single(PdfProofDocument.Load(pdf.Build()).Layout.Bookmarks);

        Assert.Equal(("Invoice", (int?)1), (bookmark.Title, bookmark.PageNumber));
        var nested = Assert.Single(bookmark.Children);
        Assert.Equal(("Terms and conditions", (int?)2), (nested.Title, nested.PageNumber));
        Assert.Null(nested.Uri);
    }

    [Fact]
    public void A_pdf_without_bookmarks_has_none()
    {
        Assert.Empty(PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 750, "Hello"))).Layout.Bookmarks);
    }

    [Fact]
    public void A_pdf_with_no_text_on_any_page_loads_and_reports_NoTextLayer()
    {
        // Review Focus 2: like a scan, the page has only an image.
        var logger = new ListLogger();
        var pdf = new RawPdf();
        pdf.AddPage("q 400 0 0 600 100 100 cm /Im1 Do Q\n");
        pdf.AddPage("");

        var doc = PdfProofDocument.Load(pdf.Build(), PdfProofOptions.Default with { Logger = logger });

        Assert.All(doc.Layout.Pages, page => Assert.Empty(page.Blocks));
        var entry = Assert.Single(doc.Diagnostics.Entries, e => e.Code == PdfDiagnosticCodes.NoTextLayer);
        Assert.Equal("No page has extractable text; the PDF may be a scan. OCR is not supported.", entry.Message);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.StartsWith("NoTextLayer", StringComparison.Ordinal));
    }

    [Fact]
    public void A_pdf_with_text_on_some_page_has_no_NoTextLayer_entry()
    {
        var pdf = new RawPdf();
        pdf.AddPage("");
        pdf.AddPage(RawPdf.Text(50, 750, "Text on page 2"));

        Assert.DoesNotContain(PdfProofDocument.Load(pdf.Build()).Diagnostics.Entries, e => e.Code == PdfDiagnosticCodes.NoTextLayer);
    }
}
