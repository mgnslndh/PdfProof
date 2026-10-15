namespace PdfProof.Layout;

// The layout model plus what only the structure and semantic layers use, collected in the same page pass so pages are
// read once: for tagged documents the marked text per page (page number -> MCID -> text), and for every document the
// boxes of small paths per page, the candidates for list markers drawn as shapes.
internal sealed record LayoutBuildResult(
    LayoutModel Model,
    IReadOnlyDictionary<int, IReadOnlyDictionary<int, MarkedText>> MarkedText,
    IReadOnlyDictionary<int, IReadOnlyList<LayoutBox>> Markers);
