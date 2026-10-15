namespace PdfProof.Semantics;

/// <summary>Where a semantic element's kind comes from.</summary>
public enum SemanticSource
{
    /// <summary>Declared by the PDF's tags (Tagged PDF).</summary>
    Declared,

    /// <summary>Inferred from the layout.</summary>
    Inferred,
}
