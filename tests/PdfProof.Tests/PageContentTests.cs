using PdfProof.Layout;

namespace PdfProof.Tests;

public sealed class PageContentTests
{
    // Like SelectPdf (WebKit): the page draws more than it shows, and a clipping path hides the rest.
    private const string ClippedContent =
        "BT /F1 12 Tf 50 750 Td (Always visible) Tj ET\n" +
        "q 0 400 595 442 re W n " +
        "BT /F1 12 Tf 50 300 Td (Hidden by clipping) Tj ET " +
        "BT /F1 12 Tf 50 500 Td (Inside the clip) Tj ET Q\n";

    [Fact]
    public void Text_hidden_by_a_clipping_path_is_left_out()
    {
        var text = PdfProofDocument.Load(RowsLayoutTests.Pdf(ClippedContent)).Layout.Pages[0].Text;

        Assert.Equal("Always visible\nInside the clip", text);
    }

    [Fact]
    public void Clipping_can_be_turned_off_for_diagnosis()
    {
        var options = new PdfProofOptions { Profile = PdfProfile.Default with { Layout = LayoutOptions.Default with { ApplyClipping = false } } };

        var text = PdfProofDocument.Load(RowsLayoutTests.Pdf(ClippedContent), options).Layout.Pages[0].Text;

        Assert.Contains("Hidden by clipping", text);
    }

    [Fact]
    public void Links_have_their_text_and_target_ordered_top_to_bottom()
    {
        var pdf = new RawPdf();
        var first = pdf.AddPage(
            RawPdf.Text(50, 750, "Read the terms") +
            RawPdf.Text(50, 700, "Visit our support site"));
        var second = pdf.AddPage(RawPdf.Text(50, 750, "Terms"));
        pdf.Annotate(first, "<< /Type /Annot /Subtype /Link /Rect [75 695 230 715] /A << /S /URI /URI (https://example.com/support) >> >>");
        pdf.Annotate(first, $"<< /Type /Annot /Subtype /Link /Rect [45 745 140 765] /A << /S /GoTo /D [{second} 0 R /Fit] >> >>");

        var links = PdfProofDocument.Load(pdf.Build()).Layout.Pages[0].Links;

        Assert.Collection(
            links,
            link =>
            {
                Assert.Equal("Read the terms", link.Text);
                Assert.Equal(2, link.TargetPage);
                Assert.Null(link.Uri);
                Assert.Equal(new LayoutBox(45, 745, 140, 765), link.Box);
            },
            link =>
            {
                Assert.Equal("our support site", link.Text);
                Assert.Equal("https://example.com/support", link.Uri);
                Assert.Null(link.TargetPage);
            });
    }

    [Fact]
    public void Link_text_is_the_words_under_the_link_in_reading_order_separated_by_spaces()
    {
        // Final review of Plan 2: link text built from raw letters lost the gap between "Read" and "more" (no
        // space glyph is drawn) and kept content order ("World" drawn before "Hello").
        var pdf = new RawPdf();
        var page = pdf.AddPage(
            RawPdf.Text(50, 750, "Read") + RawPdf.Text(82, 750, "more") +
            RawPdf.Text(82, 700, "World") + RawPdf.Text(50, 700, "Hello"));
        pdf.Annotate(page, "<< /Type /Annot /Subtype /Link /Rect [45 745 120 765] /A << /S /URI /URI (https://example.com/more) >> >>");
        pdf.Annotate(page, "<< /Type /Annot /Subtype /Link /Rect [45 695 130 715] /A << /S /URI /URI (https://example.com/hello) >> >>");

        var layoutPage = PdfProofDocument.Load(pdf.Build()).Layout.Pages[0];

        Assert.Equal(["Read more", "Hello World"], layoutPage.Links.Select(link => link.Text));
        Assert.Equal(["Read more", "Hello World"], layoutPage.Lines.Select(line => line.Text));
    }

    [Fact]
    public void A_link_over_no_text_or_with_another_action_still_loads()
    {
        // Review Focus 5.
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Text(50, 750, "Text elsewhere"));
        pdf.Annotate(page, "<< /Type /Annot /Subtype /Link /Rect [300 100 350 120] /A << /S /JavaScript /JS (app.alert(1)) >> >>");

        var link = Assert.Single(PdfProofDocument.Load(pdf.Build()).Layout.Pages[0].Links);

        Assert.Equal("", link.Text);
        Assert.Null(link.Uri);
        Assert.Null(link.TargetPage);
    }

    [Fact]
    public void Images_on_the_page_are_kept_and_images_entirely_off_the_page_are_dropped()
    {
        var pdf = RowsLayoutTests.Pdf(
            "q 40 0 0 40 50 600 cm /Im1 Do Q\n" +
            "q 40 0 0 40 50 -900 cm /Im1 Do Q\n");

        var image = Assert.Single(PdfProofDocument.Load(pdf).Layout.Pages[0].Images);
        Assert.Equal(new LayoutBox(50, 600, 90, 640), image.Box);
        Assert.Equal((2, 2), (image.PixelWidth, image.PixelHeight));
        Assert.Matches("^[0-9a-f]{16}$", image.Sha256);

        var options = new PdfProofOptions { Profile = PdfProfile.Default with { Layout = LayoutOptions.Default with { ApplyClipping = false } } };
        Assert.Equal(2, PdfProofDocument.Load(pdf, options).Layout.Pages[0].Images.Count);
    }

    [Fact]
    public void Other_annotations_are_listed_with_their_content()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Text(50, 750, "Hello"));
        pdf.Annotate(page, "<< /Type /Annot /Subtype /Text /Rect [300 300 320 320] /Contents (A note) >>");

        var annotation = Assert.Single(PdfProofDocument.Load(pdf.Build()).Layout.Pages[0].Annotations);

        Assert.Equal("Text", annotation.Type);
        Assert.Equal("A note", annotation.Content);
        Assert.Equal(new LayoutBox(300, 300, 320, 320), annotation.Box);
    }
}
