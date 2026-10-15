using System.Text.RegularExpressions;
using PdfProof.Layout;

namespace PdfProof.Semantics;

// Spec §6, pass 5 (untagged documents; nesting comes in Plan 5b). Consecutive one-cell candidates on one page whose
// first line starts an item, at the same indentation, form a list. An item starts with a text bullet, a number, or a
// marker shape (a small path just left of the line, how HTML-to-PDF converters draw CSS bullets). Lines that don't
// start an item continue the one above. Bullets and numbers are left out of the item text.
internal static class ListDetector
{
    public static void Run(SemanticContext context)
    {
        var options = context.Options.Lists;
        var numbering = options.NumberingPatterns.Select(pattern => new Regex(pattern, RegexOptions.CultureInvariant)).ToList();
        var candidates = context.Candidates;

        (int Length, string How)? Start(CandidateLine line, int page)
        {
            var text = line.Text;
            foreach (var glyph in options.BulletGlyphs)
            {
                if (text.Length > glyph.Length && text.StartsWith(glyph, StringComparison.Ordinal))
                {
                    var next = text[glyph.Length];
                    if (glyph == "-" && char.IsDigit(next))
                    {
                        continue;
                    }

                    if (char.IsWhiteSpace(next) || char.IsLetter(next))
                    {
                        return (glyph.Length, $"bullet \"{glyph}\"");
                    }
                }
            }

            foreach (var pattern in numbering)
            {
                var match = pattern.Match(text);
                if (match.Success && match.Index == 0 && text.Length > match.Length && char.IsWhiteSpace(text[match.Length]))
                {
                    return (match.Length, "numbering");
                }
            }

            if (options.UseMarkerShapes && context.Build.Markers.TryGetValue(page, out var markers) && markers.Any(marker => Fits(marker, line)))
            {
                return (0, "marker shapes");
            }

            return null;
        }

        bool Eligible(Candidate candidate) => candidate.Kind == CandidateKind.Unknown && candidate.IsSingleCell && candidate.Lines.Count > 0;

        for (var i = 0; i < candidates.Count; i++)
        {
            var first = candidates[i];
            if (!Eligible(first) || Start(first.Lines[0], first.Page) is null)
            {
                continue;
            }

            var left = first.Lines[0].Box.Left;
            var items = new List<List<CandidateLine>>();
            var how = new SortedSet<string>(StringComparer.Ordinal);
            var j = i;
            while (j < candidates.Count
                && Eligible(candidates[j])
                && candidates[j].Page == first.Page
                && Start(candidates[j].Lines[0], first.Page) is not null
                && Math.Abs(candidates[j].Lines[0].Box.Left - left) <= options.IndentTolerance)
            {
                foreach (var line in candidates[j].Lines)
                {
                    if (Start(line, first.Page) is { } start)
                    {
                        how.Add(start.How);
                        items.Add([Strip(line, start.Length)]);
                    }
                    else
                    {
                        items[items.Count - 1].Add(line);
                    }
                }

                j++;
            }

            var list = new Candidate(first.Page, items.Select(Item).ToList(), declared: false) { Kind = CandidateKind.List };
            candidates.RemoveRange(i, j - i);
            candidates.Insert(i, list);
            context.Decide(SemanticPass.ListDetection, list.Page, list, $"{items.Count} items ({string.Join(", ", how)})");
        }
    }

    // A marker shape fits a line when it is at most 0.6 font sizes across, ends left of the text by at most two font
    // sizes, and its centre is within the line's height.
    private static bool Fits(LayoutBox marker, CandidateLine line)
    {
        var size = line.Size;
        var centre = (marker.Top + marker.Bottom) / 2;
        return marker.Width <= 0.6 * size
            && marker.Height <= 0.6 * size
            && marker.Right <= line.Box.Left + 0.5
            && line.Box.Left - marker.Right <= 2 * size
            && centre >= line.Box.Bottom - 0.2 * size
            && centre <= line.Box.Top + 0.2 * size;
    }

    // The line without its first `length` characters (a bullet or number) and the whitespace after them.
    private static CandidateLine Strip(CandidateLine line, int length)
    {
        if (length == 0)
        {
            return line;
        }

        var cell = line.Cells[0];
        var runs = new List<SemanticRun>();
        var skip = length;
        foreach (var run in cell.Runs)
        {
            if (skip >= run.Text.Length)
            {
                skip -= run.Text.Length;
                continue;
            }

            runs.Add(new SemanticRun(run.Text.Substring(skip), run.IsBold, run.IsItalic, run.Link));
            skip = 0;
        }

        return line with { Cells = [cell with { Runs = SemanticText.Normalize(runs) }] };
    }

    // An item's lines as one line with one cell.
    private static CandidateLine Item(List<CandidateLine> lines) => new(
        [new CandidateCell(SemanticText.Concat(lines.Select(line => line.Cells[0].Runs)), Geometry.Union(lines.Select(line => line.Box)))],
        Geometry.Union(lines.Select(line => line.Box)),
        lines[0].Size,
        lines.All(line => line.IsBold));
}
