using System.Collections.ObjectModel;
using System.Globalization;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Tokens;

namespace PdfProof;

internal static class MetadataReader
{
    private static readonly HashSet<string> StandardKeys = new(StringComparer.Ordinal)
    {
        "Title", "Author", "Subject", "Keywords", "Creator", "Producer", "CreationDate", "ModDate",
    };

    public static PdfMetadata Read(PdfDocument document)
    {
        var info = document.Information;
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
            document.Version.ToString("0.0", CultureInfo.InvariantCulture),
            document.NumberOfPages,
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
