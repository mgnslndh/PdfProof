using PdfProof.Semantics;

namespace PdfProof.Tests;

public sealed class StructureFrontEndTests
{
    [Fact]
    public void Tagged_headings_and_paragraphs_are_declared_and_their_sections_nest()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(
            RawPdf.Marked("H1", 0, RawPdf.Text(50, 750, "Invoice", 18)) + RawPdf.Marked("P", 1, RawPdf.Text(50, 700, "Hello")) +
            RawPdf.Marked("H2", 2, RawPdf.Text(50, 650, "Terms", 14)) + RawPdf.Marked("P", 3, RawPdf.Text(50, 600, "Pay soon.")));
        var kids = new[] { "H1", "P", "H2", "P" }.Select((role, mcid) => pdf.Add($"<< /S /{role} /Pg {page} 0 R /K {mcid} >>")).ToList();
        pdf.Tag($"{pdf.Add($"<< /S /Document /K [{string.Join(" ", kids.Select(k => $"{k} 0 R"))}] >>")} 0 R");

        var semantics = PdfProofDocument.Load(pdf.Build()).Semantics;

        Assert.Equal(SemanticSource.Declared, semantics.Source);
        Assert.All(semantics.Blocks, b => Assert.Equal(SemanticSource.Declared, b.Source));
        Assert.Equal([("Invoice", 1), ("Terms", 2)], semantics.Headings.Select(h => (h.Text, h.Level)));
        var section = Assert.Single(semantics.Sections);
        Assert.Equal("Invoice", section.Heading.Text);
        Assert.Equal(["Hello"], section.Blocks.Select(b => b.Text));
        Assert.Equal("Terms", section.Sections[0].Heading.Text);
        Assert.Equal(["Pay soon."], section.Sections[0].Blocks.Select(b => b.Text));
    }

    [Fact]
    public void Containers_and_wrappers_are_looked_through_and_a_Link_child_links_its_text()
    {
        // Chromium and Typst: P > NonStruct (text), Link (text + OBJR), NonStruct (text), inside Sect and Div.
        var pdf = new RawPdf();
        var page = pdf.AddPage(
            RawPdf.Marked("Span", 0, RawPdf.Text(50, 700, "Questions? Visit")) +
            RawPdf.Marked("Link", 1, RawPdf.Text(145, 700, "our support site")) +
            RawPdf.Marked("Span", 2, RawPdf.Text(236, 700, "or e-mail us.")));
        var annotation = pdf.Add("<< /Type /Annot /Subtype /Link /Rect [143 695 230 715] /A << /S /URI /URI (https://example.com/support) >> >>");
        pdf.Annotate(page, $"{annotation} 0 R");
        var before = pdf.Add($"<< /S /NonStruct /Pg {page} 0 R /K 0 >>");
        var link = pdf.Add($"<< /S /Link /Pg {page} 0 R /K [1 << /Type /OBJR /Obj {annotation} 0 R >>] >>");
        var after = pdf.Add($"<< /S /NonStruct /Pg {page} 0 R /K 2 >>");
        var paragraph = pdf.Add($"<< /S /P /Pg {page} 0 R /K [{before} 0 R {link} 0 R {after} 0 R] >>");
        var div = pdf.Add($"<< /S /Div /K [{paragraph} 0 R] >>");
        pdf.Tag($"{pdf.Add($"<< /S /Sect /K [{div} 0 R] >>")} 0 R");

        var block = Assert.IsType<SemanticParagraph>(Assert.Single(PdfProofDocument.Load(pdf.Build()).Semantics.Blocks));

        Assert.Equal(
            [("Questions? Visit ", (string?)null), ("our support site", "https://example.com/support"), (" or e-mail us.", null)],
            block.Runs.Select(r => (r.Text, r.Link?.Uri)));
    }

    [Fact]
    public void A_link_annotation_on_a_paragraph_is_placed_on_its_text()
    {
        // QuestPDF: P > [text, OBJR] with no Link element.
        var pdf = new RawPdf();
        var first = pdf.AddPage(RawPdf.Marked("P", 0, RawPdf.Text(50, 700, "Read the terms")));
        var second = pdf.AddPage(RawPdf.Text(50, 750, "Terms"));
        var annotation = pdf.Add($"<< /Type /Annot /Subtype /Link /Rect [45 715 140 695] /A << /S /GoTo /D [{second} 0 R /Fit] >> >>");
        pdf.Annotate(first, $"{annotation} 0 R");
        pdf.Tag($"{pdf.Add($"<< /S /P /Pg {first} 0 R /K [0 << /Type /OBJR /Obj {annotation} 0 R >>] >>")} 0 R");

        var semantics = PdfProofDocument.Load(pdf.Build()).Semantics;

        var run = Assert.Single(semantics.Paragraphs[0].Runs);
        Assert.Equal(("Read the terms", (int?)2), (run.Text, run.Link?.TargetPage));
    }

    [Fact]
    public void A_tagged_table_takes_its_header_from_THead_or_TH_and_folds_paragraphs_in_cells()
    {
        // QuestPDF writes Table > THead/TBody > TR > TH/TD > P; Chromium writes Table > TR > TH/TD.
        var pdf = new RawPdf();
        var page = pdf.AddPage(
            RawPdf.Marked("P", 0, RawPdf.Text(50, 700, "Item")) + RawPdf.Marked("P", 1, RawPdf.Text(200, 700, "Qty")) +
            RawPdf.Marked("P", 2, RawPdf.Text(50, 680, "Consulting, senior")) + RawPdf.Marked("P", 3, RawPdf.Text(200, 680, "10")));
        string Cell(string role, int mcid) => $"{pdf.Add($"<< /S /{role} /K [{pdf.Add($"<< /S /P /Pg {page} 0 R /K {mcid} >>")} 0 R] >>")} 0 R";
        var head = pdf.Add($"<< /S /THead /K [{pdf.Add($"<< /S /TR /K [{Cell("TH", 0)} {Cell("TH", 1)}] >>")} 0 R] >>");
        var body = pdf.Add($"<< /S /TBody /K [{pdf.Add($"<< /S /TR /K [{Cell("TD", 2)} {Cell("TD", 3)}] >>")} 0 R] >>");
        pdf.Tag($"{pdf.Add($"<< /S /Table /K [{head} 0 R {body} 0 R] >>")} 0 R");

        var table = Assert.Single(PdfProofDocument.Load(pdf.Build()).Semantics.Tables);

        Assert.Equal(["Item", "Qty"], table.Header!.Cells.Select(c => c.Text));
        Assert.Equal([["Consulting, senior", "10"]], table.Rows.Select(r => r.Cells.Select(c => c.Text).ToArray()));
        Assert.Equal(SemanticSource.Declared, table.Source);
    }

    [Fact]
    public void A_tagged_list_item_leaves_out_its_label()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("Lbl", 0, RawPdf.Text(50, 700, "•")) + RawPdf.Marked("LBody", 1, RawPdf.Text(60, 700, "Payment within 30 days.")));
        var label = pdf.Add($"<< /S /Lbl /Pg {page} 0 R /K 0 >>");
        var itemBody = pdf.Add($"<< /S /LBody /Pg {page} 0 R /K 1 >>");
        var item = pdf.Add($"<< /S /LI /K [{label} 0 R {itemBody} 0 R] >>");
        pdf.Tag($"{pdf.Add($"<< /S /L /K [{item} 0 R] >>")} 0 R");

        var list = Assert.Single(PdfProofDocument.Load(pdf.Build()).Semantics.Lists);

        Assert.Equal(["Payment within 30 days."], list.Items.Select(i => i.Text));
    }

    [Fact]
    public void A_tagged_figure_keeps_its_alternative_text()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("Figure", 0, "q 20 0 0 20 50 700 cm /Im1 Do Q\n"));
        pdf.Tag($"{pdf.Add($"<< /S /Figure /Pg {page} 0 R /Alt (Signature) /K 0 >>")} 0 R");

        var figure = Assert.Single(PdfProofDocument.Load(pdf.Build()).Semantics.Figures);

        Assert.Equal(("Signature", "Signature", 1), (figure.Alt, figure.Text, figure.Page));
    }

    [Fact]
    public void Bold_marked_content_gives_bold_runs()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("P", 0, RawPdf.Text(50, 700, "Important", 12, "F2")));
        pdf.Tag($"{pdf.Add($"<< /S /P /Pg {page} 0 R /K 0 >>")} 0 R");

        Assert.True(Assert.Single(PdfProofDocument.Load(pdf.Build()).Semantics.Paragraphs[0].Runs).IsBold);
    }

    [Fact]
    public void An_element_whose_marked_content_is_missing_gives_no_block_and_no_error()
    {
        // Review Focus 5: MCID 5 is not on the page.
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("P", 0, RawPdf.Text(50, 700, "Kept")));
        var kept = pdf.Add($"<< /S /P /Pg {page} 0 R /K 0 >>");
        var missing = pdf.Add($"<< /S /P /Pg {page} 0 R /K 5 >>");
        pdf.Tag($"{kept} 0 R {missing} 0 R");

        Assert.Equal(["Kept"], PdfProofDocument.Load(pdf.Build()).Semantics.Blocks.Select(b => b.Text));
    }

    [Fact]
    public void A_footer_left_out_of_the_tags_is_still_an_artifact()
    {
        // Chromium's footer template is drawn outside the structure tree.
        var pdf = new RawPdf();
        var pages = Enumerable.Range(1, 2).Select(n => pdf.AddPage(RawPdf.Marked("P", 0, RawPdf.Text(50, 700, $"Content {n}.")) + RawPdf.Text(270, 40, $"Page {n} of 2", 10))).ToList();
        pdf.Tag(string.Join(" ", pages.Select(p => $"{pdf.Add($"<< /S /P /Pg {p} 0 R /K 0 >>")} 0 R")));

        var semantics = PdfProofDocument.Load(pdf.Build()).Semantics;

        Assert.Equal("Page {n} of {N}", Assert.Single(semantics.Artifacts).Text);
        Assert.Equal(["Content 1.", "Content 2."], semantics.Blocks.Select(b => b.Text));
    }

    [Fact]
    public void The_layout_source_option_ignores_the_tags()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("H1", 0, RawPdf.Text(50, 750, "Invoice", 18)) + RawPdf.Marked("P", 1, RawPdf.Text(50, 700, "Hello there, this is body text.")));
        pdf.Tag($"{pdf.Add($"<< /S /H1 /Pg {page} 0 R /K 0 >>")} 0 R {pdf.Add($"<< /S /P /Pg {page} 0 R /K 1 >>")} 0 R");
        var profile = PdfProfile.Default with { Semantics = SemanticOptions.Default with { Source = SemanticSourceOption.Layout } };

        var semantics = PdfProofDocument.Load(pdf.Build(), new PdfProofOptions { Profile = profile }).Semantics;

        Assert.Equal(SemanticSource.Inferred, semantics.Source);
        Assert.Equal(SemanticSource.Inferred, Assert.Single(semantics.Headings).Source);
    }
}
