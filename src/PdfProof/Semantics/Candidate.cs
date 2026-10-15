using PdfProof.Layout;

namespace PdfProof.Semantics;

internal enum CandidateKind
{
    Unknown,
    Heading,
    Paragraph,
    Table,
    List,
    Figure,
}

// A cell of a candidate line: its runs and where it is. A paragraph or heading line has one cell.
internal sealed record CandidateCell(IReadOnlyList<SemanticRun> Runs, LayoutBox Box)
{
    public string Text => SemanticText.Join(Runs);
}

// A visual row (layout front end) or one structural row, item or paragraph (structure front end). Size and IsBold
// describe the row's text: the size with the most characters, and whether every span is bold.
internal sealed record CandidateLine(IReadOnlyList<CandidateCell> Cells, LayoutBox Box, double Size, bool IsBold)
{
    public string Text => string.Join(" ", Cells.Select(cell => cell.Text).Where(text => text.Length > 0));
}

// A block on its way through the passes. Mutable: passes reclassify and reshape candidates; Finalizer freezes them.
internal sealed class Candidate(int page, IReadOnlyList<CandidateLine> lines, bool declared)
{
    public int Page { get; } = page;

    public bool Declared { get; } = declared;

    public IReadOnlyList<CandidateLine> Lines { get; set; } = lines;

    public CandidateKind Kind { get; set; }

    public int? Level { get; set; }

    // Table candidates: how many of the first lines are header rows (0 or 1).
    public int HeaderRows { get; set; }

    public string? Alt { get; set; }

    // Tagged documents: link annotations on the element itself, placed on its text by LinkAttachment.
    public List<LayoutLink> PendingLinks { get; } = [];

    public LayoutBox Box => Lines.Count == 0 || Lines.All(line => line.Box == default) ? default : Geometry.Union(Lines.Where(line => line.Box != default).Select(line => line.Box));

    public string Text => string.Join(" ", Lines.Select(line => line.Text).Where(text => text.Length > 0));

    public bool IsSingleCell => Lines.All(line => line.Cells.Count == 1);
}
