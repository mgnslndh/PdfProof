namespace PdfProof.FixtureGenerator;

internal static class Producers
{
    // Every producer. The corpus tests list the same names (CorpusProducer.All); Task 6 adds the C# producers.
    public static IReadOnlyList<IProducer> All(string typst) =>
    [
        new ChromiumProducer(tagged: false),
        new ChromiumProducer(tagged: true),
        new LibreOfficeProducer(LibreOfficeProducer.DefaultCandidates()),
        new SelectPdfProducer(),
        new TypstProducer(typst),
    ];

    // The producers named (all when `names` is null), in name order; names that match no producer go to `unknown`.
    public static IReadOnlyList<IProducer> Select(IReadOnlyList<IProducer> all, IReadOnlyList<string>? names, out IReadOnlyList<string> unknown)
    {
        unknown = names is null ? [] : names.Where(name => all.All(producer => producer.Name != name)).ToList();
        return all
            .Where(producer => names is null || names.Contains(producer.Name))
            .OrderBy(producer => producer.Name, StringComparer.Ordinal)
            .ToList();
    }
}
