namespace PdfProof.FixtureGenerator;

// QuestPDF, untagged or tagged (PDF/UA-1). QuestPDF runs under its Community license, which covers this repository.
internal sealed class QuestPdfProducer(bool tagged) : IProducer
{
    public string Name => tagged ? "questpdf-tagged" : "questpdf-untagged";

    public bool BestEffort => false;

    public string Version => "QuestPDF " + Versions.Of(typeof(QuestPDF.Settings));

    public Task<string?> CheckAsync()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        return Task.FromResult<string?>(null);
    }

    public Task<byte[]> RenderAsync(CorpusDocument document) => Task.FromResult(CSharpDocuments.For(document).QuestPdf(tagged));
}
