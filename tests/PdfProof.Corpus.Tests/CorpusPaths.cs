namespace PdfProof.Corpus.Tests;

// Where the corpus lives: the sources and oracles in corpus/, the generated and downloaded PDFs in artifacts/ (written
// by the GenerateFixtures and FetchCorpus build targets, never committed).
internal static class CorpusPaths
{
    public static string RepositoryRoot { get; } = FindRoot();

    public static string GeneratedSources => Path.Combine(RepositoryRoot, "corpus", "generated");

    public static string GeneratedDocument(string document) => Path.Combine(GeneratedSources, document);

    public static string Fixture(string document, string producer) =>
        Path.Combine(RepositoryRoot, "artifacts", "corpus", document, producer + ".pdf");

    public static string External(string id) => Path.Combine(RepositoryRoot, "artifacts", "external", id + ".pdf");

    // The nearest folder above the test binaries that holds PdfProof.slnx.
    private static string FindRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "PdfProof.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException($"No PdfProof.slnx above {AppContext.BaseDirectory}.");
    }
}
