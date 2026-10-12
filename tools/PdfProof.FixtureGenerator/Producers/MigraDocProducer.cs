namespace PdfProof.FixtureGenerator;

// MigraDoc on PDFsharp 6. Untagged: PDFsharp doesn't write a structure tree.
internal sealed class MigraDocProducer : IProducer
{
    public string Name => "migradoc";

    public bool BestEffort => false;

    public string Version => "PDFsharp-MigraDoc " + Versions.Of(typeof(MigraDoc.DocumentObjectModel.Document));

    public Task<string?> CheckAsync() => Task.FromResult(MigraDocFonts.Configure());

    public Task<byte[]> RenderAsync(CorpusDocument document) => Task.FromResult(CSharpDocuments.For(document).MigraDoc());
}
