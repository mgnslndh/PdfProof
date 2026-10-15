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

    // Tagged documents: layout links the structure tree connects to an element (OBJR). LinkAttachment places the
    // others by geometry, so a link a tagging producer left unconnected is not lost.
    public HashSet<LayoutLink> ClaimedLinks { get; } = [];

    public List<(SemanticPass Pass, int? Page, Candidate? Candidate, string Message)> Decisions { get; } = [];

    public void Decide(SemanticPass pass, int? page, Candidate? candidate, string message)
    {
        Decisions.Add((pass, page, candidate, message));
        diagnostics.Log(new SemanticDecision(pass, page, null, message).ToString());
    }
}
