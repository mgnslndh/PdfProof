using PdfProof.Layout;

namespace PdfProof.Structure;

// Maps the raw tree to the public model: roles through /RoleMap, marked text from the layout pass. Runs outside
// PdfPigCall, so a bug here surfaces as itself.
internal sealed class StructureBuilder
{
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
        var content = new List<StructureContent>();
        foreach (var kid in raw.Kids)
        {
            if (Content(kid) is { } item)
            {
                content.Add(item);
            }
        }

        return new StructureNode(role, raw.Role == role ? null : raw.Role, raw.Page, null, null, null, null, 1, 1, content);
    }

    private StructureContent? Content(RawKid kid) => kid switch
    {
        RawElement element => Element(element),
        RawMarkedContent marked => MarkedText(marked),
        _ => null,
    };

    private StructureText? MarkedText(RawMarkedContent marked) =>
        marked.Page is { } page
        && _layout.MarkedText.TryGetValue(page, out var texts)
        && texts.TryGetValue(marked.Mcid, out var text)
        && text.Length > 0
            ? new StructureText(text, page, marked.Mcid)
            : null;

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
