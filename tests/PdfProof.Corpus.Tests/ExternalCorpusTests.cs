using PdfProof.Layout;

namespace PdfProof.Corpus.Tests;

// Real-world PDFs pinned in corpus/external/manifest.json and downloaded by FetchCorpus. Code assertions for now;
// approved exports come with Plan 6.
public sealed class ExternalCorpusTests
{
    private const string HowToGet = "Run ./build.ps1 --target=FetchCorpus.";

    [Fact]
    public void The_W9_is_a_tagged_six_page_form_with_nested_fields()
    {
        var doc = PdfProofDocument.Load(Fixtures.Require(CorpusPaths.External("irs-w9-2024"), HowToGet));

        Assert.Equal((6, "Form W-9 (Rev. March 2024)"), (doc.Metadata.PageCount, doc.Metadata.Title));
        Assert.True(doc.Structure.IsTagged);
        Assert.DoesNotContain(doc.Diagnostics.Entries, e => e.Code == PdfDiagnosticCodes.FormFieldsUnreadable);
        var fields = doc.Layout.Pages[0].FormFields;
        Assert.Equal(23, fields.Count);
        var name = Assert.Single(fields, f => f.Name == "topmostSubform[0].Page1[0].f1_01[0]");
        Assert.Equal((LayoutFormFieldKind.Text, (string?)null, new LayoutBox(58.6, 660, 576, 674)), (name.Kind, name.Value, name.Box));
        var box = Assert.Single(fields, f => f.Name.EndsWith(".c1_1[0]", StringComparison.Ordinal));
        Assert.Equal((LayoutFormFieldKind.Checkbox, (bool?)false), (box.Kind, box.IsChecked));
    }

    [Fact]
    public void The_W4_is_a_tagged_five_page_form()
    {
        var doc = PdfProofDocument.Load(Fixtures.Require(CorpusPaths.External("irs-w4-2026"), HowToGet));

        Assert.Equal((5, "2026 Form W-4"), (doc.Metadata.PageCount, doc.Metadata.Title));
        Assert.True(doc.Structure.IsTagged);
        var fields = doc.Layout.Pages[0].FormFields;
        Assert.Equal(19, fields.Count);
        var first = Assert.Single(fields, f => f.Name.EndsWith(".f1_01[0]", StringComparison.Ordinal));
        Assert.Equal((LayoutFormFieldKind.Text, new LayoutBox(94.6, 684, 272.9, 698)), (first.Kind, first.Box));
    }
}
