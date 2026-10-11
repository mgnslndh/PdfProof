namespace PdfProof.Layout;

/// <summary>The built-in layout strategies.</summary>
public static class LayoutStrategies
{
    /// <summary>
    /// The default. Deterministic and explainable: rows by baseline from top to bottom, split into cells at
    /// column-sized gaps, and into blocks at paragraph-sized vertical gaps. Table rows stay together; multi-column
    /// pages interleave line by line.
    /// </summary>
    public static ILayoutStrategy Rows { get; } = new RowsLayoutStrategy();
}
