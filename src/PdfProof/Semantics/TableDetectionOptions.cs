namespace PdfProof.Semantics;

/// <summary>
/// TableDetection (untagged documents): consecutive rows of at least two cells whose columns line up are a table; the
/// first row is its header when its style differs from the next row's.
/// </summary>
public sealed record TableDetectionOptions
{
    /// <summary>The default options.</summary>
    public static TableDetectionOptions Default { get; } = new();

    /// <summary>Run the pass. Default on.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>The fewest rows a table has, header included. At least 2; default 2.</summary>
    public int MinRows
    {
        get => _minRows;
        init => _minRows = Throw.IfOutOfRange(value, 2, 1000, nameof(MinRows));
    }

    /// <summary>How far apart, in points, cells may be and still share a column. Default 3.</summary>
    public double ColumnTolerance
    {
        get => _columnTolerance;
        init => _columnTolerance = Throw.IfOutOfRange(value, 0, 100, nameof(ColumnTolerance));
    }

    private readonly int _minRows = 2;
    private readonly double _columnTolerance = 3;
}
