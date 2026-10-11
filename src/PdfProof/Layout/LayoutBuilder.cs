using UglyToad.PdfPig;
using UglyToad.PdfPig.Actions;
using UglyToad.PdfPig.Annotations;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using UglyToad.PdfPig.Outline;
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

        var bookmarks = PdfPigCall.Read(() => document.TryGetBookmarks(out var outline)
            ? outline.Roots.Select(Bookmark).ToList()
            : new List<LayoutBookmark>());
        return new LayoutModel(pages, bookmarks);
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
        var rotatedWords = new List<LayoutWord>();
        if (rotated.Count > 0)
        {
            rotatedWords = Words(PdfPigCall.Read(() => NearestNeighbourWordExtractor.Instance.GetWords(rotated).ToList(), number), options);
            blocks.AddRange(RotatedText.Blocks(rotatedWords, options));
        }

        diagnostics.Add(
            PdfDiagnosticCodes.Layout,
            number,
            $"{blocks.Count} blocks, {blocks.Sum(block => block.Lines.Count)} lines ({strategy.Name} layout)");

        var annotations = PdfPigCall.Read(() => page.GetAnnotations().ToList(), number);
        var links = annotations
            .Where(annotation => annotation.Type == AnnotationType.Link)
            .Select(annotation => Link(annotation, words, rotatedWords, options))
            .OrderByDescending(link => link.Box.Top).ThenBy(link => link.Box.Left).ThenBy(link => link.Uri, StringComparer.Ordinal)
            .ToList();
        var otherAnnotations = annotations
            .Where(annotation => annotation.Type != AnnotationType.Link && annotation.Type != AnnotationType.Widget)
            .Select(annotation => new LayoutAnnotation(annotation.Type.ToString(), annotation.Content, Geometry.Round(Geometry.Bounds(annotation.Rectangle), options.Decimals)))
            .OrderByDescending(annotation => annotation.Box.Top).ThenBy(annotation => annotation.Box.Left).ThenBy(annotation => annotation.Type, StringComparer.Ordinal)
            .ToList();

        // PdfPig clips letters but not images: at least drop images drawn entirely outside the visible page.
        var visible = page.CropBox.Bounds;
        var images = PdfPigCall.Read(
                () => page.GetImages().Select(image => (Bounds: image.BoundingBox, image.WidthInSamples, image.HeightInSamples, Data: image.RawMemory.ToArray())).ToList(),
                number)
            .Where(image => !options.ApplyClipping || Geometry.Intersects(image.Bounds, visible))
            .Select(image => new LayoutImage(Geometry.Round(Geometry.Bounds(image.Bounds), options.Decimals), image.WidthInSamples, image.HeightInSamples, Geometry.Sha256Prefix(image.Data)))
            .OrderByDescending(image => image.Box.Top).ThenBy(image => image.Box.Left)
            .ToList();

        return new LayoutPage(
            number,
            Geometry.Round(page.Width, options.Decimals),
            Geometry.Round(page.Height, options.Decimals),
            page.Rotation.Value,
            blocks,
            links,
            images,
            otherAnnotations);
    }

    private static LayoutLink Link(Annotation annotation, IReadOnlyList<LayoutWord> words, IReadOnlyList<LayoutWord> rotatedWords, LayoutOptions options)
    {
        string? uri = null;
        int? targetPage = null;
        switch (annotation.Action)
        {
            case UriAction uriAction:
                uri = uriAction.Uri;
                break;
            case AbstractGoToAction goTo:
                targetPage = goTo.Destination?.PageNumber;
                break;
        }

        // The text under the link is the words whose centre lies inside the link rectangle, in reading order and
        // separated by spaces, so it reads like the page text: horizontal words row by row from the top, left to
        // right, then rotated words.
        var rectangle = annotation.Rectangle;
        bool Inside(LayoutWord word) =>
            Geometry.Contains(rectangle, new PdfPoint((word.Box.Left + word.Box.Right) / 2, (word.Box.Bottom + word.Box.Top) / 2));
        var text = ReadingOrder(words.Where(Inside)).Concat(rotatedWords.Where(Inside)).Select(word => word.Text);

        return new LayoutLink(
            string.Join(" ", text),
            uri,
            targetPage,
            Geometry.Round(Geometry.Bounds(rectangle), options.Decimals));
    }

    // Rows by baseline from the top (same tolerance as the Rows strategy), each row left to right.
    private static IEnumerable<LayoutWord> ReadingOrder(IEnumerable<LayoutWord> words)
    {
        var rows = new List<List<LayoutWord>>();
        foreach (var word in words.OrderByDescending(w => w.Baseline).ThenBy(w => w.Box.Left))
        {
            var row = rows.Count > 0 ? rows[rows.Count - 1] : null;
            if (row is not null && Math.Abs(row[0].Baseline - word.Baseline) <= Math.Max(1.0, 0.3 * word.Size))
            {
                row.Add(word);
            }
            else
            {
                rows.Add([word]);
            }
        }

        return rows.SelectMany(row => row.OrderBy(word => word.Box.Left));
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

    private static LayoutBookmark Bookmark(BookmarkNode node) => new(
        node.Title,
        (node as DocumentBookmarkNode)?.PageNumber,
        (node as UriBookmarkNode)?.Uri,
        node.Children.Select(Bookmark).ToList());
}
