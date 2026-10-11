using PdfProof.Layout;

namespace PdfProof.Tests;

public sealed class GeometryTests
{
    [Theory]
    [InlineData(2.25, 1, 2.3)]
    [InlineData(-2.25, 1, -2.3)]
    [InlineData(10.04, 1, 10.0)]
    [InlineData(10.04, 2, 10.04)]
    public void Round_rounds_half_away_from_zero(double value, int decimals, double expected) =>
        Assert.Equal(expected, Geometry.Round(value, decimals));

    [Fact]
    public void Round_never_returns_negative_zero()
    {
        var rounded = Geometry.Round(-0.04, 1);

        Assert.Equal(0, rounded);
        Assert.True(1 / rounded > 0, "Rounding must give +0, not -0, so output never shows \"-0\".");
    }
}
