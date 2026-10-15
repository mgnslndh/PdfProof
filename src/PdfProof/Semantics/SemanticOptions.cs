namespace PdfProof.Semantics;

/// <summary>
/// How the semantic model is built: the front end, how the layout front end splits blocks, and one options record per
/// pass. Part of a <see cref="PdfProfile"/>; start from <see cref="Default"/> and adjust with <c>with</c>.
/// </summary>
public sealed record SemanticOptions
{
    /// <summary>The default semantic options.</summary>
    public static SemanticOptions Default { get; } = new();

    /// <summary>Which front end builds the model. Default <see cref="SemanticSourceOption.Auto"/>.</summary>
    public SemanticSourceOption Source { get; init; } = SemanticSourceOption.Auto;

    /// <summary>
    /// Layout front end: a vertical gap between two lines of a layout block larger than this many font sizes starts a
    /// new block. Default 1.0.
    /// </summary>
    public double ParagraphGapFactor
    {
        get => _paragraphGapFactor;
        init => _paragraphGapFactor = Throw.IfOutOfRange(value, 0, 100, nameof(ParagraphGapFactor));
    }

    /// <summary>
    /// Layout front end: a line that ends before this fraction of the page's text width ends its paragraph, so one-line
    /// paragraphs in the same layout block are told apart. Between 0 and 1; default 0.8.
    /// </summary>
    public double ShortLineRatio
    {
        get => _shortLineRatio;
        init => _shortLineRatio = Throw.IfOutOfRange(value, 0, 1, nameof(ShortLineRatio));
    }

    /// <summary>Running headers, footers and page numbers.</summary>
    public ArtifactDetectionOptions Artifacts
    {
        get => _artifacts;
        init => _artifacts = Throw.IfNull(value, nameof(Artifacts));
    }

    /// <summary>Headings in untagged documents.</summary>
    public HeadingClassificationOptions Headings
    {
        get => _headings;
        init => _headings = Throw.IfNull(value, nameof(Headings));
    }

    /// <summary>Tables in untagged documents.</summary>
    public TableDetectionOptions Tables
    {
        get => _tables;
        init => _tables = Throw.IfNull(value, nameof(Tables));
    }

    /// <summary>Lists in untagged documents.</summary>
    public ListDetectionOptions Lists
    {
        get => _lists;
        init => _lists = Throw.IfNull(value, nameof(Lists));
    }

    /// <summary>Links attached to the text they cover.</summary>
    public LinkAttachmentOptions Links
    {
        get => _links;
        init => _links = Throw.IfNull(value, nameof(Links));
    }

    private readonly double _paragraphGapFactor = 1.0;
    private readonly double _shortLineRatio = 0.8;
    private readonly ArtifactDetectionOptions _artifacts = ArtifactDetectionOptions.Default;
    private readonly HeadingClassificationOptions _headings = HeadingClassificationOptions.Default;
    private readonly TableDetectionOptions _tables = TableDetectionOptions.Default;
    private readonly ListDetectionOptions _lists = ListDetectionOptions.Default;
    private readonly LinkAttachmentOptions _links = LinkAttachmentOptions.Default;
}
