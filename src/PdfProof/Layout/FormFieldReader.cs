using UglyToad.PdfPig;
using UglyToad.PdfPig.AcroForms.Fields;
using UglyToad.PdfPig.Core;

namespace PdfProof.Layout;

// Reads the AcroForm field tree into one LayoutFormField per widget, grouped by page. PdfPig parses the tree inside
// PdfPigCall; mapping it happens outside. Form fields are secondary to page content, like bookmarks and the structure
// tree: a form the parser rejects is reported and left out, and the document still loads.
internal static class FormFieldReader
{
    // The same limit as the structure tree: deeper fields are left out with a warning.
    private const int MaxDepth = 256;

    private static readonly IReadOnlyList<string> NoOptions = [];

    public static IReadOnlyDictionary<int, IReadOnlyList<LayoutFormField>> Read(PdfDocument document, LayoutOptions options, DiagnosticsCollector diagnostics)
    {
        IReadOnlyList<AcroFieldBase> roots;
        try
        {
            roots = PdfPigCall.Read(() => document.TryGetForm(out var form) ? form.Fields : (IReadOnlyList<AcroFieldBase>)[]);
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
        var truncated = false;

        void Walk(IEnumerable<AcroFieldBase> fields, string? parentName, int depth)
        {
            if (depth > MaxDepth)
            {
                truncated = true;
                return;
            }

            foreach (var field in fields)
            {
                var partial = field.Information.PartialName;
                var name = partial is null || partial.Length == 0 ? parentName ?? "" : parentName is null ? partial : parentName + "." + partial;
                if (field is AcroNonTerminalField parent)
                {
                    Walk(parent.Children, name, depth + 1);
                }
                else if (field.PageNumber is { } page && field.Bounds is { } bounds)
                {
                    widgets.Add((page, Map(field, name, bounds, options)));
                }
                else
                {
                    withoutPage++;
                }
            }
        }

        Walk(roots, null, 0);

        if (widgets.Count + withoutPage > 0)
        {
            diagnostics.Add(
                PdfDiagnosticCodes.FormFields,
                null,
                $"{widgets.Count} form field widgets" + (withoutPage > 0 ? $"; {withoutPage} without a page or box are left out" : ""));
        }

        if (truncated)
        {
            diagnostics.Add(PdfDiagnosticCodes.FormFieldsUnreadable, null, $"Form fields nested deeper than {MaxDepth} levels are left out.");
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
}
