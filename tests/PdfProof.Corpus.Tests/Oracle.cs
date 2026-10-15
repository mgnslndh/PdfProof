using System.Globalization;
using System.Text.Json;

namespace PdfProof.Corpus.Tests;

// The hand-written oracle of a generated corpus document: expected.semantics.json, in the JSON export format (spec
// §7.4). Only what the comparison reads is modelled, and what the oracle leaves out is not checked.
internal sealed record Oracle(string? Title, int? PageCount, IReadOnlyList<OracleBlock> Blocks, IReadOnlyList<OracleArtifact> Artifacts)
{
    public static Oracle Load(string path) => Parse(File.ReadAllText(path));

    public static Oracle Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var metadata = Property(root, "metadata");
        var semantics = Property(root, "semantics");
        var blocks = semantics is { } s1 && Property(s1, "blocks") is { } blockArray ? blockArray.EnumerateArray().Select(Block).ToList() : [];
        var artifacts = semantics is { } s2 && Property(s2, "artifacts") is { } artifactArray
            ? artifactArray.EnumerateArray().Select(a => new OracleArtifact(String(a, "role") ?? "", String(a, "text") ?? "", String(a, "pages"))).ToList()
            : [];
        return new Oracle(
            metadata is { } m1 ? String(m1, "title") : null,
            metadata is { } m2 ? Int(m2, "pageCount") : null,
            blocks,
            artifacts);
    }

    private static OracleBlock Block(JsonElement block)
    {
        var type = String(block, "type") ?? throw new InvalidDataException("A block has no \"type\".");
        if (type is not ("heading" or "paragraph" or "table" or "list" or "figure"))
        {
            throw new InvalidDataException($"Unknown block type \"{type}\".");
        }

        return new OracleBlock(
            type,
            Int(block, "level"),
            String(block, "text"),
            Int(block, "page"),
            Strings(Property(block, "header")),
            Property(block, "rows") is { } rows ? rows.EnumerateArray().Select(row => Strings(row)).ToList() : [],
            Strings(Property(block, "items")),
            Property(block, "links") is { } links
                ? links.EnumerateArray().Select(link => new OracleLink(String(link, "text") ?? "", String(link, "uri"), Int(link, "page"))).ToList()
                : []);
    }

    private static JsonElement? Property(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : null;

    private static string? String(JsonElement element, string name) => Property(element, name)?.GetString();

    private static int? Int(JsonElement element, string name) => Property(element, name)?.GetInt32();

    private static IReadOnlyList<string> Strings(JsonElement? array) =>
        array is { } items ? items.EnumerateArray().Select(item => item.GetString() ?? "").ToList() : [];
}

internal sealed record OracleBlock(
    string Type, int? Level, string? Text, int? Page, IReadOnlyList<string> Header, IReadOnlyList<IReadOnlyList<string>> Rows, IReadOnlyList<string> Items, IReadOnlyList<OracleLink> Links)
{
    // The texts the block shows, in reading order: a table's header, then its cells row by row; a list's items.
    public IEnumerable<string> Texts => Type switch
    {
        "table" => Header.Concat(Rows.SelectMany(row => row)),
        "list" => Items,
        _ => Text is null ? Enumerable.Empty<string>() : new[] { Text },
    };
}

internal sealed record OracleLink(string Text, string? Uri, int? Page);

internal sealed record OracleArtifact(string Role, string Text, string? Pages);
