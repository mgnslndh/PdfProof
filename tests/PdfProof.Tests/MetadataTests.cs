namespace PdfProof.Tests;

public sealed class MetadataTests
{
    [Fact]
    public void Custom_info_entries_are_kept_sorted_and_standard_entries_are_left_out()
    {
        var pdf = new RawPdf { Info = "<< /Title (Invoice) /Zeta (last) /Department (Sales) /Producer (Probe) >>" };
        pdf.AddPage(RawPdf.Text(50, 750, "Hello"));

        var metadata = PdfProofDocument.Load(pdf.Build()).Metadata;

        Assert.Equal(["Department", "Zeta"], metadata.Custom.Keys);
        Assert.Equal("Sales", metadata.Custom["Department"]);
        Assert.Equal("Invoice", metadata.Title);
        Assert.Equal("Probe", metadata.Producer);
    }

    [Fact]
    public void A_pdf_without_an_info_dictionary_has_no_custom_entries()
    {
        var pdf = new RawPdf();
        pdf.AddPage(RawPdf.Text(50, 750, "Hello"));

        Assert.Empty(PdfProofDocument.Load(pdf.Build()).Metadata.Custom);
    }
}
