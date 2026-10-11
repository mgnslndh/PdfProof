namespace PdfProof.Corpus.Tests;

public sealed class FixturesTests
{
    [Fact]
    public void A_fixture_that_is_not_a_readable_pdf_becomes_a_mismatch_naming_the_file()
    {
        // Review Focus 5: e.g. a truncated artifact download.
        var path = Path.Combine(Path.GetTempPath(), $"pdfproof-truncated-{Guid.NewGuid():N}.pdf");
        File.WriteAllText(path, "%PDF-1.7\n1 0 obj\n<<");
        try
        {
            var mismatches = new List<string>();

            Assert.Null(Fixtures.TryLoad(path, mismatches));

            Assert.StartsWith($"{path} could not be loaded: ", Assert.Single(mismatches));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Deviations_are_read_from_their_file_and_a_missing_file_means_none()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pdfproof-deviations-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{ "typst": "draws list bullets as text" }""");
        try
        {
            Assert.Equal("draws list bullets as text", Deviations.Load(path)["typst"]);
            Assert.Empty(Deviations.Load(path + ".missing"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
