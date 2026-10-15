namespace PdfProof.Semantics;

/// <summary>
/// A heading and what belongs to it. With declared headings a section runs to the next heading of the same or a
/// higher level, and holds the lower-level sections as <see cref="Sections"/>; with inferred headings it runs to the
/// next heading of any level (inferred levels are unreliable), so sections don't nest.
/// </summary>
public sealed class SemanticSection
{
    internal SemanticSection(SemanticHeading heading, IReadOnlyList<SemanticBlock> blocks, IReadOnlyList<SemanticSection> sections)
    {
        Heading = heading;
        Blocks = ReadOnly.List(blocks);
        Sections = ReadOnly.List(sections);
    }

    /// <summary>The section's heading.</summary>
    public SemanticHeading Heading { get; }

    /// <summary>The blocks after the heading and before the first subsection or the next section.</summary>
    public IReadOnlyList<SemanticBlock> Blocks { get; }

    /// <summary>The subsections.</summary>
    public IReadOnlyList<SemanticSection> Sections { get; }
}
