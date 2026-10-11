using UglyToad.PdfPig;
using UglyToad.PdfPig.Tokens;

namespace PdfProof.Tests;

public sealed class RawPdfTaggingTests
{
    [Fact]
    public void Tag_adds_a_structure_tree_root_and_mark_info_and_Marked_wraps_marked_content()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("P", 0, RawPdf.Text(50, 750, "Hello")) + RawPdf.Artifact(RawPdf.Text(50, 30, "Page 1")));
        var paragraph = pdf.Add($"<< /S /P /Pg {page} 0 R /K 0 >>");
        pdf.Tag($"{paragraph} 0 R");

        using var document = PdfDocument.Open(pdf.Build());

        var catalog = document.Structure.Catalog.CatalogDictionary;
        Assert.True(catalog.Data.ContainsKey("StructTreeRoot"));
        Assert.True(catalog.Data.ContainsKey("MarkInfo"));
        var marked = document.GetPage(1).GetMarkedContents();
        Assert.Contains(marked, m => m.MarkedContentIdentifier == 0 && string.Concat(m.Letters.Select(l => l.Value)) == "Hello");
        Assert.Contains(marked, m => m.IsArtifact);
    }
}
