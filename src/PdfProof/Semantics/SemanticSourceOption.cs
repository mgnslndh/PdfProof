namespace PdfProof.Semantics;

/// <summary>Which front end builds the semantic model.</summary>
public enum SemanticSourceOption
{
    /// <summary>The structure tree when the document is tagged, otherwise the layout.</summary>
    Auto,

    /// <summary>Always the structure tree; an untagged document gets an empty model.</summary>
    Structure,

    /// <summary>Always the layout, even for a tagged document.</summary>
    Layout,
}
