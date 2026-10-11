using PdfProof.Layout;

namespace PdfProof.Tests;

// Final review of Plan 2: a loaded document is shared between threads (spec D4), so nothing reachable from it or
// from the options may be mutable, and bad options must fail when they are set, naming the option.
public sealed class ImmutabilityAndValidationTests
{
    [Fact]
    public void Model_collections_cannot_be_changed_through_a_cast()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Text(50, 750, "Hello") + "q 40 0 0 40 50 600 cm /Im1 Do Q\n");
        pdf.Annotate(page, "<< /Type /Annot /Subtype /Link /Rect [45 745 90 765] /A << /S /URI /URI (https://example.com) >> >>");
        var doc = PdfProofDocument.Load(pdf.Build());
        var layoutPage = doc.Layout.Pages[0];

        Assert.Throws<NotSupportedException>(() => ((IList<LayoutPage>)doc.Layout.Pages).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<LayoutBookmark>)doc.Layout.Bookmarks).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<LayoutBlock>)layoutPage.Blocks).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<LayoutLine>)layoutPage.Lines)[0] = null!);
        Assert.Throws<NotSupportedException>(() => ((IList<LayoutLine>)layoutPage.Blocks[0].Lines).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<LayoutSpan>)layoutPage.Lines[0].Spans).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<LayoutLink>)layoutPage.Links).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<LayoutImage>)layoutPage.Images).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<LayoutAnnotation>)layoutPage.Annotations).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<PdfDiagnostic>)doc.Diagnostics.Entries).Clear());
    }

    [Fact]
    public void The_default_text_replacements_cannot_be_changed()
    {
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<string, string>)LayoutOptions.Default.TextReplacements)["x"] = "changed");
    }

    [Fact]
    public void Text_replacements_are_copied_so_later_changes_to_the_callers_dictionary_have_no_effect()
    {
        var replacements = new Dictionary<string, string> { ["Hello"] = "Hi" };
        var options = PdfProofOptions.Default with { Layout = LayoutOptions.Default with { TextReplacements = replacements } };
        replacements["Hello"] = "Changed";

        var text = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 750, "Hello")), options).Layout.Pages[0].Text;

        Assert.Equal("Hi", text);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(16)]
    public void Decimals_outside_0_to_15_are_rejected(int decimals)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => LayoutOptions.Default with { Decimals = decimals });

        Assert.Equal("Decimals", ex.ParamName);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Gap_factors_must_be_finite_and_not_negative(double factor)
    {
        Assert.Equal("ColumnGapFactor", Assert.Throws<ArgumentOutOfRangeException>(() => LayoutOptions.Default with { ColumnGapFactor = factor }).ParamName);
        Assert.Equal("BlockGapFactor", Assert.Throws<ArgumentOutOfRangeException>(() => LayoutOptions.Default with { BlockGapFactor = factor }).ParamName);
    }

    [Fact]
    public void A_null_strategy_or_replacements_are_rejected()
    {
        Assert.Equal("Strategy", Assert.Throws<ArgumentNullException>(() => LayoutOptions.Default with { Strategy = null! }).ParamName);
        Assert.Equal("TextReplacements", Assert.Throws<ArgumentNullException>(() => LayoutOptions.Default with { TextReplacements = null! }).ParamName);
    }

    [Fact]
    public void An_empty_replacement_key_is_rejected()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            LayoutOptions.Default with { TextReplacements = new Dictionary<string, string> { [""] = "x" } });

        Assert.Equal("TextReplacements", ex.ParamName);
    }

    [Theory]
    [InlineData("two words")]
    [InlineData("tab\tkey")]
    public void A_replacement_key_with_whitespace_is_rejected_because_replacements_apply_to_single_words(string key)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            LayoutOptions.Default with { TextReplacements = new Dictionary<string, string> { [key] = "x" } });

        Assert.Equal("TextReplacements", ex.ParamName);
        Assert.StartsWith("Replacements apply to single words; a key must not contain whitespace.", ex.Message);
    }

    [Fact]
    public void Null_layout_options_are_rejected()
    {
        Assert.Equal("Layout", Assert.Throws<ArgumentNullException>(() => PdfProofOptions.Default with { Layout = null! }).ParamName);
    }

    [Fact]
    public void Garbage_inside_a_content_stream_is_tolerated_and_yields_no_text()
    {
        // PdfPig parses content streams leniently: an unterminated string drops the rest of the stream instead of
        // failing. Turning lenient parsing off would reject many real PDFs, so this behavior is kept and pinned here.
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf("BT /F1 12 Tf 50 750 Td (unterminated Tj ET\n"));

        Assert.Equal("", doc.Layout.Pages[0].Text);
        Assert.Contains(doc.Diagnostics.Entries, e => e.Code == PdfDiagnosticCodes.NoTextLayer);
    }
}
