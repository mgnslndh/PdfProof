using PdfProof.Layout;
using PdfProof.Structure;

namespace PdfProof.Semantics;

// Builds the semantic model: a front end, the passes in their fixed order, then Finalizer (spec §6).
internal static class SemanticBuilder
{
    public static SemanticModel Build(LayoutBuildResult layout, StructureModel structure, SemanticOptions options, DiagnosticsCollector diagnostics)
    {
        var declared = options.Source switch
        {
            SemanticSourceOption.Structure => true,
            SemanticSourceOption.Layout => false,
            _ => structure.IsTagged,
        };
        return new SemanticModel(declared ? SemanticSource.Declared : SemanticSource.Inferred, [], [], [], []);
    }
}
