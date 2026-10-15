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
            _ => new SemanticParagraph(candidate.Page, source, Runs(candidate), candidate.Box),
        };
    }

    private static IReadOnlyList<SemanticSection> Sections(IReadOnlyList<SemanticBlock> blocks, SemanticSource source) => [];
}
