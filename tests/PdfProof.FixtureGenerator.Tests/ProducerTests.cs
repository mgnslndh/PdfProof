namespace PdfProof.FixtureGenerator.Tests;

public sealed class ProducerTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void The_producers_have_the_names_the_corpus_tests_expect()
    {
        Assert.Equal(
            ["chromium-tagged", "chromium-untagged", "libreoffice", "migradoc", "questpdf-tagged", "questpdf-untagged", "selectpdf", "typst"],
            Producers.Select(Producers.All("typst"), null, out _).Select(p => p.Name));
    }

    [Fact]
    public async Task Typst_is_unavailable_when_its_cli_is_missing()
    {
        var reason = await new TypstProducer(_temp.Combine("no-typst")).CheckAsync();

        Assert.NotNull(reason);
        Assert.StartsWith("the Typst CLI", reason);
    }

    [Fact]
    public async Task LibreOffice_is_unavailable_when_not_installed_and_is_best_effort()
    {
        var missing = _temp.Combine("soffice");
        var producer = new LibreOfficeProducer([missing]);

        Assert.True(producer.BestEffort);
        Assert.Equal($"LibreOffice was not found (looked in: {missing})", await producer.CheckAsync());
    }

    [Fact]
    public async Task SelectPdf_runs_on_Windows_only()
    {
        Assert.Equal(OperatingSystem.IsWindows(), await new SelectPdfProducer().CheckAsync() is null);
    }

    [Fact]
    public async Task An_external_process_reports_its_exit_code_and_output()
    {
        var (exitCode, output, _) = await ExternalProcess.RunAsync("dotnet", ["--version"]);

        Assert.Equal(0, exitCode);
        Assert.Matches(@"^\d+\.\d+\.\d+", output.Trim());
    }

    [Fact]
    public void Versions_are_the_package_versions_the_manifest_should_show()
    {
        // Playwright's informational version is a placeholder (1.0.0+commit); its file version is the package version.
        Assert.Equal("1.63.0", Versions.Of(typeof(Microsoft.Playwright.IPlaywright)));
        Assert.Equal("26.4.0", Versions.Of(typeof(SelectPdf.HtmlToPdf)));
    }

    [Fact]
    public async Task QuestPdf_renders_the_invoice_and_tags_it_on_request()
    {
        var invoice = new CorpusDocument("invoice", _temp.Path);

        var tagged = await Rendered(new QuestPdfProducer(tagged: true), invoice);
        var untagged = await Rendered(new QuestPdfProducer(tagged: false), invoice);

        Assert.Contains("/StructTreeRoot", tagged);
        Assert.DoesNotContain("/StructTreeRoot", untagged);
    }

    [Fact]
    public async Task MigraDoc_renders_the_invoice_when_fonts_are_available()
    {
        var producer = new MigraDocProducer();
        if (await producer.CheckAsync() is { } reason)
        {
            Assert.Skip(reason);
        }

        Assert.StartsWith("%PDF-", await Rendered(producer, new CorpusDocument("invoice", _temp.Path)));
    }

    [Fact]
    public void A_document_without_a_CSharp_version_is_reported()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => CSharpDocuments.For(new CorpusDocument("letter", _temp.Path)));

        Assert.Equal("There is no C# version of the corpus document 'letter'. Add one in tools/PdfProof.FixtureGenerator/Documents/.", ex.Message);
    }

    // The PDF as Latin-1 text, so tests can look for PDF keywords in it.
    private static async Task<string> Rendered(IProducer producer, CorpusDocument document)
    {
        Assert.Null(await producer.CheckAsync());
        return System.Text.Encoding.Latin1.GetString(await producer.RenderAsync(document));
    }
}
