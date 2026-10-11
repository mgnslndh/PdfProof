namespace PdfProof.Corpus.Tests;

// Every producer of the generated corpus, under the names tools/PdfProof.FixtureGenerator uses, with what loading its
// output must show: tagging, and the profile detected from its /Producer and /Creator (probed in Plan 4).
internal sealed record CorpusProducer(string Name, bool Tagged, string ExpectedProfile, bool BestEffort = false)
{
    public static IReadOnlyList<CorpusProducer> All { get; } =
    [
        new("chromium-tagged", Tagged: true, "Chromium"),
        new("chromium-untagged", Tagged: false, "Chromium"),
        new("libreoffice", Tagged: false, "Default", BestEffort: true),
        new("migradoc", Tagged: false, "Default"),
        // QuestPDF writes no /Producer or /Creator, so it is never detected (spec §5.3).
        new("questpdf-tagged", Tagged: true, "Default"),
        new("questpdf-untagged", Tagged: false, "Default"),
        new("selectpdf", Tagged: false, "SelectPdf"),
        new("typst", Tagged: true, "Typst"),
    ];
}
