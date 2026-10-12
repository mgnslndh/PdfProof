namespace PdfProof.FixtureGenerator;

// One way of turning a corpus document into a PDF. Name is the fixture's file name: <document>/<Name>.pdf.
internal interface IProducer
{
    string Name { get; }

    // Best-effort producers (LibreOffice) never fail the run (spec §10.2).
    bool BestEffort { get; }

    // The producer's version for the manifest; valid after CheckAsync returned null.
    string Version { get; }

    // Null when the producer can run on this machine; otherwise why not. Called once, before rendering.
    Task<string?> CheckAsync();

    Task<byte[]> RenderAsync(CorpusDocument document);
}
