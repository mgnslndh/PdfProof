using System.Globalization;

namespace PdfProof.Semantics;

// Building and reshaping runs, and formatting for decision messages.
internal static class SemanticText
{
    public static string Join(IEnumerable<SemanticRun> runs) => string.Concat(runs.Select(run => run.Text));

    public static SemanticRun Plain(string text) => new(text, false, false, null);

    // Pieces joined by one space; then normalized. The space is bold, italic or linked only when the runs on both sides
    // are, so it merges into a styled stretch but never extends one ("Plain **bold** words", not "**bold **words").
    public static IReadOnlyList<SemanticRun> Concat(IEnumerable<IReadOnlyList<SemanticRun>> pieces)
    {
        var runs = new List<SemanticRun>();
        foreach (var piece in pieces.Where(piece => piece.Any(run => run.Text.Trim().Length > 0)))
        {
            if (runs.Count > 0)
            {
                var before = runs[runs.Count - 1];
                var after = piece.First(run => run.Text.Trim().Length > 0);
                runs.Add(new SemanticRun(
                    " ",
                    before.IsBold && after.IsBold,
                    before.IsItalic && after.IsItalic,
                    SemanticLink.Same(before.Link, after.Link) ? before.Link : null));
            }

            runs.AddRange(piece);
        }

        return Normalize(runs);
    }

    // Collapses whitespace, trims the ends, merges neighbours with the same style and link, drops empty runs.
    public static IReadOnlyList<SemanticRun> Normalize(IEnumerable<SemanticRun> runs)
    {
        var result = new List<SemanticRun>();
        foreach (var run in runs)
        {
            var text = Collapse(run.Text);
            if (result.Count > 0 && result[result.Count - 1].Text.EndsWith(" ", StringComparison.Ordinal) && text.StartsWith(" ", StringComparison.Ordinal))
            {
                text = text.Substring(1);
            }

            if (text.Length == 0)
            {
                continue;
            }

            if (result.Count > 0 && result[result.Count - 1] is var last && last.IsBold == run.IsBold && last.IsItalic == run.IsItalic && SemanticLink.Same(last.Link, run.Link))
            {
                result[result.Count - 1] = new SemanticRun(last.Text + text, last.IsBold, last.IsItalic, last.Link);
            }
            else
            {
                result.Add(new SemanticRun(text, run.IsBold, run.IsItalic, run.Link));
            }
        }

        if (result.Count > 0)
        {
            result[0] = With(result[0], result[0].Text.TrimStart());
            result[result.Count - 1] = With(result[result.Count - 1], result[result.Count - 1].Text.TrimEnd());
        }

        return result.Where(run => run.Text.Length > 0).ToList();
    }

    // The runs with the text from `start` (length `length`) linked; the runs around it keep their style.
    public static IReadOnlyList<SemanticRun> Link(IReadOnlyList<SemanticRun> runs, int start, int length, SemanticLink link)
    {
        var result = new List<SemanticRun>();
        var offset = 0;
        var end = start + length;
        foreach (var run in runs)
        {
            var runStart = offset;
            var runEnd = offset + run.Text.Length;
            offset = runEnd;
            if (runEnd <= start || runStart >= end)
            {
                result.Add(run);
                continue;
            }

            var from = Math.Max(start, runStart) - runStart;
            var to = Math.Min(end, runEnd) - runStart;
            if (from > 0)
            {
                result.Add(With(run, run.Text.Substring(0, from)));
            }

            result.Add(new SemanticRun(run.Text.Substring(from, to - from), run.IsBold, run.IsItalic, link));
            if (to < run.Text.Length)
            {
                result.Add(With(run, run.Text.Substring(to)));
            }
        }

        return Normalize(result);
    }

    public static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    public static string Excerpt(string text) => text.Length <= 30 ? text : text.Substring(0, 30) + "...";

    private static SemanticRun With(SemanticRun run, string text) => new(text, run.IsBold, run.IsItalic, run.Link);

    private static string Collapse(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length);
        foreach (var c in text)
        {
            var space = char.IsWhiteSpace(c);
            if (!space || builder.Length == 0 || builder[builder.Length - 1] != ' ')
            {
                builder.Append(space ? ' ' : c);
            }
        }

        return builder.ToString();
    }
}
