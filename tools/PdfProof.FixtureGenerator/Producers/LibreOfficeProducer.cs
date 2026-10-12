namespace PdfProof.FixtureGenerator;

// LibreOffice (HTML through Writer/Web to PDF). Best effort (spec §10.2): used when installed, never installed by the
// build, and a failure is only a warning.
internal sealed class LibreOfficeProducer(IReadOnlyList<string> candidates) : IProducer
{
    private string? _soffice;

    public string Name => "libreoffice";

    public bool BestEffort => true;

    public string Version { get; private set; } = "";

    // Where LibreOffice installs soffice by default.
    public static IReadOnlyList<string> DefaultCandidates()
    {
        if (OperatingSystem.IsWindows())
        {
            return
            [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LibreOffice", "program", "soffice.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LibreOffice", "program", "soffice.exe"),
            ];
        }

        return ["/usr/bin/soffice", "/usr/lib/libreoffice/program/soffice", "/Applications/LibreOffice.app/Contents/MacOS/soffice"];
    }

    public async Task<string?> CheckAsync()
    {
        _soffice = candidates.FirstOrDefault(File.Exists);
        if (_soffice is null)
        {
            return $"LibreOffice was not found (looked in: {string.Join(", ", candidates)})";
        }

        var (exit, output, _) = await ExternalProcess.RunAsync(_soffice, ["--version"]);
        Version = exit == 0 && output.Trim().Length > 0 ? output.Trim() : "LibreOffice (version unknown)";
        return null;
    }

    public async Task<byte[]> RenderAsync(CorpusDocument document)
    {
        var work = Directory.CreateTempSubdirectory("pdfproof-libreoffice-").FullName;
        try
        {
            // A private user profile, so a LibreOffice that is already running doesn't take over the conversion.
            var profile = new Uri(Path.Combine(work, "profile")).AbsoluteUri;
            var (exit, _, error) = await ExternalProcess.RunAsync(
                _soffice!,
                [$"-env:UserInstallation={profile}", "--headless", "--convert-to", "pdf", "--outdir", work, document.HtmlPath]);
            var pdf = Path.Combine(work, document.Name + ".pdf");
            if (exit != 0 || !File.Exists(pdf))
            {
                throw new InvalidOperationException($"soffice failed (exit code {exit}): {error.Trim()}");
            }

            return await File.ReadAllBytesAsync(pdf);
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }
}
