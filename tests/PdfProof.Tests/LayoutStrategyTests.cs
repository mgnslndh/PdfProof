using System.Text;
using PdfProof.Layout;

namespace PdfProof.Tests;

public sealed class LayoutStrategyTests
{
    [Fact]
    public void Rows_interleaves_two_columns_line_by_line()
    {
        var page = PdfProofDocument.Load(TwoColumns()).Layout.Pages[0];

        Assert.Equal("Left column line 1", page.Lines[0].Text);
        Assert.Equal("Right column line 1", page.Lines[1].Text);
    }

    [Fact]
    public void ReadingOrder_reads_two_columns_column_by_column()
    {
        var options = PdfProofOptions.Default with { Layout = LayoutOptions.Default with { Strategy = LayoutStrategies.ReadingOrder } };

        var texts = PdfProofDocument.Load(TwoColumns(), options).Layout.Pages[0].Lines.Select(l => l.Text).ToList();

        var lastLeft = texts.FindLastIndex(t => t.StartsWith("Left", StringComparison.Ordinal));
        var firstRight = texts.FindIndex(t => t.StartsWith("Right", StringComparison.Ordinal));
        Assert.True(lastLeft < firstRight, "Reading order must finish the left column before the right one: " + string.Join(" | ", texts));
        Assert.Equal(16, texts.Count(t => t.Contains("column line")));
    }

    [Fact]
    public void A_custom_strategy_decides_the_blocks_and_is_named_in_diagnostics()
    {
        var options = PdfProofOptions.Default with { Layout = LayoutOptions.Default with { Strategy = new OneBlockPerWord() } };

        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 750, "Alpha Beta")), options);

        Assert.Equal(["Alpha", "Beta"], doc.Layout.Pages[0].Blocks.Select(b => Assert.Single(b.Lines).Text));
        Assert.Contains(doc.Diagnostics.Entries, e => e.ToString() == "Layout (page 1): 2 blocks, 2 lines (OneBlockPerWord layout)");
    }

    [Fact]
    public void Rows_is_the_default_strategy()
    {
        Assert.Same(LayoutStrategies.Rows, LayoutOptions.Default.Strategy);
    }

    private static byte[] TwoColumns()
    {
        var content = new StringBuilder();
        for (var i = 0; i < 8; i++)
        {
            content.Append(RawPdf.Text(50, 700 - (14 * i), $"Left column line {i + 1}"));
            content.Append(RawPdf.Text(320, 700 - (14 * i), $"Right column line {i + 1}"));
        }

        return RowsLayoutTests.Pdf(content.ToString());
    }

    private sealed class OneBlockPerWord : ILayoutStrategy
    {
        public string Name => "OneBlockPerWord";

        public IReadOnlyList<LayoutWordBlock> Arrange(IReadOnlyList<LayoutWord> words, LayoutOptions options) =>
            words.Select(word => new LayoutWordBlock([new LayoutWordLine([word])])).ToList();
    }
}
