using System.Collections.ObjectModel;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Tokens;

namespace PdfProof;

internal static class MetadataReader
{
    private static readonly HashSet<string> StandardKeys = new(StringComparer.Ordinal)
    {
        "Title", "Author", "Subject", "Keywords", "Creator", "Producer", "CreationDate", "ModDate",
    };

    public static PdfMetadata Read(string pdfVersion, int pageCount, DocumentInformation info)
    {
        var custom = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (info.DocumentInformationDictionary is { } dictionary)
        {
            foreach (var entry in dictionary.Data)
            {
                if (!StandardKeys.Contains(entry.Key))
                {
                    custom[entry.Key] = TokenText(entry.Value);
                }
            }
        }

        return new PdfMetadata(
            pdfVersion,
            pageCount,
            info.Title,
            info.Author,
            info.Subject,
            info.Keywords,
            info.Creator,
            info.Producer,
            info.GetCreatedDateTimeOffset(),
            info.GetModifiedDateTimeOffset(),
            new ReadOnlyDictionary<string, string>(custom));
    }

    private static string TokenText(IToken token) => token switch
    {
        StringToken text => text.Data,
        NameToken name => name.Data,
        _ => token.ToString() ?? "",
    };
}
