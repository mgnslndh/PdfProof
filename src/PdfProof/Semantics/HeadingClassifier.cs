namespace PdfProof.Semantics;

// Spec §6, pass 3 (untagged documents). The body text size is the size with the most characters. A one-cell candidate
// of at most MaxLines lines is a heading when it is MinSizeRatio larger than the body text, or when it is bold, doesn't
// end a sentence and is followed by text that isn't bold. Levels come from size clusters (0.5 pt), the largest being 1.
internal static class HeadingClassifier
{
    public static void Run(SemanticContext context)
    {
        var options = context.Options.Headings;
        var candidates = context.Candidates;
        var lines = candidates.SelectMany(candidate => candidate.Lines).Where(line => line.Text.Length > 0).ToList();
        if (lines.Count == 0)
        {
            return;
        }

        var body = lines.GroupBy(line => line.Size).OrderByDescending(group => group.Sum(line => line.Text.Length)).ThenBy(group => group.Key).First().Key;
        context.Decide(SemanticPass.HeadingClassification, null, null, $"body text size {SemanticText.Number(body)} pt");

        var headings = new List<(Candidate Candidate, double Size, string Reason)>();
        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (candidate.Kind != CandidateKind.Unknown || !candidate.IsSingleCell || candidate.Lines.Count == 0 || candidate.Lines.Count > options.MaxLines)
            {
                continue;
            }

            var size = candidate.Lines.Max(line => line.Size);
            if (size >= body * options.MinSizeRatio)
            {
                headings.Add((candidate, size, $"{SemanticText.Number(size)} pt vs body {SemanticText.Number(body)} pt"));
            }
            else if (options.BoldShortLineIsHeading
                && candidate.Lines.All(line => line.IsBold)
                && !candidate.Text.TrimEnd().EndsWith(".", StringComparison.Ordinal)
                && i + 1 < candidates.Count
                && candidates[i + 1].Lines.Any(line => !line.IsBold))
            {
                headings.Add((candidate, size, "bold, short and followed by body text"));
            }
        }

        var clusters = Clusters(headings.Select(heading => heading.Size));
        foreach (var (candidate, size, reason) in headings)
        {
            candidate.Kind = CandidateKind.Heading;
            candidate.Level = clusters.FindIndex(cluster => Math.Abs(cluster - size) <= 0.5) + 1;
            context.Decide(SemanticPass.HeadingClassification, candidate.Page, candidate, $"\"{SemanticText.Excerpt(candidate.Text)}\": {reason} → heading L{candidate.Level}");
        }
    }

    // Distinct sizes, largest first; a size within 0.5 pt of the previous cluster joins it.
    private static List<double> Clusters(IEnumerable<double> sizes)
    {
        var clusters = new List<double>();
        foreach (var size in sizes.OrderByDescending(size => size))
        {
            if (clusters.Count == 0 || clusters[clusters.Count - 1] - size > 0.5)
            {
                clusters.Add(size);
            }
        }

        return clusters;
    }
}
