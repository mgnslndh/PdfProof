using Microsoft.Extensions.Logging;
using PdfProof.Layout;

namespace PdfProof.Tests;

public sealed class OptionsAndProfilesTests
{
    [Theory]
    [InlineData("Select.Pdf for .NET v26.4.0", null, PdfProfile.SelectPdf)]
    [InlineData("SelectPdf Library", null, PdfProfile.SelectPdf)]
    [InlineData("QuestPDF", null, PdfProfile.QuestPdf)]
    [InlineData("Typst 0.13.1", null, PdfProfile.Typst)]
    [InlineData("Skia/PDF m120", "Chromium", PdfProfile.Chromium)]
    [InlineData(null, "Mozilla/5.0 HeadlessChrome/120", PdfProfile.Chromium)]
    [InlineData("PdfPig", null, PdfProfile.Default)]
    [InlineData(null, null, PdfProfile.Default)]
    public void The_profile_is_detected_from_producer_then_creator(string? producer, string? creator, PdfProfile expected)
    {
        Assert.Equal(expected, ProducerProfiles.Detect(producer, creator));
    }

    [Fact]
    public void Load_without_options_detects_the_profile_and_says_so()
    {
        var doc = PdfProofDocument.Load(PdfWithProducer("Select.Pdf for .NET v26.4.0"));

        Assert.Equal(PdfProfile.SelectPdf, doc.Diagnostics.Profile);
        Assert.True(doc.Diagnostics.ProfileDetected);
        Assert.Equal(
            "Profile: SelectPdf (detected from producer \"Select.Pdf for .NET v26.4.0\")",
            doc.Diagnostics.Entries[0].ToString());
    }

    [Fact]
    public void Load_with_options_uses_their_profile()
    {
        var doc = PdfProofDocument.Load(PdfWithProducer("Select.Pdf for .NET v26.4.0"), PdfProofOptions.ForProfile(PdfProfile.QuestPdf));

        Assert.Equal(PdfProfile.QuestPdf, doc.Diagnostics.Profile);
        Assert.False(doc.Diagnostics.ProfileDetected);
        Assert.Equal("Profile: QuestPdf (from options)", doc.Diagnostics.Entries[0].ToString());
    }

    [Fact]
    public void ForProfile_gives_complete_options_with_default_layout()
    {
        var options = PdfProofOptions.ForProfile(PdfProfile.Typst);

        Assert.Equal(PdfProfile.Typst, options.Profile);
        Assert.Same(LayoutOptions.Default, options.Layout);
        Assert.Null(options.Logger);
        Assert.Equal(PdfProfile.Default, PdfProofOptions.Default.Profile);
    }

    [Fact]
    public void Diagnostics_are_logged_to_the_logger_in_the_options()
    {
        var logger = new ListLogger();

        PdfProofDocument.Load(PdfWithProducer("Typst 0.13.1"), PdfProofOptions.Default with { Logger = logger });

        Assert.Contains((LogLevel.Debug, "Profile: Default (from options)"), logger.Entries);
    }

    [Fact]
    public void Null_options_throw_ArgumentNullException()
    {
        var pdf = PdfWithProducer("PdfPig");

        Assert.Throws<ArgumentNullException>(() => PdfProofDocument.Load(pdf, null!));
        Assert.Throws<ArgumentNullException>(() => PdfProofDocument.Load(new MemoryStream(pdf), null!));
        Assert.Throws<ArgumentNullException>(() => PdfProofDocument.Load(Path.GetTempPath(), null!));
    }

    private static byte[] PdfWithProducer(string producer)
    {
        var pdf = new RawPdf { Info = $"<< /Producer ({producer}) >>" };
        pdf.AddPage(RawPdf.Text(50, 750, "Hello"));
        return pdf.Build();
    }
}
