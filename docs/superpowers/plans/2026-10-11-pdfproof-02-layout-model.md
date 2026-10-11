# PdfProof Plan 2: Layout Model Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `PdfProofDocument.Load` gains `Layout` and `Diagnostics`. `Layout` holds pages → blocks → lines → spans, plus links, images, annotations and bookmarks. Text is normalized and clipped, and grouped by a pluggable layout strategy (`Rows`, `ReadingOrder` or a custom one), with rotated text in its own frame. Load can take options and profiles, and logs what it decided.

**Architecture:**
- **Porting:** the spike's layout code is ported into `PdfProof.Layout` and rebuilt around two kinds of type:
  - our own public word types (`LayoutWord`, `LayoutWordLine`, `LayoutWordBlock`), so a custom `ILayoutStrategy` never sees PdfPig types;
  - immutable model classes with internal constructors.
- **Error handling:** every call into PdfPig goes through `PdfPigCall.Read`. Parser failures become `PdfProofException`, with the page number where known; PdfProof's own bugs surface as themselves. This resolves the Plan 1 review item.
- **Tests:** a small test helper, `RawPdf`, writes PDFs from raw PDF syntax, so tests control positions, clipping, rotation, annotations, outlines and info entries exactly. It needs no fixture files, and Plan 3 reuses it for structure trees.

**Tech Stack:** PdfPig 0.1.16 (`DefaultWordExtractor`, `NearestNeighbourWordExtractor`, `DocstrumBoundingBoxes`, `UnsupervisedReadingOrderDetector`, `ParsingOptions.ClipPaths`), Microsoft.Extensions.Logging.Abstractions 10.0.12, xUnit v3.

**Spec:** `docs/superpowers/specs/2026-10-11-pdfproof-v1-design.md` (§3 D4, D5, D8, D14; §5.1; §5.2; §5.3; §5.4)

## Where this plan fits

| Plan | Delivers |
|---|---|
| 1 Foundation | Done (PR #1) |
| **2 Layout model (this plan)** | Layout model, options and profiles, diagnostics and logging, error wrapping |
| 3 Structure model | `StructureModel`, structure tree reader. Reuses `RawPdf` for tagged test PDFs. |
| 4 Fixture generator and corpus | Generator, `invoice` per producer, corpus matrix. **Also, moved from this plan:** AcroForm fields and encrypted PDFs (password option and error message). Both need real fixtures; PdfPig can't write either, and PDFsharp, a v1 producer, can. |
| 5 Semantic passes | Candidate blocks, passes, `SemanticModel` |
| 6 Export | Export tree, scrubbing, Markdown and JSON |
| 7 CLI, skill and `0.1.0-alpha` | `export`, `explain`, `skill …`, first pre-release |

**Deferred to the `0.2.0-alpha` plans (assertions):**
- recording whether fonts are embedded (needed by invariant PP009);
- counting the letters removed by clipping (used by failure-message notes).

Both need extra PDF access that nothing in `0.1.0-alpha` uses.

## Global Constraints

- Library target frameworks: `netstandard2.0;net8.0;net10.0`. Tests: `net48;net10.0` on Windows, `net10.0` elsewhere.
- **net48/netstandard2.0-safe code in `src/`:** no `GeneratedRegex`, `SHA256.HashData`, `Convert.ToHexString*`, `Math.Clamp`, index-from-end `[^1]`, `ToHashSet`, `KeyValuePair` deconstruction or `string.Join(char, …)`. Use the alternatives shown in the task code.
- `TreatWarningsAsErrors`, nullable enabled, and XML docs on every public member.
- **Every public API change goes into `PublicAPI.Unshipped.txt`.** After adding public members, run this from the repository root (bash):
  ```bash
  dotnet build src/PdfProof/PdfProof.csproj 2>&1 | sed -n "s/.*error RS0016: Symbol '\([^']*\)' is not part of the declared public API.*/\1/p" | sort -u >> src/PdfProof/PublicAPI.Unshipped.txt
  ```
  Then rebuild; expect 0 errors. Check that the appended lines are only the members you added.
- Deterministic output: invariant culture, explicit sort orders with tie-breakers.
- Never write real company or customer names. Sample data is neutral ("Consulting, senior", `example.com`).
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- `AGENTS.md` is updated in the same change when a task adds commands, projects or rules (Task 9).
- Test commands: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`. The whole build is `./build.ps1`.

## Review Focus

1. **A PDF whose page content can't be parsed** (corrupt content stream) must throw `PdfProofException` naming the page, not a raw PdfPig exception. A bug in PdfProof code must *not* be reported as a bad PDF. Tests in Task 2.
2. **Pages with no text** (blank page, image-only scan) must load: empty `Blocks`, and the `NoTextLayer` diagnostic when no page has text. No exception. Test in Task 9.
3. **Text whose words become empty after normalization** (only zero-width characters) must not create empty lines or spans. Test in Task 5.
4. **Culture:** layout values (sizes, boxes, colors) and diagnostic messages must be the same under sv-SE as under the invariant culture. Test in Task 5.
5. **Links whose rectangle covers no text, or whose action is neither URI nor GoTo,** must load with empty text and no target, without throwing. Test in Task 8.

---

### Task 1: `RawPdf` test helper and custom info entries in `PdfMetadata`

**Files:**
- Create: `tests/PdfProof.Tests/RawPdf.cs`, `tests/PdfProof.Tests/MetadataTests.cs`, `src/PdfProof/MetadataReader.cs`
- Modify: `src/PdfProof/PdfMetadata.cs`, `src/PdfProof/PdfProofDocument.cs`, `src/PdfProof/PdfProof.csproj`, `src/PdfProof/PublicAPI.Unshipped.txt`

**Interfaces:**
- Consumes: `PdfProofDocument.Load(byte[])` and `PdfMetadata` (Plan 1).
- Produces:
  - `PdfMetadata.Custom : IReadOnlyDictionary<string, string>`: info entries other than the standard eight, sorted by key (ordinal).
  - Internal `MetadataReader.Read(PdfDocument) -> PdfMetadata`.
  - Test helper `RawPdf` with:
    - `RawPdf()`
    - `string CatalogEntries { get; set; }` and `string? Info { get; set; }`
    - `int Reserve()`, `int Add(string body)`, `void Set(int number, string body)`, `int AddStream(string entries, string data)`
    - `int AddPage(string content)`, `void Annotate(int page, string annotation)`
    - `byte[] Build()`
    - `static string Text(double x, double y, string text, int size = 12, string font = "F1")`
  - `InternalsVisibleTo("PdfProof.Tests")` on the library.

- [ ] **Step 1: Write the test helper**

`tests/PdfProof.Tests/RawPdf.cs`:
```csharp
using System.Globalization;
using System.Text;

namespace PdfProof.Tests;

// Writes small PDFs from raw PDF syntax, for tests that need exact control: positions, clipping, rotation,
// annotations, outlines, info entries and (from Plan 3) structure trees. Object 1 is the catalog, 2 the page tree.
// Every page is A4 (595 x 842 pt) and gets the fonts /F1 Helvetica, /F2 Helvetica-Bold, /F3 Helvetica-Oblique
// (WinAnsiEncoding, so U+00AD soft hyphen and U+00A0 no-break space can be written) and a 2x2 gray image /Im1.
// Text is written as ISO-8859-1. Numbers are formatted with the invariant culture.
internal sealed class RawPdf
{
    private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");

    private readonly List<string> _objects = [];
    private readonly List<int> _pages = [];
    private readonly Dictionary<int, int> _contents = [];
    private readonly Dictionary<int, List<string>> _annotations = [];
    private readonly int _resources;

    public RawPdf()
    {
        Reserve(); // 1: catalog, written by Build
        Reserve(); // 2: page tree, written by Build
        var regular = Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        var bold = Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
        var oblique = Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Oblique /Encoding /WinAnsiEncoding >>");
        var image = AddStream(
            "/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceGray /BitsPerComponent 8",
            "\u0000ÿÿ\u0000");
        _resources = Add($"<< /Font << /F1 {regular} 0 R /F2 {bold} 0 R /F3 {oblique} 0 R >> /XObject << /Im1 {image} 0 R >> >>");
    }

    /// Extra entries for the catalog, e.g. "/Outlines 9 0 R".
    public string CatalogEntries { get; set; } = "";

    /// The trailer's /Info dictionary, e.g. "<< /Title (Invoice) >>"; null leaves it out.
    public string? Info { get; set; }

    public int Reserve() => Add("");

    public int Add(string body)
    {
        _objects.Add(body);
        return _objects.Count;
    }

    public void Set(int number, string body) => _objects[number - 1] = body;

    public int AddStream(string entries, string data) =>
        Add($"<< {entries} /Length {Latin1.GetByteCount(data)} >>\nstream\n{data}\nendstream");

    /// Adds a page with the given content stream and returns its object number (for /Dest arrays).
    public int AddPage(string content)
    {
        var page = Reserve();
        _contents[page] = AddStream("", content);
        _pages.Add(page);
        return page;
    }

    /// Adds an annotation dictionary, e.g. "<< /Type /Annot /Subtype /Link /Rect [...] /A << ... >> >>", to a page.
    public void Annotate(int page, string annotation)
    {
        if (!_annotations.TryGetValue(page, out var list))
        {
            _annotations[page] = list = [];
        }

        list.Add(annotation);
    }

    public byte[] Build()
    {
        foreach (var page in _pages)
        {
            var annots = _annotations.TryGetValue(page, out var list) ? $" /Annots [{string.Join(" ", list)}]" : "";
            Set(page, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources {_resources} 0 R /Contents {_contents[page]} 0 R{annots} >>");
        }

        Set(1, $"<< /Type /Catalog /Pages 2 0 R {CatalogEntries} >>");
        Set(2, $"<< /Type /Pages /Kids [{string.Join(" ", _pages.Select(p => $"{p} 0 R"))}] /Count {_pages.Count} >>");
        var objects = new List<string>(_objects);
        var infoEntry = "";
        if (Info is not null)
        {
            objects.Add(Info);
            infoEntry = $" /Info {objects.Count} 0 R";
        }

        using var output = new MemoryStream();
        void Write(string text)
        {
            var bytes = Latin1.GetBytes(text);
            output.Write(bytes, 0, bytes.Length);
        }

        Write("%PDF-1.7\n");
        var offsets = new List<long>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(output.Position);
            Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = output.Position;
        Write($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            Write(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        }

        Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R{infoEntry} >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }

    /// A text object showing one string at (x, y).
    public static string Text(double x, double y, string text, int size = 12, string font = "F1") =>
        FormattableString.Invariant($"BT /{font} {size} Tf {x} {y} Td ({Escape(text)}) Tj ET\n");

    private static string Escape(string text) =>
        text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
```

- [ ] **Step 2: Write the failing tests**

`tests/PdfProof.Tests/MetadataTests.cs`:
```csharp
namespace PdfProof.Tests;

public sealed class MetadataTests
{
    [Fact]
    public void Custom_info_entries_are_kept_sorted_and_standard_entries_are_left_out()
    {
        var pdf = new RawPdf { Info = "<< /Title (Invoice) /Zeta (last) /Department (Sales) /Producer (Probe) >>" };
        pdf.AddPage(RawPdf.Text(50, 750, "Hello"));

        var metadata = PdfProofDocument.Load(pdf.Build()).Metadata;

        Assert.Equal(["Department", "Zeta"], metadata.Custom.Keys);
        Assert.Equal("Sales", metadata.Custom["Department"]);
        Assert.Equal("Invoice", metadata.Title);
        Assert.Equal("Probe", metadata.Producer);
    }

    [Fact]
    public void A_pdf_without_an_info_dictionary_has_no_custom_entries()
    {
        var pdf = new RawPdf();
        pdf.AddPage(RawPdf.Text(50, 750, "Hello"));

        Assert.Empty(PdfProofDocument.Load(pdf.Build()).Metadata.Custom);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: build FAILS with `CS1061`: `PdfMetadata` does not contain a definition for `Custom`.

- [ ] **Step 4: Implement**

In `src/PdfProof/PdfProof.csproj`, add inside the second `<ItemGroup>` (the one with `AdditionalFiles`):
```xml
    <InternalsVisibleTo Include="PdfProof.Tests" />
```

In `src/PdfProof/PdfMetadata.cs`:
- Add the parameter `IReadOnlyDictionary<string, string> custom` after `DateTimeOffset? modifiedDate` in the internal constructor.
- Assign it with `Custom = custom;`.
- Add this property after `ModifiedDate`:
```csharp
    /// <summary>
    /// The info dictionary's other entries (not Title, Author, Subject, Keywords, Creator, Producer, CreationDate or
    /// ModDate), sorted by key with ordinal comparison.
    /// </summary>
    public IReadOnlyDictionary<string, string> Custom { get; }
```

`src/PdfProof/MetadataReader.cs`:
```csharp
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
```

In `src/PdfProof/PdfProofDocument.cs`:
- Replace `return new PdfProofDocument(ReadMetadata(document));` with `return new PdfProofDocument(MetadataReader.Read(document));`.
- Delete the private `ReadMetadata` method.
- Remove the now-unused `using System.Globalization;`.

Record the public API: run the command from Global Constraints. Expected appended line: `PdfProof.PdfMetadata.Custom.get -> System.Collections.Generic.IReadOnlyDictionary<string!, string!>!`.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: all tests pass on every target framework, including the 2 new ones.

- [ ] **Step 6: Commit**

```bash
git add src/PdfProof tests/PdfProof.Tests
git commit -m "Add custom info entries to PdfMetadata and the RawPdf test helper

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `PdfPigCall`: parser errors become `PdfProofException`, PdfProof bugs don't

**Files:**
- Create: `src/PdfProof/PdfPigCall.cs`, `tests/PdfProof.Tests/PdfPigCallTests.cs`
- Modify: `src/PdfProof/PdfProofDocument.cs`

**Interfaces:**
- Consumes: `MetadataReader.Read` (Task 1).
- Produces: internal `PdfPigCall.Read<T>(Func<T> read, int? page = null) -> T`. Every later task routes PdfPig calls through it.

- [ ] **Step 1: Write the failing tests**

`tests/PdfProof.Tests/PdfPigCallTests.cs`:
```csharp
namespace PdfProof.Tests;

public sealed class PdfPigCallTests
{
    [Fact]
    public void A_parser_error_becomes_PdfProofException_with_the_original_as_inner_exception()
    {
        var parserError = new InvalidOperationException("Unexpected token.");

        var ex = Assert.Throws<PdfProofException>(() => PdfPigCall.Read<int>(() => throw parserError));

        Assert.Equal("The input could not be read as a PDF: Unexpected token.", ex.Message);
        Assert.Same(parserError, ex.InnerException);
    }

    [Fact]
    public void The_page_number_is_named_when_known()
    {
        var ex = Assert.Throws<PdfProofException>(() =>
            PdfPigCall.Read<int>(() => throw new FormatException("Bad operator."), page: 3));

        Assert.Equal("The input could not be read as a PDF (page 3): Bad operator.", ex.Message);
    }

    [Fact]
    public void A_PdfProofException_is_not_wrapped_twice()
    {
        var original = new PdfProofException("Already explained.");

        var ex = Assert.Throws<PdfProofException>(() => PdfPigCall.Read<int>(() => throw original));

        Assert.Same(original, ex);
    }

    [Fact]
    public void Out_of_memory_is_not_wrapped()
    {
        Assert.Throws<OutOfMemoryException>(() => PdfPigCall.Read<int>(() => throw new OutOfMemoryException()));
    }

    [Fact]
    public void A_corrupt_page_content_stream_names_the_page()
    {
        // Page 2's /Contents points at an object that is not a stream: the parser fails when the page is read.
        var pdf = new RawPdf();
        pdf.AddPage(RawPdf.Text(50, 750, "Fine"));
        var broken = pdf.AddPage(RawPdf.Text(50, 750, "Broken"));
        var notAStream = pdf.Add("<< /Not (a stream) >>");
        var bytes = pdf.Build();
        var text = System.Text.Encoding.GetEncoding("ISO-8859-1").GetString(bytes);
        var fixedUp = text.Replace($"/Contents {broken + 1} 0 R", $"/Contents {notAStream} 0 R");

        var ex = Assert.Throws<PdfProofException>(() =>
            PdfProofDocument.Load(System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(fixedUp)));

        Assert.Contains("(page 2)", ex.Message);
    }
}
```

`A_corrupt_page_content_stream_names_the_page` rewrites page 2's `/Contents` reference after building (same length, so the xref stays valid enough for PdfPig's lenient parser). Until Task 5 reads page content, PdfPig never touches page 2's content, and the test would pass vacuously. So in this task it is **skipped**: add `(Skip = "Enabled in Task 5, when page content is read")` to its `[Fact]`. Task 5 removes the skip.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: build FAILS with `CS0103`: `PdfPigCall` does not exist.

- [ ] **Step 3: Implement**

`src/PdfProof/PdfPigCall.cs`:
```csharp
namespace PdfProof;

// Runs a call into PdfPig. Any exception from the parser means the input is not a readable PDF, so it becomes a
// PdfProofException (naming the page when known). PdfProof's own code runs outside these calls, so a bug in PdfProof
// surfaces as itself instead of being reported as a bad PDF.
internal static class PdfPigCall
{
    public static T Read<T>(Func<T> read, int? page = null)
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not PdfProofException)
        {
            var where = page is { } number ? $" (page {number})" : "";
            throw new PdfProofException($"The input could not be read as a PDF{where}: {ex.Message}", ex);
        }
    }
}
```

In `src/PdfProof/PdfProofDocument.cs`, replace the whole `Open` method with:
```csharp
    private static PdfProofDocument Open(byte[] pdf)
    {
        using var document = PdfPigCall.Read(() => PdfDocument.Open(pdf));
        var metadata = PdfPigCall.Read(() => MetadataReader.Read(document));
        return new PdfProofDocument(metadata);
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: all tests pass; 1 skipped (the corrupt-content test). The Plan 1 tests `Bytes_that_are_not_a_pdf_throw_PdfProofException` and `Empty_input_throws_PdfProofException` still pass, because the message prefix is unchanged.

- [ ] **Step 5: Commit**

```bash
git add src/PdfProof tests/PdfProof.Tests
git commit -m "Wrap only PdfPig calls in PdfProofException (PdfPigCall)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Options, profiles, diagnostics and logging

**Files:**
- Create: `src/PdfProof/PdfProfile.cs`, `src/PdfProof/PdfProofOptions.cs`, `src/PdfProof/ProducerProfiles.cs`, `src/PdfProof/PdfDiagnostic.cs`, `src/PdfProof/PdfDiagnosticCodes.cs`, `src/PdfProof/PdfDiagnostics.cs`, `src/PdfProof/DiagnosticsCollector.cs`, `src/PdfProof/Layout/LayoutOptions.cs`
- Create: `tests/PdfProof.Tests/OptionsAndProfilesTests.cs`, `tests/PdfProof.Tests/ListLogger.cs`
- Modify: `Directory.Packages.props`, `src/PdfProof/PdfProof.csproj`, `src/PdfProof/PdfProofDocument.cs`, `src/PdfProof/PublicAPI.Unshipped.txt`

**Interfaces:**
- Consumes: `PdfPigCall`, `MetadataReader` (Tasks 1–2).
- Produces:
  - `enum PdfProfile { Default, SelectPdf, QuestPdf, Typst, Chromium }`.
  - `sealed record PdfProofOptions` with `Default`, `ForProfile(PdfProfile)`, `PdfProfile Profile`, `LayoutOptions Layout`, `ILogger? Logger`.
  - `sealed record PdfProof.Layout.LayoutOptions` with `Default`. Properties are added in Tasks 5, 6 and 8.
  - `PdfProofDocument.Load(byte[] | Stream | string, PdfProofOptions options)` overloads, next to the option-less ones (no optional parameters, so PublicApiAnalyzers RS0026 doesn't fire).
  - `PdfProofDocument.Diagnostics : PdfDiagnostics` with `PdfProfile Profile`, `bool ProfileDetected`, `IReadOnlyList<PdfDiagnostic> Entries`.
  - `PdfDiagnostic` with `string Code`, `int? Page`, `string Message`, and `ToString()` returning `"Code: Message"` or `"Code (page n): Message"`.
  - `PdfDiagnosticCodes.Profile = "Profile"`, `.Layout = "Layout"`, `.NoTextLayer = "NoTextLayer"`.
  - Internal `DiagnosticsCollector(ILogger?)` with `Add(string code, int? page, string message)` and `ToDiagnostics(PdfProfile, bool)`.
  - Internal `ProducerProfiles.Detect(string? producer, string? creator) -> PdfProfile`.

- [ ] **Step 1: Add the logging dependency**

In `Directory.Packages.props`, add inside the `<ItemGroup>`:
```xml
    <PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.12" />
```

In `src/PdfProof/PdfProof.csproj`, add to the first package `<ItemGroup>`:
```xml
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
```

- [ ] **Step 2: Write the failing tests**

`tests/PdfProof.Tests/ListLogger.cs`:
```csharp
using Microsoft.Extensions.Logging;

namespace PdfProof.Tests;

// Captures log entries so tests can assert what PdfProof logged.
internal sealed class ListLogger : ILogger
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception)));
}
```

`tests/PdfProof.Tests/OptionsAndProfilesTests.cs`:
```csharp
using Microsoft.Extensions.Logging;
using PdfProof.Layout;

namespace PdfProof.Tests;

public sealed class OptionsAndProfilesTests
{
    [Theory]
    [InlineData("Select.Pdf for .NET v26.4.0", null, PdfProfile.SelectPdf)]
    [InlineData("SelectPdf Library", null, PdfProfile.SelectPdf)]
    [InlineData("QuestPDF", null, PdfProfile.QuestPdf)]
    [InlineData("Typst 0.13.1", null, PdfProfile.Typst)]
    [InlineData("Skia/PDF m120", "Chromium", PdfProfile.Chromium)]
    [InlineData(null, "Mozilla/5.0 HeadlessChrome/120", PdfProfile.Chromium)]
    [InlineData("PdfPig", null, PdfProfile.Default)]
    [InlineData(null, null, PdfProfile.Default)]
    public void The_profile_is_detected_from_producer_then_creator(string? producer, string? creator, PdfProfile expected)
    {
        Assert.Equal(expected, ProducerProfiles.Detect(producer, creator));
    }

    [Fact]
    public void Load_without_options_detects_the_profile_and_says_so()
    {
        var doc = PdfProofDocument.Load(PdfWithProducer("Select.Pdf for .NET v26.4.0"));

        Assert.Equal(PdfProfile.SelectPdf, doc.Diagnostics.Profile);
        Assert.True(doc.Diagnostics.ProfileDetected);
        Assert.Equal(
            "Profile: SelectPdf (detected from producer \"Select.Pdf for .NET v26.4.0\")",
            doc.Diagnostics.Entries[0].ToString());
    }

    [Fact]
    public void Load_with_options_uses_their_profile()
    {
        var doc = PdfProofDocument.Load(PdfWithProducer("Select.Pdf for .NET v26.4.0"), PdfProofOptions.ForProfile(PdfProfile.QuestPdf));

        Assert.Equal(PdfProfile.QuestPdf, doc.Diagnostics.Profile);
        Assert.False(doc.Diagnostics.ProfileDetected);
        Assert.Equal("Profile: QuestPdf (from options)", doc.Diagnostics.Entries[0].ToString());
    }

    [Fact]
    public void ForProfile_gives_complete_options_with_default_layout()
    {
        var options = PdfProofOptions.ForProfile(PdfProfile.Typst);

        Assert.Equal(PdfProfile.Typst, options.Profile);
        Assert.Same(LayoutOptions.Default, options.Layout);
        Assert.Null(options.Logger);
        Assert.Equal(PdfProfile.Default, PdfProofOptions.Default.Profile);
    }

    [Fact]
    public void Diagnostics_are_logged_to_the_logger_in_the_options()
    {
        var logger = new ListLogger();

        PdfProofDocument.Load(PdfWithProducer("Typst 0.13.1"), PdfProofOptions.Default with { Logger = logger });

        Assert.Contains((LogLevel.Debug, "Profile: Default (from options)"), logger.Entries);
    }

    [Fact]
    public void Null_options_throw_ArgumentNullException()
    {
        var pdf = PdfWithProducer("PdfPig");

        Assert.Throws<ArgumentNullException>(() => PdfProofDocument.Load(pdf, null!));
        Assert.Throws<ArgumentNullException>(() => PdfProofDocument.Load(new MemoryStream(pdf), null!));
        Assert.Throws<ArgumentNullException>(() => PdfProofDocument.Load(Path.GetTempPath(), null!));
    }

    private static byte[] PdfWithProducer(string producer)
    {
        var pdf = new RawPdf { Info = $"<< /Producer ({producer}) >>" };
        pdf.AddPage(RawPdf.Text(50, 750, "Hello"));
        return pdf.Build();
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: build FAILS with `CS0246`/`CS0103`: `PdfProfile`, `ProducerProfiles`, `PdfProofOptions` and `LayoutOptions` do not exist.

- [ ] **Step 4: Implement**

`src/PdfProof/PdfProfile.cs`:
```csharp
namespace PdfProof;

/// <summary>
/// A named preset of options for a PDF producer. <see cref="PdfProofOptions.ForProfile"/> turns a profile into
/// complete options; <see cref="PdfProofDocument.Load(byte[])"/> without options detects it from the
/// <c>/Producer</c> and <c>/Creator</c> entries.
/// </summary>
public enum PdfProfile
{
    /// <summary>No producer-specific settings.</summary>
    Default,

    /// <summary>SelectPdf (HTML to PDF). Needs clipping and text normalization, which are on by default.</summary>
    SelectPdf,

    /// <summary>QuestPDF.</summary>
    QuestPdf,

    /// <summary>Typst.</summary>
    Typst,

    /// <summary>Chromium print-to-PDF (Chrome, Edge, Playwright, Puppeteer).</summary>
    Chromium,
}
```

`src/PdfProof/Layout/LayoutOptions.cs`:
```csharp
namespace PdfProof.Layout;

/// <summary>
/// How text is extracted and grouped into the layout model. Start from <see cref="Default"/> and adjust with
/// <c>with</c>.
/// </summary>
public sealed record LayoutOptions
{
    /// <summary>The default layout options.</summary>
    public static LayoutOptions Default { get; } = new();
}
```

`src/PdfProof/PdfProofOptions.cs`:
```csharp
using Microsoft.Extensions.Logging;
using PdfProof.Layout;

namespace PdfProof;

/// <summary>
/// Options for loading a PDF. Options are complete values: what you pass is exactly what is used. Profiles are
/// presets (<see cref="ForProfile"/>) that you adjust with <c>with</c>.
/// </summary>
public sealed record PdfProofOptions
{
    /// <summary>The default options: profile <see cref="PdfProfile.Default"/>, default layout, no logger.</summary>
    public static PdfProofOptions Default { get; } = new();

    /// <summary>The profile these options were made for. Recorded in <see cref="PdfProofDocument.Diagnostics"/>.</summary>
    public PdfProfile Profile { get; init; } = PdfProfile.Default;

    /// <summary>How text is extracted and grouped into the layout model.</summary>
    public LayoutOptions Layout { get; init; } = LayoutOptions.Default;

    /// <summary>Receives every diagnostic entry as it is recorded; <see langword="null"/> for no logging.</summary>
    public ILogger? Logger { get; init; }

    /// <summary>Complete options for a producer profile.</summary>
    /// <param name="profile">The producer profile.</param>
    public static PdfProofOptions ForProfile(PdfProfile profile) => new() { Profile = profile };
}
```

`src/PdfProof/ProducerProfiles.cs`:
```csharp
namespace PdfProof;

// Detects the producer profile from the info dictionary. Plan 4 checks these patterns against real producer output.
internal static class ProducerProfiles
{
    public static PdfProfile Detect(string? producer, string? creator) =>
        Match(producer) ?? Match(creator) ?? PdfProfile.Default;

    private static PdfProfile? Match(string? value)
    {
        if (value is null || value.Length == 0)
        {
            return null;
        }

        var text = value.ToLowerInvariant();
        if (text.Contains("select.pdf") || text.Contains("selectpdf"))
        {
            return PdfProfile.SelectPdf;
        }

        if (text.Contains("questpdf"))
        {
            return PdfProfile.QuestPdf;
        }

        if (text.Contains("typst"))
        {
            return PdfProfile.Typst;
        }

        if (text.Contains("skia/pdf") || text.Contains("chrome") || text.Contains("chromium"))
        {
            return PdfProfile.Chromium;
        }

        return null;
    }
}
```

`src/PdfProof/PdfDiagnosticCodes.cs`:
```csharp
namespace PdfProof;

/// <summary>The codes of <see cref="PdfDiagnostic"/> entries.</summary>
public static class PdfDiagnosticCodes
{
    /// <summary>Which profile was used, and whether it was detected or given.</summary>
    public const string Profile = "Profile";

    /// <summary>What the layout step produced for a page.</summary>
    public const string Layout = "Layout";

    /// <summary>No page has extractable text; the PDF is probably a scan. Logged as a warning.</summary>
    public const string NoTextLayer = "NoTextLayer";
}
```

`src/PdfProof/PdfDiagnostic.cs`:
```csharp
namespace PdfProof;

/// <summary>One decision or warning recorded while loading a PDF.</summary>
public sealed class PdfDiagnostic
{
    internal PdfDiagnostic(string code, int? page, string message)
    {
        Code = code;
        Page = page;
        Message = message;
    }

    /// <summary>What kind of entry this is; one of <see cref="PdfDiagnosticCodes"/>.</summary>
    public string Code { get; }

    /// <summary>The page the entry is about, or <see langword="null"/> for the whole document.</summary>
    public int? Page { get; }

    /// <summary>The decision or warning, in plain words.</summary>
    public string Message { get; }

    /// <summary>The entry as one line: <c>Code: Message</c> or <c>Code (page n): Message</c>.</summary>
    public override string ToString() =>
        Page is { } page ? $"{Code} (page {page}): {Message}" : $"{Code}: {Message}";
}
```

`src/PdfProof/PdfDiagnostics.cs`:
```csharp
namespace PdfProof;

/// <summary>What PdfProof decided while loading a PDF: the profile used and every recorded entry, in order.</summary>
public sealed class PdfDiagnostics
{
    internal PdfDiagnostics(PdfProfile profile, bool profileDetected, IReadOnlyList<PdfDiagnostic> entries)
    {
        Profile = profile;
        ProfileDetected = profileDetected;
        Entries = entries;
    }

    /// <summary>The profile the document was loaded with.</summary>
    public PdfProfile Profile { get; }

    /// <summary><see langword="true"/> when the profile was detected from the producer, not given in options.</summary>
    public bool ProfileDetected { get; }

    /// <summary>Every decision and warning, in the order they were recorded.</summary>
    public IReadOnlyList<PdfDiagnostic> Entries { get; }
}
```

`src/PdfProof/DiagnosticsCollector.cs`:
```csharp
using Microsoft.Extensions.Logging;

namespace PdfProof;

// Collects diagnostic entries while loading, and logs each one as it is recorded.
internal sealed class DiagnosticsCollector(ILogger? logger)
{
    private readonly List<PdfDiagnostic> _entries = [];

    public void Add(string code, int? page, string message)
    {
        var entry = new PdfDiagnostic(code, page, message);
        _entries.Add(entry);
        logger?.Log(code == PdfDiagnosticCodes.NoTextLayer ? LogLevel.Warning : LogLevel.Debug, "{Diagnostic}", entry);
    }

    public PdfDiagnostics ToDiagnostics(PdfProfile profile, bool profileDetected) =>
        new(profile, profileDetected, _entries.ToArray());
}
```

Replace `src/PdfProof/PdfProofDocument.cs` with:
```csharp
using UglyToad.PdfPig;

namespace PdfProof;

/// <summary>
/// A loaded PDF. This is the only type you load; the exporters, the CLI and the test assertions all work on it.
/// It is immutable and safe to share between threads, and it holds no open file or parser.
/// </summary>
public sealed class PdfProofDocument
{
    private PdfProofDocument(PdfMetadata metadata, PdfDiagnostics diagnostics)
    {
        Metadata = metadata;
        Diagnostics = diagnostics;
    }

    /// <summary>The PDF version, page count and info dictionary.</summary>
    public PdfMetadata Metadata { get; }

    /// <summary>The profile used and every decision and warning recorded while loading.</summary>
    public PdfDiagnostics Diagnostics { get; }

    /// <summary>Loads a PDF from its bytes, with the profile detected from the producer.</summary>
    /// <param name="pdf">The complete PDF file.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pdf"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfProofException">The bytes can't be read as a PDF.</exception>
    public static PdfProofDocument Load(byte[] pdf)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        return Open(pdf, null);
    }

    /// <summary>Loads a PDF from its bytes with the given options.</summary>
    /// <param name="pdf">The complete PDF file.</param>
    /// <param name="options">The options to use, exactly as given.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pdf"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfProofException">The bytes can't be read as a PDF.</exception>
    public static PdfProofDocument Load(byte[] pdf, PdfProofOptions options)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        ArgumentNullException.ThrowIfNull(options);
        return Open(pdf, options);
    }

    /// <summary>
    /// Loads a PDF from a stream, reading from its current position to the end, with the profile detected from the
    /// producer. The stream may be non-seekable, and it is left open.
    /// </summary>
    /// <param name="pdf">A stream positioned at the start of the PDF.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pdf"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfProofException">The content can't be read as a PDF.</exception>
    public static PdfProofDocument Load(Stream pdf)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        return Open(ReadToEnd(pdf), null);
    }

    /// <summary>
    /// Loads a PDF from a stream with the given options, reading from its current position to the end. The stream
    /// may be non-seekable, and it is left open.
    /// </summary>
    /// <param name="pdf">A stream positioned at the start of the PDF.</param>
    /// <param name="options">The options to use, exactly as given.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pdf"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfProofException">The content can't be read as a PDF.</exception>
    public static PdfProofDocument Load(Stream pdf, PdfProofOptions options)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        ArgumentNullException.ThrowIfNull(options);
        return Open(ReadToEnd(pdf), options);
    }

    /// <summary>Loads a PDF file, with the profile detected from the producer.</summary>
    /// <param name="path">The path to the PDF file.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or whitespace, or names a directory.</exception>
    /// <exception cref="FileNotFoundException">The file doesn't exist.</exception>
    /// <exception cref="PdfProofException">The file can't be read as a PDF.</exception>
    public static PdfProofDocument Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Open(ReadFile(path), null);
    }

    /// <summary>Loads a PDF file with the given options.</summary>
    /// <param name="path">The path to the PDF file.</param>
    /// <param name="options">The options to use, exactly as given.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or whitespace, or names a directory.</exception>
    /// <exception cref="FileNotFoundException">The file doesn't exist.</exception>
    /// <exception cref="PdfProofException">The file can't be read as a PDF.</exception>
    public static PdfProofDocument Load(string path, PdfProofOptions options)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(options);
        return Open(ReadFile(path), options);
    }

    // Copying makes the behavior the same for every kind of stream: read from here to the end, never seek, never
    // dispose the caller's stream.
    private static byte[] ReadToEnd(Stream pdf)
    {
        using var buffer = new MemoryStream();
        pdf.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static byte[] ReadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("The path is empty.", nameof(path));
        }

        if (Directory.Exists(path))
        {
            throw new ArgumentException($"The path is a directory, not a PDF file. Path: {path}", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The PDF file was not found.", path);
        }

        return File.ReadAllBytes(path);
    }

    // options is null when the caller gave none: the profile is then detected from the producer.
    private static PdfProofDocument Open(byte[] pdf, PdfProofOptions? options)
    {
        using var document = PdfPigCall.Read(() => PdfDocument.Open(pdf));
        var metadata = PdfPigCall.Read(() => MetadataReader.Read(document));

        var detected = options is null;
        var effective = options ?? PdfProofOptions.ForProfile(ProducerProfiles.Detect(metadata.Producer, metadata.Creator));
        var diagnostics = new DiagnosticsCollector(effective.Logger);
        diagnostics.Add(
            PdfDiagnosticCodes.Profile,
            null,
            detected ? $"{effective.Profile} (detected from producer \"{metadata.Producer}\")" : $"{effective.Profile} (from options)");

        return new PdfProofDocument(metadata, diagnostics.ToDiagnostics(effective.Profile, detected));
    }
}
```

Record the public API with the command from Global Constraints. Check that the appended lines cover:
- the 3 new `Load(…, PdfProof.PdfProofOptions! options)` overloads;
- `Diagnostics`;
- `PdfProfile`, `PdfProofOptions`, `PdfDiagnostic`, `PdfDiagnostics`, `PdfDiagnosticCodes` and `PdfProof.Layout.LayoutOptions` (records add compiler-generated members such as `<Clone>$`, `Equals` and `EqualityContract`; those lines are expected).

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: all tests pass (1 skipped), on every target framework.

- [ ] **Step 6: Commit**

```bash
git add Directory.Packages.props src/PdfProof tests/PdfProof.Tests
git commit -m "Add PdfProofOptions, profiles with producer detection, diagnostics and logging

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Text normalization

**Files:**
- Create: `src/PdfProof/Layout/TextNormalizer.cs`, `tests/PdfProof.Tests/TextNormalizerTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: internal `PdfProof.Layout.TextNormalizer.Normalize(string) -> string`.

- [ ] **Step 1: Write the failing tests**

`tests/PdfProof.Tests/TextNormalizerTests.cs`:
```csharp
using PdfProof.Layout;

namespace PdfProof.Tests;

// Spec §5.2: soft and Unicode hyphens become '-', special spaces a space, ligatures are expanded, zero-width
// characters removed. SelectPdf extracts every '-' as U+00AD, so without this "e-mail" never matches.
public sealed class TextNormalizerTests
{
    [Theory]
    [InlineData("e­mail", "e-mail")]
    [InlineData("e‐mail", "e-mail")]
    [InlineData("e‑mail", "e-mail")]
    public void Soft_and_unicode_hyphens_become_a_hyphen_minus(string input, string expected) =>
        Assert.Equal(expected, TextNormalizer.Normalize(input));

    [Theory]
    [InlineData("9 900,00")]
    [InlineData("9 900,00")]
    [InlineData("9 900,00")]
    [InlineData("9 900,00")]
    [InlineData("9 900,00")]
    [InlineData("9　900,00")]
    public void Special_spaces_become_a_space(string input) =>
        Assert.Equal("9 900,00", TextNormalizer.Normalize(input));

    [Fact]
    public void Zero_width_characters_are_removed() =>
        Assert.Equal("Invoice", TextNormalizer.Normalize("In​vo‌‍i⁠ce﻿"));

    [Fact]
    public void Latin_ligatures_are_expanded() =>
        Assert.Equal("office flow ffl", TextNormalizer.Normalize("oﬃce ﬂow ﬄ"));

    [Fact]
    public void Ordinary_text_is_returned_unchanged() =>
        Assert.Equal("Total: 25 634,50 SEK – å ä ö", TextNormalizer.Normalize("Total: 25 634,50 SEK – å ä ö"));
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: build FAILS with `CS0103`: `TextNormalizer` does not exist.

- [ ] **Step 3: Implement**

`src/PdfProof/Layout/TextNormalizer.cs`:
```csharp
using System.Text;

namespace PdfProof.Layout;

// Normalizes extracted text so assertions don't depend on how a producer encoded characters. Found with SelectPdf
// (WebKit): every '-' in the HTML comes out of the PDF as U+00AD (soft hyphen).
internal static class TextNormalizer
{
    public static string Normalize(string text)
    {
        if (!NeedsWork(text))
        {
            return text;
        }

        var result = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (IsHyphen(c))
            {
                // A soft hyphen that was actually drawn is a visible hyphen; Unicode hyphens likewise.
                result.Append('-');
            }
            else if (IsSpecialSpace(c))
            {
                result.Append(' ');
            }
            else if (IsZeroWidth(c))
            {
                // Zero-width characters and the BOM carry nothing visible.
            }
            else if (IsLigature(c))
            {
                result.Append(c.ToString().Normalize(NormalizationForm.FormKC));
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }

    private static bool NeedsWork(string text)
    {
        foreach (var c in text)
        {
            if (IsHyphen(c) || IsSpecialSpace(c) || IsZeroWidth(c) || IsLigature(c))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHyphen(char c) => c is '­' or '‐' or '‑';

    // No-break, en quad .. hair space (U+2000-U+200A), narrow no-break, medium mathematical and ideographic space.
    private static bool IsSpecialSpace(char c) =>
        c is ' ' or (>= ' ' and <= ' ') or ' ' or ' ' or '　';

    private static bool IsZeroWidth(char c) => c is '​' or '‌' or '‍' or '⁠' or '﻿';

    // Latin ligatures U+FB00-U+FB06 (ff, fi, fl, ffi, ffl, long st, st).
    private static bool IsLigature(char c) => c is >= 'ﬀ' and <= 'ﬆ';
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: all tests pass (1 skipped).

- [ ] **Step 5: Commit**

```bash
git add src/PdfProof/Layout/TextNormalizer.cs tests/PdfProof.Tests/TextNormalizerTests.cs
git commit -m "Add text normalization for hyphens, special spaces, zero-width characters and ligatures

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: The layout model and the `Rows` strategy

**Files:**
- Create in `src/PdfProof/Layout/`:
  - public types: `LayoutBox.cs`, `LayoutOrientation.cs`, `LayoutModel.cs`, `LayoutPage.cs`, `LayoutBlock.cs`, `LayoutLine.cs`, `LayoutSpan.cs`, `LayoutWord.cs`, `LayoutWordLine.cs`, `LayoutWordBlock.cs`, `ILayoutStrategy.cs`, `LayoutStrategies.cs`
  - internal types: `RowsLayoutStrategy.cs`, `Geometry.cs`, `WordFactory.cs`, `LineFactory.cs`, `LayoutBuilder.cs`
- Create: `tests/PdfProof.Tests/RowsLayoutTests.cs`, `tests/PdfProof.Tests/GeometryTests.cs`
- Modify: `src/PdfProof/Layout/LayoutOptions.cs`, `src/PdfProof/PdfProofDocument.cs`, `src/PdfProof/PublicAPI.Unshipped.txt`, `tests/PdfProof.Tests/PdfPigCallTests.cs`

**Interfaces:**
- Consumes: `LayoutOptions`, `DiagnosticsCollector`, `PdfPigCall`, `TextNormalizer` (Tasks 2–4).
- Produces:
  - `PdfProofDocument.Layout : LayoutModel` with `IReadOnlyList<LayoutPage> Pages`.
  - `LayoutPage`: `int Number`, `double Width`, `double Height`, `int Rotation`, `IReadOnlyList<LayoutBlock> Blocks`, `IReadOnlyList<LayoutLine> Lines`, `string Text`.
  - `LayoutBlock`: `LayoutBox Box`, `IReadOnlyList<LayoutLine> Lines`, `LayoutOrientation Orientation`.
  - `LayoutLine`: `string Text`, `LayoutBox Box`, `IReadOnlyList<LayoutSpan> Spans`.
  - `LayoutSpan`: `string Text`, `string? Font`, `double Size`, `string Color`, `bool IsBold`, `bool IsItalic`.
  - `readonly record struct LayoutBox(double Left, double Bottom, double Right, double Top)` with `Width` and `Height`.
  - `enum LayoutOrientation { Horizontal, Rotate90, Rotate180, Rotate270, Other }`.
  - `LayoutWord`: `string Text`, `LayoutBox Box`, `double Baseline`, `double Size`, `string? Font`, `string Color`, `bool IsBold`, `bool IsItalic`, and internal `Word Source`.
  - `LayoutWordLine(IReadOnlyList<LayoutWord> words)` with `Words`; `LayoutWordBlock(IReadOnlyList<LayoutWordLine> lines)` with `Lines`.
  - `interface ILayoutStrategy { string Name { get; } IReadOnlyList<LayoutWordBlock> Arrange(IReadOnlyList<LayoutWord> words, LayoutOptions options); }`
  - `LayoutStrategies.Rows`.
  - New `LayoutOptions` properties: `int Decimals = 1`, `double ColumnGapFactor = 1.0`, `double BlockGapFactor = 0.8`, `bool NormalizeText = true`, `IReadOnlyDictionary<string, string> TextReplacements` (empty).
  - Internal:
    - `Geometry.Round(double, int)`, `Geometry.Round(LayoutBox, int)`, `Geometry.Bounds(PdfRectangle)`, `Geometry.Union(IEnumerable<LayoutBox>)`, `Geometry.Hex(IColor?)`
    - `WordFactory.Create(Word, LayoutOptions) -> LayoutWord?` and `WordFactory.Normalize(string, LayoutOptions)`
    - `LineFactory.Line(IReadOnlyList<LayoutWord>, LayoutOptions)` and `LineFactory.Block(IReadOnlyList<LayoutLine>, LayoutOrientation)`
    - `LayoutBuilder.Build(PdfDocument, LayoutOptions, DiagnosticsCollector) -> LayoutModel`
  - A `Layout` diagnostic per page: `"<n> blocks, <m> lines (<strategy name> layout)"`.

- [ ] **Step 1: Write the failing tests**

`tests/PdfProof.Tests/GeometryTests.cs`:
```csharp
using PdfProof.Layout;

namespace PdfProof.Tests;

public sealed class GeometryTests
{
    [Theory]
    [InlineData(2.25, 1, 2.3)]
    [InlineData(-2.25, 1, -2.3)]
    [InlineData(10.04, 1, 10.0)]
    [InlineData(10.04, 2, 10.04)]
    public void Round_rounds_half_away_from_zero(double value, int decimals, double expected) =>
        Assert.Equal(expected, Geometry.Round(value, decimals));

    [Fact]
    public void Round_never_returns_negative_zero()
    {
        var rounded = Geometry.Round(-0.04, 1);

        Assert.Equal(0, rounded);
        Assert.True(1 / rounded > 0, "Rounding must give +0, not -0, so output never shows \"-0\".");
    }
}
```

`tests/PdfProof.Tests/RowsLayoutTests.cs`:
```csharp
using System.Globalization;
using PdfProof.Layout;

namespace PdfProof.Tests;

public sealed class RowsLayoutTests
{
    [Fact]
    public void Table_rows_split_into_cells_at_column_gaps_and_stay_row_by_row()
    {
        var page = LoadPage(
            RawPdf.Text(50, 750, "Item") + RawPdf.Text(300, 750, "Qty") + RawPdf.Text(400, 750, "Amount") +
            RawPdf.Text(50, 735, "Consulting, senior") + RawPdf.Text(300, 735, "10") + RawPdf.Text(400, 735, "14 500,00 SEK"));

        Assert.Equal(["Item", "Qty", "Amount", "Consulting, senior", "10", "14 500,00 SEK"], page.Lines.Select(l => l.Text));
        Assert.Single(page.Blocks);
    }

    [Fact]
    public void A_vertical_gap_larger_than_the_block_gap_starts_a_new_block()
    {
        var page = LoadPage(
            RawPdf.Text(50, 750, "First paragraph") + RawPdf.Text(50, 736, "continues here") +
            RawPdf.Text(50, 650, "Second paragraph"));

        Assert.Equal(
            [["First paragraph", "continues here"], ["Second paragraph"]],
            page.Blocks.Select(b => b.Lines.Select(l => l.Text).ToArray()));
        Assert.All(page.Blocks, b => Assert.Equal(LayoutOrientation.Horizontal, b.Orientation));
    }

    [Fact]
    public void Words_with_the_same_style_form_one_span_and_a_style_change_starts_a_new_one()
    {
        var page = LoadPage("BT /F2 12 Tf 50 600 Td (Total:) Tj /F1 12 Tf ( 25 634,50) Tj 1 0 0 rg ( SEK) Tj ET\n");

        var line = Assert.Single(page.Lines);
        Assert.Equal("Total: 25 634,50 SEK", line.Text);
        Assert.Collection(
            line.Spans,
            span =>
            {
                Assert.Equal("Total:", span.Text);
                Assert.Equal("Helvetica-Bold", span.Font);
                Assert.True(span.IsBold);
                Assert.Equal(12, span.Size);
            },
            span =>
            {
                Assert.Equal("25 634,50", span.Text);
                Assert.Equal("Helvetica", span.Font);
                Assert.False(span.IsBold);
                Assert.Equal("#000000", span.Color);
            },
            span =>
            {
                Assert.Equal("SEK", span.Text);
                Assert.Equal("#FF0000", span.Color);
            });
    }

    [Fact]
    public void An_oblique_font_is_italic()
    {
        var span = Assert.Single(LoadPage(RawPdf.Text(50, 600, "Note", font: "F3")).Lines[0].Spans);

        Assert.True(span.IsItalic);
        Assert.False(span.IsBold);
    }

    [Fact]
    public void Soft_hyphens_are_normalized_unless_normalization_is_off()
    {
        var pdf = Pdf(RawPdf.Text(50, 750, "e­mail"));

        Assert.Equal("e-mail", PdfProofDocument.Load(pdf).Layout.Pages[0].Text);
        var raw = PdfProofDocument.Load(pdf, PdfProofOptions.Default with { Layout = LayoutOptions.Default with { NormalizeText = false } });
        Assert.Equal("e­mail", raw.Layout.Pages[0].Text);
    }

    [Fact]
    public void Text_replacements_apply_after_normalization()
    {
        var options = PdfProofOptions.Default with
        {
            Layout = LayoutOptions.Default with { TextReplacements = new Dictionary<string, string> { ["e-mail"] = "email" } },
        };

        var doc = PdfProofDocument.Load(Pdf(RawPdf.Text(50, 750, "e­mail")), options);

        Assert.Equal("email", doc.Layout.Pages[0].Text);
    }

    [Fact]
    public void A_word_that_is_empty_after_normalization_creates_no_line()
    {
        // Review Focus 3. The zero-width no-break space U+FEFF can't be written in WinAnsi, so remove the word's
        // only letter with a replacement instead: the word becomes empty after normalization.
        var options = PdfProofOptions.Default with
        {
            Layout = LayoutOptions.Default with { TextReplacements = new Dictionary<string, string> { ["~"] = "" } },
        };

        var page = PdfProofDocument.Load(Pdf(RawPdf.Text(50, 750, "~") + RawPdf.Text(50, 700, "Kept")), options).Layout.Pages[0];

        Assert.Equal(["Kept"], page.Lines.Select(l => l.Text));
        Assert.All(page.Lines.SelectMany(l => l.Spans), span => Assert.NotEqual("", span.Text));
    }

    [Fact]
    public void Page_size_rotation_and_number_are_recorded()
    {
        var page = LoadPage(RawPdf.Text(50, 750, "Hello"));

        Assert.Equal((1, 595.0, 842.0, 0), (page.Number, page.Width, page.Height, page.Rotation));
    }

    [Fact]
    public void Layout_values_and_diagnostics_are_the_same_in_every_culture()
    {
        // Review Focus 4.
        var pdf = Pdf(RawPdf.Text(50.25, 750.75, "Total 1,5"));
        // The test formats numbers invariantly itself, so only the library's own output can differ between runs.
        string Describe(PdfProofDocument doc) =>
            string.Join("|", doc.Layout.Pages[0].Lines.Select(l => FormattableString.Invariant(
                $"{l.Text}:{l.Box.Left},{l.Box.Bottom},{l.Box.Right},{l.Box.Top}:{l.Spans[0].Size}:{l.Spans[0].Color}")))
            + "|" + string.Join("|", doc.Diagnostics.Entries.Select(e => e.ToString()));

        var invariant = Describe(PdfProofDocument.Load(pdf));
        var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("sv-SE");
        try
        {
            Assert.Equal(invariant, Describe(PdfProofDocument.Load(pdf)));
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous;
        }
    }

    [Fact]
    public void Each_page_records_a_layout_diagnostic()
    {
        var doc = PdfProofDocument.Load(Pdf(RawPdf.Text(50, 750, "Item") + RawPdf.Text(300, 750, "Qty")));

        Assert.Contains(doc.Diagnostics.Entries, e => e.ToString() == "Layout (page 1): 1 blocks, 2 lines (Rows layout)");
    }

    internal static byte[] Pdf(string content)
    {
        var pdf = new RawPdf();
        pdf.AddPage(content);
        return pdf.Build();
    }

    private static LayoutPage LoadPage(string content) => PdfProofDocument.Load(Pdf(content)).Layout.Pages[0];
}
```

In `tests/PdfProof.Tests/PdfPigCallTests.cs`, remove `(Skip = "Enabled in Task 5, when page content is read")` from the `[Fact]` of `A_corrupt_page_content_stream_names_the_page`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: build FAILS with `CS1061`/`CS0103`/`CS0246`: `Layout`, `LayoutPage`, `Geometry`, `LayoutOrientation`, `NormalizeText` and `TextReplacements` do not exist.

- [ ] **Step 3: Add the layout options**

In `src/PdfProof/Layout/LayoutOptions.cs`, add after the `Default` property:
```csharp

    /// <summary>Decimals kept for coordinates and font sizes; absorbs floating-point noise. Default 1.</summary>
    public int Decimals { get; init; } = 1;

    /// <summary>
    /// Words on the same baseline further apart than this many font sizes start a new line (a column or table
    /// cell). Default 1.0.
    /// </summary>
    public double ColumnGapFactor { get; init; } = 1.0;

    /// <summary>
    /// <see cref="LayoutStrategies.Rows"/>: a vertical gap larger than this many font sizes (of the smaller row)
    /// starts a new block. Default 0.8.
    /// </summary>
    public double BlockGapFactor { get; init; } = 0.8;

    /// <summary>
    /// Normalize producer-specific characters: soft and Unicode hyphens become <c>-</c>, special spaces a space,
    /// ligatures are expanded and zero-width characters removed. Default on.
    /// </summary>
    public bool NormalizeText { get; init; } = true;

    /// <summary>
    /// Extra text replacements applied to every word after normalization, in ordinal key order. Default none.
    /// </summary>
    public IReadOnlyDictionary<string, string> TextReplacements { get; init; } = new Dictionary<string, string>();
```

- [ ] **Step 4: Implement the model types**

`src/PdfProof/Layout/LayoutBox.cs`:
```csharp
namespace PdfProof.Layout;

/// <summary>A rectangle in PDF user space: points, origin at the bottom left of the page.</summary>
/// <param name="Left">The left edge.</param>
/// <param name="Bottom">The bottom edge.</param>
/// <param name="Right">The right edge.</param>
/// <param name="Top">The top edge.</param>
public readonly record struct LayoutBox(double Left, double Bottom, double Right, double Top)
{
    /// <summary>The width, <c>Right - Left</c>.</summary>
    public double Width => Right - Left;

    /// <summary>The height, <c>Top - Bottom</c>.</summary>
    public double Height => Top - Bottom;
}
```

`src/PdfProof/Layout/LayoutOrientation.cs`:
```csharp
namespace PdfProof.Layout;

/// <summary>The direction a block's text runs in.</summary>
public enum LayoutOrientation
{
    /// <summary>Normal left-to-right text.</summary>
    Horizontal,

    /// <summary>Text rotated 90 degrees.</summary>
    Rotate90,

    /// <summary>Upside-down text.</summary>
    Rotate180,

    /// <summary>Text rotated 270 degrees (on real documents this is often text reading bottom to top).</summary>
    Rotate270,

    /// <summary>Text at another angle.</summary>
    Other,
}
```

`src/PdfProof/Layout/LayoutSpan.cs`:
```csharp
namespace PdfProof.Layout;

/// <summary>Consecutive words in a line that share font, size, color and style.</summary>
public sealed class LayoutSpan
{
    internal LayoutSpan(string text, string? font, double size, string color, bool isBold, bool isItalic)
    {
        Text = text;
        Font = font;
        Size = size;
        Color = color;
        IsBold = isBold;
        IsItalic = isItalic;
    }

    /// <summary>The span's words, joined by single spaces.</summary>
    public string Text { get; }

    /// <summary>The font name without its subset prefix (<c>ABCDEF+Lato-Bold</c> becomes <c>Lato-Bold</c>).</summary>
    public string? Font { get; }

    /// <summary>The font size in points, rounded to <see cref="LayoutOptions.Decimals"/>.</summary>
    public double Size { get; }

    /// <summary>The fill color as <c>#RRGGBB</c>.</summary>
    public string Color { get; }

    /// <summary>Bold, from the font's flags or, failing that, its name.</summary>
    public bool IsBold { get; }

    /// <summary>Italic or oblique, from the font's flags or, failing that, its name.</summary>
    public bool IsItalic { get; }
}
```

`src/PdfProof/Layout/LayoutLine.cs`:
```csharp
namespace PdfProof.Layout;

/// <summary>Words on one baseline, up to a column gap.</summary>
public sealed class LayoutLine
{
    internal LayoutLine(string text, LayoutBox box, IReadOnlyList<LayoutSpan> spans)
    {
        Text = text;
        Box = box;
        Spans = spans;
    }

    /// <summary>The line's text: its spans joined by single spaces.</summary>
    public string Text { get; }

    /// <summary>The axis-aligned box around the line's words.</summary>
    public LayoutBox Box { get; }

    /// <summary>The line's spans, left to right (in reading direction for rotated text).</summary>
    public IReadOnlyList<LayoutSpan> Spans { get; }
}
```

`src/PdfProof/Layout/LayoutBlock.cs`:
```csharp
namespace PdfProof.Layout;

/// <summary>Lines read together: a paragraph, a table, a column fragment, or a run of rotated text.</summary>
public sealed class LayoutBlock
{
    internal LayoutBlock(LayoutBox box, IReadOnlyList<LayoutLine> lines, LayoutOrientation orientation)
    {
        Box = box;
        Lines = lines;
        Orientation = orientation;
    }

    /// <summary>The box around the block's lines.</summary>
    public LayoutBox Box { get; }

    /// <summary>The block's lines, in reading order.</summary>
    public IReadOnlyList<LayoutLine> Lines { get; }

    /// <summary>The direction the block's text runs in.</summary>
    public LayoutOrientation Orientation { get; }
}
```

`src/PdfProof/Layout/LayoutPage.cs`:
```csharp
namespace PdfProof.Layout;

/// <summary>One page of the layout model.</summary>
public sealed class LayoutPage
{
    internal LayoutPage(int number, double width, double height, int rotation, IReadOnlyList<LayoutBlock> blocks)
    {
        Number = number;
        Width = width;
        Height = height;
        Rotation = rotation;
        Blocks = blocks;
        Lines = blocks.SelectMany(block => block.Lines).ToArray();
        Text = string.Join("\n", Lines.Select(line => line.Text));
    }

    /// <summary>The page number, starting at 1.</summary>
    public int Number { get; }

    /// <summary>The page width in points.</summary>
    public double Width { get; }

    /// <summary>The page height in points.</summary>
    public double Height { get; }

    /// <summary>The page's <c>/Rotate</c> value in degrees (0, 90, 180 or 270).</summary>
    public int Rotation { get; }

    /// <summary>The page's blocks: horizontal text in layout order, then rotated text.</summary>
    public IReadOnlyList<LayoutBlock> Blocks { get; }

    /// <summary>All lines of all blocks, in block order.</summary>
    public IReadOnlyList<LayoutLine> Lines { get; }

    /// <summary>All line texts, joined by <c>\n</c>.</summary>
    public string Text { get; }
}
```

`src/PdfProof/Layout/LayoutModel.cs`:
```csharp
namespace PdfProof.Layout;

/// <summary>The geometry of a PDF: its pages, with text grouped into blocks, lines and spans.</summary>
public sealed class LayoutModel
{
    internal LayoutModel(IReadOnlyList<LayoutPage> pages)
    {
        Pages = pages;
    }

    /// <summary>The pages, in order.</summary>
    public IReadOnlyList<LayoutPage> Pages { get; }
}
```

`src/PdfProof/Layout/LayoutWord.cs`:
```csharp
using UglyToad.PdfPig.Content;

namespace PdfProof.Layout;

/// <summary>
/// A word as a layout strategy sees it: normalized text, geometry and style. Words are never empty.
/// </summary>
public sealed class LayoutWord
{
    internal LayoutWord(string text, LayoutBox box, double baseline, double size, string? font, string color, bool isBold, bool isItalic, Word source)
    {
        Text = text;
        Box = box;
        Baseline = baseline;
        Size = size;
        Font = font;
        Color = color;
        IsBold = isBold;
        IsItalic = isItalic;
        Source = source;
    }

    /// <summary>The word's text after normalization and replacements.</summary>
    public string Text { get; }

    /// <summary>The axis-aligned box around the word, not rounded.</summary>
    public LayoutBox Box { get; }

    /// <summary>The y coordinate of the word's baseline at its first letter.</summary>
    public double Baseline { get; }

    /// <summary>The largest font size among the word's letters, in points, not rounded.</summary>
    public double Size { get; }

    /// <summary>The font name without its subset prefix.</summary>
    public string? Font { get; }

    /// <summary>The fill color of the first letter as <c>#RRGGBB</c>.</summary>
    public string Color { get; }

    /// <summary>Bold, from the font's flags or, failing that, its name.</summary>
    public bool IsBold { get; }

    /// <summary>Italic or oblique, from the font's flags or, failing that, its name.</summary>
    public bool IsItalic { get; }

    internal Word Source { get; }
}
```

`src/PdfProof/Layout/LayoutWordLine.cs`:
```csharp
namespace PdfProof.Layout;

/// <summary>A line as returned by a layout strategy: words in reading order.</summary>
public sealed class LayoutWordLine
{
    /// <summary>Creates a line from words in reading order.</summary>
    /// <param name="words">The line's words; must not be empty.</param>
    public LayoutWordLine(IReadOnlyList<LayoutWord> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        Words = words;
    }

    /// <summary>The line's words, in reading order.</summary>
    public IReadOnlyList<LayoutWord> Words { get; }
}
```

`src/PdfProof/Layout/LayoutWordBlock.cs`:
```csharp
namespace PdfProof.Layout;

/// <summary>A block as returned by a layout strategy: lines in reading order.</summary>
public sealed class LayoutWordBlock
{
    /// <summary>Creates a block from lines in reading order.</summary>
    /// <param name="lines">The block's lines; must not be empty.</param>
    public LayoutWordBlock(IReadOnlyList<LayoutWordLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        Lines = lines;
    }

    /// <summary>The block's lines, in reading order.</summary>
    public IReadOnlyList<LayoutWordLine> Lines { get; }
}
```

`src/PdfProof/Layout/ILayoutStrategy.cs`:
```csharp
namespace PdfProof.Layout;

/// <summary>
/// Groups a page's horizontal words into blocks of lines, in reading order. Rotated text is grouped separately
/// and never passed to a strategy. Built-in strategies are in <see cref="LayoutStrategies"/>.
/// </summary>
public interface ILayoutStrategy
{
    /// <summary>The strategy's name, shown in diagnostics.</summary>
    string Name { get; }

    /// <summary>Groups words into blocks of lines.</summary>
    /// <param name="words">The page's horizontal words, in content order; never empty words.</param>
    /// <param name="options">The layout options in use.</param>
    /// <returns>Blocks in reading order. Omit no word; empty lines and blocks are ignored.</returns>
    IReadOnlyList<LayoutWordBlock> Arrange(IReadOnlyList<LayoutWord> words, LayoutOptions options);
}
```

`src/PdfProof/Layout/LayoutStrategies.cs`:
```csharp
namespace PdfProof.Layout;

/// <summary>The built-in layout strategies.</summary>
public static class LayoutStrategies
{
    /// <summary>
    /// The default. Deterministic and explainable: rows by baseline from top to bottom, split into cells at
    /// column-sized gaps, and into blocks at paragraph-sized vertical gaps. Table rows stay together; multi-column
    /// pages interleave line by line.
    /// </summary>
    public static ILayoutStrategy Rows { get; } = new RowsLayoutStrategy();
}
```

- [ ] **Step 5: Implement the internal building blocks**

`src/PdfProof/Layout/Geometry.cs`:
```csharp
using System.Globalization;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics.Colors;

namespace PdfProof.Layout;

internal static class Geometry
{
    public static double Round(double value, int decimals)
    {
        var rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        return rounded == 0 ? 0 : rounded; // never -0
    }

    public static LayoutBox Round(LayoutBox box, int decimals) =>
        new(Round(box.Left, decimals), Round(box.Bottom, decimals), Round(box.Right, decimals), Round(box.Top, decimals));

    // Axis-aligned bounds over all four corners: PdfPig reports rotated boxes with Bottom > Top.
    public static LayoutBox Bounds(PdfRectangle rectangle)
    {
        double[] xs = [rectangle.BottomLeft.X, rectangle.BottomRight.X, rectangle.TopLeft.X, rectangle.TopRight.X];
        double[] ys = [rectangle.BottomLeft.Y, rectangle.BottomRight.Y, rectangle.TopLeft.Y, rectangle.TopRight.Y];
        return new LayoutBox(xs.Min(), ys.Min(), xs.Max(), ys.Max());
    }

    public static LayoutBox Union(IEnumerable<LayoutBox> boxes)
    {
        var list = boxes.ToList();
        return new LayoutBox(list.Min(b => b.Left), list.Min(b => b.Bottom), list.Max(b => b.Right), list.Max(b => b.Top));
    }

    public static string Hex(IColor? color)
    {
        if (color is null)
        {
            return "#000000";
        }

        var (red, green, blue) = color.ToRGBValues();
        return "#" + To255(red) + To255(green) + To255(blue);
    }

    private static string To255(double value)
    {
        var clamped = value < 0 ? 0 : value > 1 ? 1 : value;
        return ((int)Math.Round(clamped * 255, MidpointRounding.AwayFromZero)).ToString("X2", CultureInfo.InvariantCulture);
    }
}
```

`src/PdfProof/Layout/WordFactory.cs`:
```csharp
using System.Text.RegularExpressions;
using UglyToad.PdfPig.Content;

namespace PdfProof.Layout;

internal static class WordFactory
{
    // Subset fonts carry a random six-letter tag per generation: "ABCDEF+Lato-Bold".
    private static readonly Regex SubsetPrefix = new(@"^[A-Z]{6}\+", RegexOptions.CultureInvariant);

    // Null when the word has no visible text after normalization, e.g. only zero-width characters.
    public static LayoutWord? Create(Word word, LayoutOptions options)
    {
        if (word.Letters.Count == 0)
        {
            return null;
        }

        var text = Normalize(word.Text, options).Trim();
        if (text.Length == 0)
        {
            return null;
        }

        var first = word.Letters[0];
        var font = word.FontName ?? first.FontName;
        font = font is null ? null : SubsetPrefix.Replace(font, "");
        var details = first.FontDetails;
        return new LayoutWord(
            text,
            Geometry.Bounds(word.BoundingBox),
            first.StartBaseLine.Y,
            word.Letters.Max(letter => letter.PointSize),
            font,
            Geometry.Hex(first.Color),
            details?.IsBold == true || NameContains(font, "Bold"),
            details?.IsItalic == true || NameContains(font, "Italic") || NameContains(font, "Oblique"),
            word);
    }

    public static string Normalize(string text, LayoutOptions options)
    {
        var result = options.NormalizeText ? TextNormalizer.Normalize(text) : text;
        foreach (var key in options.TextReplacements.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            result = result.Replace(key, options.TextReplacements[key]);
        }

        return result;
    }

    private static bool NameContains(string? font, string part) =>
        font is not null && font.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
}
```

`src/PdfProof/Layout/LineFactory.cs`:
```csharp
namespace PdfProof.Layout;

internal static class LineFactory
{
    // Consecutive words with the same font, size, color and style merge into one span.
    public static LayoutLine Line(IReadOnlyList<LayoutWord> words, LayoutOptions options)
    {
        var spans = new List<LayoutSpan>();
        foreach (var word in words)
        {
            var size = Geometry.Round(word.Size, options.Decimals);
            if (spans.Count > 0)
            {
                var last = spans[spans.Count - 1];
                if (last.Font == word.Font && last.Size == size && last.Color == word.Color && last.IsBold == word.IsBold && last.IsItalic == word.IsItalic)
                {
                    spans[spans.Count - 1] = new LayoutSpan(last.Text + " " + word.Text, last.Font, size, last.Color, last.IsBold, last.IsItalic);
                    continue;
                }
            }

            spans.Add(new LayoutSpan(word.Text, word.Font, size, word.Color, word.IsBold, word.IsItalic));
        }

        var box = Geometry.Round(Geometry.Union(words.Select(word => word.Box)), options.Decimals);
        return new LayoutLine(string.Join(" ", spans.Select(span => span.Text)), box, spans);
    }

    public static LayoutBlock Block(IReadOnlyList<LayoutLine> lines, LayoutOrientation orientation) =>
        new(Geometry.Union(lines.Select(line => line.Box)), lines, orientation);
}
```

`src/PdfProof/Layout/RowsLayoutStrategy.cs`:
```csharp
namespace PdfProof.Layout;

// Rows by baseline from top to bottom, split into cells at column-sized gaps, and into blocks at paragraph-sized
// vertical gaps.
internal sealed class RowsLayoutStrategy : ILayoutStrategy
{
    public string Name => "Rows";

    public IReadOnlyList<LayoutWordBlock> Arrange(IReadOnlyList<LayoutWord> words, LayoutOptions options)
    {
        var blocks = new List<LayoutWordBlock>();
        var current = new List<LayoutWordLine>();
        List<LayoutWord>? previousRow = null;
        foreach (var row in Rows(words))
        {
            if (previousRow is not null)
            {
                var gap = current.SelectMany(line => line.Words).Min(word => word.Box.Bottom) - row.Max(word => word.Box.Top);
                if (gap > options.BlockGapFactor * Math.Min(MaxSize(row), MaxSize(previousRow)))
                {
                    blocks.Add(new LayoutWordBlock(current));
                    current = [];
                }
            }

            current.AddRange(Cells(row, options));
            previousRow = row;
        }

        if (current.Count > 0)
        {
            blocks.Add(new LayoutWordBlock(current));
        }

        return blocks;
    }

    // Words on (nearly) the same baseline, rows from top to bottom.
    private static List<List<LayoutWord>> Rows(IEnumerable<LayoutWord> words)
    {
        var rows = new List<List<LayoutWord>>();
        foreach (var word in words.OrderByDescending(w => w.Baseline).ThenBy(w => w.Box.Left))
        {
            var row = rows.Count > 0 ? rows[rows.Count - 1] : null;
            if (row is not null && Math.Abs(row[0].Baseline - word.Baseline) <= Math.Max(1.0, 0.3 * word.Size))
            {
                row.Add(word);
            }
            else
            {
                rows.Add([word]);
            }
        }

        return rows;
    }

    // A row split into cells where the horizontal gap is wider than ColumnGapFactor font sizes.
    private static IEnumerable<LayoutWordLine> Cells(List<LayoutWord> row, LayoutOptions options)
    {
        var ordered = row.OrderBy(word => word.Box.Left).ToList();
        var cell = new List<LayoutWord> { ordered[0] };
        for (var i = 1; i < ordered.Count; i++)
        {
            var previous = cell[cell.Count - 1];
            if (ordered[i].Box.Left - previous.Box.Right > options.ColumnGapFactor * Math.Max(ordered[i].Size, previous.Size))
            {
                yield return new LayoutWordLine(cell);
                cell = [];
            }

            cell.Add(ordered[i]);
        }

        yield return new LayoutWordLine(cell);
    }

    private static double MaxSize(List<LayoutWord> row) => row.Max(word => word.Size);
}
```

`src/PdfProof/Layout/LayoutBuilder.cs`:
```csharp
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace PdfProof.Layout;

// Builds the layout model page by page; a page's letters are dropped once its model is built. Every call into
// PdfPig goes through PdfPigCall.
internal static class LayoutBuilder
{
    public static LayoutModel Build(PdfDocument document, LayoutOptions options, DiagnosticsCollector diagnostics)
    {
        var pageCount = PdfPigCall.Read(() => document.NumberOfPages);
        var pages = new List<LayoutPage>(pageCount);
        for (var number = 1; number <= pageCount; number++)
        {
            var pageNumber = number;
            var page = PdfPigCall.Read(() => document.GetPage(pageNumber), pageNumber);
            pages.Add(BuildPage(page, pageNumber, options, diagnostics));
        }

        return new LayoutModel(pages);
    }

    private static LayoutPage BuildPage(Page page, int number, LayoutOptions options, DiagnosticsCollector diagnostics)
    {
        var letters = PdfPigCall.Read(() => page.Letters.ToList(), number);
        var horizontal = letters.Where(letter => letter.TextOrientation == TextOrientation.Horizontal).ToList();
        var words = Words(PdfPigCall.Read(() => DefaultWordExtractor.Instance.GetWords(horizontal).ToList(), number), options);

        var strategy = LayoutStrategies.Rows;
        var blocks = Blocks(strategy.Arrange(words, options), options);

        diagnostics.Add(
            PdfDiagnosticCodes.Layout,
            number,
            $"{blocks.Count} blocks, {blocks.Sum(block => block.Lines.Count)} lines ({strategy.Name} layout)");

        return new LayoutPage(
            number,
            Geometry.Round(page.Width, options.Decimals),
            Geometry.Round(page.Height, options.Decimals),
            page.Rotation.Value,
            blocks);
    }

    private static List<LayoutWord> Words(IEnumerable<Word> words, LayoutOptions options)
    {
        var result = new List<LayoutWord>();
        foreach (var word in words)
        {
            if (WordFactory.Create(word, options) is { } layoutWord)
            {
                result.Add(layoutWord);
            }
        }

        return result;
    }

    // Turns a strategy's word blocks into model blocks; empty lines and blocks are dropped.
    private static List<LayoutBlock> Blocks(IReadOnlyList<LayoutWordBlock> wordBlocks, LayoutOptions options) =>
        wordBlocks
            .Select(block => block.Lines.Where(line => line.Words.Count > 0).Select(line => LineFactory.Line(line.Words, options)).ToList())
            .Where(lines => lines.Count > 0)
            .Select(lines => LineFactory.Block(lines, LayoutOrientation.Horizontal))
            .ToList();
}
```

- [ ] **Step 6: Wire the layout into `PdfProofDocument`**

In `src/PdfProof/PdfProofDocument.cs`:
1. Add `using PdfProof.Layout;` at the top.
2. Change the constructor to take and set the layout:
```csharp
    private PdfProofDocument(PdfMetadata metadata, LayoutModel layout, PdfDiagnostics diagnostics)
    {
        Metadata = metadata;
        Layout = layout;
        Diagnostics = diagnostics;
    }
```
3. Add after the `Metadata` property:
```csharp

    /// <summary>The geometry: pages, with text grouped into blocks, lines and spans.</summary>
    public LayoutModel Layout { get; }
```
4. In `Open`, replace the final `return …;` with:
```csharp
        var layout = LayoutBuilder.Build(document, effective.Layout, diagnostics);
        return new PdfProofDocument(metadata, layout, diagnostics.ToDiagnostics(effective.Profile, detected));
```

Record the public API with the command from Global Constraints. Check that the appended lines cover the new public types in `PdfProof.Layout`, the new `LayoutOptions` properties, and `PdfProofDocument.Layout`.

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: all tests pass on every target framework, nothing skipped. `A_corrupt_page_content_stream_names_the_page` now passes, because page content is read.

If `A_corrupt_page_content_stream_names_the_page` fails because PdfPig tolerates a non-stream `/Contents` and returns an empty page, the test's broken input isn't broken enough. Make page 2's content stream syntactically invalid instead: build it with `pdf.AddPage("BT /F1 12 Tf (unterminated Tj ET")`, and drop the replace. Keep the assertion on `(page 2)`. Ledger the change as a ruling.

- [ ] **Step 8: Commit**

```bash
git add src/PdfProof tests/PdfProof.Tests
git commit -m "Add the layout model with the Rows strategy: blocks, lines, spans

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: `ReadingOrder` strategy and custom strategies

**Files:**
- Create: `src/PdfProof/Layout/ReadingOrderLayoutStrategy.cs`, `tests/PdfProof.Tests/LayoutStrategyTests.cs`
- Modify: `src/PdfProof/Layout/LayoutStrategies.cs`, `src/PdfProof/Layout/LayoutOptions.cs`, `src/PdfProof/Layout/LayoutBuilder.cs`, `src/PdfProof/PublicAPI.Unshipped.txt`

**Interfaces:**
- Consumes: `ILayoutStrategy`, `LayoutWord`, `WordFactory` and `LayoutBuilder` (Task 5).
- Produces:
  - `LayoutStrategies.ReadingOrder`.
  - `LayoutOptions.Strategy : ILayoutStrategy`, defaulting to `LayoutStrategies.Rows`.

- [ ] **Step 1: Write the failing tests**

`tests/PdfProof.Tests/LayoutStrategyTests.cs`:
```csharp
using System.Text;
using PdfProof.Layout;

namespace PdfProof.Tests;

public sealed class LayoutStrategyTests
{
    [Fact]
    public void Rows_interleaves_two_columns_line_by_line()
    {
        var page = PdfProofDocument.Load(TwoColumns()).Layout.Pages[0];

        Assert.Equal("Left column line 1", page.Lines[0].Text);
        Assert.Equal("Right column line 1", page.Lines[1].Text);
    }

    [Fact]
    public void ReadingOrder_reads_two_columns_column_by_column()
    {
        var options = PdfProofOptions.Default with { Layout = LayoutOptions.Default with { Strategy = LayoutStrategies.ReadingOrder } };

        var texts = PdfProofDocument.Load(TwoColumns(), options).Layout.Pages[0].Lines.Select(l => l.Text).ToList();

        var lastLeft = texts.FindLastIndex(t => t.StartsWith("Left", StringComparison.Ordinal));
        var firstRight = texts.FindIndex(t => t.StartsWith("Right", StringComparison.Ordinal));
        Assert.True(lastLeft < firstRight, "Reading order must finish the left column before the right one: " + string.Join(" | ", texts));
        Assert.Equal(16, texts.Count(t => t.Contains("column line")));
    }

    [Fact]
    public void A_custom_strategy_decides_the_blocks_and_is_named_in_diagnostics()
    {
        var options = PdfProofOptions.Default with { Layout = LayoutOptions.Default with { Strategy = new OneBlockPerWord() } };

        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 750, "Alpha Beta")), options);

        Assert.Equal(["Alpha", "Beta"], doc.Layout.Pages[0].Blocks.Select(b => Assert.Single(b.Lines).Text));
        Assert.Contains(doc.Diagnostics.Entries, e => e.ToString() == "Layout (page 1): 2 blocks, 2 lines (OneBlockPerWord layout)");
    }

    [Fact]
    public void Rows_is_the_default_strategy()
    {
        Assert.Same(LayoutStrategies.Rows, LayoutOptions.Default.Strategy);
    }

    private static byte[] TwoColumns()
    {
        var content = new StringBuilder();
        for (var i = 0; i < 8; i++)
        {
            content.Append(RawPdf.Text(50, 700 - (14 * i), $"Left column line {i + 1}"));
            content.Append(RawPdf.Text(320, 700 - (14 * i), $"Right column line {i + 1}"));
        }

        return RowsLayoutTests.Pdf(content.ToString());
    }

    private sealed class OneBlockPerWord : ILayoutStrategy
    {
        public string Name => "OneBlockPerWord";

        public IReadOnlyList<LayoutWordBlock> Arrange(IReadOnlyList<LayoutWord> words, LayoutOptions options) =>
            words.Select(word => new LayoutWordBlock([new LayoutWordLine([word])])).ToList();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: build FAILS with `CS0117`/`CS1061`: `LayoutStrategies` has no `ReadingOrder`, and `LayoutOptions` has no `Strategy`.

- [ ] **Step 3: Implement**

`src/PdfProof/Layout/ReadingOrderLayoutStrategy.cs`:
```csharp
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;

namespace PdfProof.Layout;

// PdfPig's Docstrum segmentation into blocks, then unsupervised reading-order detection. Handles multi-column
// pages; heuristic, so a small content change can regroup blocks.
internal sealed class ReadingOrderLayoutStrategy : ILayoutStrategy
{
    public string Name => "ReadingOrder";

    public IReadOnlyList<LayoutWordBlock> Arrange(IReadOnlyList<LayoutWord> words, LayoutOptions options)
    {
        if (words.Count == 0)
        {
            return [];
        }

        var byWord = new Dictionary<Word, LayoutWord>();
        foreach (var word in words)
        {
            byWord[word.Source] = word;
        }

        var blocks = DocstrumBoundingBoxes.Instance.GetBlocks(words.Select(word => word.Source));
        return UnsupervisedReadingOrderDetector.Instance.Get(blocks)
            .Select(block => new LayoutWordBlock(block.TextLines
                .Select(line => new LayoutWordLine(line.Words.Select(word => Find(word, byWord, options)).OfType<LayoutWord>().ToList()))
                .ToList()))
            .ToList();
    }

    // Docstrum normally returns the words it was given; if it ever rebuilds one, convert it again.
    private static LayoutWord? Find(Word word, Dictionary<Word, LayoutWord> byWord, LayoutOptions options) =>
        byWord.TryGetValue(word, out var layoutWord) ? layoutWord : WordFactory.Create(word, options);
}
```

In `src/PdfProof/Layout/LayoutStrategies.cs`, add after `Rows`:
```csharp

    /// <summary>
    /// PdfPig's Docstrum segmentation and reading-order detection. Reads multi-column pages column by column;
    /// tables come out column by column, and small content changes can regroup blocks.
    /// </summary>
    public static ILayoutStrategy ReadingOrder { get; } = new ReadingOrderLayoutStrategy();
```

In `src/PdfProof/Layout/LayoutOptions.cs`, add after the `Default` property:
```csharp

    /// <summary>How horizontal text is grouped into blocks and ordered. Default <see cref="LayoutStrategies.Rows"/>.</summary>
    public ILayoutStrategy Strategy { get; init; } = LayoutStrategies.Rows;
```

In `src/PdfProof/Layout/LayoutBuilder.cs`, replace `var strategy = LayoutStrategies.Rows;` with `var strategy = options.Strategy;`.

Record the public API with the command from Global Constraints. Expected new lines: `LayoutStrategies.ReadingOrder.get`, `LayoutOptions.Strategy.get` and `LayoutOptions.Strategy.init`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: all tests pass.

If `ReadingOrder_reads_two_columns_column_by_column` fails because Docstrum merges both columns into one block on this synthetic page, widen the column gap (put the right column at x = 360) and lengthen each column to 12 lines. Docstrum needs a clear gutter and enough lines to estimate spacing. Keep the assertion, which is only about order, and ledger the change as a ruling.

- [ ] **Step 5: Commit**

```bash
git add src/PdfProof tests/PdfProof.Tests
git commit -m "Add the ReadingOrder strategy and pluggable layout strategies

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Rotated text in its own frame

**Files:**
- Create: `src/PdfProof/Layout/RotatedText.cs`, `tests/PdfProof.Tests/RotatedTextTests.cs`
- Modify: `src/PdfProof/Layout/LayoutBuilder.cs`

**Interfaces:**
- Consumes: `WordFactory`, `LineFactory`, `LayoutBuilder` (Task 5).
- Produces: internal `RotatedText.Blocks(IReadOnlyList<LayoutWord>, LayoutOptions) -> IEnumerable<LayoutBlock>`. Rotated blocks come after the horizontal blocks on a page.

- [ ] **Step 1: Write the failing tests**

`tests/PdfProof.Tests/RotatedTextTests.cs`:
```csharp
using PdfProof.Layout;

namespace PdfProof.Tests;

// Spec §5.2: rotated text is grouped in its own frame, with the reading direction taken from baselines. On real
// forms PdfPig labels bottom-to-top text Rotate270; without this it was shredded into single letters.
public sealed class RotatedTextTests
{
    [Fact]
    public void Text_reading_bottom_to_top_is_its_own_block_in_reading_order()
    {
        var page = PdfProofDocument.Load(RowsLayoutTests.Pdf(
            RawPdf.Text(50, 750, "Form title") +
            "BT /F1 10 Tf 0 1 -1 0 30 100 Tm (Print or type) Tj ET\n")).Layout.Pages[0];

        var rotated = Assert.Single(page.Blocks, b => b.Orientation != LayoutOrientation.Horizontal);
        Assert.Equal(LayoutOrientation.Rotate270, rotated.Orientation);
        var line = Assert.Single(rotated.Lines);
        Assert.Equal("Print or type", line.Text);
        Assert.True(line.Box.Bottom < line.Box.Top);
        Assert.True(line.Box.Height > line.Box.Width, "Vertical text is taller than it is wide.");

        var horizontal = Assert.Single(page.Blocks, b => b.Orientation == LayoutOrientation.Horizontal);
        Assert.Equal("Form title", Assert.Single(horizontal.Lines).Text);
        Assert.Same(rotated, page.Blocks[page.Blocks.Count - 1]);
    }

    [Fact]
    public void Rotated_runs_far_apart_on_the_same_baseline_are_separate_blocks()
    {
        var page = PdfProofDocument.Load(RowsLayoutTests.Pdf(
            "BT /F1 10 Tf 0 1 -1 0 30 100 Tm (Lower) Tj ET\n" +
            "BT /F1 10 Tf 0 1 -1 0 30 500 Tm (Upper) Tj ET\n")).Layout.Pages[0];

        Assert.Equal(["Lower", "Upper"], page.Blocks.Select(b => Assert.Single(b.Lines).Text));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: FAIL. `Assert.Single()` finds no rotated block, because rotated letters are not yet in the layout.

- [ ] **Step 3: Implement**

`src/PdfProof/Layout/RotatedText.cs`:
```csharp
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace PdfProof.Layout;

// Rotated text is grouped in its own frame. The frame comes from the baselines (start to end of each word), not from
// the orientation label or the boxes: on real PDFs PdfPig labels bottom-to-top text Rotate270, and rotated boxes
// have Bottom > Top. Works for any angle. Each run becomes a one-line block.
internal static class RotatedText
{
    public static IEnumerable<LayoutBlock> Blocks(IReadOnlyList<LayoutWord> words, LayoutOptions options)
    {
        foreach (var group in words.GroupBy(word => word.Source.TextOrientation).OrderBy(group => group.Key))
        {
            var (dx, dy) = Direction(group);
            double Along(PdfPoint point) => (point.X * dx) + (point.Y * dy);
            double Across(PdfPoint point) => (point.Y * dx) - (point.X * dy);
            var orientation = ToOrientation(group.Key);

            var rows = new List<List<LayoutWord>>();
            foreach (var word in group.OrderByDescending(w => Across(Start(w))).ThenBy(w => Along(Start(w))))
            {
                var row = rows.Count > 0 ? rows[rows.Count - 1] : null;
                if (row is not null && Math.Abs(Across(Start(row[0])) - Across(Start(word))) <= Math.Max(1.0, 0.3 * word.Size))
                {
                    row.Add(word);
                }
                else
                {
                    rows.Add([word]);
                }
            }

            foreach (var row in rows)
            {
                var ordered = row.OrderBy(w => Along(Start(w))).ToList();
                var run = new List<LayoutWord> { ordered[0] };
                foreach (var word in ordered.Skip(1))
                {
                    if (Along(Start(word)) - Along(End(run[run.Count - 1])) > options.ColumnGapFactor * word.Size)
                    {
                        yield return LineFactory.Block([LineFactory.Line(run, options)], orientation);
                        run = [];
                    }

                    run.Add(word);
                }

                yield return LineFactory.Block([LineFactory.Line(run, options)], orientation);
            }
        }
    }

    private static PdfPoint Start(LayoutWord word) => word.Source.Letters[0].StartBaseLine;

    private static PdfPoint End(LayoutWord word) => word.Source.Letters[word.Source.Letters.Count - 1].EndBaseLine;

    // The average reading direction of a group of words, as a unit vector.
    private static (double Dx, double Dy) Direction(IEnumerable<LayoutWord> words)
    {
        double x = 0;
        double y = 0;
        foreach (var word in words)
        {
            x += End(word).X - Start(word).X;
            y += End(word).Y - Start(word).Y;
        }

        var length = Math.Sqrt((x * x) + (y * y));
        return length == 0 ? (1, 0) : (x / length, y / length);
    }

    private static LayoutOrientation ToOrientation(TextOrientation orientation) => orientation switch
    {
        TextOrientation.Horizontal => LayoutOrientation.Horizontal,
        TextOrientation.Rotate90 => LayoutOrientation.Rotate90,
        TextOrientation.Rotate180 => LayoutOrientation.Rotate180,
        TextOrientation.Rotate270 => LayoutOrientation.Rotate270,
        _ => LayoutOrientation.Other,
    };
}
```

In `src/PdfProof/Layout/LayoutBuilder.cs`, in `BuildPage`, insert this after the line `var blocks = Blocks(strategy.Arrange(words, options), options);`:
```csharp

        // Rotated text: the nearest-neighbour extractor understands orientation. The default extractor shreds
        // vertical text into single letters that get mixed into horizontal rows.
        var rotated = letters.Where(letter => letter.TextOrientation != TextOrientation.Horizontal).ToList();
        if (rotated.Count > 0)
        {
            var rotatedWords = Words(PdfPigCall.Read(() => NearestNeighbourWordExtractor.Instance.GetWords(rotated).ToList(), number), options);
            blocks.AddRange(RotatedText.Blocks(rotatedWords, options));
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/PdfProof/Layout tests/PdfProof.Tests/RotatedTextTests.cs
git commit -m "Group rotated text in its own frame, read along its baselines

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Clipping, links, images and annotations

**Files:**
- Create: `src/PdfProof/Layout/LayoutLink.cs`, `src/PdfProof/Layout/LayoutImage.cs`, `src/PdfProof/Layout/LayoutAnnotation.cs`, `tests/PdfProof.Tests/PageContentTests.cs`
- Modify: `src/PdfProof/Layout/LayoutOptions.cs`, `src/PdfProof/Layout/LayoutPage.cs`, `src/PdfProof/Layout/Geometry.cs`, `src/PdfProof/Layout/LayoutBuilder.cs`, `src/PdfProof/PdfProofDocument.cs`, `src/PdfProof/PublicAPI.Unshipped.txt`

**Interfaces:**
- Consumes: `LayoutBuilder`, `Geometry`, `WordFactory.Normalize` (Task 5).
- Produces:
  - `LayoutOptions.ApplyClipping = true`.
  - `LayoutPage.Links`: `LayoutLink` with `string Text`, `string? Uri`, `int? TargetPage`, `LayoutBox Box`.
  - `LayoutPage.Images`: `LayoutImage` with `LayoutBox Box`, `int PixelWidth`, `int PixelHeight`, `string Sha256` (16 lowercase hex digits).
  - `LayoutPage.Annotations`: `LayoutAnnotation` with `string Type`, `string? Content`, `LayoutBox Box`. Link and widget annotations are excluded.
  - Internal `Geometry.Intersects(PdfRectangle, PdfRectangle)`, `Geometry.Contains(PdfRectangle, PdfPoint)` and `Geometry.Sha256Prefix(byte[])`.

- [ ] **Step 1: Write the failing tests**

`tests/PdfProof.Tests/PageContentTests.cs`:
```csharp
using PdfProof.Layout;

namespace PdfProof.Tests;

public sealed class PageContentTests
{
    // Like SelectPdf (WebKit): the page draws more than it shows, and a clipping path hides the rest.
    private const string ClippedContent =
        "BT /F1 12 Tf 50 750 Td (Always visible) Tj ET\n" +
        "q 0 400 595 442 re W n " +
        "BT /F1 12 Tf 50 300 Td (Hidden by clipping) Tj ET " +
        "BT /F1 12 Tf 50 500 Td (Inside the clip) Tj ET Q\n";

    [Fact]
    public void Text_hidden_by_a_clipping_path_is_left_out()
    {
        var text = PdfProofDocument.Load(RowsLayoutTests.Pdf(ClippedContent)).Layout.Pages[0].Text;

        Assert.Equal("Always visible\nInside the clip", text);
    }

    [Fact]
    public void Clipping_can_be_turned_off_for_diagnosis()
    {
        var options = PdfProofOptions.Default with { Layout = LayoutOptions.Default with { ApplyClipping = false } };

        var text = PdfProofDocument.Load(RowsLayoutTests.Pdf(ClippedContent), options).Layout.Pages[0].Text;

        Assert.Contains("Hidden by clipping", text);
    }

    [Fact]
    public void Links_have_their_text_and_target_ordered_top_to_bottom()
    {
        var pdf = new RawPdf();
        var first = pdf.AddPage(
            RawPdf.Text(50, 750, "Read the terms") +
            RawPdf.Text(50, 700, "Visit our support site"));
        var second = pdf.AddPage(RawPdf.Text(50, 750, "Terms"));
        pdf.Annotate(first, "<< /Type /Annot /Subtype /Link /Rect [75 695 230 715] /A << /S /URI /URI (https://example.com/support) >> >>");
        pdf.Annotate(first, $"<< /Type /Annot /Subtype /Link /Rect [45 745 140 765] /A << /S /GoTo /D [{second} 0 R /Fit] >> >>");

        var links = PdfProofDocument.Load(pdf.Build()).Layout.Pages[0].Links;

        Assert.Collection(
            links,
            link =>
            {
                Assert.Equal("Read the terms", link.Text);
                Assert.Equal(2, link.TargetPage);
                Assert.Null(link.Uri);
                Assert.Equal(new LayoutBox(45, 745, 140, 765), link.Box);
            },
            link =>
            {
                Assert.Equal("our support site", link.Text);
                Assert.Equal("https://example.com/support", link.Uri);
                Assert.Null(link.TargetPage);
            });
    }

    [Fact]
    public void A_link_over_no_text_or_with_another_action_still_loads()
    {
        // Review Focus 5.
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Text(50, 750, "Text elsewhere"));
        pdf.Annotate(page, "<< /Type /Annot /Subtype /Link /Rect [300 100 350 120] /A << /S /JavaScript /JS (app.alert(1)) >> >>");

        var link = Assert.Single(PdfProofDocument.Load(pdf.Build()).Layout.Pages[0].Links);

        Assert.Equal("", link.Text);
        Assert.Null(link.Uri);
        Assert.Null(link.TargetPage);
    }

    [Fact]
    public void Images_on_the_page_are_kept_and_images_entirely_off_the_page_are_dropped()
    {
        var pdf = RowsLayoutTests.Pdf(
            "q 40 0 0 40 50 600 cm /Im1 Do Q\n" +
            "q 40 0 0 40 50 -900 cm /Im1 Do Q\n");

        var image = Assert.Single(PdfProofDocument.Load(pdf).Layout.Pages[0].Images);
        Assert.Equal(new LayoutBox(50, 600, 90, 640), image.Box);
        Assert.Equal((2, 2), (image.PixelWidth, image.PixelHeight));
        Assert.Matches("^[0-9a-f]{16}$", image.Sha256);

        var options = PdfProofOptions.Default with { Layout = LayoutOptions.Default with { ApplyClipping = false } };
        Assert.Equal(2, PdfProofDocument.Load(pdf, options).Layout.Pages[0].Images.Count);
    }

    [Fact]
    public void Other_annotations_are_listed_with_their_content()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Text(50, 750, "Hello"));
        pdf.Annotate(page, "<< /Type /Annot /Subtype /Text /Rect [300 300 320 320] /Contents (A note) >>");

        var annotation = Assert.Single(PdfProofDocument.Load(pdf.Build()).Layout.Pages[0].Annotations);

        Assert.Equal("Text", annotation.Type);
        Assert.Equal("A note", annotation.Content);
        Assert.Equal(new LayoutBox(300, 300, 320, 320), annotation.Box);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: build FAILS with `CS1061`: `LayoutPage` has no `Links`, `Images` or `Annotations`, and `LayoutOptions` has no `ApplyClipping`.

- [ ] **Step 3: Implement the types and the option**

`src/PdfProof/Layout/LayoutLink.cs`:
```csharp
namespace PdfProof.Layout;

/// <summary>A link annotation. Normally exactly one of <see cref="Uri"/> and <see cref="TargetPage"/> is set.</summary>
public sealed class LayoutLink
{
    internal LayoutLink(string text, string? uri, int? targetPage, LayoutBox box)
    {
        Text = text;
        Uri = uri;
        TargetPage = targetPage;
        Box = box;
    }

    /// <summary>The text under the link: letters whose centre lies inside the link rectangle.</summary>
    public string Text { get; }

    /// <summary>The target of an external (URI) link.</summary>
    public string? Uri { get; }

    /// <summary>The target page of an internal (GoTo) link.</summary>
    public int? TargetPage { get; }

    /// <summary>The link rectangle.</summary>
    public LayoutBox Box { get; }
}
```

`src/PdfProof/Layout/LayoutImage.cs`:
```csharp
namespace PdfProof.Layout;

/// <summary>An image drawn on the page.</summary>
public sealed class LayoutImage
{
    internal LayoutImage(LayoutBox box, int pixelWidth, int pixelHeight, string sha256)
    {
        Box = box;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        Sha256 = sha256;
    }

    /// <summary>Where the image is drawn.</summary>
    public LayoutBox Box { get; }

    /// <summary>The image's width in pixels.</summary>
    public int PixelWidth { get; }

    /// <summary>The image's height in pixels.</summary>
    public int PixelHeight { get; }

    /// <summary>The first 16 lowercase hex digits of the SHA-256 of the image's raw data.</summary>
    public string Sha256 { get; }
}
```

`src/PdfProof/Layout/LayoutAnnotation.cs`:
```csharp
namespace PdfProof.Layout;

/// <summary>An annotation other than a link or a form widget, e.g. a note or a highlight.</summary>
public sealed class LayoutAnnotation
{
    internal LayoutAnnotation(string type, string? content, LayoutBox box)
    {
        Type = type;
        Content = content;
        Box = box;
    }

    /// <summary>The annotation subtype, e.g. <c>Text</c> or <c>Highlight</c>.</summary>
    public string Type { get; }

    /// <summary>The annotation's <c>/Contents</c>.</summary>
    public string? Content { get; }

    /// <summary>The annotation rectangle.</summary>
    public LayoutBox Box { get; }
}
```

In `src/PdfProof/Layout/LayoutOptions.cs`, add after the `Strategy` property:
```csharp

    /// <summary>
    /// Leave out text hidden by clipping paths, and images drawn entirely outside the page. Essential for
    /// HTML-to-PDF converters such as SelectPdf, which draw the whole document on every page and clip each page to
    /// its slice. Default on; turn it off only to diagnose.
    /// </summary>
    public bool ApplyClipping { get; init; } = true;
```

In `src/PdfProof/Layout/LayoutPage.cs`:
- Extend the internal constructor with `IReadOnlyList<LayoutLink> links, IReadOnlyList<LayoutImage> images, IReadOnlyList<LayoutAnnotation> annotations` after `blocks`.
- Assign them.
- Add these properties after `Text`:
```csharp

    /// <summary>The page's links, top to bottom, then left to right.</summary>
    public IReadOnlyList<LayoutLink> Links { get; }

    /// <summary>The page's images, top to bottom, then left to right.</summary>
    public IReadOnlyList<LayoutImage> Images { get; }

    /// <summary>The page's other annotations (not links or form widgets), top to bottom, then left to right.</summary>
    public IReadOnlyList<LayoutAnnotation> Annotations { get; }
```

In `src/PdfProof/Layout/Geometry.cs`:
- Add `using System.Security.Cryptography;` and `using System.Text;`.
- Add these methods:
```csharp

    public static bool Intersects(PdfRectangle a, PdfRectangle b) =>
        Math.Min(a.Left, a.Right) < b.Right && Math.Max(a.Left, a.Right) > b.Left &&
        Math.Min(a.Bottom, a.Top) < b.Top && Math.Max(a.Bottom, a.Top) > b.Bottom;

    public static bool Contains(PdfRectangle rectangle, PdfPoint point) =>
        point.X >= rectangle.Left && point.X <= rectangle.Right && point.Y >= rectangle.Bottom && point.Y <= rectangle.Top;

    // The first 16 lowercase hex digits of the SHA-256: short, and stable for the same image data.
    public static string Sha256Prefix(byte[] bytes)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        var hex = new StringBuilder(16);
        for (var i = 0; i < 8; i++)
        {
            hex.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
        }

        return hex.ToString();
    }
```

- [ ] **Step 4: Build links, images and annotations, and apply clipping**

In `src/PdfProof/Layout/LayoutBuilder.cs`:
- Add `using System.Text.RegularExpressions;`, `using UglyToad.PdfPig.Actions;` and `using UglyToad.PdfPig.Annotations;`.
- Add the field `private static readonly Regex Whitespace = new(@"\s+", RegexOptions.CultureInvariant);` at the top of the class.
- In `BuildPage`, replace the final `return new LayoutPage(…);` with:
```csharp
        var annotations = PdfPigCall.Read(() => page.GetAnnotations().ToList(), number);
        var links = annotations
            .Where(annotation => annotation.Type == AnnotationType.Link)
            .Select(annotation => Link(annotation, letters, options))
            .OrderByDescending(link => link.Box.Top).ThenBy(link => link.Box.Left).ThenBy(link => link.Uri, StringComparer.Ordinal)
            .ToList();
        var otherAnnotations = annotations
            .Where(annotation => annotation.Type != AnnotationType.Link && annotation.Type != AnnotationType.Widget)
            .Select(annotation => new LayoutAnnotation(annotation.Type.ToString(), annotation.Content, Geometry.Round(Geometry.Bounds(annotation.Rectangle), options.Decimals)))
            .OrderByDescending(annotation => annotation.Box.Top).ThenBy(annotation => annotation.Box.Left).ThenBy(annotation => annotation.Type, StringComparer.Ordinal)
            .ToList();

        // PdfPig clips letters but not images: at least drop images drawn entirely outside the visible page.
        var visible = page.CropBox.Bounds;
        var images = PdfPigCall.Read(
                () => page.GetImages().Select(image => (Bounds: image.BoundingBox, image.WidthInSamples, image.HeightInSamples, Data: image.RawMemory.ToArray())).ToList(),
                number)
            .Where(image => !options.ApplyClipping || Geometry.Intersects(image.Bounds, visible))
            .Select(image => new LayoutImage(Geometry.Round(Geometry.Bounds(image.Bounds), options.Decimals), image.WidthInSamples, image.HeightInSamples, Geometry.Sha256Prefix(image.Data)))
            .OrderByDescending(image => image.Box.Top).ThenBy(image => image.Box.Left)
            .ToList();

        return new LayoutPage(
            number,
            Geometry.Round(page.Width, options.Decimals),
            Geometry.Round(page.Height, options.Decimals),
            page.Rotation.Value,
            blocks,
            links,
            images,
            otherAnnotations);
    }

    private static LayoutLink Link(Annotation annotation, IReadOnlyList<Letter> letters, LayoutOptions options)
    {
        string? uri = null;
        int? targetPage = null;
        switch (annotation.Action)
        {
            case UriAction uriAction:
                uri = uriAction.Uri;
                break;
            case AbstractGoToAction goTo:
                targetPage = goTo.Destination?.PageNumber;
                break;
        }

        var rectangle = annotation.Rectangle;
        var text = string.Concat(letters.Where(letter => Geometry.Contains(rectangle, letter.BoundingBox.Centroid)).Select(letter => letter.Value));
        return new LayoutLink(
            Whitespace.Replace(WordFactory.Normalize(text, options), " ").Trim(),
            uri,
            targetPage,
            Geometry.Round(Geometry.Bounds(rectangle), options.Decimals));
```

(The last line closes `Link`'s `return` statement. The method's closing brace `}` follows, as does the existing closing brace that ended `BuildPage`. Check that the braces balance.)

In `src/PdfProof/PdfProofDocument.cs`:
- Add `using PdfProof.Layout;` if it isn't already there.
- In `Open`, replace `using var document = PdfPigCall.Read(() => PdfDocument.Open(pdf));` with:
```csharp
        // Clipping is a parsing option, so it is read from the options before the profile is known. Every profile
        // uses the same layout options today; when they differ, detection must happen before this point.
        var applyClipping = (options?.Layout ?? LayoutOptions.Default).ApplyClipping;
        using var document = PdfPigCall.Read(() => PdfDocument.Open(pdf, new ParsingOptions { ClipPaths = applyClipping }));
```

Record the public API with the command from Global Constraints.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/PdfProof tests/PdfProof.Tests/PageContentTests.cs
git commit -m "Add clipping, links, images and annotations to the layout model

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Bookmarks, the `NoTextLayer` diagnostic, and repository guidance

**Files:**
- Create: `src/PdfProof/Layout/LayoutBookmark.cs`, `tests/PdfProof.Tests/BookmarkAndTextLayerTests.cs`
- Modify: `src/PdfProof/Layout/LayoutModel.cs`, `src/PdfProof/Layout/LayoutBuilder.cs`, `src/PdfProof/PdfProofDocument.cs`, `src/PdfProof/PublicAPI.Unshipped.txt`, `AGENTS.md`

**Interfaces:**
- Consumes: `LayoutBuilder`, `DiagnosticsCollector` (Tasks 3 and 5).
- Produces:
  - `LayoutModel.Bookmarks`: `LayoutBookmark` with `string Title`, `int? PageNumber`, `string? Uri`, `IReadOnlyList<LayoutBookmark> Children`.
  - A `NoTextLayer` diagnostic (logged as a warning) when no page has any text.

- [ ] **Step 1: Write the failing tests**

`tests/PdfProof.Tests/BookmarkAndTextLayerTests.cs`:
```csharp
using Microsoft.Extensions.Logging;

namespace PdfProof.Tests;

public sealed class BookmarkAndTextLayerTests
{
    [Fact]
    public void Bookmarks_are_read_as_a_tree_with_their_target_pages()
    {
        var pdf = new RawPdf();
        var first = pdf.AddPage(RawPdf.Text(50, 750, "Invoice"));
        var second = pdf.AddPage(RawPdf.Text(50, 750, "Terms"));
        var child = pdf.Reserve();
        var top = pdf.Reserve();
        var outlines = pdf.Reserve();
        pdf.Set(child, $"<< /Title (Terms and conditions) /Parent {top} 0 R /Dest [{second} 0 R /Fit] >>");
        pdf.Set(top, $"<< /Title (Invoice) /Parent {outlines} 0 R /First {child} 0 R /Last {child} 0 R /Count 1 /Dest [{first} 0 R /Fit] >>");
        pdf.Set(outlines, $"<< /Type /Outlines /First {top} 0 R /Last {top} 0 R /Count 2 >>");
        pdf.CatalogEntries = $"/Outlines {outlines} 0 R";

        var bookmark = Assert.Single(PdfProofDocument.Load(pdf.Build()).Layout.Bookmarks);

        Assert.Equal(("Invoice", (int?)1), (bookmark.Title, bookmark.PageNumber));
        var nested = Assert.Single(bookmark.Children);
        Assert.Equal(("Terms and conditions", (int?)2), (nested.Title, nested.PageNumber));
        Assert.Null(nested.Uri);
    }

    [Fact]
    public void A_pdf_without_bookmarks_has_none()
    {
        Assert.Empty(PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 750, "Hello"))).Layout.Bookmarks);
    }

    [Fact]
    public void A_pdf_with_no_text_on_any_page_loads_and_reports_NoTextLayer()
    {
        // Review Focus 2: like a scan, the page has only an image.
        var logger = new ListLogger();
        var pdf = new RawPdf();
        pdf.AddPage("q 400 0 0 600 100 100 cm /Im1 Do Q\n");
        pdf.AddPage("");

        var doc = PdfProofDocument.Load(pdf.Build(), PdfProofOptions.Default with { Logger = logger });

        Assert.All(doc.Layout.Pages, page => Assert.Empty(page.Blocks));
        var entry = Assert.Single(doc.Diagnostics.Entries, e => e.Code == PdfDiagnosticCodes.NoTextLayer);
        Assert.Equal("No page has extractable text; the PDF may be a scan. OCR is not supported.", entry.Message);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.StartsWith("NoTextLayer", StringComparison.Ordinal));
    }

    [Fact]
    public void A_pdf_with_text_on_some_page_has_no_NoTextLayer_entry()
    {
        var pdf = new RawPdf();
        pdf.AddPage("");
        pdf.AddPage(RawPdf.Text(50, 750, "Text on page 2"));

        Assert.DoesNotContain(PdfProofDocument.Load(pdf.Build()).Diagnostics.Entries, e => e.Code == PdfDiagnosticCodes.NoTextLayer);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: build FAILS with `CS1061`: `LayoutModel` has no `Bookmarks`.

- [ ] **Step 3: Implement**

`src/PdfProof/Layout/LayoutBookmark.cs`:
```csharp
namespace PdfProof.Layout;

/// <summary>A bookmark (outline item) with its children.</summary>
public sealed class LayoutBookmark
{
    internal LayoutBookmark(string title, int? pageNumber, string? uri, IReadOnlyList<LayoutBookmark> children)
    {
        Title = title;
        PageNumber = pageNumber;
        Uri = uri;
        Children = children;
    }

    /// <summary>The bookmark's title.</summary>
    public string Title { get; }

    /// <summary>The page the bookmark points to, for document bookmarks.</summary>
    public int? PageNumber { get; }

    /// <summary>The URI the bookmark points to, for URI bookmarks.</summary>
    public string? Uri { get; }

    /// <summary>Nested bookmarks, in order.</summary>
    public IReadOnlyList<LayoutBookmark> Children { get; }
}
```

In `src/PdfProof/Layout/LayoutModel.cs`, extend the constructor with `IReadOnlyList<LayoutBookmark> bookmarks`, assign it, and add:
```csharp

    /// <summary>The document's bookmarks (outline), as a tree.</summary>
    public IReadOnlyList<LayoutBookmark> Bookmarks { get; }
```

In `src/PdfProof/Layout/LayoutBuilder.cs`:
- Add `using UglyToad.PdfPig.Outline;`.
- In `Build`, replace `return new LayoutModel(pages);` with:
```csharp
        var bookmarks = PdfPigCall.Read(() => document.TryGetBookmarks(out var outline)
            ? outline.Roots.Select(Bookmark).ToList()
            : new List<LayoutBookmark>());
        return new LayoutModel(pages, bookmarks);
```
- Add the method:
```csharp

    private static LayoutBookmark Bookmark(BookmarkNode node) => new(
        node.Title,
        (node as DocumentBookmarkNode)?.PageNumber,
        (node as UriBookmarkNode)?.Uri,
        node.Children.Select(Bookmark).ToList());
```

In `src/PdfProof/PdfProofDocument.cs`, in `Open`, insert after `var layout = LayoutBuilder.Build(…);`:
```csharp
        if (layout.Pages.Count > 0 && layout.Pages.All(page => page.Blocks.Count == 0))
        {
            diagnostics.Add(PdfDiagnosticCodes.NoTextLayer, null, "No page has extractable text; the PDF may be a scan. OCR is not supported.");
        }
```

Record the public API with the command from Global Constraints.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: all tests pass.

- [ ] **Step 5: Update `AGENTS.md`**

In `AGENTS.md`, in the `## Commands` code block, add after the `dotnet test` line:
```bash
# record new public API (run after adding public members; then rebuild and review the appended lines)
dotnet build src/PdfProof/PdfProof.csproj 2>&1 | sed -n "s/.*error RS0016: Symbol '\([^']*\)' is not part of the declared public API.*/\1/p" | sort -u >> src/PdfProof/PublicAPI.Unshipped.txt
```

In the `## Layout` table, replace the `src/PdfProof` row with:
```markdown
| `src/PdfProof` | Core: `PdfProofDocument`, options and profiles, diagnostics; `Layout/` (layout model and strategies) (netstandard2.0; net8.0; net10.0) |
```

Add this rule to the `## Rules` list, after the TDD rule:
```markdown
- **Test PDFs:** build them in code. Use `tests/PdfProof.Tests/RawPdf.cs` (raw PDF syntax: exact positions, clipping, rotation, annotations, outlines) or PdfPig's `PdfDocumentBuilder`. Don't commit generated PDF files.
- **Calls into PdfPig** go through `PdfPigCall.Read`, so parser errors become `PdfProofException` and PdfProof bugs don't.
```

- [ ] **Step 6: Run the whole build**

Run: `./build.ps1`
Expected: BuildTests, Build, Test (net48 + net10.0) and Pack all succeed.

- [ ] **Step 7: Commit**

```bash
git add src/PdfProof tests/PdfProof.Tests AGENTS.md
git commit -m "Add bookmarks and the NoTextLayer diagnostic; update AGENTS.md

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Self-review notes

- **Spec coverage:**

  | Spec item | Task |
  |---|---|
  | §5.1 `PdfProofDocument` with `Metadata`, `Layout`, `Diagnostics` (`Structure` and `Semantics` come in Plans 3 and 5) | 3, 5 |
  | §5.1 custom info entries | 1 |
  | §5.2 eager page-by-page building, letters dropped per page; clipping; normalization | 5, 8 |
  | §5.2 `Rows`, `ReadingOrder`, custom `ILayoutStrategy` (D8) | 5, 6 |
  | §5.2 rotated text | 7 |
  | §5.2 fonts: subset prefix, bold and italic | 5 |
  | §5.2 images off-page dropped | 8 |
  | §5.3 options, presets, auto-detection, `ILogger` (D14) | 3 |
  | §5.4 corrupt PDF names the page; `NoTextLayer` loads with a diagnostic | 2, 9 |

- **Moved to later plans, with the reason recorded in "Where this plan fits":**
  - AcroForm fields and encrypted PDFs go to Plan 4, which has the fixtures.
  - Font embedding (PP009) and clipped letter counts go to the `0.2.0-alpha` plans.
