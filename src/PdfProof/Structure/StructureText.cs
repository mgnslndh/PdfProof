namespace PdfProof.Structure;

/// <summary>The text of one marked-content sequence, referenced from a structure element.</summary>
public sealed class StructureText : StructureContent
{
    internal StructureText(string text, int? page, int markedContentId)
        : base(page)
    {
        Text = text;
        MarkedContentId = markedContentId;
    }

    /// <summary>
    /// The sequence's visible text, normalized like the layout model: words joined by single spaces. Text hidden by
    /// clipping is left out.
    /// </summary>
    public string Text { get; }

    /// <summary>The marked-content identifier (MCID) of the sequence on its page.</summary>
    public int MarkedContentId { get; }
}
