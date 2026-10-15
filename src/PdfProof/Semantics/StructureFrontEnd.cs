using PdfProof.Layout;
using PdfProof.Structure;

namespace PdfProof.Semantics;

// Tagged documents (spec §6, row 0b): structure elements become candidates with declared kinds. Containers are walked
// through; inline content is folded into runs in content order. Producer quirks: Chromium wraps text in NonStruct,
// QuestPDF puts a P inside every table cell and list item and its link annotations on the P, list labels are Lbl
// elements, and Typst writes a nested list beside its items (L > [LI, L, LI]). Tags are trusted: these candidates are
// not reclassified. Each line gets the box of its marked content, so artifacts and links can be placed by position.
internal static class StructureFrontEnd
{
    private static readonly HashSet<string> Containers = new(StringComparer.Ordinal)
    {
        "StructTreeRoot", "Document", "DocumentFragment", "Part", "Sect", "Div", "Art", "Aside", "BlockQuote", "NonStruct",
        "Private", "TOC", "TOCI", "Index", "Form",
    };

    private static readonly HashSet<string> Paragraphs = new(StringComparer.Ordinal) { "P", "Link", "Caption", "Quote", "Code", "Note" };

    public static List<Candidate> Build(StructureModel structure, SemanticContext context)
    {
        var candidates = new List<Candidate>();
        Walk(structure.Root, candidates, context);
        return candidates;
    }

    private static void Walk(StructureNode node, List<Candidate> candidates, SemanticContext context)
    {
        var role = node.Role;
        if (HeadingLevel(role) is { } level)
        {
            if (Text(node, context, CandidateKind.Heading) is { } heading)
            {
                heading.Level = level;
                candidates.Add(heading);
            }
        }
        else if (Paragraphs.Contains(role))
        {
            if (Text(node, context, CandidateKind.Paragraph) is { } paragraph)
            {
                candidates.Add(paragraph);
            }
        }
        else if (role == "L")
        {
            if (List(node, context) is { } list)
            {
                candidates.Add(list);
            }
        }
        else if (role == "Table")
        {
            if (Table(node, context) is { } table)
            {
                candidates.Add(table);
            }
        }
        else if (role is "Figure" or "Formula")
        {
            candidates.Add(new Candidate(Page(node), [], declared: true) { Kind = CandidateKind.Figure, Alt = node.Alt ?? node.ActualText });
        }
        else if (Containers.Contains(role) || !node.Content.OfType<StructureText>().Any())
        {
            foreach (var child in node.Children)
            {
                Walk(child, candidates, context);
            }
        }
        else if (Text(node, context, CandidateKind.Paragraph) is { } other)
        {
            candidates.Add(other); // an unknown role with text of its own reads as a paragraph
        }
    }

    private static int? HeadingLevel(string role) =>
        role == "H" ? 1
        : role.Length == 2 && role[0] == 'H' && role[1] >= '1' && role[1] <= '6' ? role[1] - '0'
        : null;

    // A Link element at block level reads as a paragraph whose text is linked.
    private static Candidate? Text(StructureNode node, SemanticContext context, CandidateKind kind)
    {
        var pending = new List<LayoutLink>();
        var runs = Runs(node, context, pending, node.Role == "Link" ? LinkOf(node, context) : null, skipLists: false);
        if (runs.Count == 0)
        {
            return null;
        }

        var page = Page(node);
        var candidate = new Candidate(page, [Line(runs, Box(node, context, page))], declared: true) { Kind = kind };
        candidate.PendingLinks.AddRange(pending);
        return candidate;
    }

    private static Candidate? List(StructureNode node, SemanticContext context)
    {
        var pending = new List<LayoutLink>();
        var items = new List<CandidateLine>();
        Items(node, context, pending, items);
        if (items.Count == 0)
        {
            return null;
        }

        var list = new Candidate(Page(node), items, declared: true) { Kind = CandidateKind.List };
        list.PendingLinks.AddRange(pending);
        return list;
    }

    // Each LI is an item. A nested list (nesting is Plan 5b) is flattened into the items: after its parent item when it
    // is inside the item (LI > L, or LI > LBody > L), or in place when it sits beside the items (L > L, as Typst writes).
    private static void Items(StructureNode list, SemanticContext context, List<LayoutLink> pending, List<CandidateLine> items)
    {
        foreach (var child in list.Children)
        {
            if (child.Role == "LI")
            {
                var runs = Runs(child, context, pending, null, skipLists: true);
                if (runs.Count > 0)
                {
                    var page = Page(child);
                    items.Add(Line(runs, Box(child, context, page)));
                }

                foreach (var nested in NestedLists(child))
                {
                    Flatten(nested, context, pending, items);
                }
            }
            else if (child.Role == "L")
            {
                Flatten(child, context, pending, items);
            }
        }
    }

    private static void Flatten(StructureNode nested, SemanticContext context, List<LayoutLink> pending, List<CandidateLine> items)
    {
        context.Decide(SemanticPass.StructureFrontEnd, Page(nested), null, "a nested list is flattened into its parent list");
        Items(nested, context, pending, items);
    }

    private static IEnumerable<StructureNode> NestedLists(StructureNode item) =>
        item.Children.SelectMany(child =>
            child.Role == "L" ? [child]
            : child.Role == "LBody" ? child.Children.Where(grandchild => grandchild.Role == "L")
            : Enumerable.Empty<StructureNode>());

    private static Candidate? Table(StructureNode node, SemanticContext context)
    {
        var pending = new List<LayoutLink>();
        var rows = new List<(StructureNode Row, bool InHead)>();
        Rows(node, inHead: false, rows);
        var page = Page(node);
        var lines = new List<CandidateLine>();
        var header = 0;
        foreach (var (row, inHead) in rows)
        {
            var cells = row.Children.Where(child => child.Role is "TH" or "TD").ToList();
            if (cells.Count == 0)
            {
                continue;
            }

            if (lines.Count == 0 && (inHead || cells.All(cell => cell.Role == "TH")))
            {
                header = 1;
            }

            var candidateCells = cells.Select(cell => new CandidateCell(Runs(cell, context, pending, null, skipLists: false), Box(cell, context, page))).ToList();
            var boxes = candidateCells.Select(cell => cell.Box).Where(box => box != default).ToList();
            lines.Add(new CandidateLine(candidateCells, boxes.Count == 0 ? default : Geometry.Union(boxes), 0, false));
        }

        if (lines.Count == 0)
        {
            return null;
        }

        var table = new Candidate(page, lines, declared: true) { Kind = CandidateKind.Table, HeaderRows = header };
        table.PendingLinks.AddRange(pending);
        return table;
    }

    private static void Rows(StructureNode node, bool inHead, List<(StructureNode Row, bool InHead)> rows)
    {
        foreach (var child in node.Children)
        {
            if (child.Role == "TR")
            {
                rows.Add((child, inHead));
            }
            else if (child.Role is "THead" or "TBody" or "TFoot")
            {
                Rows(child, inHead || child.Role == "THead", rows);
            }
        }
    }

    // The element's inline content as runs, in content order. A Link child gives its text the link from its OBJR; an
    // OBJR link on another element is left for LinkAttachment. Both are claimed, so LinkAttachment doesn't place them a
    // second time by geometry. Lbl (list labels) is left out; nested lists are left out of list items (they are
    // flattened into the list) and folded in everywhere else, such as a list inside a table cell.
    private static IReadOnlyList<SemanticRun> Runs(StructureNode node, SemanticContext context, List<LayoutLink> pending, SemanticLink? link, bool skipLists)
    {
        var pieces = new List<IReadOnlyList<SemanticRun>>();
        foreach (var content in node.Content)
        {
            switch (content)
            {
                case StructureText text:
                    var (bold, italic) = Style(context, text);
                    pieces.Add([new SemanticRun(text.Text, bold, italic, link)]);
                    break;
                case StructureNode { Role: "Lbl" }:
                    break;
                case StructureNode { Role: "L" } when skipLists:
                    break;
                case StructureNode child:
                    pieces.Add(Runs(child, context, pending, child.Role == "Link" ? LinkOf(child, context) ?? link : link, skipLists));
                    break;
                case StructureObjectReference { Link: { } layoutLink } when node.Role != "Link":
                    pending.Add(layoutLink);
                    context.ClaimedLinks.Add(layoutLink);
                    break;
            }
        }

        return SemanticText.Concat(pieces);
    }

    private static (bool IsBold, bool IsItalic) Style(SemanticContext context, StructureText text) =>
        Marked(context, text) is { } marked ? (marked.IsBold, marked.IsItalic) : (false, false);

    private static MarkedText? Marked(SemanticContext context, StructureText text) =>
        text.Page is { } page && context.Build.MarkedText.TryGetValue(page, out var texts) && texts.TryGetValue(text.MarkedContentId, out var marked)
            ? marked
            : null;

    private static SemanticLink? LinkOf(StructureNode link, SemanticContext context)
    {
        var found = link.Content.OfType<StructureObjectReference>().Select(reference => reference.Link).FirstOrDefault(candidate => candidate is not null);
        if (found is null)
        {
            return null;
        }

        context.ClaimedLinks.Add(found);
        return new SemanticLink(found.Uri, found.TargetPage);
    }

    // Where the element's marked content is on `page`, from the boxes collected with the marked text; default when none.
    private static LayoutBox Box(StructureNode node, SemanticContext context, int page)
    {
        var boxes = new List<LayoutBox>();

        void Collect(StructureNode current)
        {
            foreach (var content in current.Content)
            {
                if (content is StructureText text && text.Page == page && Marked(context, text) is { } marked && marked.Box != default)
                {
                    boxes.Add(marked.Box);
                }
                else if (content is StructureNode child)
                {
                    Collect(child);
                }
            }
        }

        Collect(node);
        return boxes.Count == 0 ? default : Geometry.Union(boxes);
    }

    private static CandidateLine Line(IReadOnlyList<SemanticRun> runs, LayoutBox box) => new([new CandidateCell(runs, box)], box, 0, false);

    // The element's page, or the first page found in its content.
    private static int Page(StructureNode node)
    {
        if (node.Page is { } page)
        {
            return page;
        }

        foreach (var content in node.Content)
        {
            var found = content is StructureNode child ? Page(child) : content.Page ?? 0;
            if (found > 0)
            {
                return found;
            }
        }

        return 1;
    }
}
