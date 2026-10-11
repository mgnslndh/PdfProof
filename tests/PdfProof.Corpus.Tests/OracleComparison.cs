using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PdfProof.Layout;

namespace PdfProof.Corpus.Tests;

// Compares a loaded document with its oracle at the level the layout and structure models allow until the semantic
// model exists (Plan 5): title and page count; every block's text found in order (on its page, when the oracle gives
// one); its links; for tagged producers, the blocks' structure roles in order; and the artifacts on every page. Text
// is compared with whitespace collapsed. Returns the mismatches; empty means the document matches.
internal static class OracleComparison
{
    public static IReadOnlyList<string> Compare(Oracle oracle, PdfProofDocument document, bool checkRoles)
    {
        var mismatches = new List<string>();
        var pages = document.Layout.Pages;
        if (oracle.Title is { } title && document.Metadata.Title != title)
        {
            mismatches.Add($"title: expected \"{title}\", was \"{document.Metadata.Title}\"");
        }

        if (oracle.PageCount is { } count && pages.Count != count)
        {
            mismatches.Add($"page count: expected {count}, was {pages.Count}");
        }

        // All page texts joined by a space; pageStart[i] is where page i + 1 begins.
        var texts = pages.Select(page => Normalize(page.Text)).ToList();
        var pageStart = new List<int>();
        var joined = new StringBuilder();
        foreach (var text in texts)
        {
            pageStart.Add(joined.Length);
            joined.Append(text).Append(' ');
        }

        var haystack = joined.ToString();
        var cursor = 0;
        for (var b = 0; b < oracle.Blocks.Count; b++)
        {
            var block = oracle.Blocks[b];
            var where = block.Page is { } onPage ? $" on page {onPage}" : "";
            var start = cursor;
            var end = haystack.Length;
            if (block.Page is { } page)
            {
                if (page < 1 || page > pages.Count)
                {
                    mismatches.Add($"block {b + 1} ({block.Type}): page {page} doesn't exist");
                    continue;
                }

                start = Math.Max(cursor, pageStart[page - 1]);
                end = pageStart[page - 1] + texts[page - 1].Length;
            }

            foreach (var text in block.Texts)
            {
                var needle = Normalize(text);
                var at = start <= end ? haystack.IndexOf(needle, start, StringComparison.Ordinal) : -1;
                if (at < 0 || at + needle.Length > end)
                {
                    mismatches.Add($"block {b + 1} ({block.Type}): \"{text}\" not found{where} after the previous block's text");
                    continue;
                }

                start = cursor = at + needle.Length;
            }

            foreach (var link in block.Links.Where(link => !HasLink(pages, block.Page, link)))
            {
                mismatches.Add($"block {b + 1} ({block.Type}): no link \"{link.Text}\" to {Target(link)}{where}");
            }
        }

        if (checkRoles)
        {
            Roles(oracle, document, mismatches);
        }

        foreach (var artifact in oracle.Artifacts)
        {
            for (var i = 0; i < pages.Count; i++)
            {
                var expected = Normalize(artifact.Text
                    .Replace("{n}", (i + 1).ToString(CultureInfo.InvariantCulture))
                    .Replace("{N}", pages.Count.ToString(CultureInfo.InvariantCulture)));
                if (texts[i].IndexOf(expected, StringComparison.Ordinal) < 0)
                {
                    mismatches.Add($"page {i + 1}: {artifact.Role} \"{expected}\" not found");
                }
            }
        }

        return mismatches;
    }

    private static bool HasLink(IReadOnlyList<LayoutPage> pages, int? page, OracleLink expected)
    {
        IEnumerable<LayoutPage> scope = page is { } number && number >= 1 && number <= pages.Count ? [pages[number - 1]] : pages;
        return scope
            .SelectMany(candidate => candidate.Links)
            .Any(link => Normalize(link.Text) == Normalize(expected.Text)
                && (expected.Uri is null || link.Uri == expected.Uri)
                && (expected.Page is null || link.TargetPage == expected.Page));
    }

    private static string Target(OracleLink link) => link.Uri ?? (link.Page is { } page ? $"page {page}" : "anywhere");

    // Tagged producers must declare the blocks' roles in document order; other elements may come in between.
    private static void Roles(Oracle oracle, PdfProofDocument document, List<string> mismatches)
    {
        var roles = document.Structure.Root.Descendants().Select(node => node.Role).ToList();
        var at = 0;
        for (var b = 0; b < oracle.Blocks.Count; b++)
        {
            var expected = oracle.Blocks[b].Role;
            var found = roles.FindIndex(at, role => expected == "H" ? IsHeading(role) : role == expected);
            if (found < 0)
            {
                mismatches.Add($"structure: no {expected} element for block {b + 1} ({oracle.Blocks[b].Type}) after the previous block's; roles in order: {string.Join(" ", roles)}");
                continue;
            }

            at = found + 1;
        }
    }

    private static bool IsHeading(string role) =>
        role == "H" || (role.Length == 2 && role[0] == 'H' && role[1] >= '1' && role[1] <= '6');

    private static string Normalize(string text) => Regex.Replace(text, @"\s+", " ").Trim();
}
