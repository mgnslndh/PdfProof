namespace PdfProof.FixtureGenerator;

// A corpus document written with the C# PDF libraries, matching its HTML and Typst sources.
internal interface ICSharpDocument
{
    byte[] QuestPdf(bool tagged);

    byte[] MigraDoc();
}

internal static class CSharpDocuments
{
    public static ICSharpDocument For(CorpusDocument document) => document.Name switch
    {
        "invoice" => new InvoiceDocument(),
        _ => throw new InvalidOperationException(
            $"There is no C# version of the corpus document '{document.Name}'. Add one in tools/PdfProof.FixtureGenerator/Documents/."),
    };
}
