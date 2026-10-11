using PdfProof.Structure;

namespace PdfProof.Tests;

public sealed class StructureTests
{
    [Fact]
    public void An_untagged_pdf_has_an_empty_structure_root_and_is_not_tagged()
    {
        var structure = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 750, "Hello"))).Structure;

        Assert.False(structure.IsTagged);
        Assert.Equal("StructTreeRoot", structure.Root.Role);
        Assert.Empty(structure.Root.Content);
        Assert.Empty(structure.Root.Children);
        Assert.Equal("", structure.Root.Text);
        Assert.Null(structure.Root.Page);
        Assert.Empty(structure.Root.Descendants());
    }

    [Fact]
    public void Elements_are_read_with_their_roles_marked_text_and_pages()
    {
        var pdf = new RawPdf();
        var first = pdf.AddPage(
            RawPdf.Marked("H1", 0, RawPdf.Text(50, 750, "Invoice", 18)) +
            RawPdf.Marked("P", 1, RawPdf.Text(50, 700, "Hello") + RawPdf.Text(90, 700, "world")));
        var second = pdf.AddPage(RawPdf.Marked("P", 0, RawPdf.Text(50, 750, "Terms apply.")));
        var heading = pdf.Add($"<< /S /H1 /Pg {first} 0 R /K 0 >>");
        var paragraph = pdf.Add($"<< /S /P /Pg {first} 0 R /K 1 >>");
        var terms = pdf.Add($"<< /S /P /Pg {second} 0 R /K 0 >>");
        var document = pdf.Add($"<< /S /Document /K [{heading} 0 R {paragraph} 0 R {terms} 0 R] >>");
        pdf.Tag($"{document} 0 R");

        var doc = PdfProofDocument.Load(pdf.Build());

        Assert.True(doc.Structure.IsTagged);
        var root = Assert.Single(doc.Structure.Root.Children);
        Assert.Equal("Document", root.Role);
        Assert.Equal(
            [("H1", "Invoice", (int?)1), ("P", "Hello world", 1), ("P", "Terms apply.", 2)],
            root.Children.Select(n => (n.Role, n.Text, n.Page)));
        var text = Assert.IsType<StructureText>(Assert.Single(root.Children[1].Content));
        Assert.Equal((1, (int?)1), (text.MarkedContentId, text.Page));
        Assert.Contains(doc.Diagnostics.Entries, e => e.ToString() == "Structure: 4 elements");
    }

    [Fact]
    public void RoleMap_maps_custom_roles_to_standard_ones_and_keeps_the_custom_name()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("Title", 0, RawPdf.Text(50, 750, "Invoice", 18)));
        var title = pdf.Add($"<< /S /Title /Pg {page} 0 R /K 0 >>");
        pdf.Tag($"{title} 0 R", "/RoleMap << /Title /Heading /Heading /H1 >>");

        var node = Assert.Single(PdfProofDocument.Load(pdf.Build()).Structure.Root.Children);

        Assert.Equal(("H1", "Title"), (node.Role, node.CustomRole));
    }

    [Fact]
    public void Marked_text_is_normalized_like_the_layout()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("P", 0, RawPdf.Text(50, 750, "e" + (char)0xAD + "mail", font: "F4")));
        pdf.Tag($"{pdf.Add($"<< /S /P /Pg {page} 0 R /K 0 >>")} 0 R");

        Assert.Equal("e-mail", PdfProofDocument.Load(pdf.Build()).Structure.Root.Children[0].Text);
    }

    [Fact]
    public void Marked_content_hidden_by_clipping_has_no_text_in_the_structure_either()
    {
        // Review Focus 3: the layout drops clipped text, and the structure must agree.
        var pdf = new RawPdf();
        var page = pdf.AddPage(
            RawPdf.Marked("P", 0, RawPdf.Text(50, 750, "Visible")) +
            "q 0 0 10 10 re W n\n" + RawPdf.Marked("P", 1, RawPdf.Text(50, 600, "Clipped away")) + "Q\n");
        var visible = pdf.Add($"<< /S /P /Pg {page} 0 R /K 0 >>");
        var clipped = pdf.Add($"<< /S /P /Pg {page} 0 R /K 1 >>");
        pdf.Tag($"{visible} 0 R {clipped} 0 R");

        var structure = PdfProofDocument.Load(pdf.Build()).Structure;

        Assert.Equal(["Visible", ""], structure.Root.Children.Select(n => n.Text));
        Assert.Empty(structure.Root.Children[1].Content);
    }

    [Fact]
    public void Content_keeps_document_order_of_text_child_elements_and_link_references()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(
            RawPdf.Marked("P", 0, RawPdf.Text(50, 700, "Questions? Visit")) +
            RawPdf.Marked("Link", 1, RawPdf.Text(150, 700, "our support site")) +
            RawPdf.Marked("P", 2, RawPdf.Text(250, 700, "today.")));
        var annotation = pdf.Add("<< /Type /Annot /Subtype /Link /Rect [145 695 245 715] /A << /S /URI /URI (https://example.com/support) >> >>");
        pdf.Annotate(page, $"{annotation} 0 R");
        var link = pdf.Add($"<< /S /Link /Pg {page} 0 R /K [1 << /Type /OBJR /Obj {annotation} 0 R >>] >>");
        var paragraph = pdf.Add($"<< /S /P /Pg {page} 0 R /K [0 {link} 0 R 2] >>");
        pdf.Tag($"{paragraph} 0 R");

        var node = Assert.Single(PdfProofDocument.Load(pdf.Build()).Structure.Root.Children);

        Assert.Equal("Questions? Visit our support site today.", node.Text);
        Assert.Collection(
            node.Content,
            item => Assert.Equal("Questions? Visit", Assert.IsType<StructureText>(item).Text),
            item => Assert.Equal("Link", Assert.IsType<StructureNode>(item).Role),
            item => Assert.Equal("today.", Assert.IsType<StructureText>(item).Text));
        var linkNode = node.Children[0];
        var reference = Assert.Single(linkNode.Content.OfType<StructureObjectReference>());
        Assert.Equal("Link", reference.Type);
        Assert.Equal(("our support site", "https://example.com/support"), (reference.Link!.Text, reference.Link.Uri));
        Assert.Equal("our support site", linkNode.Text);
    }

    [Fact]
    public void A_link_annotation_on_a_paragraph_is_kept_where_the_producer_put_it()
    {
        // QuestPDF attaches link annotations to the enclosing P instead of a Link element. The structure model
        // shows that as it is; the semantic layer (Plan 5) repairs it.
        var pdf = new RawPdf();
        var first = pdf.AddPage(RawPdf.Marked("P", 0, RawPdf.Text(50, 700, "Read the terms")));
        var second = pdf.AddPage(RawPdf.Text(50, 750, "Terms"));
        var annotation = pdf.Add($"<< /Type /Annot /Subtype /Link /Rect [45 715 140 695] /A << /S /GoTo /D [{second} 0 R /Fit] >> >>");
        pdf.Annotate(first, $"{annotation} 0 R");
        var paragraph = pdf.Add($"<< /S /P /Pg {first} 0 R /K [0 << /Type /OBJR /Obj {annotation} 0 R >>] >>");
        pdf.Tag($"{paragraph} 0 R");

        var node = Assert.Single(PdfProofDocument.Load(pdf.Build()).Structure.Root.Children);

        Assert.Equal("P", node.Role);
        var reference = Assert.Single(node.Content.OfType<StructureObjectReference>());
        Assert.Equal(2, reference.Link!.TargetPage); // the rectangle's corners are reversed: Review Focus 5
    }

    [Fact]
    public void A_reference_to_a_non_link_annotation_has_no_link()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("P", 0, RawPdf.Text(50, 700, "Note")));
        var annotation = pdf.Add("<< /Type /Annot /Subtype /Text /Rect [300 300 320 320] /Contents (A note) >>");
        pdf.Annotate(page, $"{annotation} 0 R");
        var paragraph = pdf.Add($"<< /S /P /Pg {page} 0 R /K [0 << /Type /OBJR /Obj {annotation} 0 R >>] >>");
        pdf.Tag($"{paragraph} 0 R");

        var reference = Assert.Single(PdfProofDocument.Load(pdf.Build()).Structure.Root.Children[0].Content.OfType<StructureObjectReference>());

        Assert.Equal(("Text", (int?)1), (reference.Type, reference.Page));
        Assert.Null(reference.Link);
    }

    [Fact]
    public void Alt_actual_text_and_language_are_read()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(
            RawPdf.Marked("Figure", 0, "q 40 0 0 40 50 600 cm /Im1 Do Q\n") +
            RawPdf.Marked("Span", 1, RawPdf.Text(50, 500, "Pg.")));
        var figure = pdf.Add($"<< /S /Figure /Pg {page} 0 R /K 0 /Alt (Company logo) >>");
        var span = pdf.Add($"<< /S /Span /Pg {page} 0 R /K 1 /ActualText (Page) /Lang (en-US) >>");
        pdf.Tag($"{figure} 0 R {span} 0 R");

        var children = PdfProofDocument.Load(pdf.Build()).Structure.Root.Children;

        Assert.Equal(("Figure", "Company logo", ""), (children[0].Role, children[0].Alt, children[0].Text));
        Assert.Equal(("Page", "Page", "en-US"), (children[1].ActualText, children[1].Text, children[1].Language));
    }

    [Fact]
    public void Table_attributes_scope_and_spans_are_read_from_the_attribute_objects()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("TH", 0, RawPdf.Text(50, 750, "Amount")) + RawPdf.Marked("TD", 1, RawPdf.Text(50, 730, "9 900,00 SEK")));
        var header = pdf.Add($"<< /S /TH /Pg {page} 0 R /K 0 /A << /O /Table /Scope /Column /ColSpan 2 >> >>");
        var cell = pdf.Add($"<< /S /TD /Pg {page} 0 R /K 1 /A [<< /O /Layout /Placement /Block >> 0 << /O /Table /RowSpan 3 >> 0] >>");
        var row = pdf.Add($"<< /S /TR /K [{header} 0 R {cell} 0 R] >>");
        pdf.Tag($"{pdf.Add($"<< /S /Table /K [{row} 0 R] >>")} 0 R");

        var cells = PdfProofDocument.Load(pdf.Build()).Structure.Root.Descendants().Where(n => n.Role is "TH" or "TD").ToList();

        Assert.Equal(("Column", 1, 2), (cells[0].Scope, cells[0].RowSpan, cells[0].ColumnSpan));
        Assert.Equal(((string?)null, 3, 1), (cells[1].Scope, cells[1].RowSpan, cells[1].ColumnSpan));
    }

    [Fact]
    public void Artifacts_are_left_out_of_the_structure()
    {
        // Review Focus 4: running headers and page numbers are artifacts, both as elements and as marked content.
        var pdf = new RawPdf();
        var page = pdf.AddPage(
            RawPdf.Marked("P", 0, RawPdf.Text(50, 750, "Body text")) +
            RawPdf.Artifact(RawPdf.Text(50, 30, "Page 1 of 1")) +
            RawPdf.Marked("Footer", 1, RawPdf.Text(400, 30, "Confidential")));
        var paragraph = pdf.Add($"<< /S /P /Pg {page} 0 R /K 0 >>");
        var footer = pdf.Add($"<< /S /Footer /Pg {page} 0 R /K 1 >>");
        pdf.Tag($"{paragraph} 0 R {footer} 0 R", "/RoleMap << /Footer /Artifact >>");

        var structure = PdfProofDocument.Load(pdf.Build()).Structure;

        var node = Assert.Single(structure.Root.Children);
        Assert.Equal("Body text", node.Text);
    }
}
