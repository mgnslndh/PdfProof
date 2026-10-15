namespace PdfProof.Semantics;

// The last pass (spec §6, pass 7): unclassified candidates become paragraphs, candidates become immutable blocks,
// decisions point at their blocks, and sections are built from the headings.
internal static class Finalizer
{
    public static SemanticModel Build(SemanticContext context, SemanticSource source)
    {
        var blocks = new List<SemanticBlock>();
        var byCandidate = new Dictionary<Candidate, SemanticBlock>();
        foreach (var candidate in context.Candidates)
        {
            var block = Block(candidate);
            blocks.Add(block);
            byCandidate[candidate] = block;
        }

        var decisions = context.Decisions
            .Select(d => new SemanticDecision(d.Pass, d.Page, d.Candidate is { } c && byCandidate.TryGetValue(c, out var b) ? b : null, d.Message))
            .ToList();
        var artifacts = context.Artifacts.OrderBy(a => a.Role).ThenBy(a => a.Pages[0]).ThenBy(a => a.Text, StringComparer.Ordinal).ToList();
        return new SemanticModel(source, blocks, Sections(blocks, source), artifacts, decisions);
    }

    // A candidate's lines as one run list, lines and cells joined by a space.
    public static IReadOnlyList<SemanticRun> Runs(Candidate candidate) =>
        SemanticText.Concat(candidate.Lines.SelectMany(line => line.Cells).Select(cell => cell.Runs));

    private static SemanticBlock Block(Candidate candidate)
    {
        var source = candidate.Declared ? SemanticSource.Declared : SemanticSource.Inferred;
        return candidate.Kind switch
        {
            CandidateKind.Heading => new SemanticHeading(candidate.Page, source, candidate.Level ?? 1, Runs(candidate), candidate.Box),
            CandidateKind.Table => new SemanticTable(
                candidate.Page,
                source,
                candidate.HeaderRows > 0 ? Row(candidate.Lines[0]) : null,
                candidate.Lines.Skip(candidate.HeaderRows).Select(Row).ToList(),
                candidate.Box),
            _ => new SemanticParagraph(candidate.Page, source, Runs(candidate), candidate.Box),
        };
    }

    private static SemanticRow Row(CandidateLine line) =>
        new(line.Cells.Select(cell => new SemanticCell(SemanticText.Normalize(cell.Runs))).ToList());

    // Declared headings nest: a section runs to the next heading of the same or a higher level. Inferred headings
    // don't (their levels are unreliable): a section runs to the next heading of any level (spec §6, rules).
    private static IReadOnlyList<SemanticSection> Sections(IReadOnlyList<SemanticBlock> blocks, SemanticSource source)
    {
        var roots = new List<SectionBuilder>();
        var open = new List<SectionBuilder>();
        foreach (var block in blocks)
        {
            if (block is SemanticHeading heading)
            {
                if (source == SemanticSource.Inferred)
                {
                    open.Clear();
                }
                else
                {
                    while (open.Count > 0 && open[open.Count - 1].Heading.Level >= heading.Level)
                    {
                        open.RemoveAt(open.Count - 1);
                    }
                }

                var section = new SectionBuilder(heading);
                (open.Count == 0 ? roots : open[open.Count - 1].Children).Add(section);
                open.Add(section);
            }
            else if (open.Count > 0)
            {
                open[open.Count - 1].Blocks.Add(block);
            }
        }

        return roots.Select(section => section.Build()).ToList();
    }

    private sealed class SectionBuilder(SemanticHeading heading)
    {
        public SemanticHeading Heading { get; } = heading;

        public List<SemanticBlock> Blocks { get; } = [];

        public List<SectionBuilder> Children { get; } = [];

        public SemanticSection Build() => new(Heading, Blocks, Children.Select(child => child.Build()).ToList());
    }
}
