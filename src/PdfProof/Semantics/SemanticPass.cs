namespace PdfProof.Semantics;

/// <summary>The steps that build the semantic model, in the order they run.</summary>
public enum SemanticPass
{
    /// <summary>Turns layout blocks into candidate blocks (untagged documents).</summary>
    LayoutFrontEnd,

    /// <summary>Turns the structure tree into candidate blocks (tagged documents).</summary>
    StructureFrontEnd,

    /// <summary>Finds running headers, footers and page numbers.</summary>
    ArtifactDetection,

    /// <summary>Finds headings and their levels from font size and weight (untagged documents).</summary>
    HeadingClassification,

    /// <summary>Finds tables from aligned rows of cells (untagged documents).</summary>
    TableDetection,

    /// <summary>Finds lists from bullets, numbering and marker shapes (untagged documents).</summary>
    ListDetection,

    /// <summary>Attaches link annotations to the text they cover.</summary>
    LinkAttachment,

    /// <summary>Turns the remaining candidates into paragraphs and builds the sections.</summary>
    Finalize,
}
