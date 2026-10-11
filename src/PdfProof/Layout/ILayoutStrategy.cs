namespace PdfProof.Layout;

/// <summary>
/// Groups a page's horizontal words into blocks of lines, in reading order. Rotated text is grouped separately
/// and never passed to a strategy. Built-in strategies are in <see cref="LayoutStrategies"/>.
/// </summary>
public interface ILayoutStrategy
{
    /// <summary>The strategy's name, shown in diagnostics.</summary>
    string Name { get; }

    /// <summary>Groups words into blocks of lines.</summary>
    /// <param name="words">The page's horizontal words, in content order; never empty words.</param>
    /// <param name="options">The layout options in use.</param>
    /// <returns>Blocks in reading order. Omit no word; empty lines and blocks are ignored.</returns>
    IReadOnlyList<LayoutWordBlock> Arrange(IReadOnlyList<LayoutWord> words, LayoutOptions options);
}
