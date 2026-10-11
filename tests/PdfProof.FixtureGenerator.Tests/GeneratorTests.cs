using System.Text;

namespace PdfProof.FixtureGenerator.Tests;

public sealed class GeneratorTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly StringWriter _log = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task Each_fixture_and_a_manifest_are_written()
    {
        var exit = await Run(new FakeProducer("alpha"), new FakeProducer("beta", unavailable: "not here"));

        Assert.Equal(0, exit);
        Assert.Equal("%PDF invoice alpha", File.ReadAllText(_temp.Combine("out", "invoice", "alpha.pdf")));
        Assert.False(File.Exists(_temp.Combine("out", "invoice", "beta.pdf")));
        Assert.Contains("warning: beta is unavailable: not here", _log.ToString());
        Assert.Equal(
            """
            {
              "documents": [
                "invoice"
              ],
              "producers": [
                {
                  "name": "alpha",
                  "version": "1.0",
                  "status": "generated"
                },
                {
                  "name": "beta",
                  "status": "unavailable",
                  "reason": "not here"
                }
              ]
            }

            """.Replace("\r\n", "\n"),
            File.ReadAllText(_temp.Combine("out", "manifest.json")));
    }

    [Fact]
    public async Task A_failing_producer_fails_the_run_and_is_recorded()
    {
        var exit = await Run(new FakeProducer("alpha", fails: true));

        Assert.Equal(1, exit);
        Assert.Contains("\"status\": \"failed\"", File.ReadAllText(_temp.Combine("out", "manifest.json")));
        Assert.Contains("\"reason\": \"invoice: boom\"", File.ReadAllText(_temp.Combine("out", "manifest.json")));
        Assert.Contains("error: alpha failed on invoice: boom", _log.ToString());
    }

    [Fact]
    public async Task A_failing_best_effort_producer_is_only_a_warning()
    {
        var exit = await Run(new FakeProducer("alpha", fails: true, bestEffort: true));

        Assert.Equal(0, exit);
        Assert.Contains("warning: alpha failed on invoice: boom", _log.ToString());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task A_previous_fixture_never_stands_in_for_a_producer_that_is_unavailable_or_fails(bool unavailable, bool fails)
    {
        // Review Focus 4.
        Directory.CreateDirectory(_temp.Combine("out", "invoice"));
        File.WriteAllText(_temp.Combine("out", "invoice", "alpha.pdf"), "stale");

        await Run(new FakeProducer("alpha", unavailable: unavailable ? "gone" : null, fails: fails));

        Assert.False(File.Exists(_temp.Combine("out", "invoice", "alpha.pdf")));
    }

    [Fact]
    public void Producers_are_selected_by_name_in_name_order_and_unknown_names_are_reported()
    {
        IReadOnlyList<IProducer> all = [new FakeProducer("beta"), new FakeProducer("alpha"), new FakeProducer("gamma")];

        var selected = Producers.Select(all, ["gamma", "alpha", "delta"], out var unknown);

        Assert.Equal(["alpha", "gamma"], selected.Select(p => p.Name));
        Assert.Equal(["delta"], unknown);
        Assert.Equal(["alpha", "beta", "gamma"], Producers.Select(all, null, out _).Select(p => p.Name));
    }

    [Fact]
    public void Documents_are_the_folders_with_an_oracle_in_name_order()
    {
        foreach (var name in new[] { "report", "invoice", "draft" })
        {
            Directory.CreateDirectory(_temp.Combine("src", name));
        }

        File.WriteAllText(_temp.Combine("src", "report", "expected.semantics.json"), "{}");
        File.WriteAllText(_temp.Combine("src", "invoice", "expected.semantics.json"), "{}");

        var documents = CorpusDocument.Find(_temp.Combine("src"));

        Assert.Equal(["invoice", "report"], documents.Select(d => d.Name));
        Assert.Equal(_temp.Combine("src", "invoice", "invoice.html"), documents[0].HtmlPath);
        Assert.Equal(_temp.Combine("src", "invoice", "invoice.typ"), documents[0].TypstPath);
    }

    private Task<int> Run(params IProducer[] producers) =>
        new Generator(producers, _log).RunAsync([new CorpusDocument("invoice", _temp.Combine("src", "invoice"))], _temp.Combine("out"));

    private sealed class FakeProducer(string name, string? unavailable = null, bool fails = false, bool bestEffort = false) : IProducer
    {
        public string Name => name;

        public bool BestEffort => bestEffort;

        public string Version => "1.0";

        public Task<string?> CheckAsync() => Task.FromResult(unavailable);

        public Task<byte[]> RenderAsync(CorpusDocument document) =>
            fails ? throw new InvalidOperationException("boom") : Task.FromResult(Encoding.ASCII.GetBytes($"%PDF {document.Name} {name}"));
    }
}
