namespace PdfProof.FixtureGenerator.Tests;

public sealed class ProducerTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void The_producers_have_the_names_the_corpus_tests_expect()
    {
        Assert.Equal(
            ["chromium-tagged", "chromium-untagged", "libreoffice", "selectpdf", "typst"],
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
}
