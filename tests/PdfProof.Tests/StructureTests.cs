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
}
