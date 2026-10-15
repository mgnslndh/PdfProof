using PdfProof.Semantics;

namespace PdfProof.Tests;

public sealed class TableDetectionTests
{
    [Fact]
    public void Aligned_rows_of_cells_are_a_table_with_a_header_row_by_style()
    {
        var doc = PdfProofDocument.Load(LayoutFrontEndTests.OneBlockInvoice());

        var table = Assert.Single(doc.Semantics.Tables);
        Assert.Equal(["Item", "Qty", "Amount"], table.Header!.Cells.Select(c => c.Text));
        Assert.Equal([["Consulting, senior", "10", "14 500,00 SEK"]], table.Rows.Select(r => r.Cells.Select(c => c.Text).ToArray()));
        Assert.Equal("Item | Qty | Amount\nConsulting, senior | 10 | 14 500,00 SEK", table.Text);
        Assert.Contains(doc.Semantics.Decisions, d => d.ToString() == "TableDetection (page 1): 2 rows × 3 columns, header row by style");
    }

    [Fact]
    public void Right_aligned_numbers_share_a_column_with_their_header()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(
            RawPdf.Text(50, 700, "Item", 12, "F2") + RawPdf.Text(400, 700, "Amount", 12, "F2") +
            RawPdf.Text(50, 685, "Consulting, senior", 12) + RawPdf.Text(380, 685, "14 500,00 SEK", 12) +
            RawPdf.Text(50, 670, "Travel", 12) + RawPdf.Text(420, 670, "900,00", 12)));

        var table = Assert.Single(doc.Semantics.Tables);
        Assert.Equal(["Item", "Amount"], table.Header!.Cells.Select(c => c.Text));
        Assert.Equal([["Consulting, senior", "14 500,00 SEK"], ["Travel", "900,00"]], table.Rows.Select(r => r.Cells.Select(c => c.Text).ToArray()));
    }

    [Fact]
    public void A_single_row_of_cells_is_not_a_table()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 700, "Total:", 12) + RawPdf.Text(400, 700, "14 500,00 SEK", 12)));

        Assert.Empty(doc.Semantics.Tables);
        Assert.Equal("Total: 14 500,00 SEK", Assert.IsType<SemanticParagraph>(Assert.Single(doc.Semantics.Blocks)).Text);
    }

    [Fact]
    public void Rows_whose_cells_share_no_column_are_not_a_table()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(
            RawPdf.Text(50, 700, "North", 12) + RawPdf.Text(250, 700, "East", 12) +
            RawPdf.Text(150, 685, "South", 12) + RawPdf.Text(350, 685, "West", 12)));

        Assert.Empty(doc.Semantics.Tables);
    }

    [Fact]
    public void Rows_of_the_same_style_have_no_header()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(
            RawPdf.Text(50, 700, "Consulting", 12) + RawPdf.Text(300, 700, "10", 12) +
            RawPdf.Text(50, 685, "Travel", 12) + RawPdf.Text(300, 685, "2", 12)));

        var table = Assert.Single(doc.Semantics.Tables);
        Assert.Null(table.Header);
        Assert.Equal(2, table.Rows.Count);
    }

    [Fact]
    public void The_pass_can_be_switched_off()
    {
        var profile = PdfProfile.Default with { Semantics = SemanticOptions.Default with { Tables = TableDetectionOptions.Default with { Enabled = false } } };

        var doc = PdfProofDocument.Load(LayoutFrontEndTests.OneBlockInvoice(), new PdfProofOptions { Profile = profile });

        Assert.Empty(doc.Semantics.Tables);
    }
}
