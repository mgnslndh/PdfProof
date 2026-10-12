namespace PdfProof.Corpus.Tests;

internal enum MatrixOutcome
{
    Pass,
    KnownDeviation,
    Fail,
}

// The outcome of one matrix case. A known deviation that starts passing fails until its entry is removed, so
// deviations.json never hides a fixed gap (spec §10.2). A fixture that can't be loaded always fails: a deviation
// describes how a producer renders, not a broken file.
internal sealed record MatrixVerdict(MatrixOutcome Outcome, string Message)
{
    public static MatrixVerdict Of(
        string producer, IReadOnlyList<string> mismatches, IReadOnlyDictionary<string, string> deviations, string? loadError = null)
    {
        if (loadError is not null)
        {
            return new MatrixVerdict(MatrixOutcome.Fail, $"{producer}: {loadError}");
        }

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
