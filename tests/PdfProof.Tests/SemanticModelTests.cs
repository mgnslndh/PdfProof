using PdfProof.Semantics;

namespace PdfProof.Tests;

public sealed class SemanticModelTests
{
    [Fact]
    public void Every_document_has_a_semantic_model_whose_source_follows_the_tags()
    {
        var untagged = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 750, "Hello")));

        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("P", 0, RawPdf.Text(50, 750, "Hello")));
        pdf.Tag($"{pdf.Add($"<< /S /P /Pg {page} 0 R /K 0 >>")} 0 R");
        var tagged = PdfProofDocument.Load(pdf.Build());

        Assert.Equal(SemanticSource.Inferred, untagged.Semantics.Source);
        Assert.Equal(SemanticSource.Declared, tagged.Semantics.Source);
    }

    [Fact]
    public void The_model_collections_are_read_only()
    {
        var semantics = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 750, "Hello"))).Semantics;

        Assert.Throws<NotSupportedException>(() => ((IList<SemanticBlock>)semantics.Blocks).Add(null!));
        Assert.Throws<NotSupportedException>(() => ((IList<SemanticArtifact>)semantics.Artifacts).Add(null!));
        Assert.Throws<NotSupportedException>(() => ((IList<SemanticDecision>)semantics.Decisions).Add(null!));
    }

    [Fact]
    public void Semantic_options_have_the_spec_defaults_and_belong_to_the_profile()
    {
        var options = SemanticOptions.Default;

        Assert.Same(options, PdfProfile.Default.Semantics);
        Assert.Equal((SemanticSourceOption.Auto, 1.0, 0.8), (options.Source, options.ParagraphGapFactor, options.ShortLineRatio));
        Assert.Equal((true, 0.1, 0.5), (options.Artifacts.Enabled, options.Artifacts.BandRatio, options.Artifacts.MinPageFraction));
        Assert.NotEmpty(options.Artifacts.PageNumberPatterns);
        Assert.Equal((true, 1.15, 2, true), (options.Headings.Enabled, options.Headings.MinSizeRatio, options.Headings.MaxLines, options.Headings.BoldShortLineIsHeading));
        Assert.Equal((true, 2, 3.0), (options.Tables.Enabled, options.Tables.MinRows, options.Tables.ColumnTolerance));
        Assert.Equal(["•", "◦", "▪", "–", "*", "-"], options.Lists.BulletGlyphs);
        Assert.Equal((true, true, 3.0), (options.Lists.Enabled, options.Lists.UseMarkerShapes, options.Lists.IndentTolerance));
        Assert.True(options.Links.Enabled);
    }

    [Fact]
    public void Invalid_semantic_options_are_rejected()
    {
        Assert.Equal("ParagraphGapFactor", Assert.Throws<ArgumentOutOfRangeException>(() => SemanticOptions.Default with { ParagraphGapFactor = -1 }).ParamName);
        Assert.Equal("ShortLineRatio", Assert.Throws<ArgumentOutOfRangeException>(() => SemanticOptions.Default with { ShortLineRatio = 1.5 }).ParamName);
        Assert.Equal("Artifacts", Assert.Throws<ArgumentNullException>(() => SemanticOptions.Default with { Artifacts = null! }).ParamName);
        Assert.Equal("PageNumberPatterns", Assert.Throws<ArgumentException>(() => ArtifactDetectionOptions.Default with { PageNumberPatterns = ["("] }).ParamName);
        Assert.Equal("MinRows", Assert.Throws<ArgumentOutOfRangeException>(() => TableDetectionOptions.Default with { MinRows = 1 }).ParamName);
        Assert.Equal("BulletGlyphs", Assert.Throws<ArgumentException>(() => ListDetectionOptions.Default with { BulletGlyphs = [""] }).ParamName);
    }

    [Fact]
    public void Option_lists_are_copied()
    {
        var glyphs = new List<string> { "•" };
        var options = ListDetectionOptions.Default with { BulletGlyphs = glyphs };
        glyphs.Add("-");

        Assert.Equal(["•"], options.BulletGlyphs);
    }
}
