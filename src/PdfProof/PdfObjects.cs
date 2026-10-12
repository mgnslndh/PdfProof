using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Tokens;

namespace PdfProof;

// Reading PDF objects at the token level, for the parts of a PDF PdfPig doesn't expose (the structure tree) or reads
// without guarding against cycles (the form field tree). Runs inside PdfPigCall: everything here reads PdfPig tokens.
internal static class PdfObjects
{
    // Follows indirect references to the object they point at; a chain longer than 32 is left unresolved.
    public static IToken? Resolve(PdfDocument document, IToken? token)
    {
        for (var i = 0; i < 32 && token is IndirectReferenceToken reference; i++)
        {
            token = document.Structure.GetObject(reference.Data)?.Data;
        }

        return token;
    }

    public static IToken? Get(DictionaryToken dictionary, string key) =>
        dictionary.Data.TryGetValue(key, out var value) ? value : null;

    // Page objects in page-tree order mapped to their page numbers, so /P and /Pg references resolve to pages. The
    // walk is iterative and visits each node once, so a damaged page tree can't loop.
    public static Dictionary<IndirectReference, int> PageNumbers(PdfDocument document)
    {
        var ordered = new List<IndirectReference>();
        var seen = new HashSet<IndirectReference>();
        var stack = new Stack<IToken>();
        if (Get(document.Structure.Catalog.CatalogDictionary, "Pages") is { } pages)
        {
            stack.Push(pages);
        }

        while (stack.Count > 0)
        {
            if (stack.Pop() is not IndirectReferenceToken reference || !seen.Add(reference.Data))
            {
                continue;
            }

            if (Resolve(document, reference) is not DictionaryToken node)
            {
                continue;
            }

            if (Resolve(document, Get(node, "Kids")) is not ArrayToken kids)
            {
                ordered.Add(reference.Data); // a leaf is a page, with or without /Type /Page (as PdfPig counts them)
            }
            else
            {
                for (var i = kids.Data.Count - 1; i >= 0; i--)
                {
                    stack.Push(kids.Data[i]); // reversed, so pages pop in document order
                }
            }
        }

        var numbers = new Dictionary<IndirectReference, int>();
        for (var i = 0; i < ordered.Count; i++)
        {
            numbers[ordered[i]] = i + 1;
        }

        return numbers;
    }
}
