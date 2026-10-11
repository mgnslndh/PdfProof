using System.Text.Encodings.Web;
using System.Text.Json;

namespace PdfProof.FixtureGenerator;

internal sealed record ManifestEntry(string Producer, string? Version, string Status, string? Reason);

// What one run produced, written to <output>/manifest.json: the documents, and per producer its version and status
// (generated, unavailable or failed, with the reason). Deterministic: producers in name order, "\n" line endings and
// a trailing newline, no timestamps.
internal static class Manifest
{
    public static void Write(string path, IReadOnlyList<string> documents, IReadOnlyList<ManifestEntry> entries)
    {
        using var stream = File.Create(path);
        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            json.WriteStartObject();
            json.WriteStartArray("documents");
            foreach (var document in documents)
            {
                json.WriteStringValue(document);
            }

            json.WriteEndArray();
            json.WriteStartArray("producers");
            foreach (var entry in entries.OrderBy(entry => entry.Producer, StringComparer.Ordinal))
            {
                json.WriteStartObject();
                json.WriteString("name", entry.Producer);
                if (entry.Version is not null)
                {
                    json.WriteString("version", entry.Version);
                }

                json.WriteString("status", entry.Status);
                if (entry.Reason is not null)
                {
                    json.WriteString("reason", entry.Reason);
                }

                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        stream.WriteByte((byte)'\n');
    }
}
