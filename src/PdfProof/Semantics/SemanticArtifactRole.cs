namespace PdfProof.Semantics;

/// <summary>Where an artifact sits on the page.</summary>
public enum SemanticArtifactRole
{
    /// <summary>A running header in the top band of the page.</summary>
    Header,

    /// <summary>A running footer or page number in the bottom band of the page.</summary>
    Footer,
}
