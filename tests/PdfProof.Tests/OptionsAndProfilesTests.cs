using Microsoft.Extensions.Logging;
using PdfProof.Layout;

namespace PdfProof.Tests;

public sealed class OptionsAndProfilesTests
{
    [Theory]
    [InlineData("Select.Pdf Html To Pdf for .NET Community Edition v26.4.0", null, "SelectPdf", "detected from producer \"Select.Pdf Html To Pdf for .NET Community Edition v26.4.0\"")]
    [InlineData("SelectPdf Library", null, "SelectPdf", "detected from producer \"SelectPdf Library\"")]
    [InlineData("Skia/PDF m153", "Chromium", "Chromium", "detected from producer \"Skia/PDF m153\"")]
    [InlineData(null, "Mozilla/5.0 HeadlessChrome/120", "Chromium", "detected from creator \"Mozilla/5.0 HeadlessChrome/120\"")]
    [InlineData(null, "Typst 0.15.1", "Typst", "detected from creator \"Typst 0.15.1\"")]
    [InlineData("QuestPDF", null, "QuestPdf", "detected from producer \"QuestPDF\"")]
    [InlineData("PDFsharp 6.2.4", null, "Default", "no profile matches producer \"PDFsharp 6.2.4\"")]
    [InlineData("PdfPig", "Writer", "Default", "no profile matches producer \"PdfPig\" or creator \"Writer\"")]
    [InlineData(null, null, "Default", "the PDF names no producer or creator")]
    public void The_profile_is_detected_from_producer_then_creator(string? producer, string? creator, string expected, string reason)
    {
        var (profile, why) = ProducerProfiles.Detect(producer, creator);

        Assert.Same(PdfProfile.Presets.Single(p => p.Name == expected), profile);
        Assert.Equal(reason, why);
    }

    [Fact]
    public void Load_without_options_detects_the_profile_and_says_from_where()
    {
        var doc = PdfProofDocument.Load(PdfWithInfo("<< /Producer (Select.Pdf Html To Pdf for .NET Community Edition v26.4.0) >>"));

        Assert.Same(PdfProfile.SelectPdf, doc.Diagnostics.Profile);
        Assert.True(doc.Diagnostics.ProfileDetected);
        Assert.Equal(
            "Profile: SelectPdf (detected from producer \"Select.Pdf Html To Pdf for .NET Community Edition v26.4.0\")",
            doc.Diagnostics.Entries[0].ToString());
    }

    [Fact]
    public void Typst_is_detected_from_the_creator_and_the_diagnostic_says_so()
    {
        var doc = PdfProofDocument.Load(PdfWithInfo("<< /Creator (Typst 0.15.1) >>"));

        Assert.Same(PdfProfile.Typst, doc.Diagnostics.Profile);
        Assert.Equal("Profile: Typst (detected from creator \"Typst 0.15.1\")", doc.Diagnostics.Entries[0].ToString());
    }

    [Fact]
    public void Options_without_a_profile_still_detect_it()
    {
        var logger = new ListLogger();

        var doc = PdfProofDocument.Load(PdfWithInfo("<< /Producer (Typst 0.13.1) >>"), new PdfProofOptions { Logger = logger });

        Assert.Same(PdfProfile.Typst, doc.Diagnostics.Profile);
        Assert.True(doc.Diagnostics.ProfileDetected);
        Assert.Contains((LogLevel.Debug, "Profile: Typst (detected from producer \"Typst 0.13.1\")"), logger.Entries);
    }

    [Fact]
    public void A_profile_in_the_options_is_used_as_given()
    {
        var doc = PdfProofDocument.Load(
            PdfWithInfo("<< /Producer (Select.Pdf Html To Pdf for .NET Community Edition v26.4.0) >>"),
            new PdfProofOptions { Profile = PdfProfile.QuestPdf });

        Assert.Same(PdfProfile.QuestPdf, doc.Diagnostics.Profile);
        Assert.False(doc.Diagnostics.ProfileDetected);
        Assert.Equal("Profile: QuestPdf (from options)", doc.Diagnostics.Entries[0].ToString());
    }

    [Fact]
    public void An_adjusted_preset_keeps_its_name_and_is_reported_as_adjusted()
    {
        var adjusted = PdfProfile.SelectPdf with { Layout = LayoutOptions.Default with { NormalizeText = false } };

        var doc = PdfProofDocument.Load(PdfWithInfo("<< >>"), new PdfProofOptions { Profile = adjusted });

        Assert.Equal("SelectPdf", doc.Diagnostics.Profile.Name);
        Assert.False(doc.Diagnostics.Profile.Layout.NormalizeText);
        Assert.Equal("Profile: SelectPdf (from options, adjusted)", doc.Diagnostics.Entries[0].ToString());
    }

    [Fact]
    public void A_custom_profile_is_reported_as_custom()
    {
        var doc = PdfProofDocument.Load(PdfWithInfo("<< >>"), new PdfProofOptions { Profile = new PdfProfile("In-house") });

        Assert.Equal("Profile: In-house (from options, custom)", doc.Diagnostics.Entries[0].ToString());
    }

    [Fact]
    public void The_presets_have_default_layout_and_the_default_options_choose_nothing()
    {
        Assert.Equal(["Default", "SelectPdf", "QuestPdf", "Typst", "Chromium"], PdfProfile.Presets.Select(p => p.Name));
        Assert.All(PdfProfile.Presets, p => Assert.Same(LayoutOptions.Default, p.Layout));
        Assert.Equal("Typst", PdfProfile.Typst.ToString());
        Assert.Null(PdfProofOptions.Default.Profile);
        Assert.Null(PdfProofOptions.Default.Logger);
    }

    [Fact]
    public void A_profile_needs_a_name_and_layout_options()
    {
        Assert.Equal("name", Assert.Throws<ArgumentNullException>(() => new PdfProfile(null!)).ParamName);
        Assert.Equal("name", Assert.Throws<ArgumentException>(() => new PdfProfile(" ")).ParamName);
        Assert.Equal("Name", Assert.Throws<ArgumentException>(() => PdfProfile.Default with { Name = "" }).ParamName);
        Assert.Equal("Layout", Assert.Throws<ArgumentNullException>(() => PdfProfile.Default with { Layout = null! }).ParamName);
    }

    [Fact]
    public void Null_options_throw_ArgumentNullException()
    {
        var pdf = PdfWithInfo("<< /Producer (PdfPig) >>");

        Assert.Throws<ArgumentNullException>(() => PdfProofDocument.Load(pdf, null!));
        Assert.Throws<ArgumentNullException>(() => PdfProofDocument.Load(new MemoryStream(pdf), null!));
        Assert.Throws<ArgumentNullException>(() => PdfProofDocument.Load(Path.GetTempPath(), null!));
    }

    private static byte[] PdfWithInfo(string info)
    {
        var pdf = new RawPdf { Info = info };
        pdf.AddPage(RawPdf.Text(50, 750, "Hello"));
        return pdf.Build();
    }
}
