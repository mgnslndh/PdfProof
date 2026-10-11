namespace PdfProof.FixtureGenerator;

internal static class Producers
{
    // Every producer. Tasks 5 and 6 add them; the corpus tests list the same names (CorpusProducer.All).
    public static IReadOnlyList<IProducer> All(string typst) => [];

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
