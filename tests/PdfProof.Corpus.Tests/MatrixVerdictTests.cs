namespace PdfProof.Corpus.Tests;

public sealed class MatrixVerdictTests
{
    private static readonly IReadOnlyDictionary<string, string> Known = new Dictionary<string, string> { ["typst"] = "draws list bullets as text" };

    [Fact]
    public void No_mismatches_and_no_deviation_passes()
    {
        Assert.Equal(new MatrixVerdict(MatrixOutcome.Pass, "selectpdf matches the oracle."), MatrixVerdict.Of("selectpdf", [], Known));
    }

    [Fact]
    public void Mismatches_without_a_deviation_fail_and_list_every_mismatch()
    {
        var verdict = MatrixVerdict.Of("selectpdf", ["page 2: footer \"Page 2 of 2\" not found", "page count: expected 2, was 3"], Known);

        Assert.Equal(MatrixOutcome.Fail, verdict.Outcome);
        Assert.Equal(
            "selectpdf doesn't match the oracle (2 mismatches):\n- page 2: footer \"Page 2 of 2\" not found\n- page count: expected 2, was 3\n" +
            "Fix the cause, or record a known producer gap in deviations.json with the reason.",
            verdict.Message);
    }

    [Fact]
    public void Mismatches_with_a_deviation_are_a_known_deviation()
    {
        var verdict = MatrixVerdict.Of("typst", ["block 6 (list): \"x\" not found"], Known);

        Assert.Equal(MatrixOutcome.KnownDeviation, verdict.Outcome);
        Assert.Equal("typst: known deviation (draws list bullets as text):\n- block 6 (list): \"x\" not found", verdict.Message);
    }

    [Fact]
    public void A_deviation_that_no_longer_deviates_fails_until_it_is_removed()
    {
        var verdict = MatrixVerdict.Of("typst", [], Known);

        Assert.Equal(MatrixOutcome.Fail, verdict.Outcome);
        Assert.Equal("typst now matches the oracle, but deviations.json still lists it (\"draws list bullets as text\"). Remove the entry.", verdict.Message);
    }
}
