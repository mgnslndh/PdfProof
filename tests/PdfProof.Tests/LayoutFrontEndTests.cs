using System.Globalization;
using PdfProof.Semantics;

namespace PdfProof.Tests;

public sealed class LayoutFrontEndTests
{
    // One layout block holding a heading, two one-line paragraphs and a two-row table, as MigraDoc writes it.
    internal static byte[] OneBlockInvoice()
    {
        var pdf = new RawPdf();
        pdf.AddPage(
            RawPdf.Text(70, 760, "Invoice", 20, "F2") +
            RawPdf.Text(70, 750, "Questions? Visit our support site or e-mail us.", 10) +
            RawPdf.Text(70, 738, "Read the terms and conditions", 10) +
            RawPdf.Text(70, 726, "Item", 10, "F2") + RawPdf.Text(250, 726, "Qty", 10, "F2") + RawPdf.Text(400, 726, "Amount", 10, "F2") +
            RawPdf.Text(70, 714, "Consulting, senior", 10) + RawPdf.Text(250, 714, "10", 10) + RawPdf.Text(400, 714, "14 500,00 SEK", 10));
        return pdf.Build();
    }

    [Fact]
    public void A_layout_block_is_split_where_size_boldness_cells_or_a_short_line_change()
    {
        var doc = PdfProofDocument.Load(OneBlockInvoice());

        Assert.Single(doc.Layout.Pages[0].Blocks); // the premise: one layout block
        Assert.Equal(
            ["Invoice", "Questions? Visit our support site or e-mail us.", "Read the terms and conditions", "Item | Qty | Amount\nConsulting, senior | 10 | 14 500,00 SEK"],
            doc.Semantics.Blocks.Select(b => b.Text));
        Assert.Equal(
            [
                "LayoutFrontEnd (page 1): block 1: split before \"Questions? Visit our support s...\": size 20 → 10",
                "LayoutFrontEnd (page 1): block 1: split before \"Read the terms and conditions\": a short line ends a paragraph",
                "LayoutFrontEnd (page 1): block 1: split before \"Item Qty Amount\": cells 1 → 3",
                "LayoutFrontEnd (page 1): block 1: split before \"Consulting, senior 10 14 500,0...\": bold → regular",
            ],
            doc.Semantics.Decisions.Where(d => d.Pass == SemanticPass.LayoutFrontEnd).Select(d => d.ToString()));
    }

    [Fact]
    public void Unclassified_candidates_become_paragraphs_with_runs_from_the_spans()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 750, "Plain", 12) + RawPdf.Text(85, 750, "bold", 12, "F2") + RawPdf.Text(115, 750, "words", 12)));

        var paragraph = Assert.IsType<SemanticParagraph>(Assert.Single(doc.Semantics.Blocks));
        Assert.Equal("Plain bold words", paragraph.Text);
        Assert.Equal([("Plain ", false), ("bold", true), (" words", false)], paragraph.Runs.Select(r => (r.Text, r.IsBold)));
        Assert.Equal((1, SemanticSource.Inferred), (paragraph.Page, paragraph.Source));
    }

    [Fact]
    public void A_gap_larger_than_the_paragraph_gap_factor_splits_a_block()
    {
        var profile = PdfProfile.Default with { Semantics = SemanticOptions.Default with { ParagraphGapFactor = 0.1 } };
        var text = RawPdf.Text(50, 750, "A first line that runs on for quite a while here", 10) + RawPdf.Text(50, 738, "A second line that runs on for quite a while here", 10);

        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(text), new PdfProofOptions { Profile = profile });

        Assert.Equal(2, doc.Semantics.Blocks.Count);
        Assert.Contains(doc.Semantics.Decisions, d => d.Message.StartsWith("block 1: split before \"A second line", StringComparison.Ordinal) && d.Message.Contains(": gap "));
    }

    [Fact]
    public void An_empty_document_gives_an_empty_model()
    {
        // Review Focus 1.
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(""));

        Assert.Empty(doc.Semantics.Blocks);
        Assert.Empty(doc.Semantics.Artifacts);
        Assert.Equal(SemanticSource.Inferred, doc.Semantics.Source);
        Assert.Contains(doc.Diagnostics.Entries, e => e.ToString() == "Semantics: inferred, 0 blocks, 0 artifacts");
    }

    [Fact]
    public void The_semantic_summary_is_a_diagnostic_and_decisions_are_logged()
    {
        var logger = new ListLogger();

        var doc = PdfProofDocument.Load(OneBlockInvoice(), new PdfProofOptions { Logger = logger });

        Assert.Contains(doc.Diagnostics.Entries, e => e.ToString() == "Semantics: inferred, 4 blocks, 0 artifacts");
        Assert.Contains(logger.Entries, e => e.Level == Microsoft.Extensions.Logging.LogLevel.Debug && e.Message.StartsWith("LayoutFrontEnd (page 1): block 1: split before", StringComparison.Ordinal));
    }
}
