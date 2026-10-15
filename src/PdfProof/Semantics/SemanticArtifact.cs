namespace PdfProof.Semantics;

/// <summary>
/// Text that repeats as page furniture rather than content: a running header or footer, or a page number. It is not
/// part of <see cref="SemanticModel.Blocks"/>.
/// </summary>
public sealed class SemanticArtifact
{
    internal SemanticArtifact(SemanticArtifactRole role, string text, IReadOnlyList<int> pages)
    {
        Role = role;
        Text = text;
        Pages = ReadOnly.List(pages);
    }

    /// <summary>Header or footer.</summary>
    public SemanticArtifactRole Role { get; }

    /// <summary>The text, with the page number written <c>{n}</c> and the page count <c>{N}</c>: <c>Page {n} of {N}</c>.</summary>
    public string Text { get; }

    /// <summary>The pages it appears on, ascending.</summary>
    public IReadOnlyList<int> Pages { get; }
}
