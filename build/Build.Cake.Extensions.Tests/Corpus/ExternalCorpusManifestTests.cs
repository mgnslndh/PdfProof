using Build.Cake.Extensions.Corpus;

namespace Build.Cake.Extensions.Tests.Corpus;

public sealed class ExternalCorpusManifestTests
{
    private const string Sha = "2d420cbb4123dcf1fb82595b2359cfbb5d81f00b9df9d359fcc7af361d093f53";

    [Fact]
    public void Entries_are_read_with_every_field()
    {
        var entry = Assert.Single(ExternalCorpusManifest.Parse(Json("sample-form", Sha, "Public domain")));

        Assert.Equal(
            new ExternalCorpusEntry("sample-form", new Uri("https://example.com/form.pdf"), Sha, "Public domain", "Example source", "A form"),
            entry);
    }

    [Theory]
    [InlineData("sample-form", Sha, "", "Entry 1 has no \"license\".")]
    [InlineData("Sample Form", Sha, "Public domain", "Entry \"Sample Form\": the id must be lowercase letters, digits and hyphens.")]
    [InlineData("sample-form", "ABC", "Public domain", "Entry \"sample-form\": sha256 must be 64 lowercase hex digits.")]
    public void Invalid_entries_are_rejected(string id, string sha, string license, string expected)
    {
        var ex = Assert.Throws<InvalidDataException>(() => ExternalCorpusManifest.Parse(Json(id, sha, license)));

        Assert.Equal(expected, ex.Message);
    }

    [Fact]
    public void An_id_listed_twice_is_rejected()
    {
        var one = Json("sample-form", Sha, "Public domain").Trim().TrimStart('[').TrimEnd(']');

        var ex = Assert.Throws<InvalidDataException>(() => ExternalCorpusManifest.Parse($"[{one},{one}]"));

        Assert.Equal("Entry \"sample-form\" is listed twice.", ex.Message);
    }

    private static string Json(string id, string sha, string license) =>
        $$"""
        [{ "id": "{{id}}", "url": "https://example.com/form.pdf", "sha256": "{{sha}}", "license": "{{license}}",
           "source": "Example source", "purpose": "A form" }]
        """;
}
