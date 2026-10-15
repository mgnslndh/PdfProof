using PdfProof.Layout;
using PdfProof.Semantics;

namespace PdfProof.Tests;

// Shapes found by the final review of Plan 5a that the invoice corpus doesn't exercise.
public sealed class SemanticEdgeCaseTests
{
    [Fact]
    public void A_nested_list_beside_its_items_is_flattened_and_logged()
    {
        // Typst writes nested lists as L > [LI, L, LI].
        var pdf = new RawPdf();
        var page = pdf.AddPage(
            RawPdf.Marked("LBody", 0, RawPdf.Text(60, 700, "Outer one")) +
            RawPdf.Marked("LBody", 1, RawPdf.Text(70, 685, "Inner one")) +
            RawPdf.Marked("LBody", 2, RawPdf.Text(60, 670, "Outer two")));
        string Item(int mcid) => $"{pdf.Add($"<< /S /LI /K [{pdf.Add($"<< /S /LBody /Pg {page} 0 R /K {mcid} >>")} 0 R] >>")} 0 R";
        var outerOne = Item(0);
        var inner = pdf.Add($"<< /S /L /K [{Item(1)}] >>");
        var outerTwo = Item(2);
        pdf.Tag($"{pdf.Add($"<< /S /L /K [{outerOne} {inner} 0 R {outerTwo}] >>")} 0 R");

        var semantics = PdfProofDocument.Load(pdf.Build()).Semantics;

        Assert.Equal(["Outer one", "Inner one", "Outer two"], Assert.Single(semantics.Lists).Items.Select(i => i.Text));
        Assert.Contains(semantics.Decisions, d => d.Message == "a nested list is flattened into its parent list");
    }

    [Fact]
    public void A_nested_list_inside_an_item_body_is_flattened_after_its_item()
    {
        // The PDF/UA best-practice shape: LI > LBody > [text, L].
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("LBody", 0, RawPdf.Text(60, 700, "Outer one")) + RawPdf.Marked("LBody", 1, RawPdf.Text(70, 685, "Inner one")));
        var innerItem = pdf.Add($"<< /S /LI /K [{pdf.Add($"<< /S /LBody /Pg {page} 0 R /K 1 >>")} 0 R] >>");
        var inner = pdf.Add($"<< /S /L /K [{innerItem} 0 R] >>");
        var body = pdf.Add($"<< /S /LBody /Pg {page} 0 R /K [0 {inner} 0 R] >>");
        pdf.Tag($"{pdf.Add($"<< /S /L /K [{pdf.Add($"<< /S /LI /K [{body} 0 R] >>")} 0 R] >>")} 0 R");

        Assert.Equal(["Outer one", "Inner one"], Assert.Single(PdfProofDocument.Load(pdf.Build()).Semantics.Lists).Items.Select(i => i.Text));
    }

    [Fact]
    public void A_list_inside_a_table_cell_keeps_its_text_in_the_cell()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("TD", 0, RawPdf.Text(50, 700, "Notes")) + RawPdf.Marked("LBody", 1, RawPdf.Text(200, 700, "Paid in full")));
        var list = pdf.Add($"<< /S /L /K [{pdf.Add($"<< /S /LI /K [{pdf.Add($"<< /S /LBody /Pg {page} 0 R /K 1 >>")} 0 R] >>")} 0 R] >>");
        var first = pdf.Add($"<< /S /TD /Pg {page} 0 R /K 0 >>");
        var second = pdf.Add($"<< /S /TD /K [{list} 0 R] >>");
        pdf.Tag($"{pdf.Add($"<< /S /Table /K [{pdf.Add($"<< /S /TR /K [{first} 0 R {second} 0 R] >>")} 0 R] >>")} 0 R");

        var table = Assert.Single(PdfProofDocument.Load(pdf.Build()).Semantics.Tables);

        Assert.Equal(["Notes", "Paid in full"], table.Rows[0].Cells.Select(c => c.Text));
    }

    [Fact]
    public void A_tagged_heading_with_the_same_text_as_the_running_header_stays()
    {
        var pdf = new RawPdf();
        var first = pdf.AddPage(RawPdf.Artifact(RawPdf.Text(50, 810, "Quarterly report", 10)) + RawPdf.Marked("H1", 0, RawPdf.Text(50, 600, "Quarterly report", 18)));
        var second = pdf.AddPage(RawPdf.Artifact(RawPdf.Text(50, 810, "Quarterly report", 10)) + RawPdf.Marked("P", 0, RawPdf.Text(50, 600, "Results.")));
        pdf.Tag($"{pdf.Add($"<< /S /H1 /Pg {first} 0 R /K 0 >>")} 0 R {pdf.Add($"<< /S /P /Pg {second} 0 R /K 0 >>")} 0 R");

        var semantics = PdfProofDocument.Load(pdf.Build()).Semantics;

        Assert.Equal("Quarterly report", Assert.Single(semantics.Artifacts).Text);
        Assert.Equal(["Quarterly report", "Results."], semantics.Blocks.Select(b => b.Text));
    }

    [Fact]
    public void A_footer_tagged_as_a_paragraph_is_an_artifact_and_leaves_the_body()
    {
        var pdf = new RawPdf();
        var pages = Enumerable.Range(1, 2)
            .Select(n => pdf.AddPage(RawPdf.Marked("P", 0, RawPdf.Text(50, 700, $"Content {n}.")) + RawPdf.Marked("P", 1, RawPdf.Text(270, 40, $"Page {n} of 2", 10))))
            .ToList();
        pdf.Tag(string.Join(" ", pages.SelectMany(p => new[] { $"{pdf.Add($"<< /S /P /Pg {p} 0 R /K 0 >>")} 0 R", $"{pdf.Add($"<< /S /P /Pg {p} 0 R /K 1 >>")} 0 R" })));

        var semantics = PdfProofDocument.Load(pdf.Build()).Semantics;

        Assert.Equal("Page {n} of {N}", Assert.Single(semantics.Artifacts).Text);
        Assert.Equal(["Content 1.", "Content 2."], semantics.Blocks.Select(b => b.Text));
    }

    [Fact]
    public void A_two_part_footer_split_across_layout_blocks_leaves_the_body_entirely()
    {
        var profile = PdfProfile.Default with { Layout = LayoutOptions.Default with { Strategy = LayoutStrategies.ReadingOrder } };

        var doc = PdfProofDocument.Load(
            ArtifactDetectionTests.Pages(n => RawPdf.Text(50, 700, $"Content {n}.") + RawPdf.Text(50, 40, "Quarterly report", 10) + RawPdf.Text(500, 40, $"{n}", 10), 2),
            new PdfProofOptions { Profile = profile });

        Assert.Equal("Quarterly report {n}", Assert.Single(doc.Semantics.Artifacts).Text);
        Assert.Equal(["Content 1.", "Content 2."], doc.Semantics.Blocks.Select(b => b.Text));
    }

    [Fact]
    public void A_number_in_the_bottom_band_is_a_page_number_only_when_it_is_the_page_number()
    {
        var doc = PdfProofDocument.Load(ArtifactDetectionTests.Pages(_ => RawPdf.Text(50, 700, "Total due:") + RawPdf.Text(500, 40, "1250", 10), 1));

        Assert.Empty(doc.Semantics.Artifacts);
        Assert.Contains(doc.Semantics.Blocks, b => b.Text == "1250");
    }

    [Fact]
    public void A_table_header_repeated_at_the_top_of_each_page_is_not_a_running_header()
    {
        // Row names differ per page and contain no page number, so only the header repeats.
        string[] names = ["Alpha", "Beta", "Gamma"];
        var doc = PdfProofDocument.Load(ArtifactDetectionTests.Pages(
            n =>
                RawPdf.Text(50, 800, "Item", 12, "F2") + RawPdf.Text(300, 800, "Amount", 12, "F2") +
                RawPdf.Text(50, 785, names[n - 1] + " one", 12) + RawPdf.Text(300, 785, "10", 12) +
                RawPdf.Text(50, 770, names[n - 1] + " two", 12) + RawPdf.Text(300, 770, "20", 12),
            3));

        Assert.Empty(doc.Semantics.Artifacts);
        Assert.Equal(3, doc.Semantics.Tables.Count);
        Assert.All(doc.Semantics.Tables, t => Assert.Equal(["Item", "Amount"], t.Header!.Cells.Select(c => c.Text)));
    }

    [Fact]
    public void A_ragged_paragraph_is_not_split_by_a_wider_block_elsewhere_on_the_page()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(
            RawPdf.Text(50, 750, "A very wide line of text that sets the width of the page text column here.", 12) +
            RawPdf.Text(50, 650, "A ragged paragraph whose lines", 12) +
            RawPdf.Text(50, 636, "are a little shorter than the", 12) +
            RawPdf.Text(50, 622, "widest line on this page.", 12)));

        Assert.Equal(
            ["A very wide line of text that sets the width of the page text column here.", "A ragged paragraph whose lines are a little shorter than the widest line on this page."],
            doc.Semantics.Blocks.Select(b => b.Text));
    }

    [Fact]
    public void A_bullet_list_followed_by_a_numbered_list_is_two_lists()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(
            RawPdf.Text(50, 700, "- Bullet one", 12) + RawPdf.Text(50, 685, "- Bullet two", 12) +
            RawPdf.Text(50, 670, "1. Numbered one", 12) + RawPdf.Text(50, 655, "2. Numbered two", 12)));

        Assert.Equal([["Bullet one", "Bullet two"], ["Numbered one", "Numbered two"]], doc.Semantics.Lists.Select(l => l.Items.Select(i => i.Text).ToArray()));
    }

    [Fact]
    public void A_link_on_the_second_line_of_a_paragraph_links_the_text_on_that_line()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Text(50, 700, "Read more here about our pricing and our terms of") + RawPdf.Text(50, 686, "service, then click") + RawPdf.Text(150, 686, "here"));
        pdf.Annotate(page, "<< /Type /Annot /Subtype /Link /Rect [148 682 180 695] /A << /S /URI /URI (https://example.com/terms) >> >>");

        var runs = PdfProofDocument.Load(pdf.Build()).Semantics.Paragraphs.Single().Runs;

        Assert.Equal(
            [("Read more here about our pricing and our terms of service, then click ", (string?)null), ("here", "https://example.com/terms")],
            runs.Select(r => (r.Text, r.Link?.Uri)));
    }

    [Fact]
    public void A_Link_element_at_block_level_keeps_its_link()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("Link", 0, RawPdf.Text(50, 700, "Read the docs")));
        var annotation = pdf.Add("<< /Type /Annot /Subtype /Link /Rect [45 695 150 715] /A << /S /URI /URI (https://example.com/docs) >> >>");
        pdf.Annotate(page, $"{annotation} 0 R");
        pdf.Tag($"{pdf.Add($"<< /S /Link /Pg {page} 0 R /K [0 << /Type /OBJR /Obj {annotation} 0 R >>] >>")} 0 R");

        var semantics = PdfProofDocument.Load(pdf.Build()).Semantics;

        Assert.Equal("https://example.com/docs", Assert.Single(semantics.Paragraphs[0].Runs).Link?.Uri);
        Assert.DoesNotContain(semantics.Decisions, d => d.Message.Contains("not found"));
    }

    [Fact]
    public void A_link_annotation_without_a_structure_reference_is_still_attached_in_a_tagged_document()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("P", 0, RawPdf.Text(50, 700, "Visit the support site today")));
        pdf.Annotate(page, "<< /Type /Annot /Subtype /Link /Rect [45 695 300 715] /A << /S /URI /URI (https://example.com/support) >> >>");
        pdf.Tag($"{pdf.Add($"<< /S /P /Pg {page} 0 R /K 0 >>")} 0 R");

        var run = Assert.Single(PdfProofDocument.Load(pdf.Build()).Semantics.Paragraphs[0].Runs);

        Assert.Equal("https://example.com/support", run.Link?.Uri);
    }

    [Fact]
    public void A_single_numbered_line_is_not_a_list()
    {
        // "1. Introduction" is a numbered heading or paragraph; "C. Smith" is an initial. A bullet may stand alone.
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(
            RawPdf.Text(50, 700, "1. Introduction", 12) +
            RawPdf.Text(50, 650, "C. Smith wrote this report about the quarter.", 12)));

        Assert.Empty(doc.Semantics.Lists);
    }

    [Fact]
    public void Rows_of_cells_that_are_not_a_table_say_why()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(
            RawPdf.Text(50, 700, "North", 12) + RawPdf.Text(250, 700, "East", 12) +
            RawPdf.Text(150, 685, "South", 12) + RawPdf.Text(350, 685, "West", 12)));

        Assert.Contains(doc.Semantics.Decisions, d => d.ToString() == "TableDetection (page 1): 2 rows of cells are not a table: fewer than two columns have a cell in every row");
    }

    [Fact]
    public void Tagged_table_spans_and_extra_header_rows_are_logged()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(
            RawPdf.Marked("TH", 0, RawPdf.Text(50, 720, "Period")) + RawPdf.Marked("TH", 1, RawPdf.Text(50, 700, "Item")) +
            RawPdf.Marked("TD", 2, RawPdf.Text(50, 680, "Consulting")));
        var spanning = pdf.Add($"<< /S /TH /Pg {page} 0 R /K 0 /A << /O /Table /ColSpan 2 >> >>");
        var second = pdf.Add($"<< /S /TH /Pg {page} 0 R /K 1 >>");
        var head = pdf.Add($"<< /S /THead /K [{pdf.Add($"<< /S /TR /K [{spanning} 0 R] >>")} 0 R {pdf.Add($"<< /S /TR /K [{second} 0 R] >>")} 0 R] >>");
        var body = pdf.Add($"<< /S /TBody /K [{pdf.Add($"<< /S /TR /K [{pdf.Add($"<< /S /TD /Pg {page} 0 R /K 2 >>")} 0 R] >>")} 0 R] >>");
        pdf.Tag($"{pdf.Add($"<< /S /Table /K [{head} 0 R {body} 0 R] >>")} 0 R");

        var decisions = PdfProofDocument.Load(pdf.Build()).Semantics.Decisions.Select(d => d.ToString()).ToList();

        Assert.Contains("StructureFrontEnd (page 1): a table has row or column spans, which are not supported yet; its cells are read as written", decisions);
        Assert.Contains("StructureFrontEnd (page 1): a table has 2 header rows; only the first is its header", decisions);
    }

    [Fact]
    public void A_running_header_drifting_by_tenths_of_a_point_still_repeats()
    {
        double[] baselines = [803.0, 803.6, 804.2];
        var doc = PdfProofDocument.Load(ArtifactDetectionTests.Pages(n => RawPdf.Text(50, baselines[n - 1], "Quarterly report", 10) + RawPdf.Text(50, 700, $"Content {n}."), 3));

        var artifact = Assert.Single(doc.Semantics.Artifacts);
        Assert.Equal([1, 2, 3], artifact.Pages);
    }

    [Fact]
    public void The_CrossPageMerge_pass_has_its_place_in_the_order()
    {
        Assert.Equal(
            ["LayoutFrontEnd", "StructureFrontEnd", "ArtifactDetection", "CrossPageMerge", "HeadingClassification", "TableDetection", "ListDetection", "LinkAttachment", "Finalize"],
            Enum.GetNames(typeof(SemanticPass)));
    }

    [Fact]
    public void Asking_for_structure_semantics_from_an_untagged_pdf_is_a_warning()
    {
        var profile = PdfProfile.Default with { Semantics = SemanticOptions.Default with { Source = SemanticSourceOption.Structure } };

        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 700, "Hello")), new PdfProofOptions { Profile = profile });

        Assert.Empty(doc.Semantics.Blocks);
        Assert.Contains(doc.Diagnostics.Entries, e => e.ToString() == "NoStructure: The options ask for semantics from the structure tree, but the PDF isn't tagged; the semantic model is empty.");
    }
}
