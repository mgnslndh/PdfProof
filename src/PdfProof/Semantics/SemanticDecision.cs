using System.Globalization;

namespace PdfProof.Semantics;

/// <summary>One logged decision of a semantic pass: what it decided and, where it concerns one block, which.</summary>
public sealed class SemanticDecision
{
    internal SemanticDecision(SemanticPass pass, int? page, SemanticBlock? block, string message)
    {
        Pass = pass;
        Page = page;
        Block = block;
        Message = message;
    }

    /// <summary>The pass that decided.</summary>
    public SemanticPass Pass { get; }

    /// <summary>The page the decision is about, or <see langword="null"/> for the whole document.</summary>
    public int? Page { get; }

    /// <summary>The block the decision is about, or <see langword="null"/>.</summary>
    public SemanticBlock? Block { get; }

    /// <summary>The decision, in plain words.</summary>
    public string Message { get; }

    /// <summary>The decision as one line: <c>Pass: Message</c> or <c>Pass (page n): Message</c>.</summary>
    public override string ToString() =>
        Page is { } page ? $"{Pass} (page {page.ToString(CultureInfo.InvariantCulture)}): {Message}" : $"{Pass}: {Message}";
}
