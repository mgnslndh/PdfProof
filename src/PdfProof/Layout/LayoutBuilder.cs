using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using UglyToad.PdfPig.Util;

namespace PdfProof.Layout;

// Builds the layout model page by page; a page's letters are dropped once its model is built. Every call into
// PdfPig goes through PdfPigCall.
internal static class LayoutBuilder
{
    public static LayoutModel Build(PdfDocument document, LayoutOptions options, DiagnosticsCollector diagnostics)
    {
        var pageCount = PdfPigCall.Read(() => document.NumberOfPages);
        var pages = new List<LayoutPage>(pageCount);
        for (var number = 1; number <= pageCount; number++)
        {
            var pageNumber = number;
            var page = PdfPigCall.Read(() => document.GetPage(pageNumber), pageNumber);
            pages.Add(BuildPage(page, pageNumber, options, diagnostics));
        }

        return new LayoutModel(pages);
    }

    private static LayoutPage BuildPage(Page page, int number, LayoutOptions options, DiagnosticsCollector diagnostics)
    {
        var letters = PdfPigCall.Read(() => page.Letters.ToList(), number);
        var horizontal = letters.Where(letter => letter.TextOrientation == TextOrientation.Horizontal).ToList();
        var words = Words(PdfPigCall.Read(() => DefaultWordExtractor.Instance.GetWords(horizontal).ToList(), number), options);

        var strategy = options.Strategy;
        var blocks = Blocks(strategy.Arrange(words, options), options);

        // Rotated text: the nearest-neighbour extractor understands orientation. The default extractor shreds
        // vertical text into single letters that get mixed into horizontal rows.
        var rotated = letters.Where(letter => letter.TextOrientation != TextOrientation.Horizontal).ToList();
        if (rotated.Count > 0)
        {
            var rotatedWords = Words(PdfPigCall.Read(() => NearestNeighbourWordExtractor.Instance.GetWords(rotated).ToList(), number), options);
            blocks.AddRange(RotatedText.Blocks(rotatedWords, options));
        }

        diagnostics.Add(
            PdfDiagnosticCodes.Layout,
            number,
            $"{blocks.Count} blocks, {blocks.Sum(block => block.Lines.Count)} lines ({strategy.Name} layout)");

        return new LayoutPage(
            number,
            Geometry.Round(page.Width, options.Decimals),
            Geometry.Round(page.Height, options.Decimals),
            page.Rotation.Value,
            blocks);
    }

    private static List<LayoutWord> Words(IEnumerable<Word> words, LayoutOptions options)
    {
        var result = new List<LayoutWord>();
        foreach (var word in words)
        {
            if (WordFactory.Create(word, options) is { } layoutWord)
            {
                result.Add(layoutWord);
            }
        }

        return result;
    }

    // Turns a strategy's word blocks into model blocks; empty lines and blocks are dropped.
    private static List<LayoutBlock> Blocks(IReadOnlyList<LayoutWordBlock> wordBlocks, LayoutOptions options) =>
        wordBlocks
            .Select(block => block.Lines.Where(line => line.Words.Count > 0).Select(line => LineFactory.Line(line.Words, options)).ToList())
            .Where(lines => lines.Count > 0)
            .Select(lines => LineFactory.Block(lines, LayoutOrientation.Horizontal))
            .ToList();
}
