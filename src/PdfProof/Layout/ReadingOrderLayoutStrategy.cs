using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;

namespace PdfProof.Layout;

// PdfPig's Docstrum segmentation into blocks, then unsupervised reading-order detection. Handles multi-column
// pages; heuristic, so a small content change can regroup blocks.
internal sealed class ReadingOrderLayoutStrategy : ILayoutStrategy
{
    public string Name => "ReadingOrder";

    public IReadOnlyList<LayoutWordBlock> Arrange(IReadOnlyList<LayoutWord> words, LayoutOptions options)
    {
        if (words.Count == 0)
        {
            return [];
        }

        var byWord = new Dictionary<Word, LayoutWord>();
        foreach (var word in words)
        {
            byWord[word.Source] = word;
        }

        var blocks = DocstrumBoundingBoxes.Instance.GetBlocks(words.Select(word => word.Source));
        return UnsupervisedReadingOrderDetector.Instance.Get(blocks)
            .Select(block => new LayoutWordBlock(block.TextLines
                .Select(line => new LayoutWordLine(line.Words.Select(word => Find(word, byWord, options)).OfType<LayoutWord>().ToList()))
                .ToList()))
            .ToList();
    }

    // Docstrum normally returns the words it was given; if it ever rebuilds one, convert it again.
    private static LayoutWord? Find(Word word, Dictionary<Word, LayoutWord> byWord, LayoutOptions options) =>
        byWord.TryGetValue(word, out var layoutWord) ? layoutWord : WordFactory.Create(word, options);
}
