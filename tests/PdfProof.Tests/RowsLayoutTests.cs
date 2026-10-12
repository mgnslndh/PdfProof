using System.Globalization;
using PdfProof.Layout;

namespace PdfProof.Tests;

public sealed class RowsLayoutTests
{
    [Fact]
    public void Table_rows_split_into_cells_at_column_gaps_and_stay_row_by_row()
    {
        var page = LoadPage(
            RawPdf.Text(50, 750, "Item") + RawPdf.Text(300, 750, "Qty") + RawPdf.Text(400, 750, "Amount") +
            RawPdf.Text(50, 735, "Consulting, senior") + RawPdf.Text(300, 735, "10") + RawPdf.Text(400, 735, "14 500,00 SEK"));

        Assert.Equal(["Item", "Qty", "Amount", "Consulting, senior", "10", "14 500,00 SEK"], page.Lines.Select(l => l.Text));
        Assert.Single(page.Blocks);
    }

    [Fact]
    public void A_vertical_gap_larger_than_the_block_gap_starts_a_new_block()
    {
        var page = LoadPage(
            RawPdf.Text(50, 750, "First paragraph") + RawPdf.Text(50, 736, "continues here") +
            RawPdf.Text(50, 650, "Second paragraph"));

        Assert.Equal(
            [["First paragraph", "continues here"], ["Second paragraph"]],
            page.Blocks.Select(b => b.Lines.Select(l => l.Text).ToArray()));
        Assert.All(page.Blocks, b => Assert.Equal(LayoutOrientation.Horizontal, b.Orientation));
    }

    [Fact]
    public void Words_with_the_same_style_form_one_span_and_a_style_change_starts_a_new_one()
    {
        var page = LoadPage("BT /F2 12 Tf 50 600 Td (Total:) Tj /F1 12 Tf ( 25 634,50) Tj 1 0 0 rg ( SEK) Tj ET\n");

        var line = Assert.Single(page.Lines);
        Assert.Equal("Total: 25 634,50 SEK", line.Text);
        Assert.Collection(
            line.Spans,
            span =>
            {
                Assert.Equal("Total:", span.Text);
                Assert.Equal("Helvetica-Bold", span.Font);
                Assert.True(span.IsBold);
                Assert.Equal(12, span.Size);
            },
            span =>
            {
                Assert.Equal("25 634,50", span.Text);
                Assert.Equal("Helvetica", span.Font);
                Assert.False(span.IsBold);
                Assert.Equal("#000000", span.Color);
            },
            span =>
            {
                Assert.Equal("SEK", span.Text);
                Assert.Equal("#FF0000", span.Color);
            });
    }

    [Fact]
    public void An_oblique_font_is_italic()
    {
        var span = Assert.Single(LoadPage(RawPdf.Text(50, 600, "Note", font: "F3")).Lines[0].Spans);

        Assert.True(span.IsItalic);
        Assert.False(span.IsBold);
    }

    [Fact]
    public void Soft_hyphens_are_normalized_unless_normalization_is_off()
    {
        var pdf = Pdf(RawPdf.Text(50, 750, "e\u00ADmail", font: "F4"));

        Assert.Equal("e-mail", PdfProofDocument.Load(pdf).Layout.Pages[0].Text);
        var raw = PdfProofDocument.Load(pdf, new PdfProofOptions { Profile = PdfProfile.Default with { Layout = LayoutOptions.Default with { NormalizeText = false } } });
        Assert.Equal("e\u00ADmail", raw.Layout.Pages[0].Text);
    }

    [Fact]
    public void Text_replacements_apply_after_normalization()
    {
        var options = new PdfProofOptions
        {
            Profile = PdfProfile.Default with
            {
                Layout = LayoutOptions.Default with { TextReplacements = new Dictionary<string, string> { ["e-mail"] = "email" } },
            },
        };

        var doc = PdfProofDocument.Load(Pdf(RawPdf.Text(50, 750, "e\u00ADmail", font: "F4")), options);

        Assert.Equal("email", doc.Layout.Pages[0].Text);
    }

    [Fact]
    public void A_word_that_is_empty_after_normalization_creates_no_line()
    {
        // Review Focus 3. The zero-width no-break space U+FEFF can't be written in WinAnsi, so remove the word's
        // only letter with a replacement instead: the word becomes empty after normalization.
        var options = new PdfProofOptions
        {
            Profile = PdfProfile.Default with
            {
                Layout = LayoutOptions.Default with { TextReplacements = new Dictionary<string, string> { ["~"] = "" } },
            },
        };

        var page = PdfProofDocument.Load(Pdf(RawPdf.Text(50, 750, "~") + RawPdf.Text(50, 700, "Kept")), options).Layout.Pages[0];

        Assert.Equal(["Kept"], page.Lines.Select(l => l.Text));
        Assert.All(page.Lines.SelectMany(l => l.Spans), span => Assert.NotEqual("", span.Text));
    }

    [Fact]
    public void Page_size_rotation_and_number_are_recorded()
    {
        var page = LoadPage(RawPdf.Text(50, 750, "Hello"));

        Assert.Equal((1, 595.0, 842.0, 0), (page.Number, page.Width, page.Height, page.Rotation));
    }

    [Fact]
    public void Layout_values_and_diagnostics_are_the_same_in_every_culture()
    {
        // Review Focus 4.
        var pdf = Pdf(RawPdf.Text(50.25, 750.75, "Total 1,5"));
        // The test formats numbers invariantly itself, so only the library's own output can differ between runs.
        string Describe(PdfProofDocument doc) =>
            string.Join("|", doc.Layout.Pages[0].Lines.Select(l => FormattableString.Invariant(
                $"{l.Text}:{l.Box.Left},{l.Box.Bottom},{l.Box.Right},{l.Box.Top}:{l.Spans[0].Size}:{l.Spans[0].Color}")))
            + "|" + string.Join("|", doc.Diagnostics.Entries.Select(e => e.ToString()));

        var invariant = Describe(PdfProofDocument.Load(pdf));
        var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("sv-SE");
        try
        {
            Assert.Equal(invariant, Describe(PdfProofDocument.Load(pdf)));
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous;
        }
    }

    [Fact]
    public void Each_page_records_a_layout_diagnostic()
    {
        var doc = PdfProofDocument.Load(Pdf(RawPdf.Text(50, 750, "Item") + RawPdf.Text(300, 750, "Qty")));

        Assert.Contains(doc.Diagnostics.Entries, e => e.ToString() == "Layout (page 1): 1 blocks, 2 lines (Rows layout)");
    }

    internal static byte[] Pdf(string content)
    {
        var pdf = new RawPdf();
        pdf.AddPage(content);
        return pdf.Build();
    }

    private static LayoutPage LoadPage(string content) => PdfProofDocument.Load(Pdf(content)).Layout.Pages[0];
}
