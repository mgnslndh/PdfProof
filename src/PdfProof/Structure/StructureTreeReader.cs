using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Tokens;

namespace PdfProof.Structure;

// Reads the logical structure tree of a Tagged PDF (ISO 32000 section 14.7) from the catalog's /StructTreeRoot into a
// raw tree. PdfPig exposes marked content per page but not the tree. Runs inside PdfPigCall: everything here reads
// PdfPig tokens. Cycles and depth are bounded, so damaged trees load in bounded time.
internal sealed class StructureTreeReader
{
    private const int MaxDepth = 256;

    private readonly PdfDocument _document;
    private readonly Dictionary<IndirectReference, int> _pageNumbers = [];
    private readonly HashSet<IndirectReference> _visited = [];

    private StructureTreeReader(PdfDocument document)
    {
        _document = document;
    }

    public static RawStructure Read(PdfDocument document) => new StructureTreeReader(document).ReadRoot();

    private RawStructure ReadRoot()
    {
        var catalog = _document.Structure.Catalog.CatalogDictionary;
        var marked = Resolve(Get(catalog, "MarkInfo")) is DictionaryToken markInfo
            && Resolve(Get(markInfo, "Marked")) is BooleanToken { Data: true };
        if (Resolve(Get(catalog, "StructTreeRoot")) is not DictionaryToken root)
        {
            return RawStructure.None with { Marked = marked };
        }

        var roleMap = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Resolve(Get(root, "RoleMap")) is DictionaryToken map)
        {
            foreach (var entry in map.Data)
            {
                if (Resolve(entry.Value) is NameToken name)
                {
                    roleMap[entry.Key] = name.Data;
                }
            }
        }

        IndexPages(Get(catalog, "Pages"));
        var elements = Kids(root).Select(kid => ReadKid(kid, null, 0)).OfType<RawElement>().ToList();
        return new RawStructure(true, marked, roleMap, elements);
    }

    private RawKid? ReadKid(IToken kid, int? page, int depth)
    {
        if (depth > MaxDepth)
        {
            return null;
        }

        if (kid is IndirectReferenceToken reference && !_visited.Add(reference.Data))
        {
            return null; // cycle guard
        }

        switch (Resolve(kid))
        {
            case NumericToken mcid:
                return new RawMarkedContent(page, mcid.Int);
            case DictionaryToken dictionary when Name(dictionary, "Type") == "MCR":
                return Get(dictionary, "MCID") is NumericToken id ? new RawMarkedContent(PageOf(dictionary) ?? page, id.Int) : null;
            case DictionaryToken dictionary when Name(dictionary, "Type") == "OBJR":
                return null; // Task 4
            case DictionaryToken dictionary when Name(dictionary, "S") is { } role:
                return ReadElement(dictionary, role, page, depth);
            default:
                return null;
        }
    }

    private RawElement ReadElement(DictionaryToken element, string role, int? inheritedPage, int depth)
    {
        var page = PageOf(element) ?? inheritedPage;
        var kids = Kids(element).Select(kid => ReadKid(kid, page, depth + 1)).OfType<RawKid>().ToList();
        return new RawElement(role, page, null, null, null, null, 1, 1, kids);
    }

    // Page objects in page-tree order, so /Pg references resolve to page numbers.
    private void IndexPages(IToken? pages)
    {
        var ordered = new List<IndirectReference>();
        var seen = new HashSet<IndirectReference>();
        var stack = new Stack<IToken>();
        if (pages is not null)
        {
            stack.Push(pages);
        }

        while (stack.Count > 0)
        {
            if (stack.Pop() is not IndirectReferenceToken reference || !seen.Add(reference.Data))
            {
                continue;
            }

            if (Resolve(reference) is not DictionaryToken node)
            {
                continue;
            }

            if (Name(node, "Type") == "Page")
            {
                ordered.Add(reference.Data);
            }
            else if (Resolve(Get(node, "Kids")) is ArrayToken kids)
            {
                for (var i = kids.Data.Count - 1; i >= 0; i--)
                {
                    stack.Push(kids.Data[i]); // reversed, so pages pop in document order
                }
            }
        }

        for (var i = 0; i < ordered.Count; i++)
        {
            _pageNumbers[ordered[i]] = i + 1;
        }
    }

    private int? PageOf(DictionaryToken dictionary) =>
        Get(dictionary, "Pg") is IndirectReferenceToken reference && _pageNumbers.TryGetValue(reference.Data, out var number) ? number : null;

    private IEnumerable<IToken> Kids(DictionaryToken dictionary)
    {
        var kids = Get(dictionary, "K");
        return Resolve(kids) switch
        {
            null => [],
            ArrayToken array => array.Data,
            _ => [kids!], // keep an indirect reference, so ReadKid can guard against cycles
        };
    }

    private IToken? Resolve(IToken? token)
    {
        for (var i = 0; i < 32 && token is IndirectReferenceToken reference; i++)
        {
            token = _document.Structure.GetObject(reference.Data)?.Data;
        }

        return token;
    }

    private static IToken? Get(DictionaryToken dictionary, string key) =>
        dictionary.Data.TryGetValue(key, out var value) ? value : null;

    private string? Name(DictionaryToken dictionary, string key) =>
        Resolve(Get(dictionary, key)) is NameToken name ? name.Data : null;
}
