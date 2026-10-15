using System.Globalization;
using System.Text.RegularExpressions;
using PdfProof.Layout;

namespace PdfProof.Semantics;

// Spec §6, pass 1. Rows in the top or bottom band of the page are artifacts when the same text (page numbers written
// {n} and {N}) repeats at the same height on enough pages, or when a row is a page number by pattern. The rows come
// from the layout in both modes: tags often leave footers out. Their text is then removed from the candidates.
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
            foreach (var row in LayoutFrontEnd.Rows(page.Lines))
            {
                SemanticArtifactRole? role = row.Box.Bottom >= page.Height * (1 - options.BandRatio) ? SemanticArtifactRole.Header
                    : row.Box.Top <= page.Height * options.BandRatio ? SemanticArtifactRole.Footer
                    : null;
                if (role is { } r)
                {
                    band.Add((page.Number, r, row, Placeholders(row.Text, page.Number, pages.Count)));
                }
            }
        }

        var needed = Math.Max(2, (int)Math.Ceiling(options.MinPageFraction * pages.Count));
        var found = new List<(int Page, SemanticArtifactRole Role, CandidateLine Row, string Text)>();
        foreach (var group in band.GroupBy(item => (item.Role, item.Text, Height: Math.Round(item.Row.Box.Top / 2))))
        {
            var onPages = group.Select(item => item.Page).Distinct().OrderBy(page => page).ToList();
            if (onPages.Count >= needed)
            {
                found.AddRange(group);
                context.Decide(
                    SemanticPass.ArtifactDetection,
                    null,
                    null,
                    $"{Name(group.Key.Role)} \"{group.Key.Text}\" repeats on pages {string.Join(", ", onPages.Select(p => p.ToString(CultureInfo.InvariantCulture)))}");
            }
        }

        foreach (var item in band.Where(item => !found.Contains(item) && patterns.Any(pattern => pattern.IsMatch(item.Row.Text.Trim()))))
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
                .Where(line => !mine.Any(item => line.Box != default ? line.Box == item.Row.Box : line.Text == item.Row.Text))
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
}
