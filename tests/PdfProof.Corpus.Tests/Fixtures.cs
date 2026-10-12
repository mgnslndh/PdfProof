namespace PdfProof.Corpus.Tests;

// Fixtures are made by the build (GenerateFixtures, FetchCorpus), not committed. A missing one skips its test with a
// hint, or fails it when PDFPROOF_REQUIRE_FIXTURES=true (always set in CI). Best-effort producers only ever skip.
internal static class Fixtures
{
    public static bool Required => Environment.GetEnvironmentVariable("PDFPROOF_REQUIRE_FIXTURES") == "true";

    public static string Require(string path, string howToGet, bool bestEffort = false)
    {
        if (File.Exists(path))
        {
            return path;
        }

        var message = $"Missing {path}. {howToGet}";
        if (Required && !bestEffort)
        {
            Assert.Fail(message);
        }

        Assert.Skip(message);
        return path;
    }

    // A fixture PdfProof can't read gives the error instead of a document, so the case fails with the file and the
    // error instead of crashing (MatrixVerdict fails it even when the producer has a known deviation).
    public static PdfProofDocument? TryLoad(string path, out string? error)
    {
        try
        {
            error = null;
            return PdfProofDocument.Load(path);
        }
        catch (PdfProofException ex)
        {
            error = $"{path} could not be loaded: {ex.Message}";
            return null;
        }
    }
}
