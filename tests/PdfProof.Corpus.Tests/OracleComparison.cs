using System.Globalization;
using System.Text.RegularExpressions;
using PdfProof.Semantics;

namespace PdfProof.Corpus.Tests;

// Compares a loaded document's semantic model with its oracle (spec §10.2): title and page count; the blocks in order
// by type, page, level, text, table header and rows, list items and links; and the artifacts by role, text and pages.
// What the oracle leaves out is not compared. Text is compared with whitespace collapsed. Returns the mismatches.
internal static class OracleComparison
{
    public static IReadOnlyList<string> Compare(Oracle oracle, PdfProofDocument document)
    {
        var mismatches = new List<string>();
        if (oracle.Title is { } title && document.Metadata.Title != title)
        {
            mismatches.Add($"title: expected \"{title}\", was \"{document.Metadata.Title}\"");
        }

        if (oracle.PageCount is { } count && document.Metadata.PageCount != count)
        {
            mismatches.Add($"page count: expected {count}, was {document.Metadata.PageCount}");
        }

        var blocks = document.Semantics.Blocks;
        if (blocks.Count != oracle.Blocks.Count)
        {
            mismatches.Add($"blocks: expected {oracle.Blocks.Count}, found {blocks.Count}: {string.Join(", ", blocks.Select(b => $"{Kind(b)} \"{Excerpt(b.Text)}\""))}");
        }

        for (var i = 0; i < Math.Min(blocks.Count, oracle.Blocks.Count); i++)
        {
            Block(i + 1, oracle.Blocks[i], blocks[i], mismatches);
        }

        foreach (var expected in oracle.Artifacts)
        {
            var role = expected.Role.ToLowerInvariant();
            var match = document.Semantics.Artifacts.FirstOrDefault(a => Name(a.Role) == role && Normalize(a.Text) == Normalize(expected.Text));
            if (match is null)
            {
                mismatches.Add($"artifact: expected {role} \"{expected.Text}\", found {Found(document.Semantics.Artifacts)}");
            }
            else if (expected.Pages is { } pages && Range(match.Pages) != pages)
            {
                mismatches.Add($"artifact: expected {role} \"{expected.Text}\" on pages {pages}, found {Name(match.Role)} \"{match.Text}\" on pages {Range(match.Pages)}");
            }
        }

        return mismatches;
    }

    private static void Block(int number, OracleBlock expected, SemanticBlock actual, List<string> mismatches)
    {
        var where = $"block {number} ({expected.Type})";
        if (Kind(actual) != expected.Type)
        {
            mismatches.Add($"{where}: found {Kind(actual)} \"{Excerpt(actual.Text)}\"");
            return;
        }

        if (expected.Page is { } page && actual.Page != page)
        {
            mismatches.Add($"{where}: expected page {page}, found page {actual.Page}");
        }

        switch (actual)
        {
            case SemanticHeading heading:
                if (expected.Level is { } level && heading.Level != level)
                {
                    mismatches.Add($"{where}: expected level {level}, found {heading.Level}");
                }

                Text(where, expected.Text, heading.Text, mismatches);
                break;
            case SemanticParagraph paragraph:
                Text(where, expected.Text, paragraph.Text, mismatches);
                break;
            case SemanticTable table:
                if (expected.Header.Count > 0)
                {
                    var header = table.Header is null ? "none" : Cells(table.Header);
                    if (header != Join(expected.Header))
                    {
                        mismatches.Add($"{where}: expected header {Join(expected.Header)}, found {header}");
                    }
                }

                var rows = table.Rows.Select(Cells).ToList();
                var expectedRows = expected.Rows.Select(Join).ToList();
                if (expectedRows.Count > 0 && !rows.SequenceEqual(expectedRows))
                {
                    mismatches.Add($"{where}: expected rows {string.Join(", ", expectedRows)}, found {string.Join(", ", rows)}");
                }

                break;
            case SemanticList list:
                var items = list.Items.Select(item => Normalize(item.Text)).ToList();
                if (expected.Items.Count > 0 && !items.SequenceEqual(expected.Items.Select(Normalize)))
                {
                    mismatches.Add($"{where}: expected items [{string.Join(" | ", expected.Items)}], found [{string.Join(" | ", items)}]");
                }

                break;
        }

        var runs = Runs(actual).Where(run => run.Link is not null).ToList();
        foreach (var link in expected.Links.Where(link => !runs.Any(run =>
            Normalize(run.Text) == Normalize(link.Text)
            && (link.Uri is null || run.Link!.Uri == link.Uri)
            && (link.Page is null || run.Link!.TargetPage == link.Page))))
        {
            mismatches.Add($"{where}: no link \"{link.Text}\" to {link.Uri ?? (link.Page is { } target ? $"page {target}" : "anywhere")}");
        }
    }

    private static void Text(string where, string? expected, string actual, List<string> mismatches)
    {
        if (expected is not null && Normalize(expected) != Normalize(actual))
        {
            mismatches.Add($"{where}: expected text \"{expected}\", found \"{actual}\"");
        }
    }

    private static IEnumerable<SemanticRun> Runs(SemanticBlock block) => block switch
    {
        SemanticHeading heading => heading.Runs,
        SemanticParagraph paragraph => paragraph.Runs,
        SemanticList list => list.Items.SelectMany(item => item.Runs),
        SemanticTable table => (table.Header is { } header ? new[] { header } : []).Concat(table.Rows).SelectMany(row => row.Cells).SelectMany(cell => cell.Runs),
        _ => [],
    };

    private static string Kind(SemanticBlock block) => block switch
    {
        SemanticHeading => "heading",
        SemanticParagraph => "paragraph",
        SemanticTable => "table",
        SemanticList => "list",
        _ => "figure",
    };

    private static string Name(SemanticArtifactRole role) => role == SemanticArtifactRole.Header ? "header" : "footer";

    private static string Found(IReadOnlyList<SemanticArtifact> artifacts) =>
        artifacts.Count == 0 ? "none" : string.Join(", ", artifacts.Select(a => $"{Name(a.Role)} \"{a.Text}\""));

    private static string Cells(SemanticRow row) => Join(row.Cells.Select(cell => cell.Text).ToList());

    private static string Join(IReadOnlyList<string> cells) => "[" + string.Join(" | ", cells.Select(Normalize)) + "]";

    // Pages as ranges: 1, 2, 3, 5 → "1-3,5".
    private static string Range(IReadOnlyList<int> pages)
    {
        var parts = new List<string>();
        for (var i = 0; i < pages.Count; i++)
        {
            var start = pages[i];
            while (i + 1 < pages.Count && pages[i + 1] == pages[i] + 1)
            {
                i++;
            }

            parts.Add(start == pages[i] ? start.ToString(CultureInfo.InvariantCulture) : $"{start.ToString(CultureInfo.InvariantCulture)}-{pages[i].ToString(CultureInfo.InvariantCulture)}");
        }

        return string.Join(",", parts);
    }

    private static string Excerpt(string text) => text.Length <= 40 ? text : text.Substring(0, 40) + "...";

    private static string Normalize(string text) => Regex.Replace(text, @"\s+", " ").Trim();
}
