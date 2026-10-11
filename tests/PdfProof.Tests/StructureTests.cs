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
}
