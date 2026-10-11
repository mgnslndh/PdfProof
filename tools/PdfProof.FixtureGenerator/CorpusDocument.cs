namespace PdfProof.FixtureGenerator;

// A logical document of the generated corpus: corpus/generated/<Name>/ holds its HTML and Typst sources and its oracle
// (expected.semantics.json); its C# versions live in Documents/.
internal sealed record CorpusDocument(string Name, string Directory)
{
    public string HtmlPath => Path.Combine(Directory, Name + ".html");

    public string TypstPath => Path.Combine(Directory, Name + ".typ");

    // The folders under `source` that hold an oracle, in name order (ordinal).
    public static IReadOnlyList<CorpusDocument> Find(string source) =>
        System.IO.Directory.GetDirectories(source)
            .Where(folder => File.Exists(Path.Combine(folder, "expected.semantics.json")))
            .Select(folder => new CorpusDocument(Path.GetFileName(folder), Path.GetFullPath(folder)))
            .OrderBy(document => document.Name, StringComparer.Ordinal)
            .ToList();
}
