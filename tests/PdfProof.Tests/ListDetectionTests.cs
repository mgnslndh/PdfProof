using PdfProof.Semantics;

namespace PdfProof.Tests;

public sealed class ListDetectionTests
{
    [Fact]
    public void Lines_starting_with_a_text_bullet_are_a_list_without_the_bullets()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 700, "- First item", 12) + RawPdf.Text(50, 685, "- Second item", 12)));

        var list = Assert.Single(doc.Semantics.Lists);
        Assert.Equal(["First item", "Second item"], list.Items.Select(i => i.Text));
        Assert.Contains(doc.Semantics.Decisions, d => d.ToString() == "ListDetection (page 1): 2 items (bullet \"-\")");
    }

    [Fact]
    public void A_bullet_directly_followed_by_a_letter_is_still_a_bullet()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 700, "-Payment within 30 days.", 12)));

        Assert.Equal(["Payment within 30 days."], Assert.Single(doc.Semantics.Lists).Items.Select(i => i.Text));
    }

    [Fact]
    public void A_minus_sign_before_a_number_is_not_a_bullet()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 700, "-5 degrees outside.", 12)));

        Assert.Empty(doc.Semantics.Lists);
    }

    [Fact]
    public void Numbered_lines_are_a_list()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 700, "1. First", 12) + RawPdf.Text(50, 685, "2. Second", 12)));

        var list = Assert.Single(doc.Semantics.Lists);
        Assert.Equal(["First", "Second"], list.Items.Select(i => i.Text));
        Assert.Contains(doc.Semantics.Decisions, d => d.Message == "2 items (numbering)");
    }

    [Fact]
    public void A_small_square_just_left_of_a_line_is_a_bullet()
    {
        // How SelectPdf and Chromium draw a CSS list bullet: a 2-3 pt filled square left of the item text.
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Square(40, 700, 3) + RawPdf.Text(50, 698, "Payment within 30 days.", 12)));

        Assert.Equal(["Payment within 30 days."], Assert.Single(doc.Semantics.Lists).Items.Select(i => i.Text));
        Assert.Contains(doc.Semantics.Decisions, d => d.Message == "1 items (marker shapes)");
    }

    [Fact]
    public void A_square_elsewhere_on_the_page_is_not_a_bullet()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Square(300, 500, 3) + RawPdf.Text(50, 698, "Payment within 30 days.", 12)));

        Assert.Empty(doc.Semantics.Lists);
    }

    [Fact]
    public void A_line_without_a_bullet_continues_the_item_above()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(
            RawPdf.Text(50, 700, "- A first item long enough to wrap onto the next line of text", 12) +
            RawPdf.Text(58, 686, "and it continues here.", 12)));

        Assert.Equal(["A first item long enough to wrap onto the next line of text and it continues here."], Assert.Single(doc.Semantics.Lists).Items.Select(i => i.Text));
    }

    [Fact]
    public void The_pass_can_be_switched_off()
    {
        var profile = PdfProfile.Default with { Semantics = SemanticOptions.Default with { Lists = ListDetectionOptions.Default with { Enabled = false } } };

        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 700, "- First item", 12)), new PdfProofOptions { Profile = profile });

        Assert.Empty(doc.Semantics.Lists);
    }
}
