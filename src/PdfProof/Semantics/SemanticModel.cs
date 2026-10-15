namespace PdfProof.Semantics;

/// <summary>
/// What the PDF means: its blocks in reading order, the sections they form, the page furniture left out of them, and
/// every decision taken to get there. Built from the tags of a tagged PDF, otherwise inferred from the layout.
/// </summary>
public sealed class SemanticModel
{
    internal SemanticModel(
        SemanticSource source,
        IReadOnlyList<SemanticBlock> blocks,
        IReadOnlyList<SemanticSection> sections,
        IReadOnlyList<SemanticArtifact> artifacts,
        IReadOnlyList<SemanticDecision> decisions)
    {
        Source = source;
        Blocks = ReadOnly.List(blocks);
        Sections = ReadOnly.List(sections);
        Artifacts = ReadOnly.List(artifacts);
        Decisions = ReadOnly.List(decisions);
        Headings = ReadOnly.List(blocks.OfType<SemanticHeading>());
        Paragraphs = ReadOnly.List(blocks.OfType<SemanticParagraph>());
        Tables = ReadOnly.List(blocks.OfType<SemanticTable>());
        Lists = ReadOnly.List(blocks.OfType<SemanticList>());
        Figures = ReadOnly.List(blocks.OfType<SemanticFigure>());
        Links = ReadOnly.List(blocks.SelectMany(LinkedTexts));
    }

    /// <summary>Declared when the blocks come from tags, inferred when they come from the layout.</summary>
    public SemanticSource Source { get; }

    /// <summary>The body blocks in reading order. Artifacts are not included.</summary>
    public IReadOnlyList<SemanticBlock> Blocks { get; }

    /// <summary>The sections formed by the headings; blocks before the first heading belong to no section.</summary>
    public IReadOnlyList<SemanticSection> Sections { get; }

    /// <summary>Running headers, footers and page numbers.</summary>
    public IReadOnlyList<SemanticArtifact> Artifacts { get; }

    /// <summary>Every decision the passes logged, in the order they were taken.</summary>
    public IReadOnlyList<SemanticDecision> Decisions { get; }

    /// <summary>The headings, in reading order.</summary>
    public IReadOnlyList<SemanticHeading> Headings { get; }

    /// <summary>The paragraphs, in reading order.</summary>
    public IReadOnlyList<SemanticParagraph> Paragraphs { get; }

    /// <summary>The tables, in reading order.</summary>
    public IReadOnlyList<SemanticTable> Tables { get; }

    /// <summary>The lists, in reading order.</summary>
    public IReadOnlyList<SemanticList> Lists { get; }

    /// <summary>The figures, in reading order.</summary>
    public IReadOnlyList<SemanticFigure> Figures { get; }

    /// <summary>Every linked text, with its block, in reading order.</summary>
    public IReadOnlyList<SemanticLinkedText> Links { get; }

    private static IEnumerable<SemanticLinkedText> LinkedTexts(SemanticBlock block)
    {
        IEnumerable<SemanticRun> runs = block switch
        {
            SemanticHeading heading => heading.Runs,
            SemanticParagraph paragraph => paragraph.Runs,
            SemanticList list => list.Items.SelectMany(item => item.Runs),
            SemanticTable table => (table.Header is { } header ? new[] { header } : []).Concat(table.Rows).SelectMany(row => row.Cells).SelectMany(cell => cell.Runs),
            _ => [],
        };
        return runs.Where(run => run.Link is not null).Select(run => new SemanticLinkedText(block, run.Text, run.Link!));
    }
}
