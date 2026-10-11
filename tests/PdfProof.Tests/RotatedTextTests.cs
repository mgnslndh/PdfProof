using PdfProof.Layout;

namespace PdfProof.Tests;

// Spec §5.2: rotated text is grouped in its own frame, with the reading direction taken from baselines. On real
// forms PdfPig labels bottom-to-top text Rotate270; without this it was shredded into single letters.
public sealed class RotatedTextTests
{
    [Fact]
    public void Text_reading_bottom_to_top_is_its_own_block_in_reading_order()
    {
        var page = PdfProofDocument.Load(RowsLayoutTests.Pdf(
            RawPdf.Text(50, 750, "Form title") +
            "BT /F1 10 Tf 0 1 -1 0 30 100 Tm (Print or type) Tj ET\n")).Layout.Pages[0];

        var rotated = Assert.Single(page.Blocks, b => b.Orientation != LayoutOrientation.Horizontal);
        Assert.Equal(LayoutOrientation.Rotate270, rotated.Orientation);
        var line = Assert.Single(rotated.Lines);
        Assert.Equal("Print or type", line.Text);
        Assert.True(line.Box.Bottom < line.Box.Top);
        Assert.True(line.Box.Height > line.Box.Width, "Vertical text is taller than it is wide.");

        var horizontal = Assert.Single(page.Blocks, b => b.Orientation == LayoutOrientation.Horizontal);
        Assert.Equal("Form title", Assert.Single(horizontal.Lines).Text);
        Assert.Same(rotated, page.Blocks[page.Blocks.Count - 1]);
    }

    [Fact]
    public void Rotated_runs_far_apart_on_the_same_baseline_are_separate_blocks()
    {
        var page = PdfProofDocument.Load(RowsLayoutTests.Pdf(
            "BT /F1 10 Tf 0 1 -1 0 30 100 Tm (Lower) Tj ET\n" +
            "BT /F1 10 Tf 0 1 -1 0 30 500 Tm (Upper) Tj ET\n")).Layout.Pages[0];

        Assert.Equal(["Lower", "Upper"], page.Blocks.Select(b => Assert.Single(b.Lines).Text));
    }
}
