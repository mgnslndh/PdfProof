namespace PdfProof.Layout;

// The layout model plus, for tagged documents, the marked text per page (page number -> MCID -> text), collected in
// the same page pass so pages are read once.
internal sealed record LayoutBuildResult(LayoutModel Model, IReadOnlyDictionary<int, IReadOnlyDictionary<int, string>> MarkedText);
