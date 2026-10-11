namespace PdfProof.FixtureGenerator.Tests;

// A fresh folder under the temp path, deleted when the test ends.
internal sealed class TempFolder : IDisposable
{
    public string Path { get; } = Directory.CreateDirectory(
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"pdfproof-generator-{Guid.NewGuid():N}")).FullName;

    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
