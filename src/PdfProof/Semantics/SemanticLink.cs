namespace PdfProof.Semantics;

/// <summary>Where a link goes: an external URI or a page of the same document.</summary>
public sealed class SemanticLink
{
    internal SemanticLink(string? uri, int? targetPage)
    {
        Uri = uri;
        TargetPage = targetPage;
    }

    /// <summary>The external URI, or <see langword="null"/> for a link within the document.</summary>
    public string? Uri { get; }

    /// <summary>The page number the link goes to, or <see langword="null"/> for an external link.</summary>
    public int? TargetPage { get; }

    /// <summary>The URI, or <c>page n</c>.</summary>
    public override string ToString() => Uri ?? (TargetPage is { } page ? "page " + page.ToString(System.Globalization.CultureInfo.InvariantCulture) : "(no target)");

    internal static bool Same(SemanticLink? a, SemanticLink? b) =>
        a is null ? b is null : b is not null && a.Uri == b.Uri && a.TargetPage == b.TargetPage;
}
