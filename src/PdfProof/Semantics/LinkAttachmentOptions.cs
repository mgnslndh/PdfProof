namespace PdfProof.Semantics;

/// <summary>LinkAttachment: link annotations become links on the runs of the text they cover.</summary>
public sealed record LinkAttachmentOptions
{
    /// <summary>The default options.</summary>
    public static LinkAttachmentOptions Default { get; } = new();

    /// <summary>Run the pass. Default on.</summary>
    public bool Enabled { get; init; } = true;
}
