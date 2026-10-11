using UglyToad.PdfPig;
using UglyToad.PdfPig.AcroForms.Fields;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Tokens;

namespace PdfProof.Layout;

// Reads the AcroForm field tree into one LayoutFormField per widget, grouped by page. PdfPig parses the tree inside
// PdfPigCall; mapping it happens outside. Form fields are secondary to page content, like bookmarks and the structure
// tree: a form the parser rejects is reported and left out, and the document still loads.
//
// Two gaps in PdfPig 0.1.16 are covered at the token level:
// - Its form reader recurses through /Kids without a visited set, so a cycle overflows the stack, which nothing can
//   catch. The tree is checked first, and a cyclic or over-deep tree is reported instead of read.
// - For text, choice, signature and push button fields it drops the widget kids, so a field shown through separate
//   widgets has no page or box. Those widgets are read from the field's /Kids, with the page from the widget's /P or,
//   without one, from the page whose /Annots lists it.
internal static class FormFieldReader
{
    // The same limit as the structure tree: deeper trees are reported and left out.
    private const int MaxDepth = 256;

    // Visits allowed while checking the tree; bounds the work on a tree that shares kids between many parents.
    private const int MaxVisits = 100_000;

    private static readonly IReadOnlyList<string> NoOptions = [];

    public static IReadOnlyDictionary<int, IReadOnlyList<LayoutFormField>> Read(PdfDocument document, LayoutOptions options, DiagnosticsCollector diagnostics)
    {
        IReadOnlyList<AcroFieldBase> roots;
        try
        {
            roots = PdfPigCall.Read(() =>
            {
                if (TreeProblem(document) is { } problem)
                {
                    throw new InvalidDataException(problem + ".");
                }

                return document.TryGetForm(out var form) ? form.Fields : (IReadOnlyList<AcroFieldBase>)[];
            });
        }
        catch (PdfProofException ex)
        {
            diagnostics.Add(
                PdfDiagnosticCodes.FormFieldsUnreadable,
                null,
                "The form fields could not be read and are left out: " + (ex.InnerException ?? ex).Message);
            return new Dictionary<int, IReadOnlyList<LayoutFormField>>();
        }

        var widgets = new List<(int Page, LayoutFormField Field)>();
        var withoutPage = 0;
        WidgetPages? pages = null;

        // The tree was checked for depth and cycles above, so this recursion is bounded.
        void Walk(IEnumerable<AcroFieldBase> fields, string? parentName)
        {
            foreach (var field in fields)
            {
                var partial = field.Information.PartialName;
                var name = partial is null || partial.Length == 0 ? parentName ?? "" : parentName is null ? partial : parentName + "." + partial;
                if (field is AcroNonTerminalField parent)
                {
                    Walk(parent.Children, name);
                }
                else if (field.PageNumber is { } page && field.Bounds is { } bounds)
                {
                    widgets.Add((page, Map(field, name, bounds, options)));
                }
                else
                {
                    var known = pages ??= PdfPigCall.Read(() => WidgetPages.Read(document));
                    var kids = PdfPigCall.Read(() => WidgetKids(document, field.Dictionary, known));
                    if (kids.Count == 0)
                    {
                        withoutPage++;
                    }

                    foreach (var kid in kids)
                    {
                        if (kid.Page is { } kidPage)
                        {
                            widgets.Add((kidPage, Map(field, name, kid.Bounds, options)));
                        }
                        else
                        {
                            withoutPage++;
                        }
                    }
                }
            }
        }

        Walk(roots, null);

        if (widgets.Count + withoutPage > 0)
        {
            diagnostics.Add(
                PdfDiagnosticCodes.FormFields,
                null,
                $"{widgets.Count} form field widgets" + (withoutPage > 0 ? $"; {withoutPage} without a page or box are left out" : ""));
        }

        return widgets
            .GroupBy(widget => widget.Page)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<LayoutFormField>)group
                    .Select(widget => widget.Field)
                    .OrderByDescending(field => field.Box.Top).ThenBy(field => field.Box.Left).ThenBy(field => field.Name, StringComparer.Ordinal)
                    .ToList());
    }

    // Why PdfPig's recursive form reader must not read this tree (a cycle, too deep, too many nodes), or null.
    private static string? TreeProblem(PdfDocument document)
    {
        var catalog = document.Structure.Catalog.CatalogDictionary;
        if (PdfObjects.Resolve(document, PdfObjects.Get(catalog, "AcroForm")) is not DictionaryToken form
            || PdfObjects.Resolve(document, PdfObjects.Get(form, "Fields")) is not ArrayToken fields)
        {
            return null;
        }

        var path = new HashSet<IndirectReference>();
        var visits = 0;

        string? Visit(IToken kid, int depth)
        {
            if (depth > MaxDepth)
            {
                return $"the field tree is nested deeper than {MaxDepth} levels";
            }

            if (++visits > MaxVisits)
            {
                return $"the field tree has more than {MaxVisits} nodes";
            }

            IndirectReference? reference = kid is IndirectReferenceToken indirect ? indirect.Data : null;
            if (reference is { } onPath && !path.Add(onPath))
            {
                return "the field tree has a cycle";
            }

            try
            {
                if (PdfObjects.Resolve(document, kid) is DictionaryToken node
                    && PdfObjects.Resolve(document, PdfObjects.Get(node, "Kids")) is ArrayToken kids)
                {
                    foreach (var child in kids.Data)
                    {
                        if (Visit(child, depth + 1) is { } problem)
                        {
                            return problem;
                        }
                    }
                }

                return null;
            }
            finally
            {
                if (reference is { } done)
                {
                    path.Remove(done);
                }
            }
        }

        foreach (var field in fields.Data)
        {
            if (Visit(field, 0) is { } problem)
            {
                return problem;
            }
        }

        return null;
    }

    // The widget annotations in a terminal field's /Kids, with their page (from /P, or from the page whose /Annots
    // lists them) and rectangle. Kids without a numeric rectangle are skipped.
    private static List<(int? Page, PdfRectangle Bounds)> WidgetKids(PdfDocument document, DictionaryToken field, WidgetPages pages)
    {
        var widgets = new List<(int? Page, PdfRectangle Bounds)>();
        if (PdfObjects.Resolve(document, PdfObjects.Get(field, "Kids")) is not ArrayToken kids)
        {
            return widgets;
        }

        foreach (var kid in kids.Data)
        {
            if (PdfObjects.Resolve(document, kid) is not DictionaryToken widget
                || PdfObjects.Resolve(document, PdfObjects.Get(widget, "Rect")) is not ArrayToken { Length: 4 } rect)
            {
                continue;
            }

            var corners = rect.Data.Select(token => PdfObjects.Resolve(document, token) as NumericToken).ToList();
            if (corners.Any(corner => corner is null))
            {
                continue;
            }

            int? page = PdfObjects.Get(widget, "P") is IndirectReferenceToken onPage && pages.ByPage.TryGetValue(onPage.Data, out var byP) ? byP
                : kid is IndirectReferenceToken self && pages.ByAnnotation.TryGetValue(self.Data, out var byAnnots) ? byAnnots
                : null;
            widgets.Add((page, new PdfRectangle(corners[0]!.Double, corners[1]!.Double, corners[2]!.Double, corners[3]!.Double)));
        }

        return widgets;
    }

    private static LayoutFormField Map(AcroFieldBase field, string name, PdfRectangle bounds, LayoutOptions options)
    {
        LayoutFormFieldKind kind;
        string? value = null;
        bool? isChecked = null;
        var choices = NoOptions;
        switch (field)
        {
            case AcroTextField text:
                kind = LayoutFormFieldKind.Text;
                value = string.IsNullOrEmpty(text.Value) ? null : text.Value;
                break;
            case AcroCheckboxField checkbox:
                kind = LayoutFormFieldKind.Checkbox;
                value = checkbox.CurrentValue?.Data;
                isChecked = checkbox.IsChecked;
                break;
            case AcroRadioButtonField radio:
                kind = LayoutFormFieldKind.RadioButton;
                value = radio.CurrentValue?.Data;
                isChecked = radio.IsSelected;
                break;
            case AcroComboBoxField combo:
                kind = LayoutFormFieldKind.ComboBox;
                (value, choices) = Choice(combo.Options);
                break;
            case AcroListBoxField list:
                kind = LayoutFormFieldKind.ListBox;
                (value, choices) = Choice(list.Options);
                break;
            case AcroPushButtonField:
                kind = LayoutFormFieldKind.PushButton;
                break;
            case AcroSignatureField:
                kind = LayoutFormFieldKind.Signature;
                break;
            default:
                kind = LayoutFormFieldKind.Unknown;
                break;
        }

        var tooltip = field.Information.AlternateName;
        return new LayoutFormField(
            name,
            kind,
            value,
            isChecked,
            choices,
            string.IsNullOrEmpty(tooltip) ? null : tooltip,
            (field.FieldFlags & 1) != 0,
            (field.FieldFlags & 2) != 0,
            Geometry.Round(Geometry.Bounds(bounds), options.Decimals));
    }

    private static (string? Value, IReadOnlyList<string> Options) Choice(IReadOnlyList<AcroChoiceOption> options)
    {
        var selected = options.Where(option => option.IsSelected).Select(option => option.Name).ToList();
        return (selected.Count == 0 ? null : string.Join(", ", selected), options.Select(option => option.Name).ToList());
    }

    // Page numbers by page object and by annotation object (from each page's /Annots), read once per document.
    private sealed class WidgetPages
    {
        private WidgetPages(Dictionary<IndirectReference, int> byPage, Dictionary<IndirectReference, int> byAnnotation)
        {
            ByPage = byPage;
            ByAnnotation = byAnnotation;
        }

        public Dictionary<IndirectReference, int> ByPage { get; }

        public Dictionary<IndirectReference, int> ByAnnotation { get; }

        public static WidgetPages Read(PdfDocument document)
        {
            var byPage = PdfObjects.PageNumbers(document);
            var byAnnotation = new Dictionary<IndirectReference, int>();
            foreach (var page in byPage.OrderBy(entry => entry.Value))
            {
                if (PdfObjects.Resolve(document, new IndirectReferenceToken(page.Key)) is DictionaryToken node
                    && PdfObjects.Resolve(document, PdfObjects.Get(node, "Annots")) is ArrayToken annotations)
                {
                    foreach (var annotation in annotations.Data.OfType<IndirectReferenceToken>())
                    {
                        if (!byAnnotation.ContainsKey(annotation.Data))
                        {
                            byAnnotation[annotation.Data] = page.Value;
                        }
                    }
                }
            }

            return new WidgetPages(byPage, byAnnotation);
        }
    }
}
