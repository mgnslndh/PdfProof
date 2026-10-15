using System.Globalization;
using System.Text.RegularExpressions;
using PdfProof.Layout;

namespace PdfProof.Semantics;

// Spec §6, pass 1. Rows in the top or bottom band of the page are artifacts when the same text (page numbers written
// {n} and {N}) repeats at the same height on enough pages, or when a row is a page number by pattern. The rows come
// from the layout in both modes: tags often leave footers out. Candidate lines inside a found row then leave the body;
// tagged lines are placed by their marked content, so a heading that repeats the running header's text stays.
internal static class ArtifactDetector
{
    private static readonly Regex Digits = new(@"\d+", RegexOptions.CultureInvariant);

    public static void Run(SemanticContext context)
    {
        var options = context.Options.Artifacts;
        var pages = context.Layout.Pages;
        var patterns = options.PageNumberPatterns.Select(pattern => new Regex(pattern, RegexOptions.CultureInvariant)).ToList();

        var band = new List<(int Page, SemanticArtifactRole Role, CandidateLine Row, string Text)>();
        foreach (var page in pages)
        {
            var rows = LayoutFrontEnd.Rows(page.Lines);
            foreach (var row in rows)
            {
                SemanticArtifactRole? role = row.Box.Bottom >= page.Height * (1 - options.BandRatio) ? SemanticArtifactRole.Header
                    : row.Box.Top <= page.Height * options.BandRatio ? SemanticArtifactRole.Footer
                    : null;
                if (role == SemanticArtifactRole.Header && TableHeader(row, rows))
                {
                    continue;
                }

                if (role is { } r)
                {
                    band.Add((page.Number, r, row, Placeholders(row.Text, page.Number, pages.Count)));
                }
            }
        }

        var needed = Math.Max(2, (int)Math.Ceiling(options.MinPageFraction * pages.Count));
        var found = new List<(int Page, SemanticArtifactRole Role, CandidateLine Row, string Text)>();
        foreach (var group in band.GroupBy(item => (item.Role, item.Text)).SelectMany(Heights))
        {
            var onPages = group.Select(item => item.Page).Distinct().OrderBy(page => page).ToList();
            if (onPages.Count >= needed)
            {
                found.AddRange(group);
                context.Decide(
                    SemanticPass.ArtifactDetection,
                    null,
                    null,
                    $"{Name(group[0].Role)} \"{group[0].Text}\" repeats on pages {string.Join(", ", onPages.Select(p => p.ToString(CultureInfo.InvariantCulture)))}");
            }
        }

        // A pattern only counts when the number it matched is this page's number ({n}): a lone amount such as "1250" in
        // the bottom band is content.
        foreach (var item in band.Where(item => !found.Contains(item) && item.Text.Contains("{n}") && patterns.Any(pattern => pattern.IsMatch(item.Row.Text.Trim()))))
        {
            found.Add(item);
            context.Decide(SemanticPass.ArtifactDetection, item.Page, null, $"{Name(item.Role)} \"{item.Text}\" is a page number");
        }

        foreach (var artifact in found.GroupBy(item => (item.Role, item.Text)))
        {
            context.Artifacts.Add(new SemanticArtifact(artifact.Key.Role, artifact.Key.Text, artifact.Select(item => item.Page).Distinct().OrderBy(page => page).ToList()));
        }

        Remove(context, found);
    }

    // The rows of one text split where their heights are more than 2 pt apart, so a header drifting by tenths of a
    // point stays one group.
    private static IEnumerable<List<(int Page, SemanticArtifactRole Role, CandidateLine Row, string Text)>> Heights(
        IEnumerable<(int Page, SemanticArtifactRole Role, CandidateLine Row, string Text)> items)
    {
        var cluster = new List<(int Page, SemanticArtifactRole Role, CandidateLine Row, string Text)>();
        foreach (var item in items.OrderBy(item => item.Row.Box.Top).ThenBy(item => item.Page))
        {
            if (cluster.Count > 0 && item.Row.Box.Top - cluster[cluster.Count - 1].Row.Box.Top > 2)
            {
                yield return cluster;
                cluster = [];
            }

            cluster.Add(item);
        }

        if (cluster.Count > 0)
        {
            yield return cluster;
        }
    }

    // The page number becomes {n}; after it, the page count becomes {N}. Other numbers stay.
    private static string Placeholders(string text, int page, int count)
    {
        var pageText = page.ToString(CultureInfo.InvariantCulture);
        var countText = count.ToString(CultureInfo.InvariantCulture);
        var seenPage = false;
        return Digits.Replace(text, match =>
        {
            if (!seenPage && match.Value == pageText)
            {
                seenPage = true;
                return "{n}";
            }

            return seenPage && match.Value == countText ? "{N}" : match.Value;
        });
    }

    private static string Name(SemanticArtifactRole role) => role == SemanticArtifactRole.Header ? "header" : "footer";

    // Untagged candidates lose the rows with the artifact's box; tagged candidates (no boxes) the lines with its text.
    // A row of two or more cells in the top band with another such row below it is a table's header repeated on each
    // page (HTML converters repeat <thead>), not a running header.
    private static bool TableHeader(CandidateLine row, List<CandidateLine> rows) =>
        row.Cells.Count >= 2
        && rows.Where(other => other.Box.Top < row.Box.Bottom).OrderByDescending(other => other.Box.Top).FirstOrDefault() is { Cells.Count: >= 2 };

    // Candidate lines whose centre lies in a found row leave the body. The centre, because tagged lines are placed by
    // their marked content's letter boxes, which are taller than the layout's glyph boxes. Lines without a box (tagged
    // content with no marked content found) stay: the tags are trusted.
    private static void Remove(SemanticContext context, List<(int Page, SemanticArtifactRole Role, CandidateLine Row, string Text)> found)
    {
        foreach (var candidate in context.Candidates.ToList())
        {
            var mine = found.Where(item => item.Page == candidate.Page).ToList();
            if (mine.Count == 0)
            {
                continue;
            }

            var kept = candidate.Lines
                .Where(line => line.Box == default || !mine.Any(item => CentreInside(line.Box, item.Row.Box)))
                .ToList();
            if (kept.Count == 0)
            {
                context.Candidates.Remove(candidate);
            }
            else if (kept.Count != candidate.Lines.Count)
            {
                candidate.Lines = kept;
            }
        }
    }

    private static bool CentreInside(LayoutBox inner, LayoutBox outer)
    {
        var x = (inner.Left + inner.Right) / 2;
        var y = (inner.Bottom + inner.Top) / 2;
        return x >= outer.Left && x <= outer.Right && y >= outer.Bottom && y <= outer.Top;
    }
}
