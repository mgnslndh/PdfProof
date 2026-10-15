namespace PdfProof.Corpus.Tests;

// The corpus matrix (spec §10.2): every generated document × every producer, checked against the document's oracle.
// A case passes, is a known deviation (deviations.json, reason printed), or fails.
public sealed class GeneratedCorpusTests
{
    public static TheoryData<string, string> Cases()
    {
        var cases = new TheoryData<string, string>();
        foreach (var document in Documents())
        {
            foreach (var producer in CorpusProducer.All)
            {
                cases.Add(document, producer.Name);
            }
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Every_producer_matches_the_oracle_or_a_known_deviation(string document, string producerName)
    {
        var producer = CorpusProducer.All.Single(p => p.Name == producerName);
        var path = Fixtures.Require(CorpusPaths.Fixture(document, producer.Name), "Run ./build.ps1 --target=GenerateFixtures.", producer.BestEffort);
        var sources = CorpusPaths.GeneratedDocument(document);

        var mismatches = new List<string>();
        var pdf = Fixtures.TryLoad(path, out var loadError);
        if (pdf is not null)
        {
            if (pdf.Diagnostics.Profile.Name != producer.ExpectedProfile)
            {
                mismatches.Add($"profile: expected {producer.ExpectedProfile}, was {pdf.Diagnostics.Profile.Name}");
            }

            if (pdf.Structure.IsTagged != producer.Tagged)
            {
                mismatches.Add($"tagged: expected {producer.Tagged}, was {pdf.Structure.IsTagged}");
            }

            var oracle = Oracle.Load(Path.Combine(sources, "expected.semantics.json"));
            mismatches.AddRange(OracleComparison.Compare(oracle, pdf));
        }

        var verdict = MatrixVerdict.Of(producer.Name, mismatches, Deviations.Load(Path.Combine(sources, "deviations.json")), loadError);
        TestContext.Current.TestOutputHelper?.WriteLine(verdict.Message);
        Assert.True(verdict.Outcome != MatrixOutcome.Fail, verdict.Message);
    }

    [Fact]
    public void Every_deviation_names_a_corpus_producer()
    {
        foreach (var document in Documents())
        {
            foreach (var producer in Deviations.Load(Path.Combine(CorpusPaths.GeneratedDocument(document), "deviations.json")).Keys)
            {
                Assert.True(CorpusProducer.All.Any(p => p.Name == producer), $"{document}/deviations.json lists \"{producer}\", which is not a corpus producer.");
            }
        }
    }

    private static IEnumerable<string> Documents() =>
        Directory.GetDirectories(CorpusPaths.GeneratedSources)
            .Where(folder => File.Exists(Path.Combine(folder, "expected.semantics.json")))
            .Select(folder => Path.GetFileName(folder))
            .OrderBy(name => name, StringComparer.Ordinal);
}
