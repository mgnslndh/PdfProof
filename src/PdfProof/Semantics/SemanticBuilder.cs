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
        var context = new SemanticContext(layout, options, diagnostics);

        // The structure front end comes in Task 8; until then tagged documents use the layout front end too.
        context.Candidates.AddRange(LayoutFrontEnd.Build(context));

        if (options.Artifacts.Enabled)
        {
            ArtifactDetector.Run(context);
        }

        var source = declared ? SemanticSource.Declared : SemanticSource.Inferred;
        var model = Finalizer.Build(context, source);
        diagnostics.Add(
            PdfDiagnosticCodes.Semantics,
            null,
            $"{(declared ? "declared" : "inferred")}, {model.Blocks.Count} blocks, {model.Artifacts.Count} artifacts");
        return model;
    }
}
