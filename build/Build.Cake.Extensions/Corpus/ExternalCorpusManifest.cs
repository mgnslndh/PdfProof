using System.Text.Json;
using System.Text.RegularExpressions;

namespace Build.Cake.Extensions.Corpus;

public sealed record ExternalCorpusEntry(string Id, Uri Url, string Sha256, string License, string Source, string Purpose);

// corpus/external/manifest.json: the external PDFs the corpus tests use (spec §10.3). Nothing is listed without a
// license; ids are unique and become file names; hashes are lowercase SHA-256 hex.
public static class ExternalCorpusManifest
{
    public static IReadOnlyList<ExternalCorpusEntry> Load(string path) => Parse(File.ReadAllText(path));

    public static IReadOnlyList<ExternalCorpusEntry> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var entries = new List<ExternalCorpusEntry>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var number = entries.Count + 1;
            string Required(string name) =>
                item.TryGetProperty(name, out var value) && value.GetString() is { Length: > 0 } text
                    ? text
                    : throw new InvalidDataException($"Entry {number} has no \"{name}\".");

            var id = Required("id");
            if (!Regex.IsMatch(id, "^[a-z0-9-]+$"))
            {
                throw new InvalidDataException($"Entry \"{id}\": the id must be lowercase letters, digits and hyphens.");
            }

            var sha256 = Required("sha256");
            if (!Regex.IsMatch(sha256, "^[0-9a-f]{64}$"))
            {
                throw new InvalidDataException($"Entry \"{id}\": sha256 must be 64 lowercase hex digits.");
            }

            if (entries.Any(entry => entry.Id == id))
            {
                throw new InvalidDataException($"Entry \"{id}\" is listed twice.");
            }

            entries.Add(new ExternalCorpusEntry(id, new Uri(Required("url")), sha256, Required("license"), Required("source"), Required("purpose")));
        }

        return entries;
    }
}
