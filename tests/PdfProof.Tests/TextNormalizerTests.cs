using PdfProof.Layout;

namespace PdfProof.Tests;

// Spec §5.2: soft and Unicode hyphens become '-', special spaces a space, ligatures are expanded, zero-width
// characters removed. SelectPdf extracts every '-' as U+00AD, so without this "e-mail" never matches.
public sealed class TextNormalizerTests
{
    [Theory]
    [InlineData("e\u00ADmail", "e-mail")]
    [InlineData("e\u2010mail", "e-mail")]
    [InlineData("e\u2011mail", "e-mail")]
    public void Soft_and_unicode_hyphens_become_a_hyphen_minus(string input, string expected) =>
        Assert.Equal(expected, TextNormalizer.Normalize(input));

    [Theory]
    [InlineData("9\u00A0900,00")]
    [InlineData("9\u2007900,00")]
    [InlineData("9\u202F900,00")]
    [InlineData("9\u2009900,00")]
    [InlineData("9\u200A900,00")]
    [InlineData("9\u3000900,00")]
    public void Special_spaces_become_a_space(string input) =>
        Assert.Equal("9 900,00", TextNormalizer.Normalize(input));

    [Fact]
    public void Zero_width_characters_are_removed() =>
        Assert.Equal("Invoice", TextNormalizer.Normalize("In\u200Bvo\u200C\u200Di\u2060ce\uFEFF"));

    [Fact]
    public void Latin_ligatures_are_expanded() =>
        Assert.Equal("office flow ffl", TextNormalizer.Normalize("o\uFB03ce \uFB02ow \uFB04"));

    [Fact]
    public void Ordinary_text_is_returned_unchanged() =>
        Assert.Equal("Total: 25 634,50 SEK – å ä ö", TextNormalizer.Normalize("Total: 25 634,50 SEK – å ä ö"));
}
