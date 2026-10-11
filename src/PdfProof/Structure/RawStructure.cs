namespace PdfProof.Structure;

// The structure tree as PdfPig's tokens describe it: roles as written, pages resolved, attributes read. Built inside
// PdfPigCall by StructureTreeReader; StructureBuilder maps it to the public model outside.
internal sealed record RawStructure(
    bool HasStructTreeRoot,
    bool Marked,
    IReadOnlyDictionary<string, string> RoleMap,
    IReadOnlyList<RawElement> Elements,
    bool SkippedStreamContent = false)
{
    public static RawStructure None { get; } = new(false, false, new Dictionary<string, string>(), []);
}

internal abstract record RawKid;

internal sealed record RawElement(
    string Role,
    int? Page,
    string? Alt,
    string? ActualText,
    string? Language,
    string? Scope,
    int RowSpan,
    int ColumnSpan,
    IReadOnlyList<RawKid> Kids) : RawKid;

internal sealed record RawMarkedContent(int? Page, int Mcid) : RawKid;

internal sealed record RawObjectReference(int? Page, string? Subtype, double[]? Rectangle) : RawKid;
