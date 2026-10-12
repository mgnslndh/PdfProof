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
    private bool _skippedStreamContent;
    private bool _truncated;

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

        foreach (var page in PdfObjects.PageNumbers(_document))
        {
            _pageNumbers[page.Key] = page.Value;
        }
        var elements = Kids(root).Select(kid => ReadKid(kid, null, 0)).OfType<RawElement>().ToList();
        return new RawStructure(true, marked, roleMap, elements, _skippedStreamContent, _truncated);
    }

    private RawKid? ReadKid(IToken kid, int? page, int depth)
    {
        if (depth > MaxDepth)
        {
            _truncated = true;
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
            case DictionaryToken dictionary when Name(dictionary, "Type") == "MCR" && (Get(dictionary, "Stm") is not null || Get(dictionary, "StmOwn") is not null):
                // Marked content inside a form XObject or an annotation's appearance stream. PdfPig reports it mixed
                // with the page's own marked content, so it can't be resolved faithfully: leave it out and say so.
                _skippedStreamContent = true;
                return null;
            case DictionaryToken dictionary when Name(dictionary, "Type") == "MCR":
                return Resolve(Get(dictionary, "MCID")) is NumericToken id ? new RawMarkedContent(PageOf(dictionary) ?? page, id.Int) : null;
            case DictionaryToken dictionary when Name(dictionary, "Type") == "OBJR":
                return ObjectReference(dictionary, page);
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
        var (scope, rowSpan, columnSpan) = TableAttributes(element);
        return new RawElement(
            role,
            page,
            Text(element, "Alt"),
            Text(element, "ActualText"),
            Text(element, "Lang"),
            scope,
            rowSpan,
            columnSpan,
            kids);
    }

    // /A holds one attribute object or an array of them (optionally followed by revision numbers). Table attributes
    // are those with /O /Table.
    private (string? Scope, int RowSpan, int ColumnSpan) TableAttributes(DictionaryToken element)
    {
        string? scope = null;
        var rowSpan = 1;
        var columnSpan = 1;
        var attributes = Resolve(Get(element, "A")) switch
        {
            DictionaryToken single => [single],
            ArrayToken array => array.Data.Select(Resolve).OfType<DictionaryToken>().ToList(),
            _ => new List<DictionaryToken>(),
        };
        foreach (var attribute in attributes.Where(a => Name(a, "O") == "Table"))
        {
            scope = Name(attribute, "Scope") ?? scope;
            rowSpan = Resolve(Get(attribute, "RowSpan")) is NumericToken rows && rows.Int > 0 ? rows.Int : rowSpan;
            columnSpan = Resolve(Get(attribute, "ColSpan")) is NumericToken columns && columns.Int > 0 ? columns.Int : columnSpan;
        }

        return (scope, rowSpan, columnSpan);
    }

    private string? Text(DictionaryToken dictionary, string key) => Resolve(Get(dictionary, key)) switch
    {
        StringToken text => text.Data,
        HexToken hex => hex.Data,
        _ => null,
    };

    private RawObjectReference ObjectReference(DictionaryToken objr, int? elementPage)
    {
        if (Resolve(Get(objr, "Obj")) is not DictionaryToken annotation)
        {
            return new RawObjectReference(PageOf(objr) ?? elementPage, null, null);
        }

        double[]? rectangle = null;
        if (Resolve(Get(annotation, "Rect")) is ArrayToken { Length: 4 } rect)
        {
            rectangle = rect.Data.Select(token => Resolve(token) is NumericToken number ? number.Double : double.NaN).ToArray();
        }

        return new RawObjectReference(PageOf(objr) ?? AnnotationPage(annotation) ?? elementPage, Name(annotation, "Subtype"), rectangle);
    }

    // An annotation's own /P (page) entry.
    private int? AnnotationPage(DictionaryToken annotation) =>
        Get(annotation, "P") is IndirectReferenceToken reference && _pageNumbers.TryGetValue(reference.Data, out var number) ? number : null;

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

    private IToken? Resolve(IToken? token) => PdfObjects.Resolve(_document, token);

    private static IToken? Get(DictionaryToken dictionary, string key) => PdfObjects.Get(dictionary, key);

    private string? Name(DictionaryToken dictionary, string key) =>
        Resolve(Get(dictionary, key)) is NameToken name ? name.Data : null;
}
