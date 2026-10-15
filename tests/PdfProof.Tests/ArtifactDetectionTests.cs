using PdfProof.Semantics;

namespace PdfProof.Tests;

public sealed class ArtifactDetectionTests
{
    [Fact]
    public void A_repeating_page_number_footer_is_an_artifact_with_placeholders_and_not_a_block()
    {
        var doc = PdfProofDocument.Load(Pages(n => RawPdf.Text(50, 700, $"Body text on page {n}.") + RawPdf.Text(270, 40, $"Page {n} of 2", 10), 2));

        var artifact = Assert.Single(doc.Semantics.Artifacts);
        Assert.Equal((SemanticArtifactRole.Footer, "Page {n} of {N}"), (artifact.Role, artifact.Text));
        Assert.Equal([1, 2], artifact.Pages);
        Assert.Equal(["Body text on page 1.", "Body text on page 2."], doc.Semantics.Blocks.Select(b => b.Text));
        Assert.Contains(doc.Semantics.Decisions, d => d.ToString() == "ArtifactDetection: footer \"Page {n} of {N}\" repeats on pages 1, 2");
    }

    [Fact]
    public void A_running_header_repeating_on_every_page_is_an_artifact()
    {
        var doc = PdfProofDocument.Load(Pages(n => RawPdf.Text(50, 810, "Quarterly report", 10) + RawPdf.Text(50, 700, $"Content {n}."), 3));

        var artifact = Assert.Single(doc.Semantics.Artifacts);
        Assert.Equal((SemanticArtifactRole.Header, "Quarterly report"), (artifact.Role, artifact.Text));
        Assert.Equal([1, 2, 3], artifact.Pages);
    }

    [Fact]
    public void A_page_number_on_a_one_page_document_is_an_artifact_by_its_pattern()
    {
        var doc = PdfProofDocument.Load(Pages(_ => RawPdf.Text(50, 700, "Only page.") + RawPdf.Text(290, 40, "Page 1", 10), 1));

        var artifact = Assert.Single(doc.Semantics.Artifacts);
        Assert.Equal("Page {n}", artifact.Text);
        Assert.Equal([1], artifact.Pages);
        Assert.Contains(doc.Semantics.Decisions, d => d.ToString() == "ArtifactDetection (page 1): footer \"Page {n}\" is a page number");
    }

    [Fact]
    public void Band_text_that_neither_repeats_nor_is_a_page_number_stays_content()
    {
        var doc = PdfProofDocument.Load(Pages(n => RawPdf.Text(50, 700, $"Content {n}.") + (n == 2 ? RawPdf.Text(50, 40, "Total: 10", 10) : ""), 2));

        Assert.Empty(doc.Semantics.Artifacts);
        Assert.Contains(doc.Semantics.Blocks, b => b.Text == "Total: 10");
    }

    [Fact]
    public void A_two_part_footer_is_an_artifact_and_not_a_table()
    {
        // Review Focus 4: left text and right page number form one row of two cells.
        var doc = PdfProofDocument.Load(Pages(n => RawPdf.Text(50, 700, $"Content {n}.") + RawPdf.Text(50, 40, "Quarterly report", 10) + RawPdf.Text(500, 40, $"{n}", 10), 2));

        var artifact = Assert.Single(doc.Semantics.Artifacts);
        Assert.Equal("Quarterly report {n}", artifact.Text);
        Assert.Equal([1, 2], artifact.Pages);
        Assert.Equal(["Content 1.", "Content 2."], doc.Semantics.Blocks.Select(b => b.Text));
    }

    [Fact]
    public void The_pass_can_be_switched_off()
    {
        var profile = PdfProfile.Default with { Semantics = SemanticOptions.Default with { Artifacts = ArtifactDetectionOptions.Default with { Enabled = false } } };

        var doc = PdfProofDocument.Load(Pages(n => RawPdf.Text(50, 700, $"Body {n}.") + RawPdf.Text(270, 40, $"Page {n} of 2", 10), 2), new PdfProofOptions { Profile = profile });

        Assert.Empty(doc.Semantics.Artifacts);
        Assert.Contains(doc.Semantics.Blocks, b => b.Text == "Page 1 of 2");
    }

    internal static byte[] Pages(Func<int, string> content, int count)
    {
        var pdf = new RawPdf();
        for (var n = 1; n <= count; n++)
        {
            pdf.AddPage(content(n));
        }

        return pdf.Build();
    }
}
