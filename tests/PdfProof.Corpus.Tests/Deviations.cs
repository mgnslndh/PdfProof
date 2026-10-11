using System.Text.Json;

namespace PdfProof.Corpus.Tests;

// deviations.json next to an oracle: { "<producer>": "<reason>" }, the known gaps of producers. A missing file means
// none.
internal static class Deviations
{
    public static IReadOnlyDictionary<string, string> Load(string path)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>();
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.EnumerateObject()
            .ToDictionary(entry => entry.Name, entry => entry.Value.GetString() ?? "", StringComparer.Ordinal);
    }
}
