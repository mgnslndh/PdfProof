using System.Globalization;
using PdfProof.Semantics;

namespace PdfProof.Tests;

public sealed class HeadingClassificationTests
{
    [Fact]
    public void Larger_short_blocks_are_headings_with_levels_from_their_sizes()
    {
        var doc = PdfProofDocument.Load(ArtifactDetectionTests.Pages(n => n == 1
            ? RawPdf.Text(50, 750, "Invoice", 18) + RawPdf.Text(50, 700, "Questions? Visit our support site.", 12)
            : RawPdf.Text(50, 750, "Terms and conditions", 14) + RawPdf.Text(50, 700, "Payment within 30 days.", 12), 2));

        Assert.Equal([("Invoice", 1), ("Terms and conditions", 2)], doc.Semantics.Headings.Select(h => (h.Text, h.Level)));
        Assert.Equal(
            [
                "HeadingClassification: body text size 12 pt",
                "HeadingClassification (page 1): \"Invoice\": 18 pt vs body 12 pt → heading L1",
                "HeadingClassification (page 2): \"Terms and conditions\": 14 pt vs body 12 pt → heading L2",
            ],
            doc.Semantics.Decisions.Where(d => d.Pass == SemanticPass.HeadingClassification).Select(d => d.ToString()));
        Assert.Same(doc.Semantics.Headings[0], doc.Semantics.Decisions.Single(d => d.Message.StartsWith("\"Invoice\"", StringComparison.Ordinal)).Block);
    }

    [Fact]
    public void A_bold_short_block_followed_by_body_text_is_a_heading_below_the_larger_ones()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(
            RawPdf.Text(50, 750, "Invoice", 18) +
            RawPdf.Text(50, 700, "Payment terms", 12, "F2") +
            RawPdf.Text(50, 650, "Payment within 30 days of the invoice date.", 12)));

        Assert.Equal([("Invoice", 1), ("Payment terms", 2)], doc.Semantics.Headings.Select(h => (h.Text, h.Level)));
        Assert.Contains(doc.Semantics.Decisions, d => d.Message == "\"Payment terms\": bold, short and followed by body text → heading L2");
    }

    [Fact]
    public void A_bold_sentence_is_not_a_heading()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(
            RawPdf.Text(50, 700, "Payment is due now.", 12, "F2") +
            RawPdf.Text(50, 650, "Contact us with any questions about the invoice.", 12)));

        Assert.Empty(doc.Semantics.Headings);
    }

    [Fact]
    public void A_large_block_with_more_lines_than_MaxLines_is_not_a_heading()
    {
        // ShortLineRatio 0 keeps the three equal lines in one candidate; two body lines keep the body size at 12 pt.
        const string Line = "Large text that runs on and on";
        var profile = PdfProfile.Default with { Semantics = SemanticOptions.Default with { ShortLineRatio = 0 } };
        var doc = PdfProofDocument.Load(
            RowsLayoutTests.Pdf(
                RawPdf.Text(50, 750, Line, 18) + RawPdf.Text(50, 730, Line, 18) + RawPdf.Text(50, 710, Line, 18) +
                RawPdf.Text(50, 600, "Body text at the usual size, long enough to set the body size.", 12) +
                RawPdf.Text(50, 586, "More body text at the usual size, so that it outweighs the large.", 12)),
            new PdfProofOptions { Profile = profile });

        Assert.Empty(doc.Semantics.Headings);
    }

    [Fact]
    public void Inferred_headings_give_flat_sections()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(
            RawPdf.Text(50, 750, "Invoice", 18) +
            RawPdf.Text(50, 700, "Payment terms", 12, "F2") +
            RawPdf.Text(50, 650, "Payment within 30 days of the invoice date.", 12)));

        Assert.Equal(
            [("Invoice", 0, 0), ("Payment terms", 1, 0)],
            doc.Semantics.Sections.Select(s => (s.Heading.Text, s.Blocks.Count, s.Sections.Count)));
    }

    [Fact]
    public void Sizes_in_decisions_use_the_invariant_culture()
    {
        // Review Focus 3.
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("sv-SE");
        try
        {
            var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 750, "Invoice", 15.5) + RawPdf.Text(50, 700, "Body text at the usual size.", 10.5)));

            Assert.Contains(doc.Semantics.Decisions, d => d.Message == "\"Invoice\": 15.5 pt vs body 10.5 pt → heading L1");
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void The_pass_can_be_switched_off()
    {
        var profile = PdfProfile.Default with { Semantics = SemanticOptions.Default with { Headings = HeadingClassificationOptions.Default with { Enabled = false } } };

        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 750, "Invoice", 18) + RawPdf.Text(50, 700, "Body.", 12)), new PdfProofOptions { Profile = profile });

        Assert.Empty(doc.Semantics.Headings);
    }
}
