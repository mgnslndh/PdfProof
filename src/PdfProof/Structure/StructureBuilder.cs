using System.Text.RegularExpressions;
using PdfProof.Layout;

namespace PdfProof.Structure;

// Maps the raw tree to the public model: roles through /RoleMap, marked text from the layout pass. Runs outside
// PdfPigCall, so a bug here surfaces as itself.
internal sealed class StructureBuilder
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.CultureInvariant);

    private readonly RawStructure _raw;
    private readonly LayoutBuildResult _layout;
    private readonly LayoutOptions _options;

    private StructureBuilder(RawStructure raw, LayoutBuildResult layout, LayoutOptions options)
    {
        _raw = raw;
        _layout = layout;
        _options = options;
    }

    public static StructureModel Build(RawStructure raw, LayoutBuildResult layout, LayoutOptions options)
    {
        var builder = new StructureBuilder(raw, layout, options);
        var elements = raw.Elements.Select(builder.Element).OfType<StructureNode>().ToList();
        return new StructureModel(new StructureNode("StructTreeRoot", null, null, null, null, null, null, 1, 1, elements));
    }

    private StructureNode? Element(RawElement raw)
    {
        var role = MapRole(raw.Role);
        if (role == "Artifact")
        {
            return null;
        }

        var content = new List<StructureContent>();
        foreach (var kid in raw.Kids)
        {
            if (Content(kid) is { } item)
            {
                content.Add(item);
            }
        }

        var actualText = Normalize(raw.ActualText);
        var alt = Normalize(raw.Alt);
        var node = new StructureNode(role, raw.Role == role ? null : raw.Role, raw.Page, alt, actualText, raw.Language, raw.Scope, raw.RowSpan, raw.ColumnSpan, content);
        return alt is not null && alt == node.Text
            ? new StructureNode(role, node.CustomRole, raw.Page, null, actualText, raw.Language, raw.Scope, raw.RowSpan, raw.ColumnSpan, content) // some producers repeat the text as /Alt
            : node;
    }

    private string? Normalize(string? text) =>
        text is null ? null : Whitespace.Replace(WordFactory.Normalize(text, _options), " ").Trim();

    private StructureContent? Content(RawKid kid) => kid switch
    {
        RawElement element => Element(element),
        RawMarkedContent marked => MarkedText(marked),
        RawObjectReference reference => new StructureObjectReference(reference.Subtype ?? "Unknown", reference.Page, Link(reference)),
        _ => null,
    };

    private StructureText? MarkedText(RawMarkedContent marked) =>
        marked.Page is { } page
        && _layout.MarkedText.TryGetValue(page, out var texts)
        && texts.TryGetValue(marked.Mcid, out var text)
        && text.Length > 0
            ? new StructureText(text, page, marked.Mcid)
            : null;

    // The layout link on the same page whose annotation has the same /Rect as written in the PDF, so its text and
    // target resolve exactly as in LayoutPage.Links. Raw rectangles are compared, because PdfPig transforms layout
    // rectangles by the page's crop box and rotation.
    private LayoutLink? Link(RawObjectReference reference)
    {
        if (reference.Subtype != "Link" || reference.Page is not { } page || page < 1 || page > _layout.Model.Pages.Count
            || reference.Rectangle is not { } rectangle)
        {
            return null;
        }

        return _layout.Model.Pages[page - 1].Links.FirstOrDefault(link => link.SourceRectangle is { } source && SameRectangle(source, rectangle));
    }

    private static bool SameRectangle(IReadOnlyList<double> a, double[] b) =>
        a.Count == 4 && b.Length == 4 && Enumerable.Range(0, 4).All(i => Math.Abs(a[i] - b[i]) < 0.001);

    // RoleMap can chain (Custom -> Custom2 -> P); stop at a role that isn't mapped further.
    private string MapRole(string role)
    {
        for (var i = 0; i < 10 && _raw.RoleMap.TryGetValue(role, out var mapped) && mapped != role; i++)
        {
            role = mapped;
        }

        return role;
    }
}
