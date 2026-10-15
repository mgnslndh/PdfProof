using PdfProof.Layout;

namespace PdfProof.Semantics;

// Spec §6, pass 6. A link becomes a link run on the text it covers. Untagged: each layout link (in the page's link
// order, top to bottom then left to right) goes to the first candidate overlapping its rectangle whose cell under the
// rectangle contains the link's text at a still-unlinked position; the layout already gives a link the text of the
// words under it. Tagged: links the structure front end couldn't place (annotations on a P, as QuestPDF writes them)
// go to the first unlinked occurrence of their text in their element.
internal static class LinkAttacher
{
    public static void Run(SemanticContext context)
    {
        foreach (var candidate in context.Candidates.Where(c => c.Kind is CandidateKind.Unknown or CandidateKind.Paragraph or CandidateKind.Heading && c.Lines.Count > 1))
        {
            // One line, so a link that wraps from one line to the next still matches.
            candidate.Lines = [new CandidateLine(
                [new CandidateCell(Finalizer.Runs(candidate), candidate.Box)],
                candidate.Box,
                candidate.Lines[0].Size,
                candidate.Lines.All(line => line.IsBold))];
        }

        foreach (var page in context.Layout.Pages)
        {
            var onPage = context.Candidates.Where(c => !c.Declared && c.Page == page.Number).ToList();
            foreach (var link in page.Links)
            {
                Place(context, page.Number, link, onPage.Where(c => Overlaps(c.Box, link.Box)), useBoxes: true);
            }
        }

        foreach (var candidate in context.Candidates.Where(c => c.Declared))
        {
            foreach (var link in candidate.PendingLinks)
            {
                Place(context, candidate.Page, link, [candidate], useBoxes: false);
            }
        }
    }

    private static void Place(SemanticContext context, int page, LayoutLink link, IEnumerable<Candidate> candidates, bool useBoxes)
    {
        var target = new SemanticLink(link.Uri, link.TargetPage);
        var text = link.Text.Trim();
        if (text.Length == 0)
        {
            context.Decide(SemanticPass.LinkAttachment, page, null, $"a link to {target} covers no text and is left out");
            return;
        }

        foreach (var candidate in candidates)
        {
            if (Attach(candidate, link, text, target, useBoxes))
            {
                context.Decide(SemanticPass.LinkAttachment, page, candidate, $"\"{SemanticText.Excerpt(text)}\" → {target}");
                return;
            }
        }

        context.Decide(SemanticPass.LinkAttachment, page, null, $"\"{SemanticText.Excerpt(text)}\" → {target}: the text was not found in a block and the link is left out");
    }

    private static bool Attach(Candidate candidate, LayoutLink link, string text, SemanticLink target, bool useBoxes)
    {
        var lines = candidate.Lines.ToList();
        for (var l = 0; l < lines.Count; l++)
        {
            var cells = lines[l].Cells.ToList();
            for (var c = 0; c < cells.Count; c++)
            {
                if (useBoxes && cells[c].Box != default && !Overlaps(cells[c].Box, link.Box))
                {
                    continue;
                }

                var at = Unlinked(cells[c].Runs, text);
                if (at >= 0)
                {
                    cells[c] = cells[c] with { Runs = SemanticText.Link(cells[c].Runs, at, text.Length, target) };
                    lines[l] = lines[l] with { Cells = cells };
                    candidate.Lines = lines;
                    return true;
                }
            }
        }

        return false;
    }

    // The first position of `text` whose characters are all in runs without a link, or -1.
    private static int Unlinked(IReadOnlyList<SemanticRun> runs, string text)
    {
        var joined = SemanticText.Join(runs);
        var linked = new bool[joined.Length];
        var offset = 0;
        foreach (var run in runs)
        {
            for (var i = 0; i < run.Text.Length; i++)
            {
                linked[offset + i] = run.Link is not null;
            }

            offset += run.Text.Length;
        }

        for (var at = joined.IndexOf(text, StringComparison.Ordinal); at >= 0; at = joined.IndexOf(text, at + 1, StringComparison.Ordinal))
        {
            var free = true;
            for (var i = at; i < at + text.Length && free; i++)
            {
                free = !linked[i];
            }

            if (free)
            {
                return at;
            }
        }

        return -1;
    }

    private static bool Overlaps(LayoutBox a, LayoutBox b) =>
        a.Left < b.Right && a.Right > b.Left && a.Bottom < b.Top && a.Top > b.Bottom;
}
