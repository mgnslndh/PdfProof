namespace PdfProof.Corpus.Tests;

internal enum MatrixOutcome
{
    Pass,
    KnownDeviation,
    Fail,
}

// The outcome of one matrix case. A known deviation that starts passing fails until its entry is removed, so
// deviations.json never hides a fixed gap (spec §10.2).
internal sealed record MatrixVerdict(MatrixOutcome Outcome, string Message)
{
    public static MatrixVerdict Of(string producer, IReadOnlyList<string> mismatches, IReadOnlyDictionary<string, string> deviations)
    {
        var known = deviations.TryGetValue(producer, out var reason);
        if (mismatches.Count == 0)
        {
            return known
                ? new MatrixVerdict(MatrixOutcome.Fail, $"{producer} now matches the oracle, but deviations.json still lists it (\"{reason}\"). Remove the entry.")
                : new MatrixVerdict(MatrixOutcome.Pass, $"{producer} matches the oracle.");
        }

        var list = string.Join("\n", mismatches.Select(mismatch => "- " + mismatch));
        return known
            ? new MatrixVerdict(MatrixOutcome.KnownDeviation, $"{producer}: known deviation ({reason}):\n{list}")
            : new MatrixVerdict(
                MatrixOutcome.Fail,
                $"{producer} doesn't match the oracle ({mismatches.Count} mismatches):\n{list}\nFix the cause, or record a known producer gap in deviations.json with the reason.");
    }
}
