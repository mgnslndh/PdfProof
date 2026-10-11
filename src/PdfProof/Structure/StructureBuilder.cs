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

    // The layout link on the same page with the same rectangle (normalized and rounded like the layout), so its text
    // and target resolve exactly as in LayoutPage.Links.
    private LayoutLink? Link(RawObjectReference reference)
    {
        if (reference.Subtype != "Link" || reference.Page is not { } page || page < 1 || page > _layout.Model.Pages.Count
            || reference.Rectangle is not { } r || r.Any(double.IsNaN))
        {
            return null;
        }

        var box = Geometry.Round(new LayoutBox(Math.Min(r[0], r[2]), Math.Min(r[1], r[3]), Math.Max(r[0], r[2]), Math.Max(r[1], r[3])), _options.Decimals);
        return _layout.Model.Pages[page - 1].Links.FirstOrDefault(link =>
            Math.Abs(link.Box.Left - box.Left) < 0.5 && Math.Abs(link.Box.Bottom - box.Bottom) < 0.5 &&
            Math.Abs(link.Box.Right - box.Right) < 0.5 && Math.Abs(link.Box.Top - box.Top) < 0.5);
    }

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
