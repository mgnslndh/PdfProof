using System.ComponentModel;

namespace PdfProof.FixtureGenerator;

// The Typst CLI. The build passes the pinned CLI it downloaded (--typst); without it, "typst" on PATH is used.
internal sealed class TypstProducer(string executable) : IProducer
{
    public string Name => "typst";

    public bool BestEffort => false;

    public string Version { get; private set; } = "";

    public async Task<string?> CheckAsync()
    {
        try
        {
            var (exit, output, error) = await ExternalProcess.RunAsync(executable, ["--version"]);
            if (exit != 0)
            {
                return $"the Typst CLI '{executable}' failed: {error.Trim()}";
            }

            Version = output.Trim();
            return null;
        }
        catch (Win32Exception ex)
        {
            return $"the Typst CLI '{executable}' was not found ({ex.Message}). Run ./build.ps1 --target=GenerateFixtures, which downloads the pinned version, or pass --typst <path>.";
        }
    }

    public async Task<byte[]> RenderAsync(CorpusDocument document)
    {
        var output = Path.Combine(Path.GetTempPath(), $"pdfproof-typst-{Guid.NewGuid():N}.pdf");
        try
        {
            var (exit, _, error) = await ExternalProcess.RunAsync(executable, ["compile", document.TypstPath, output], document.Directory);
            if (exit != 0)
            {
                throw new InvalidOperationException($"typst compile failed: {error.Trim()}");
            }

            return await File.ReadAllBytesAsync(output);
        }
        finally
        {
            if (File.Exists(output))
            {
                File.Delete(output);
            }
        }
    }
}
