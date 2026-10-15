using PdfProof.Layout;

namespace PdfProof.Semantics;

// What the passes share: the layout they read, the candidates they rewrite, the artifacts and decisions they record.
internal sealed class SemanticContext(LayoutBuildResult layout, SemanticOptions options, DiagnosticsCollector diagnostics)
{
    public LayoutModel Layout => layout.Model;

    public LayoutBuildResult Build => layout;

    public SemanticOptions Options => options;

    public List<Candidate> Candidates { get; } = [];

    public List<SemanticArtifact> Artifacts { get; } = [];

    public List<(SemanticPass Pass, int? Page, Candidate? Candidate, string Message)> Decisions { get; } = [];

    public void Decide(SemanticPass pass, int? page, Candidate? candidate, string message)
    {
        Decisions.Add((pass, page, candidate, message));
        diagnostics.Log(new SemanticDecision(pass, page, null, message).ToString());
    }
}
