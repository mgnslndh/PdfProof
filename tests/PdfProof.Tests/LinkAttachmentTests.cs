using PdfProof.Semantics;

namespace PdfProof.Tests;

public sealed class LinkAttachmentTests
{
    [Fact]
    public void A_link_becomes_a_run_on_the_text_under_it()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Text(50, 700, "Questions? Visit") + RawPdf.Text(145, 700, "our support site") + RawPdf.Text(236, 700, "or e-mail us."));
        pdf.Annotate(page, "<< /Type /Annot /Subtype /Link /Rect [143 695 230 715] /A << /S /URI /URI (https://example.com/support) >> >>");

        var doc = PdfProofDocument.Load(pdf.Build());

        var paragraph = Assert.IsType<SemanticParagraph>(Assert.Single(doc.Semantics.Blocks));
        Assert.Equal("Questions? Visit our support site or e-mail us.", paragraph.Text);
        Assert.Equal(
            [("Questions? Visit ", (string?)null), ("our support site", "https://example.com/support"), (" or e-mail us.", null)],
            paragraph.Runs.Select(r => (r.Text, r.Link?.Uri)));
        var linked = Assert.Single(doc.Semantics.Links);
        Assert.Equal(("our support site", "https://example.com/support"), (linked.Text, linked.Link.Uri));
        Assert.Same(paragraph, linked.Block);
        Assert.Contains(doc.Semantics.Decisions, d => d.ToString() == "LinkAttachment (page 1): \"our support site\" → https://example.com/support");
    }

    [Fact]
    public void An_internal_link_has_its_target_page()
    {
        var pdf = new RawPdf();
        var first = pdf.AddPage(RawPdf.Text(50, 700, "Read the terms and conditions"));
        var second = pdf.AddPage(RawPdf.Text(50, 750, "Terms"));
        pdf.Annotate(first, $"<< /Type /Annot /Subtype /Link /Rect [45 695 250 715] /A << /S /GoTo /D [{second} 0 R /Fit] >> >>");

        var doc = PdfProofDocument.Load(pdf.Build());

        var run = Assert.Single(doc.Semantics.Paragraphs[0].Runs);
        Assert.Equal(("Read the terms and conditions", (int?)2, (string?)null), (run.Text, run.Link?.TargetPage, run.Link?.Uri));
    }

    [Fact]
    public void Two_links_with_the_same_text_each_take_their_own_occurrence()
    {
        // Review Focus 2.
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Text(50, 700, "See terms") + RawPdf.Text(112, 700, "here") + RawPdf.Text(143, 700, "and pricing") + RawPdf.Text(209, 700, "here"));
        pdf.Annotate(page, "<< /Type /Annot /Subtype /Link /Rect [110 695 138 712] /A << /S /URI /URI (https://example.com/terms) >> >>");
        pdf.Annotate(page, "<< /Type /Annot /Subtype /Link /Rect [207 695 235 712] /A << /S /URI /URI (https://example.com/pricing) >> >>");

        var doc = PdfProofDocument.Load(pdf.Build());

        Assert.Equal(
            [("See terms ", (string?)null), ("here", "https://example.com/terms"), (" and pricing ", null), ("here", "https://example.com/pricing")],
            doc.Semantics.Paragraphs[0].Runs.Select(r => (r.Text, r.Link?.Uri)));
    }

    [Fact]
    public void A_link_in_a_table_cell_links_that_cell()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(
            RawPdf.Text(70, 726, "Item", 10, "F2") + RawPdf.Text(250, 726, "Qty", 10, "F2") +
            RawPdf.Text(70, 714, "Consulting, senior", 10) + RawPdf.Text(250, 714, "10", 10));
        pdf.Annotate(page, "<< /Type /Annot /Subtype /Link /Rect [65 710 160 724] /A << /S /URI /URI (https://example.com/rates) >> >>");

        var table = Assert.Single(PdfProofDocument.Load(pdf.Build()).Semantics.Tables);

        Assert.Equal("https://example.com/rates", Assert.Single(table.Rows[0].Cells[0].Runs).Link?.Uri);
        Assert.Null(table.Rows[0].Cells[1].Runs[0].Link);
    }

    [Fact]
    public void A_link_without_text_is_logged_and_left_out()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Text(50, 700, "Body text."));
        pdf.Annotate(page, "<< /Type /Annot /Subtype /Link /Rect [300 300 350 320] /A << /S /URI /URI (https://example.com) >> >>");

        var doc = PdfProofDocument.Load(pdf.Build());

        Assert.Empty(doc.Semantics.Links);
        Assert.Contains(doc.Semantics.Decisions, d => d.ToString() == "LinkAttachment (page 1): a link to https://example.com covers no text and is left out");
    }

    [Fact]
    public void The_pass_can_be_switched_off()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Text(50, 700, "Read the terms"));
        pdf.Annotate(page, "<< /Type /Annot /Subtype /Link /Rect [45 695 250 715] /A << /S /URI /URI (https://example.com) >> >>");
        var profile = PdfProfile.Default with { Semantics = SemanticOptions.Default with { Links = LinkAttachmentOptions.Default with { Enabled = false } } };

        var doc = PdfProofDocument.Load(pdf.Build(), new PdfProofOptions { Profile = profile });

        Assert.Empty(doc.Semantics.Links);
    }
}
