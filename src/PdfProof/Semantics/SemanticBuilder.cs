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

        context.Candidates.AddRange(declared ? StructureFrontEnd.Build(structure, context) : LayoutFrontEnd.Build(context));

        if (options.Artifacts.Enabled)
        {
            ArtifactDetector.Run(context);
        }

        if (!declared && options.Headings.Enabled)
        {
            HeadingClassifier.Run(context);
        }

        if (!declared && options.Tables.Enabled)
        {
            TableDetector.Run(context);
        }

        if (!declared && options.Lists.Enabled)
        {
            ListDetector.Run(context);
        }

        if (options.Links.Enabled)
        {
            LinkAttacher.Run(context);
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
