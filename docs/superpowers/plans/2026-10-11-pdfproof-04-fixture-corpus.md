# PdfProof Plan 4: Fixture Generator and Corpus Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every build renders the `invoice` document with every available producer (SelectPdf, Chromium, Typst, QuestPDF, MigraDoc, LibreOffice), checks each rendering against one hand-written oracle, and checks two pinned real-world PDFs (IRS W-9 and W-4). Along the way the core gains passwords for encrypted PDFs, AcroForm fields in the layout model, and profiles that are settings rather than labels.

**Architecture:**
- **Core first.** Three changes to `src/PdfProof`, each test-first:
  1. `PdfProfile` becomes a settings record with presets, and `PdfProofOptions` becomes `{ Profile?, Password?, Logger? }`, where a null profile means "detect" (spec §5.3, amended in `b218c1a`).
  2. A password opens encrypted PDFs, and missing or wrong passwords get clear errors.
  3. `LayoutPage.FormFields` holds one `LayoutFormField` per widget.

  Encrypted test PDFs come from PDFsharp, a test-only dependency. Form test PDFs come from `RawPdf`.
- **Generator.** `tools/PdfProof.FixtureGenerator` is a net10.0 console app. Each producer sits behind a small internal `IProducer` interface. It renders each document folder under `corpus/generated/` to `artifacts/corpus/<document>/<producer>.pdf` and writes a `manifest.json` of producer versions and statuses. Generated PDFs are never committed.
- **Corpus tests.** `tests/PdfProof.Corpus.Tests` holds:
  - a matrix of document × producer, compared against `expected.semantics.json`. Until the semantic model exists (Plan 5), the comparison is text-level: title and page count, block text in order, links, structure roles for tagged producers, and the footer artifact. Known producer gaps live in `deviations.json`.
  - code assertions on the IRS forms.

  A missing fixture skips its test; with `PDFPROOF_REQUIRE_FIXTURES=true` (CI) it fails.
- **Build and CI.**
  - `Build.Cake.Extensions` gets pinned downloads (URL + SHA-256) and the pinned Typst CLI.
  - The Cake targets `GenerateFixtures` and `FetchCorpus` join the `Default` chain.
  - In CI, a Windows `fixtures` job feeds the Windows and Linux `build` jobs.

**Tech Stack:** PdfPig 0.1.16 (`ParsingOptions.Password`, `TryGetForm`), PDFsharp 6.2.4 (tests only), QuestPDF 2026.9.1, PDFsharp-MigraDoc 6.2.4, Select.HtmlToPdf.NetCore 26.4.0, Microsoft.Playwright 1.63.0, Typst CLI 0.15.1, System.Text.Json 10.0.12 (net48 tests), xUnit v3, Cake.Sdk 6.3.0.

**Spec:** `docs/superpowers/specs/2026-10-11-pdfproof-v1-design.md` (§5.1–5.4, §10.1–10.3, §11.1, §11.2, §12; amended for this plan in commit `b218c1a`)

## Where this plan fits

| Plan | Delivers |
|---|---|
| 1 Foundation, 2 Layout model, 3 Structure model | Done (PR #1, #2, #3) |
| **4 Fixture generator and corpus (this plan)** | Profiles as settings; passwords; AcroForm fields; generator with six producers; `invoice` oracle and matrix; IRS W-9/W-4; `GenerateFixtures`, `FetchCorpus`; CI fixtures job |
| 5 Semantic passes | Candidate blocks, passes, `SemanticModel`; the corpus comparison moves from text level to full block lists |
| 6 Export | Export tree, scrubbing, Markdown and JSON; approved exports for the corpus |
| 7 CLI, skill and `0.1.0-alpha` | `export`, `explain`, `skill …`, first pre-release |

**Decisions made while brainstorming this plan** (recorded so later plans don't re-open them):
- **The oracle comparison** in this plan is text-level (spec §10.2 amendment). The oracle files and the matrix logic are final.
- **AcroForm test inputs** come from `RawPdf`, because PDFsharp 6.2.4 can't create form fields (internal constructors). The IRS forms are the real-world check. The generated `form` document waits for the plan that adds the remaining corpus documents.
- **Encrypted test inputs** are made in memory: PDFsharp re-saves a `RawPdf` PDF with passwords (RC4-128, AES-128, AES-256). This needs no fonts.
- **QuestPDF 2026.9.1 writes no `/Producer` or `/Creator`**, so `PdfProfile.QuestPdf` is never detected. The generator doesn't fake one.
- **LibreOffice** renders when installed and is never installed in CI.
- **IRS forms are pinned by `irs-prior` URLs.** The `irs-pdf` URLs always serve the latest revision.
- **Typstium is not on a package feed (OQ-2)**, so the build downloads the Typst CLI, pinned by version and SHA-256.

**Probe results this plan relies on** (2026-10-11, this machine):

| Producer | `/Producer` · `/Creator` | Detected | Tagged |
|---|---|---|---|
| SelectPdf 26.4.0 | `Select.Pdf Html To Pdf for .NET Community Edition v26.4.0` · – | SelectPdf | no |
| Chromium (Playwright 1.63) | `Skia/PDF m153` · `Chromium` | Chromium | with `Tagged = true` |
| Typst 0.15.1 | – · `Typst 0.15.1` | Typst (from /Creator) | yes, by default |
| QuestPDF 2026.9.1 | – · – | Default | with `PDFUA_Conformance` |
| MigraDoc 6.2.4 | `PDFsharp 6.2.4` · (MigraDoc's own string) | Default | no |

**Notes for Plan 5** (found while probing, deliberately not fixed here):
- **Typst tables split differently.** Typst sizes table columns tightly. Gaps between cells can fall below `ColumnGapFactor` (1.0 × font size), so the Rows strategy gives "Item | Qty Amount" for a 3-cell row instead of three lines. Rows are never mixed. Typst output is tagged, so its tables will come from the structure tree.
- **Clipping and detection.** Clipping is a PdfPig parse option, read from the given profile (or `PdfProfile.Default`) before detection. Once presets differ in `ApplyClipping`, detection must read the info dictionary before the pages are parsed.
- **Chromium's tagged output** wraps text in `NonStruct` elements. The structure front end has to look through them.

## Global Constraints

- Library target frameworks: `netstandard2.0;net8.0;net10.0`. Tests: `net48;net10.0` on Windows, `net10.0` elsewhere. Exceptions:
  - `tools/PdfProof.FixtureGenerator` and `tests/PdfProof.FixtureGenerator.Tests` are `net10.0` only, because their producers need modern .NET.
  - `build/` stays `net10.0`.
- **netstandard2.0/net48-safe code in `src/` and in `tests/PdfProof.Tests`/`tests/PdfProof.Corpus.Tests`:** no `GeneratedRegex`, `SHA256.HashData`, `Convert.ToHexString*`, `[^1]`, `ToHashSet`, `KeyValuePair` deconstruction or `string.Join(char, …)`.
- `TreatWarningsAsErrors`, nullable enabled, XML docs on every public member.
- **Every public API change goes into `src/PdfProof/PublicAPI.Unshipped.txt`.** After adding public members, run from the repository root (bash):
  ```bash
  dotnet build src/PdfProof/PdfProof.csproj 2>&1 | sed -n "s/.*error RS0016: Symbol '\([^']*\)' is not part of the declared public API.*/\1/p" | sort -u >> src/PdfProof/PublicAPI.Unshipped.txt
  ```
  Then rebuild; expect 0 errors. Check that the appended lines are only the members you added. Removed members are deleted from the file by hand (the build reports them as RS0017).
- **Throwing:** guard clauses and throws in `src/` use the shared `Throw` helper (`src/Shared/Throw/`). New PdfProof-specific helpers go in `Throw.PdfProof.cs`.
- **Calls into PdfPig** go through `PdfPigCall.Read`; mapping PdfPig objects to the model happens outside it.
- **Public model collections** are read-only copies (`ReadOnly.List`).
- **Deterministic output:** invariant culture, explicit sort orders with tie-breakers, `\n` line endings, no timestamps (the generator's `manifest.json` included).
- **Test PDFs are built in code** (`RawPdf`, `EncryptedPdf`). Generated corpus PDFs go to `artifacts/` and are never committed.
- **Escapes get mangled when files are written.** Never write `\u`, `\t` or similar escapes in this plan's code. Use `(char)0xAD`-style expressions, or edit through a Python script that builds backslashes with `chr(92)`. After writing a file, scan it for control or format characters.
- **Never write real company or customer names.** Sample data is neutral ("Consulting, senior", `example.com`). Producer product names (SelectPdf, Typst, QuestPDF, Chromium, PDFsharp, MigraDoc, LibreOffice) are fine; vendor or company names are not.
- Package versions are pinned in `Directory.Packages.props`.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Test commands: `dotnet test --project <test project .csproj>`. The whole build is `./build.ps1`.

## Review Focus

1. **The password must never leak.** It must not appear in an exception message, a diagnostic, a log entry or `PdfProofOptions.ToString()`, whether it is right, wrong or unneeded. Test in Task 2.
2. **A damaged `/AcroForm`** (a field reference to a missing object makes PdfPig throw a `NullReferenceException`) must not fail the load. The document loads without form fields and with a `FormFieldsUnreadable` warning. Test in Task 3.
3. **A form widget without `/P`** (PdfPig gives it no page) must not crash or disappear silently. It is left out and counted in the `FormFields` diagnostic. Test in Task 3.
4. **A producer that fails mid-run** (Typst syntax error, browser crash) must not abort the run, and must not leave the previous run's PDF behind as a fixture. Its status is recorded and its stale file removed. Test in Task 4.
5. **A fixture that exists but isn't a readable PDF** (a truncated artifact download) must fail its matrix case with the file path and the error, not skip and not crash the run. Test in Task 8.

---

### Task 1: Profiles become settings; options choose one

**Files:**
- Modify: `src/PdfProof/PdfProfile.cs` (enum → record), `src/PdfProof/PdfProofOptions.cs`, `src/PdfProof/ProducerProfiles.cs`, `src/PdfProof/PdfDiagnostics.cs`, `src/PdfProof/DiagnosticsCollector.cs`, `src/PdfProof/PdfProofDocument.cs`, `src/PdfProof/PublicAPI.Unshipped.txt`
- Modify (tests): `tests/PdfProof.Tests/OptionsAndProfilesTests.cs` (rewritten), `tests/PdfProof.Tests/ImmutabilityAndValidationTests.cs`, `tests/PdfProof.Tests/RowsLayoutTests.cs`, `tests/PdfProof.Tests/LayoutStrategyTests.cs`, `tests/PdfProof.Tests/PageContentTests.cs`

**Interfaces:**
- Consumes: `LayoutOptions` (Plan 2), `PdfProofDocument.Load` overloads, `DiagnosticsCollector`.
- Produces:
  - `public sealed record PdfProfile`:
    - `PdfProfile(string name)`, `string Name { get; init; }`, `LayoutOptions Layout { get; init; }`;
    - static presets `Default`, `SelectPdf`, `QuestPdf`, `Typst`, `Chromium`;
    - `override string ToString()`, which returns `Name`;
    - `internal static IReadOnlyList<PdfProfile> Presets`, in that order.
  - `public sealed record PdfProofOptions`: `static Default` (all null), `PdfProfile? Profile`, `ILogger? Logger`. `Password` comes in Task 2.
  - `PdfDiagnostics.Profile` is now a `PdfProfile`; `ProfileDetected` is unchanged.
  - Internal `ProducerProfiles.Detect(string? producer, string? creator) -> (PdfProfile Profile, string Reason)`.
  - The profile diagnostic reads `"<Name> (<reason>)"`. The reason is one of:
    - `detected from producer "…"`
    - `detected from creator "…"`
    - `no profile matches producer "…" or creator "…"` (only the entries present)
    - `the PDF names no producer or creator`
    - `from options`
    - `from options, adjusted`: a preset changed with `with`
    - `from options, custom`: a name no preset has

- [ ] **Step 1: Rewrite `tests/PdfProof.Tests/OptionsAndProfilesTests.cs` with the new shape**

The detection cases use the real strings from the probes above.

```csharp
using Microsoft.Extensions.Logging;
using PdfProof.Layout;

namespace PdfProof.Tests;

public sealed class OptionsAndProfilesTests
{
    [Theory]
    [InlineData("Select.Pdf Html To Pdf for .NET Community Edition v26.4.0", null, "SelectPdf", "detected from producer \"Select.Pdf Html To Pdf for .NET Community Edition v26.4.0\"")]
    [InlineData("SelectPdf Library", null, "SelectPdf", "detected from producer \"SelectPdf Library\"")]
    [InlineData("Skia/PDF m153", "Chromium", "Chromium", "detected from producer \"Skia/PDF m153\"")]
    [InlineData(null, "Mozilla/5.0 HeadlessChrome/120", "Chromium", "detected from creator \"Mozilla/5.0 HeadlessChrome/120\"")]
    [InlineData(null, "Typst 0.15.1", "Typst", "detected from creator \"Typst 0.15.1\"")]
    [InlineData("QuestPDF", null, "QuestPdf", "detected from producer \"QuestPDF\"")]
    [InlineData("PDFsharp 6.2.4", null, "Default", "no profile matches producer \"PDFsharp 6.2.4\"")]
    [InlineData("PdfPig", "Writer", "Default", "no profile matches producer \"PdfPig\" or creator \"Writer\"")]
    [InlineData(null, null, "Default", "the PDF names no producer or creator")]
    public void The_profile_is_detected_from_producer_then_creator(string? producer, string? creator, string expected, string reason)
    {
        var (profile, why) = ProducerProfiles.Detect(producer, creator);

        Assert.Same(PdfProfile.Presets.Single(p => p.Name == expected), profile);
        Assert.Equal(reason, why);
    }

    [Fact]
    public void Load_without_options_detects_the_profile_and_says_from_where()
    {
        var doc = PdfProofDocument.Load(PdfWithInfo("<< /Producer (Select.Pdf Html To Pdf for .NET Community Edition v26.4.0) >>"));

        Assert.Same(PdfProfile.SelectPdf, doc.Diagnostics.Profile);
        Assert.True(doc.Diagnostics.ProfileDetected);
        Assert.Equal(
            "Profile: SelectPdf (detected from producer \"Select.Pdf Html To Pdf for .NET Community Edition v26.4.0\")",
            doc.Diagnostics.Entries[0].ToString());
    }

    [Fact]
    public void Typst_is_detected_from_the_creator_and_the_diagnostic_says_so()
    {
        var doc = PdfProofDocument.Load(PdfWithInfo("<< /Creator (Typst 0.15.1) >>"));

        Assert.Same(PdfProfile.Typst, doc.Diagnostics.Profile);
        Assert.Equal("Profile: Typst (detected from creator \"Typst 0.15.1\")", doc.Diagnostics.Entries[0].ToString());
    }

    [Fact]
    public void Options_without_a_profile_still_detect_it()
    {
        var logger = new ListLogger();

        var doc = PdfProofDocument.Load(PdfWithInfo("<< /Producer (Typst 0.13.1) >>"), new PdfProofOptions { Logger = logger });

        Assert.Same(PdfProfile.Typst, doc.Diagnostics.Profile);
        Assert.True(doc.Diagnostics.ProfileDetected);
        Assert.Contains((LogLevel.Debug, "Profile: Typst (detected from producer \"Typst 0.13.1\")"), logger.Entries);
    }

    [Fact]
    public void A_profile_in_the_options_is_used_as_given()
    {
        var doc = PdfProofDocument.Load(
            PdfWithInfo("<< /Producer (Select.Pdf Html To Pdf for .NET Community Edition v26.4.0) >>"),
            new PdfProofOptions { Profile = PdfProfile.QuestPdf });

        Assert.Same(PdfProfile.QuestPdf, doc.Diagnostics.Profile);
        Assert.False(doc.Diagnostics.ProfileDetected);
        Assert.Equal("Profile: QuestPdf (from options)", doc.Diagnostics.Entries[0].ToString());
    }

    [Fact]
    public void An_adjusted_preset_keeps_its_name_and_is_reported_as_adjusted()
    {
        var adjusted = PdfProfile.SelectPdf with { Layout = LayoutOptions.Default with { NormalizeText = false } };

        var doc = PdfProofDocument.Load(PdfWithInfo("<< >>"), new PdfProofOptions { Profile = adjusted });

        Assert.Equal("SelectPdf", doc.Diagnostics.Profile.Name);
        Assert.False(doc.Diagnostics.Profile.Layout.NormalizeText);
        Assert.Equal("Profile: SelectPdf (from options, adjusted)", doc.Diagnostics.Entries[0].ToString());
    }

    [Fact]
    public void A_custom_profile_is_reported_as_custom()
    {
        var doc = PdfProofDocument.Load(PdfWithInfo("<< >>"), new PdfProofOptions { Profile = new PdfProfile("In-house") });

        Assert.Equal("Profile: In-house (from options, custom)", doc.Diagnostics.Entries[0].ToString());
    }

    [Fact]
    public void The_presets_have_default_layout_and_the_default_options_choose_nothing()
    {
        Assert.Equal(["Default", "SelectPdf", "QuestPdf", "Typst", "Chromium"], PdfProfile.Presets.Select(p => p.Name));
        Assert.All(PdfProfile.Presets, p => Assert.Same(LayoutOptions.Default, p.Layout));
        Assert.Equal("Typst", PdfProfile.Typst.ToString());
        Assert.Null(PdfProofOptions.Default.Profile);
        Assert.Null(PdfProofOptions.Default.Logger);
    }

    [Fact]
    public void A_profile_needs_a_name_and_layout_options()
    {
        Assert.Equal("name", Assert.Throws<ArgumentNullException>(() => new PdfProfile(null!)).ParamName);
        Assert.Equal("name", Assert.Throws<ArgumentException>(() => new PdfProfile(" ")).ParamName);
        Assert.Equal("Name", Assert.Throws<ArgumentException>(() => PdfProfile.Default with { Name = "" }).ParamName);
        Assert.Equal("Layout", Assert.Throws<ArgumentNullException>(() => PdfProfile.Default with { Layout = null! }).ParamName);
    }

    [Fact]
    public void Null_options_throw_ArgumentNullException()
    {
        var pdf = PdfWithInfo("<< /Producer (PdfPig) >>");

        Assert.Throws<ArgumentNullException>(() => PdfProofDocument.Load(pdf, null!));
        Assert.Throws<ArgumentNullException>(() => PdfProofDocument.Load(new MemoryStream(pdf), null!));
        Assert.Throws<ArgumentNullException>(() => PdfProofDocument.Load(Path.GetTempPath(), null!));
    }

    private static byte[] PdfWithInfo(string info)
    {
        var pdf = new RawPdf { Info = info };
        pdf.AddPage(RawPdf.Text(50, 750, "Hello"));
        return pdf.Build();
    }
}
```

- [ ] **Step 2: Move the other tests to the new shape**

Wherever a test builds options with layout settings, the layout settings move into a profile. The pattern:

```csharp
// before
PdfProofOptions.Default with { Layout = LayoutOptions.Default with { … } }
// after
new PdfProofOptions { Profile = PdfProfile.Default with { Layout = LayoutOptions.Default with { … } } }
```

Apply it to:
- `RowsLayoutTests.cs`: line 77 (`NormalizeText = false`), and the two multi-line `options` in `Text_replacements_apply_after_normalization` and `A_word_that_is_empty_after_normalization_creates_no_line`.
- `LayoutStrategyTests.cs`: lines 20 and 33.
- `PageContentTests.cs`: lines 25 and 107.
- `ImmutabilityAndValidationTests.cs`: line 41.

In `ImmutabilityAndValidationTests.cs`, delete the test `Null_layout_options_are_rejected`. `A_profile_needs_a_name_and_layout_options` in Step 1 replaces it.

`PdfProofOptions.Default with { Logger = logger }` (in `BookmarkAndTextLayerTests.cs` and `StructureTests.cs`) still compiles and stays as is.

- [ ] **Step 3: Run the tests to see them fail**

Run: `dotnet build tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: build FAILS. Among the errors:
- `PdfProfile` has no constructor taking a string;
- `ProducerProfiles.Detect` returns no tuple;
- `PdfProfile.Presets` doesn't exist;
- `PdfProofOptions.Profile` is not a `PdfProfile?`.

- [ ] **Step 4: Replace `src/PdfProof/PdfProfile.cs`**

```csharp
using PdfProof.Layout;

namespace PdfProof;

/// <summary>
/// How to read PDFs from one producer: a named, complete set of settings. Use a preset such as
/// <see cref="SelectPdf"/> or <see cref="Typst"/>, adjusted with <c>with</c> when needed, or create your own. Choose a
/// profile with <see cref="PdfProofOptions.Profile"/>; without one, <see cref="PdfProofDocument"/> detects it from the
/// PDF's <c>/Producer</c> and <c>/Creator</c> entries. Settings are never merged: a document is read with exactly the
/// settings of one profile.
/// </summary>
public sealed record PdfProfile
{
    /// <summary>Creates a profile with default settings.</summary>
    /// <param name="name">The profile's name, shown in diagnostics.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or whitespace.</exception>
    public PdfProfile(string name)
    {
        Name = Throw.IfNullOrWhitespace(name);
    }

    /// <summary>No producer-specific settings. Used when no other profile is detected.</summary>
    public static PdfProfile Default { get; } = new("Default");

    /// <summary>SelectPdf (HTML to PDF). Needs clipping and text normalization, which are on by default.</summary>
    public static PdfProfile SelectPdf { get; } = new("SelectPdf");

    /// <summary>
    /// QuestPDF. QuestPDF writes no <c>/Producer</c> or <c>/Creator</c> entry, so this profile is never detected:
    /// choose it with <see cref="PdfProofOptions.Profile"/>.
    /// </summary>
    public static PdfProfile QuestPdf { get; } = new("QuestPdf");

    /// <summary>Typst. Detected from the <c>/Creator</c> entry, which is the only one Typst writes.</summary>
    public static PdfProfile Typst { get; } = new("Typst");

    /// <summary>Chromium print-to-PDF (Chrome, Edge, Playwright, Puppeteer).</summary>
    public static PdfProfile Chromium { get; } = new("Chromium");

    // Declared after the presets: static initializers run in order.
    internal static IReadOnlyList<PdfProfile> Presets { get; } = ReadOnly.List([Default, SelectPdf, QuestPdf, Typst, Chromium]);

    /// <summary>The profile's name, shown in diagnostics. A preset adjusted with <c>with</c> keeps its name.</summary>
    public string Name
    {
        get => _name;
        init => _name = Throw.IfNullOrWhitespace(value, nameof(Name));
    }

    /// <summary>How text is extracted and grouped into the layout model.</summary>
    public LayoutOptions Layout
    {
        get => _layout;
        init => _layout = Throw.IfNull(value, nameof(Layout));
    }

    /// <summary>The profile's name.</summary>
    public override string ToString() => Name;

    private readonly string _name = "";
    private readonly LayoutOptions _layout = LayoutOptions.Default;
}
```

- [ ] **Step 5: Replace `src/PdfProof/PdfProofOptions.cs`**

```csharp
using Microsoft.Extensions.Logging;

namespace PdfProof;

/// <summary>
/// How to load one PDF: which profile to read it with and where to log. Producer-specific settings live in
/// <see cref="PdfProfile"/>; these options only choose one.
/// </summary>
public sealed record PdfProofOptions
{
    /// <summary>No profile (it is detected), no logging.</summary>
    public static PdfProofOptions Default { get; } = new();

    /// <summary>
    /// The profile to read the PDF with, exactly as given. <see langword="null"/> (the default) detects it from the
    /// PDF's <c>/Producer</c> and <c>/Creator</c> entries.
    /// </summary>
    public PdfProfile? Profile { get; init; }

    /// <summary>Receives every diagnostic entry as it is recorded; <see langword="null"/> for no logging.</summary>
    public ILogger? Logger { get; init; }
}
```

- [ ] **Step 6: Replace `src/PdfProof/ProducerProfiles.cs`**

```csharp
namespace PdfProof;

// Detects the profile from the info dictionary and says which entry matched. Checked against real output in Plan 4:
// SelectPdf writes "Select.Pdf Html To Pdf for .NET Community Edition v26.4.0" in /Producer; Chromium "Skia/PDF m153"
// with /Creator "Chromium"; Typst only /Creator "Typst 0.15.1". QuestPDF 2026.9 writes neither entry, so it is never
// detected; "QuestPDF" is still matched for versions or callers that set it.
internal static class ProducerProfiles
{
    public static (PdfProfile Profile, string Reason) Detect(string? producer, string? creator)
    {
        if (Match(producer) is { } fromProducer)
        {
            return (fromProducer, $"detected from producer \"{producer}\"");
        }

        if (Match(creator) is { } fromCreator)
        {
            return (fromCreator, $"detected from creator \"{creator}\"");
        }

        var named = new List<string>();
        if (!string.IsNullOrEmpty(producer))
        {
            named.Add($"producer \"{producer}\"");
        }

        if (!string.IsNullOrEmpty(creator))
        {
            named.Add($"creator \"{creator}\"");
        }

        return (PdfProfile.Default, named.Count == 0 ? "the PDF names no producer or creator" : "no profile matches " + string.Join(" or ", named));
    }

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

- [ ] **Step 7: Change the profile's type in `PdfDiagnostics.cs` and `DiagnosticsCollector.cs`**

In `src/PdfProof/PdfDiagnostics.cs`, change the constructor parameter and the property to `PdfProfile` (a class now) and keep the docs:
```csharp
    internal PdfDiagnostics(PdfProfile profile, bool profileDetected, IReadOnlyList<PdfDiagnostic> entries)
```
```csharp
    /// <summary>The profile the document was read with: the one in the options, or the detected one.</summary>
    public PdfProfile Profile { get; }
```
`DiagnosticsCollector.ToDiagnostics(PdfProfile profile, bool profileDetected)` keeps its signature; the type is now the record.

- [ ] **Step 8: Choose the profile in `PdfProofDocument.cs`**

Change the three option-less overloads to call `Open(…, PdfProofOptions.Default)` instead of `Open(…, null)`. Change the docs of the three `options` overloads to:
```csharp
    /// <param name="options">How to load the PDF. Without a profile in them, the profile is detected from the producer.</param>
```
Then replace the start of `Open`, up to and including the profile diagnostic:

```csharp
    private static PdfProofDocument Open(byte[] pdf, PdfProofOptions options)
    {
        // Clipping is a parsing option, so it is read before the profile is detected. Every preset uses the same
        // layout options today; when they differ, detection must read the info dictionary before the pages (Plan 5).
        var applyClipping = (options.Profile ?? PdfProfile.Default).Layout.ApplyClipping;
        using var document = PdfPigCall.Read(() => PdfDocument.Open(pdf, new ParsingOptions { ClipPaths = applyClipping }));
        // PdfPig returns the raw values; mapping them to PdfMetadata happens outside PdfPigCall.
        var info = PdfPigCall.Read(() => (document.Version, document.NumberOfPages, document.Information));
        var metadata = MetadataReader.Read(info.Version.ToString("0.0", CultureInfo.InvariantCulture), info.NumberOfPages, info.Information);

        var (profile, reason) = options.Profile is { } given
            ? (given, Chosen(given))
            : ProducerProfiles.Detect(metadata.Producer, metadata.Creator);
        var detected = options.Profile is null;
        var diagnostics = new DiagnosticsCollector(options.Logger);
        diagnostics.Add(PdfDiagnosticCodes.Profile, null, $"{profile.Name} ({reason})");
```

In the rest of `Open`:
- replace every `effective.Layout` with `profile.Layout`;
- end the method with `return new PdfProofDocument(metadata, layout, structure, diagnostics.ToDiagnostics(profile, detected));`.

Add this helper below `Open`:

```csharp
    // How a profile given in the options relates to the presets, for the profile diagnostic.
    private static string Chosen(PdfProfile profile) =>
        PdfProfile.Presets.Contains(profile) ? "from options"
        : PdfProfile.Presets.Any(preset => preset.Name == profile.Name) ? "from options, adjusted"
        : "from options, custom";
```

- [ ] **Step 9: Update `PublicAPI.Unshipped.txt`**

Delete these lines (the enum and the removed options members):
```
PdfProof.PdfDiagnostics.Profile.get -> PdfProof.PdfProfile
PdfProof.PdfProfile.Chromium = 4 -> PdfProof.PdfProfile
PdfProof.PdfProfile.Default = 0 -> PdfProof.PdfProfile
PdfProof.PdfProfile.QuestPdf = 2 -> PdfProof.PdfProfile
PdfProof.PdfProfile.SelectPdf = 1 -> PdfProof.PdfProfile
PdfProof.PdfProfile.Typst = 3 -> PdfProof.PdfProfile
PdfProof.PdfProofOptions.Layout.get -> PdfProof.Layout.LayoutOptions!
PdfProof.PdfProofOptions.Layout.init -> void
PdfProof.PdfProofOptions.Profile.get -> PdfProof.PdfProfile
PdfProof.PdfProofOptions.Profile.init -> void
static PdfProof.PdfProofOptions.ForProfile(PdfProof.PdfProfile profile) -> PdfProof.PdfProofOptions!
```
Then run the RS0016 command from the Global Constraints and rebuild.

Expected appended lines:
- the `PdfProfile` record members (`PdfProfile(string! name)`, `Name`, `Layout`, `<Clone>$`, `Equals`, `GetHashCode`, `ToString`, the operators, the five static presets);
- `PdfProofOptions.Profile.get -> PdfProof.PdfProfile?` and its `init`;
- `PdfDiagnostics.Profile.get -> PdfProof.PdfProfile!`.

- [ ] **Step 10: Run the tests**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: all tests pass on net48 and net10.0.

- [ ] **Step 11: Commit**

```bash
git add src/PdfProof tests/PdfProof.Tests
git commit -m "Profiles are settings and options choose one; detection says which entry matched

PdfProfile becomes a record with presets; PdfProofOptions holds the profile
(null = detect) and the logger. Detection reasons name the matching entry, which
fixes Typst being reported as detected from an empty producer. Detection tests
use the producer strings of real SelectPdf, Chromium and Typst output.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Encrypted PDFs: a password option and clear errors

**Files:**
- Create: `tests/PdfProof.Tests/EncryptedPdf.cs`, `tests/PdfProof.Tests/EncryptionTests.cs`
- Modify: `src/PdfProof/PdfProofOptions.cs`, `src/PdfProof/PdfProofDocument.cs`, `src/PdfProof/PdfDiagnosticCodes.cs`, `src/Shared/Throw/Throw.PdfProof.cs`, `src/PdfProof/PublicAPI.Unshipped.txt`, `tests/PdfProof.Tests/PdfProof.Tests.csproj`, `Directory.Packages.props`

**Interfaces:**
- Consumes: `PdfProofOptions` (Task 1), `PdfPigCall.Read`, `RawPdf`.
- Produces:
  - `PdfProofOptions.Password : string?`. Its `ToString()` never shows the password.
  - `PdfDiagnosticCodes.Encrypted = "Encrypted"` (info).
  - `Throw.EncryptedPdf(Exception parserError, bool passwordGiven)` in `Throw.PdfProof.cs`.
  - Test helper `EncryptedPdf.Encrypt(byte[] pdf, string? userPassword, string ownerPassword, string level = "aes-256")`, where level is `"rc4-128"`, `"aes-128"` or `"aes-256"`.

- [ ] **Step 1: Add PDFsharp as a test-only dependency**

In `Directory.Packages.props`, add inside the `<ItemGroup>`:
```xml
    <!-- Tests only: encrypts test PDFs in memory (PdfPig reads encrypted PDFs but can't write them). -->
    <PackageVersion Include="PDFsharp" Version="6.2.4" />
```
In `tests/PdfProof.Tests/PdfProof.Tests.csproj`, add to the package `<ItemGroup>`:
```xml
    <PackageReference Include="PDFsharp" />
```

- [ ] **Step 2: Write `tests/PdfProof.Tests/EncryptedPdf.cs`**

```csharp
using PdfSharp.Pdf.IO;

namespace PdfProof.Tests;

// Encrypts a PDF in memory with PDFsharp (a test-only dependency): PdfPig reads encrypted PDFs but can't write them.
// Opening and saving draws nothing, so PDFsharp needs no font resolver.
internal static class EncryptedPdf
{
    // level: "rc4-128", "aes-128" or "aes-256". userPassword null: only an owner password (opens without one).
    public static byte[] Encrypt(byte[] pdf, string? userPassword, string ownerPassword, string level = "aes-256")
    {
        using var input = new MemoryStream(pdf);
        using var document = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
        if (userPassword is not null)
        {
            document.SecuritySettings.UserPassword = userPassword;
        }

        document.SecuritySettings.OwnerPassword = ownerPassword;
        switch (level)
        {
            case "rc4-128":
                document.SecurityHandler.SetEncryptionToV2With128Bits();
                break;
            case "aes-128":
                document.SecurityHandler.SetEncryptionToV4UsingAES();
                break;
            case "aes-256":
                document.SecurityHandler.SetEncryptionToV5();
                break;
            default:
                throw new ArgumentException($"Unknown encryption level: {level}", nameof(level));
        }

        using var output = new MemoryStream();
        document.Save(output);
        return output.ToArray();
    }
}
```

- [ ] **Step 3: Write the failing tests in `tests/PdfProof.Tests/EncryptionTests.cs`**

```csharp
namespace PdfProof.Tests;

public sealed class EncryptionTests
{
    private const string Secret = "s3cret-Pa55";

    [Theory]
    [InlineData("rc4-128", "user")]
    [InlineData("rc4-128", "owner")]
    [InlineData("aes-128", "user")]
    [InlineData("aes-128", "owner")]
    [InlineData("aes-256", "user")]
    [InlineData("aes-256", "owner")]
    public void An_encrypted_pdf_loads_with_its_user_or_owner_password(string level, string password)
    {
        var pdf = EncryptedPdf.Encrypt(Plain(), "user", "owner", level);

        var doc = PdfProofDocument.Load(pdf, new PdfProofOptions { Password = password });

        Assert.Equal("Confidential sample", doc.Layout.Pages[0].Text);
        Assert.Contains(doc.Diagnostics.Entries, e => e.ToString() == "Encrypted: The PDF is encrypted; it was opened with the password from the options.");
    }

    [Fact]
    public void The_profile_is_still_detected_when_a_password_is_given()
    {
        var pdf = EncryptedPdf.Encrypt(Plain("<< /Producer (Select.Pdf Html To Pdf for .NET Community Edition v26.4.0) >>"), "user", "owner");

        var doc = PdfProofDocument.Load(pdf, new PdfProofOptions { Password = "user" });

        Assert.Same(PdfProfile.SelectPdf, doc.Diagnostics.Profile);
        Assert.True(doc.Diagnostics.ProfileDetected);
    }

    [Fact]
    public void Without_a_password_the_error_says_how_to_give_one()
    {
        var pdf = EncryptedPdf.Encrypt(Plain(), "user", "owner");

        var ex = Assert.Throws<PdfProofException>(() => PdfProofDocument.Load(pdf));

        Assert.Equal("The PDF is encrypted and needs a password. Pass it in the options: new PdfProofOptions { Password = \"...\" }.", ex.Message);
        Assert.Equal("PdfDocumentEncryptedException", ex.InnerException?.GetType().Name);
    }

    [Fact]
    public void A_wrong_password_gets_its_own_error_without_the_password_in_it()
    {
        var pdf = EncryptedPdf.Encrypt(Plain(), "user", "owner");

        var ex = Assert.Throws<PdfProofException>(() => PdfProofDocument.Load(pdf, new PdfProofOptions { Password = Secret }));

        Assert.Equal("The PDF is encrypted, and the password in the options is neither its user password nor its owner password.", ex.Message);
        Assert.DoesNotContain(Secret, ex.ToString());
    }

    [Fact]
    public void A_pdf_with_only_an_owner_password_opens_without_one()
    {
        var pdf = EncryptedPdf.Encrypt(Plain(), userPassword: null, "owner");

        var doc = PdfProofDocument.Load(pdf);

        Assert.Equal("Confidential sample", doc.Layout.Pages[0].Text);
        Assert.Contains(doc.Diagnostics.Entries, e => e.ToString() == "Encrypted: The PDF is encrypted; it opened without a password.");
    }

    [Fact]
    public void A_password_for_an_unencrypted_pdf_is_ignored()
    {
        var doc = PdfProofDocument.Load(Plain(), new PdfProofOptions { Password = Secret });

        Assert.Equal("Confidential sample", doc.Layout.Pages[0].Text);
        Assert.DoesNotContain(doc.Diagnostics.Entries, e => e.Code == PdfDiagnosticCodes.Encrypted);
    }

    [Fact]
    public void The_password_never_appears_in_diagnostics_logs_or_the_options()
    {
        // Review Focus 1.
        var logger = new ListLogger();
        var options = new PdfProofOptions { Password = Secret, Logger = logger };
        var pdf = EncryptedPdf.Encrypt(Plain(), Secret, "owner");

        var doc = PdfProofDocument.Load(pdf, options);

        Assert.DoesNotContain(doc.Diagnostics.Entries, e => e.Message.Contains(Secret));
        Assert.DoesNotContain(logger.Entries, e => e.Message.Contains(Secret));
        Assert.DoesNotContain(Secret, options.ToString());
        Assert.Contains("Password = ***", options.ToString());
        Assert.Contains("Password = (none)", PdfProofOptions.Default.ToString());
    }

    private static byte[] Plain(string? info = null)
    {
        var pdf = new RawPdf { Info = info };
        pdf.AddPage(RawPdf.Text(50, 750, "Confidential sample"));
        return pdf.Build();
    }
}
```

`ListLogger.Entries` is a list of `(LogLevel Level, string Message)` tuples.

- [ ] **Step 4: Run the tests to see them fail**

Run: `dotnet build tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: build FAILS: `PdfProofOptions` has no `Password`, and `PdfDiagnosticCodes` has no `Encrypted`.

- [ ] **Step 5: Add `Password` and a masking `PrintMembers` to `PdfProofOptions`**

Add `using System.Text;` at the top. Add after `Profile`:
```csharp
    /// <summary>
    /// The user or owner password of an encrypted PDF; <see langword="null"/> (the default) for none. A password for
    /// a PDF that isn't encrypted is ignored. The password is never written to diagnostics, logs or
    /// <see cref="ToString"/>.
    /// </summary>
    public string? Password { get; init; }
```
Add at the end of the record:
```csharp
    // Shapes the record's ToString: the password is masked, so options can be logged safely.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Profile = ").Append(Profile?.Name ?? "(detect)");
        builder.Append(", Password = ").Append(Password is null ? "(none)" : "***");
        builder.Append(", Logger = ").Append(Logger is null ? "(none)" : Logger.GetType().Name);
        return true;
    }
```

- [ ] **Step 6: Add the code and the throw helper**

In `src/PdfProof/PdfDiagnosticCodes.cs`, add:
```csharp
    /// <summary>The PDF is encrypted, and how it was opened.</summary>
    public const string Encrypted = "Encrypted";
```
In `src/Shared/Throw/Throw.PdfProof.cs`, add:
```csharp
    /// <summary>
    /// Throws a <see cref="PdfProofException"/> for an encrypted PDF that couldn't be opened: no password, or one that
    /// is neither the user nor the owner password. The password itself is never part of the message.
    /// </summary>
    /// <param name="parserError">The parser's exception; kept as the inner exception.</param>
    /// <param name="passwordGiven">Whether the options held a password.</param>
    [DoesNotReturn]
    public static void EncryptedPdf(Exception parserError, bool passwordGiven) =>
        throw new PdfProofException(
            passwordGiven
                ? "The PDF is encrypted, and the password in the options is neither its user password nor its owner password."
                : "The PDF is encrypted and needs a password. Pass it in the options: new PdfProofOptions { Password = \"...\" }.",
            parserError);
```

- [ ] **Step 7: Open with the password in `PdfProofDocument.Open`**

Add `using UglyToad.PdfPig.Exceptions;` at the top. Replace the line that opens the document:
```csharp
        using var document = OpenPdf(pdf, options.Password, applyClipping);
```
Add this helper below `Open`:
```csharp
    // PdfPig throws PdfDocumentEncryptedException from Open when the password is missing or wrong (probed with
    // RC4-128, AES-128 and AES-256): that gets its own message instead of "could not be read as a PDF".
    private static PdfDocument OpenPdf(byte[] pdf, string? password, bool applyClipping)
    {
        var parsing = new ParsingOptions { ClipPaths = applyClipping };
        if (password is not null)
        {
            parsing.Password = password;
        }

        try
        {
            return PdfPigCall.Read(() => PdfDocument.Open(pdf, parsing));
        }
        catch (PdfProofException ex) when (ex.InnerException is PdfDocumentEncryptedException encrypted)
        {
            Throw.EncryptedPdf(encrypted, password is not null);
            throw; // unreachable: EncryptedPdf always throws
        }
    }
```
Right after the profile diagnostic, record encryption:
```csharp
        if (PdfPigCall.Read(() => document.IsEncrypted))
        {
            diagnostics.Add(
                PdfDiagnosticCodes.Encrypted,
                null,
                options.Password is null
                    ? "The PDF is encrypted; it opened without a password."
                    : "The PDF is encrypted; it was opened with the password from the options.");
        }
```

- [ ] **Step 8: Record the public API and run the tests**

Run the RS0016 command and rebuild. Expected appended lines:
- `PdfProof.PdfProofOptions.Password.get -> string?` and its `init`;
- `const PdfProof.PdfDiagnosticCodes.Encrypted = "Encrypted" -> string!`.

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: all tests pass on net48 and net10.0.

If an RC4 or AES case fails on net48 only, the cause is the platform's crypto, not PdfProof. Report it instead of skipping the case.

- [ ] **Step 9: Commit**

```bash
git add Directory.Packages.props src tests/PdfProof.Tests
git commit -m "Open encrypted PDFs with a password from the options

A missing or wrong password gets its own message; the password never appears in
messages, diagnostics, logs or the options' ToString. Tests encrypt RawPdf output
in memory with PDFsharp (test-only) using RC4-128, AES-128 and AES-256.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: AcroForm fields in the layout model

**Files:**
- Create: `src/PdfProof/Layout/LayoutFormField.cs`, `src/PdfProof/Layout/LayoutFormFieldKind.cs`, `src/PdfProof/Layout/FormFieldReader.cs`, `tests/PdfProof.Tests/FormFieldTests.cs`
- Modify: `src/PdfProof/Layout/LayoutPage.cs`, `src/PdfProof/Layout/LayoutBuilder.cs`, `src/PdfProof/PdfDiagnosticCodes.cs`, `src/PdfProof/DiagnosticsCollector.cs`, `src/PdfProof/PublicAPI.Unshipped.txt`, `tests/PdfProof.Tests/RawPdf.cs`

**Interfaces:**
- Consumes: `LayoutBuilder.Build`, `Geometry.Round`/`Geometry.Bounds`, `DiagnosticsCollector`, `PdfPigCall.Read`.
- Produces:
  - `public sealed class LayoutFormField` with `Name`, `Kind`, `Value`, `IsChecked`, `Options`, `Tooltip`, `IsReadOnly`, `IsRequired` and `Box`.
  - `public enum LayoutFormFieldKind { Text, Checkbox, RadioButton, ComboBox, ListBox, PushButton, Signature, Unknown }`.
  - `LayoutPage.FormFields : IReadOnlyList<LayoutFormField>`: top to bottom, then left to right, then by name (ordinal).
  - `PdfDiagnosticCodes.FormFields` (info) and `PdfDiagnosticCodes.FormFieldsUnreadable` (warning).
  - Internal `FormFieldReader.Read(PdfDocument, LayoutOptions, DiagnosticsCollector) -> IReadOnlyDictionary<int, IReadOnlyList<LayoutFormField>>`.
  - `RawPdf`: `List<int> FormFields` (root field object numbers, written as the catalog's `/AcroForm`), `int Widget(int page, string entries, double left, double bottom, double right, double top, int? parent = null)`, and `int Appearance()`.

PdfPig facts this task relies on (probed with PdfPig 0.1.16):
- `TryGetForm` returns the field tree. Parent fields are `AcroNonTerminalField` with `Children`.
- `Information.PartialName` is only the partial name, so the full dotted name is built while walking the tree.
- `FieldFlags` are inherited: bit 1 is read-only, bit 2 is required.
- Radio buttons are `AcroRadioButtonField` children of `AcroRadioButtonsField`. `CurrentValue` is the button's appearance state (`/Off` or its on-state) and `IsSelected` says whether it is on.
- A widget without `/P` has `PageNumber == null`.
- `/Fields [99 0 R]` (a missing object) makes `TryGetForm` throw a `NullReferenceException`.
- A 100-level nested chain reads fine.

- [ ] **Step 1: Add form helpers to `tests/PdfProof.Tests/RawPdf.cs`**

Add to the class comment's list: "AcroForm fields (`Widget`, `FormFields`)". Add the members:
```csharp
    /// The root form fields (object numbers), written as the catalog's /AcroForm << /Fields [...] >> when not empty.
    public List<int> FormFields { get; } = [];

    /// Adds a widget annotation to `page` and returns its object number. `entries` are raw field entries, e.g.
    /// "/T (name) /FT /Tx /V (Ada)"; with `parent` the widget is a kid of that field (whose /Kids the caller writes).
    public int Widget(int page, string entries, double left, double bottom, double right, double top, int? parent = null)
    {
        var parentEntry = parent is { } number ? $" /Parent {number} 0 R" : "";
        var widget = Add(FormattableString.Invariant(
            $"<< /Type /Annot /Subtype /Widget /P {page} 0 R /Rect [{left} {bottom} {right} {top}]{parentEntry} {entries} >>"));
        Annotate(page, $"{widget} 0 R");
        return widget;
    }

    /// An empty appearance stream, for /AP << /N << /On n 0 R /Off n 0 R >> >> entries of checkboxes and radio buttons.
    public int Appearance() => AddStream("/Type /XObject /Subtype /Form /BBox [0 0 10 10]", "");
```
In `Build`, write the form into the catalog:
```csharp
        var acroForm = FormFields.Count > 0 ? $" /AcroForm << /Fields [{string.Join(" ", FormFields.Select(f => $"{f} 0 R"))}] >>" : "";
        Set(1, $"<< /Type /Catalog /Pages 2 0 R {CatalogEntries}{acroForm} >>");
```

- [ ] **Step 2: Write the failing tests in `tests/PdfProof.Tests/FormFieldTests.cs`**

```csharp
using PdfProof.Layout;

namespace PdfProof.Tests;

public sealed class FormFieldTests
{
    [Fact]
    public void Text_fields_have_full_names_values_tooltips_flags_and_boxes()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Text(50, 750, "Name:"));
        var person = pdf.Reserve();
        var name = pdf.Widget(page, "/T (name) /FT /Tx /V (Ada) /TU (Full name) /Ff 3", 100, 700, 300, 720, parent: person);
        pdf.Set(person, $"<< /T (person) /Kids [{name} 0 R] >>");
        pdf.FormFields.Add(person);

        var field = Assert.Single(PdfProofDocument.Load(pdf.Build()).Layout.Pages[0].FormFields);

        Assert.Equal("person.name", field.Name);
        Assert.Equal(LayoutFormFieldKind.Text, field.Kind);
        Assert.Equal("Ada", field.Value);
        Assert.Null(field.IsChecked);
        Assert.Empty(field.Options);
        Assert.Equal("Full name", field.Tooltip);
        Assert.True(field.IsReadOnly);
        Assert.True(field.IsRequired);
        Assert.Equal(new LayoutBox(100, 700, 300, 720), field.Box);
    }

    [Fact]
    public void An_empty_text_field_has_no_value()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage("");
        pdf.FormFields.Add(pdf.Widget(page, "/T (city) /FT /Tx", 100, 700, 300, 720));

        var field = Assert.Single(PdfProofDocument.Load(pdf.Build()).Layout.Pages[0].FormFields);

        Assert.Null(field.Value);
        Assert.False(field.IsReadOnly);
        Assert.False(field.IsRequired);
    }

    [Fact]
    public void Checkboxes_and_radio_buttons_have_one_entry_per_widget_with_their_state()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage("");
        var on = pdf.Appearance();
        pdf.FormFields.Add(pdf.Widget(page, $"/T (agree) /FT /Btn /V /Yes /AS /Yes /AP << /N << /Yes {on} 0 R /Off {on} 0 R >> >>", 100, 600, 110, 610));
        var group = pdf.Reserve();
        var a = pdf.Widget(page, $"/AS /Off /AP << /N << /a {on} 0 R /Off {on} 0 R >> >>", 100, 500, 110, 510, parent: group);
        var b = pdf.Widget(page, $"/AS /b /AP << /N << /b {on} 0 R /Off {on} 0 R >> >>", 120, 500, 130, 510, parent: group);
        pdf.Set(group, $"<< /T (choice) /FT /Btn /Ff 49152 /V /b /Kids [{a} 0 R {b} 0 R] >>");
        pdf.FormFields.Add(group);

        var fields = PdfProofDocument.Load(pdf.Build()).Layout.Pages[0].FormFields;

        Assert.Equal(
            [
                ("agree", LayoutFormFieldKind.Checkbox, (string?)"Yes", (bool?)true),
                ("choice", LayoutFormFieldKind.RadioButton, "Off", false),
                ("choice", LayoutFormFieldKind.RadioButton, "b", true),
            ],
            fields.Select(f => (f.Name, f.Kind, f.Value, f.IsChecked)));
    }

    [Fact]
    public void Choice_fields_list_their_options_and_the_selection()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage("");
        pdf.FormFields.Add(pdf.Widget(page, "/T (size) /FT /Ch /Ff 131072 /Opt [(Small) (Medium) (Large)] /V (Medium)", 100, 700, 200, 720));
        pdf.FormFields.Add(pdf.Widget(page, "/T (extras) /FT /Ch /Ff 2097152 /Opt [(Wrap) (Card) (Ribbon)] /V [(Wrap) (Ribbon)]", 100, 600, 200, 680));

        var fields = PdfProofDocument.Load(pdf.Build()).Layout.Pages[0].FormFields;

        var size = fields.Single(f => f.Name == "size");
        Assert.Equal((LayoutFormFieldKind.ComboBox, (string?)"Medium"), (size.Kind, size.Value));
        Assert.Equal(["Small", "Medium", "Large"], size.Options);
        var extras = fields.Single(f => f.Name == "extras");
        Assert.Equal((LayoutFormFieldKind.ListBox, (string?)"Wrap, Ribbon"), (extras.Kind, extras.Value));
    }

    [Fact]
    public void Push_buttons_and_signatures_have_no_value()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage("");
        pdf.FormFields.Add(pdf.Widget(page, "/T (send) /FT /Btn /Ff 65536", 100, 700, 200, 720));
        pdf.FormFields.Add(pdf.Widget(page, "/T (signature) /FT /Sig", 100, 600, 300, 650));

        var fields = PdfProofDocument.Load(pdf.Build()).Layout.Pages[0].FormFields;

        Assert.Equal(
            [("send", LayoutFormFieldKind.PushButton, (string?)null), ("signature", LayoutFormFieldKind.Signature, null)],
            fields.Select(f => (f.Name, f.Kind, f.Value)));
    }

    [Fact]
    public void Widgets_go_to_their_own_page_in_reading_order()
    {
        var pdf = new RawPdf();
        var first = pdf.AddPage("");
        var second = pdf.AddPage("");
        pdf.FormFields.Add(pdf.Widget(second, "/T (later) /FT /Tx", 100, 700, 200, 720));
        pdf.FormFields.Add(pdf.Widget(first, "/T (right) /FT /Tx", 300, 700, 400, 720));
        pdf.FormFields.Add(pdf.Widget(first, "/T (left) /FT /Tx", 100, 700, 200, 720));
        pdf.FormFields.Add(pdf.Widget(first, "/T (top) /FT /Tx", 300, 750, 400, 770));

        var doc = PdfProofDocument.Load(pdf.Build());

        Assert.Equal(["top", "left", "right"], doc.Layout.Pages[0].FormFields.Select(f => f.Name));
        Assert.Equal(["later"], doc.Layout.Pages[1].FormFields.Select(f => f.Name));
        Assert.Contains(doc.Diagnostics.Entries, e => e.ToString() == "FormFields: 4 form field widgets");
    }

    [Fact]
    public void Widgets_are_not_reported_as_other_annotations()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage("");
        pdf.FormFields.Add(pdf.Widget(page, "/T (name) /FT /Tx", 100, 700, 200, 720));

        Assert.Empty(PdfProofDocument.Load(pdf.Build()).Layout.Pages[0].Annotations);
    }

    [Fact]
    public void A_widget_without_a_page_is_left_out_and_counted()
    {
        // Review Focus 3: PdfPig gives a widget without /P no page number.
        var pdf = new RawPdf();
        var page = pdf.AddPage("");
        pdf.FormFields.Add(pdf.Widget(page, "/T (kept) /FT /Tx", 100, 700, 200, 720));
        pdf.FormFields.Add(pdf.Add("<< /Type /Annot /Subtype /Widget /T (lost) /FT /Tx /V (x) /Rect [10 10 50 30] >>"));

        var doc = PdfProofDocument.Load(pdf.Build());

        Assert.Equal(["kept"], doc.Layout.Pages[0].FormFields.Select(f => f.Name));
        Assert.Contains(doc.Diagnostics.Entries, e => e.ToString() == "FormFields: 1 form field widgets; 1 without a page or box are left out");
    }

    [Fact]
    public void A_damaged_form_is_left_out_and_the_document_still_loads()
    {
        // Review Focus 2: a field reference to a missing object makes PdfPig's form reader throw.
        var pdf = new RawPdf();
        pdf.AddPage(RawPdf.Text(50, 750, "Hello"));
        pdf.FormFields.Add(99);

        var doc = PdfProofDocument.Load(pdf.Build());

        Assert.Equal("Hello", doc.Layout.Pages[0].Text);
        Assert.Empty(doc.Layout.Pages[0].FormFields);
        var warning = Assert.Single(doc.Diagnostics.Entries, e => e.Code == PdfDiagnosticCodes.FormFieldsUnreadable);
        Assert.StartsWith("The form fields could not be read and are left out: ", warning.Message);
    }

    [Fact]
    public void Fields_nested_deeper_than_the_limit_are_left_out_with_a_warning()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage("");
        const int depth = 300;
        var parents = Enumerable.Range(0, depth).Select(_ => pdf.Reserve()).ToList();
        var leaf = pdf.Widget(page, "/T (leaf) /FT /Tx /V (deep)", 0, 0, 10, 10, parent: parents[depth - 1]);
        for (var i = 0; i < depth; i++)
        {
            var kid = i + 1 < depth ? parents[i + 1] : leaf;
            var parentEntry = i > 0 ? $" /Parent {parents[i - 1]} 0 R" : "";
            pdf.Set(parents[i], $"<< /T (n{i}) /Kids [{kid} 0 R]{parentEntry} >>");
        }

        pdf.FormFields.Add(parents[0]);

        var doc = PdfProofDocument.Load(pdf.Build());

        // Either PdfPig rejects the depth or the reader's limit (256) does; both leave the field out with a warning.
        Assert.Empty(doc.Layout.Pages[0].FormFields);
        Assert.Contains(doc.Diagnostics.Entries, e => e.Code == PdfDiagnosticCodes.FormFieldsUnreadable);
    }

    [Fact]
    public void A_pdf_without_a_form_has_no_form_fields_and_no_form_diagnostic()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 750, "Hello")));

        Assert.Empty(doc.Layout.Pages[0].FormFields);
        Assert.DoesNotContain(doc.Diagnostics.Entries, e => e.Code is PdfDiagnosticCodes.FormFields or PdfDiagnosticCodes.FormFieldsUnreadable);
    }
}
```

- [ ] **Step 3: Run the tests to see them fail**

Run: `dotnet build tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: build FAILS: `LayoutFormField`, `LayoutFormFieldKind`, `LayoutPage.FormFields` and the two diagnostic codes don't exist.

- [ ] **Step 4: Write `src/PdfProof/Layout/LayoutFormFieldKind.cs` and `LayoutFormField.cs`**

```csharp
namespace PdfProof.Layout;

/// <summary>What kind of AcroForm field a <see cref="LayoutFormField"/> belongs to.</summary>
public enum LayoutFormFieldKind
{
    /// <summary>A text field.</summary>
    Text,

    /// <summary>A checkbox.</summary>
    Checkbox,

    /// <summary>One button of a radio group.</summary>
    RadioButton,

    /// <summary>A combo box (drop-down list).</summary>
    ComboBox,

    /// <summary>A list box.</summary>
    ListBox,

    /// <summary>A push button.</summary>
    PushButton,

    /// <summary>A signature field.</summary>
    Signature,

    /// <summary>A field whose type the PDF doesn't state or PdfProof doesn't know.</summary>
    Unknown,
}
```

```csharp
namespace PdfProof.Layout;

/// <summary>
/// One widget of an AcroForm field: where a field appears on a page. A field shown in several places, such as the
/// buttons of a radio group, has one entry per widget, all with the same <see cref="Name"/>.
/// </summary>
public sealed class LayoutFormField
{
    internal LayoutFormField(string name, LayoutFormFieldKind kind, string? value, bool? isChecked, IReadOnlyList<string> options, string? tooltip, bool isReadOnly, bool isRequired, LayoutBox box)
    {
        Name = name;
        Kind = kind;
        Value = value;
        IsChecked = isChecked;
        Options = ReadOnly.List(options);
        Tooltip = tooltip;
        IsReadOnly = isReadOnly;
        IsRequired = isRequired;
        Box = box;
    }

    /// <summary>The field's full name: the partial names from the root, joined by dots (e.g. <c>person.name</c>).</summary>
    public string Name { get; }

    /// <summary>What kind of field this is.</summary>
    public LayoutFormFieldKind Kind { get; }

    /// <summary>
    /// The value. Text fields: their text. Checkboxes and radio buttons: the widget's state name (<c>Off</c>, or the
    /// name of its on-state, e.g. <c>Yes</c>). Combo and list boxes: the selected options' names joined by
    /// <c>", "</c>. <see langword="null"/> when there is none, and for push buttons and signatures.
    /// </summary>
    public string? Value { get; }

    /// <summary>Checkboxes and radio buttons: whether this widget is on. <see langword="null"/> for other kinds.</summary>
    public bool? IsChecked { get; }

    /// <summary>Combo and list boxes: the option names, in order. Empty for other kinds.</summary>
    public IReadOnlyList<string> Options { get; }

    /// <summary>The field's tooltip (<c>/TU</c>), often the accessible name; <see langword="null"/> when absent.</summary>
    public string? Tooltip { get; }

    /// <summary>The read-only flag is set.</summary>
    public bool IsReadOnly { get; }

    /// <summary>The required flag is set.</summary>
    public bool IsRequired { get; }

    /// <summary>The widget rectangle.</summary>
    public LayoutBox Box { get; }
}
```

- [ ] **Step 5: Write `src/PdfProof/Layout/FormFieldReader.cs`**

```csharp
using UglyToad.PdfPig;
using UglyToad.PdfPig.AcroForms.Fields;
using UglyToad.PdfPig.Core;

namespace PdfProof.Layout;

// Reads the AcroForm field tree into one LayoutFormField per widget, grouped by page. PdfPig parses the tree inside
// PdfPigCall; mapping it happens outside. Form fields are secondary to page content, like bookmarks and the structure
// tree: a form the parser rejects is reported and left out, and the document still loads.
internal static class FormFieldReader
{
    // The same limit as the structure tree: deeper fields are left out with a warning.
    private const int MaxDepth = 256;

    private static readonly IReadOnlyList<string> NoOptions = [];

    public static IReadOnlyDictionary<int, IReadOnlyList<LayoutFormField>> Read(PdfDocument document, LayoutOptions options, DiagnosticsCollector diagnostics)
    {
        IReadOnlyList<AcroFieldBase> roots;
        try
        {
            roots = PdfPigCall.Read(() => document.TryGetForm(out var form) ? form.Fields : (IReadOnlyList<AcroFieldBase>)[]);
        }
        catch (PdfProofException ex)
        {
            diagnostics.Add(
                PdfDiagnosticCodes.FormFieldsUnreadable,
                null,
                "The form fields could not be read and are left out: " + (ex.InnerException ?? ex).Message);
            return new Dictionary<int, IReadOnlyList<LayoutFormField>>();
        }

        var widgets = new List<(int Page, LayoutFormField Field)>();
        var withoutPage = 0;
        var truncated = false;

        void Walk(IEnumerable<AcroFieldBase> fields, string? parentName, int depth)
        {
            if (depth > MaxDepth)
            {
                truncated = true;
                return;
            }

            foreach (var field in fields)
            {
                var partial = field.Information.PartialName;
                var name = string.IsNullOrEmpty(partial) ? parentName ?? "" : parentName is null ? partial : parentName + "." + partial;
                if (field is AcroNonTerminalField parent)
                {
                    Walk(parent.Children, name, depth + 1);
                }
                else if (field.PageNumber is { } page && field.Bounds is { } bounds)
                {
                    widgets.Add((page, Map(field, name, bounds, options)));
                }
                else
                {
                    withoutPage++;
                }
            }
        }

        Walk(roots, null, 0);

        if (widgets.Count + withoutPage > 0)
        {
            diagnostics.Add(
                PdfDiagnosticCodes.FormFields,
                null,
                $"{widgets.Count} form field widgets" + (withoutPage > 0 ? $"; {withoutPage} without a page or box are left out" : ""));
        }

        if (truncated)
        {
            diagnostics.Add(PdfDiagnosticCodes.FormFieldsUnreadable, null, $"Form fields nested deeper than {MaxDepth} levels are left out.");
        }

        return widgets
            .GroupBy(widget => widget.Page)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<LayoutFormField>)group
                    .Select(widget => widget.Field)
                    .OrderByDescending(field => field.Box.Top).ThenBy(field => field.Box.Left).ThenBy(field => field.Name, StringComparer.Ordinal)
                    .ToList());
    }

    private static LayoutFormField Map(AcroFieldBase field, string name, PdfRectangle bounds, LayoutOptions options)
    {
        LayoutFormFieldKind kind;
        string? value = null;
        bool? isChecked = null;
        var choices = NoOptions;
        switch (field)
        {
            case AcroTextField text:
                kind = LayoutFormFieldKind.Text;
                value = string.IsNullOrEmpty(text.Value) ? null : text.Value;
                break;
            case AcroCheckboxField checkbox:
                kind = LayoutFormFieldKind.Checkbox;
                value = checkbox.CurrentValue?.Data;
                isChecked = checkbox.IsChecked;
                break;
            case AcroRadioButtonField radio:
                kind = LayoutFormFieldKind.RadioButton;
                value = radio.CurrentValue?.Data;
                isChecked = radio.IsSelected;
                break;
            case AcroComboBoxField combo:
                kind = LayoutFormFieldKind.ComboBox;
                (value, choices) = Choice(combo.Options);
                break;
            case AcroListBoxField list:
                kind = LayoutFormFieldKind.ListBox;
                (value, choices) = Choice(list.Options);
                break;
            case AcroPushButtonField:
                kind = LayoutFormFieldKind.PushButton;
                break;
            case AcroSignatureField:
                kind = LayoutFormFieldKind.Signature;
                break;
            default:
                kind = LayoutFormFieldKind.Unknown;
                break;
        }

        var tooltip = field.Information.AlternateName;
        return new LayoutFormField(
            name,
            kind,
            value,
            isChecked,
            choices,
            string.IsNullOrEmpty(tooltip) ? null : tooltip,
            (field.FieldFlags & 1) != 0,
            (field.FieldFlags & 2) != 0,
            Geometry.Round(Geometry.Bounds(bounds), options.Decimals));
    }

    private static (string? Value, IReadOnlyList<string> Options) Choice(IReadOnlyList<AcroChoiceOption> options)
    {
        var selected = options.Where(option => option.IsSelected).Select(option => option.Name).ToList();
        return (selected.Count == 0 ? null : string.Join(", ", selected), options.Select(option => option.Name).ToList());
    }
}
```

- [ ] **Step 6: Wire the reader into the layout**

In `src/PdfProof/PdfDiagnosticCodes.cs`, add:
```csharp
    /// <summary>How many form field widgets were read, and how many were left out for having no page.</summary>
    public const string FormFields = "FormFields";

    /// <summary>The form fields, or some of them, could not be read and are left out; pages are unaffected. Logged as a warning.</summary>
    public const string FormFieldsUnreadable = "FormFieldsUnreadable";
```
In `DiagnosticsCollector.IsWarning`, add `or PdfDiagnosticCodes.FormFieldsUnreadable`.

In `LayoutPage.cs`:
- add a constructor parameter `IReadOnlyList<LayoutFormField> formFields` after `annotations`;
- set `FormFields = ReadOnly.List(formFields);`;
- add the property:
```csharp
    /// <summary>The page's form field widgets, top to bottom, then left to right, then by name.</summary>
    public IReadOnlyList<LayoutFormField> FormFields { get; }
```

In `LayoutBuilder.Build`:
- read the fields once, before the page loop: `var formFields = FormFieldReader.Read(document, options, diagnostics);`;
- pass `formFields.TryGetValue(pageNumber, out var fields) ? fields : []` to `BuildPage` as a new last parameter `IReadOnlyList<LayoutFormField> formFields`;
- `BuildPage` passes it on as the new last argument of `new LayoutPage(…)`.

- [ ] **Step 7: Record the public API and run the tests**

Run the RS0016 command and rebuild. Expected appended lines:
- `LayoutFormField` and its nine getters;
- `LayoutFormFieldKind` and its eight members;
- `LayoutPage.FormFields.get`;
- the two `PdfDiagnosticCodes` constants.

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: all tests pass on net48 and net10.0.

If `Checkboxes_and_radio_buttons_have_one_entry_per_widget_with_their_state` fails because PdfPig reports a different state for the unselected radio button (e.g. the group's `/V` instead of `/Off`), change the test to what PdfPig reports and update the `Value` XML doc to match. The model shows what PdfPig reads, and the doc says what that is.

- [ ] **Step 8: Commit**

```bash
git add src tests/PdfProof.Tests
git commit -m "Read AcroForm fields into the layout model, one entry per widget

LayoutPage.FormFields lists text, checkbox, radio, choice, push button and
signature widgets with full names, values, options, tooltips, flags and boxes.
A damaged form, widgets without a page and over-deep nesting are reported and
left out; the document still loads. RawPdf gains Widget and FormFields.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 4: The fixture generator: runner, arguments and manifest

**Files:**
- Create: `tools/PdfProof.FixtureGenerator/PdfProof.FixtureGenerator.csproj`, `Program.cs`, `GeneratorArguments.cs`, `IProducer.cs`, `CorpusDocument.cs`, `Generator.cs`, `Manifest.cs`, `Producers.cs` (all in `tools/PdfProof.FixtureGenerator/`)
- Create: `tests/PdfProof.FixtureGenerator.Tests/PdfProof.FixtureGenerator.Tests.csproj`, `GeneratorArgumentsTests.cs`, `GeneratorTests.cs`, `TempFolder.cs`
- Modify: `PdfProof.slnx`

**Interfaces:**
- Consumes: nothing from earlier tasks. The generator doesn't reference PdfProof, because it makes inputs, not readings.
- Produces (all `internal`, namespace `PdfProof.FixtureGenerator`):
  - `interface IProducer`:
    - `string Name`, the fixture file name;
    - `bool BestEffort`;
    - `Task<string?> CheckAsync()`: null when the producer can run, otherwise the reason;
    - `string Version`: valid after a successful check;
    - `Task<byte[]> RenderAsync(CorpusDocument document)`.
  - `sealed record CorpusDocument(string Name, string Directory)` with `HtmlPath`, `TypstPath` and `static IReadOnlyList<CorpusDocument> Find(string source)`.
  - `sealed record GeneratorArguments(string Source, string Output, IReadOnlyList<string>? Producers, string Typst)` with `static (GeneratorArguments? Arguments, string? Error) Parse(IReadOnlyList<string> args)` and `const string Usage`.
  - `sealed class Generator(IReadOnlyList<IProducer> producers, TextWriter log)` with `Task<int> RunAsync(IReadOnlyList<CorpusDocument> documents, string output)`.
  - `sealed record ManifestEntry(string Producer, string? Version, string Status, string? Reason)` and `static class Manifest` with `Write(string path, IReadOnlyList<string> documents, IReadOnlyList<ManifestEntry> entries)`.
  - `static class Producers` with `All(string typst)` and `Select(all, names, out unknown)`.
- **Command line:** `dotnet run --project tools/PdfProof.FixtureGenerator -- [--source corpus/generated] [--output artifacts/corpus] [--producers a,b] [--typst <path>]`.
  - Writes `<output>/<document>/<producer>.pdf` and `<output>/manifest.json`.
  - Exit codes: 0 means done (unavailable producers are warnings); 1 means a producer that isn't best effort failed; 2 means bad arguments or no documents.

- [ ] **Step 1: Create the projects**

`tools/PdfProof.FixtureGenerator/PdfProof.FixtureGenerator.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!-- Renders the generated corpus with every producer available here (spec §10.2). A build tool: not packed, and
       net10.0 only, because its producers (Playwright, QuestPDF, SelectPdf) need modern .NET. -->
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="PdfProof.FixtureGenerator.Tests" />
  </ItemGroup>

</Project>
```

`tests/PdfProof.FixtureGenerator.Tests/PdfProof.FixtureGenerator.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <!-- The generator is net10.0 only, so are its tests. -->
    <TargetFrameworks>net10.0</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3" />
    <ProjectReference Include="../../tools/PdfProof.FixtureGenerator/PdfProof.FixtureGenerator.csproj" />
  </ItemGroup>

</Project>
```

In `PdfProof.slnx`, add before `</Solution>`:
```xml
  <Folder Name="/tools/">
    <Project Path="tools/PdfProof.FixtureGenerator/PdfProof.FixtureGenerator.csproj" />
  </Folder>
```
and add `<Project Path="tests/PdfProof.FixtureGenerator.Tests/PdfProof.FixtureGenerator.Tests.csproj" />` to the `/tests/` folder.

`tests/PdfProof.FixtureGenerator.Tests/TempFolder.cs`:
```csharp
namespace PdfProof.FixtureGenerator.Tests;

// A fresh folder under the temp path, deleted when the test ends.
internal sealed class TempFolder : IDisposable
{
    public string Path { get; } = Directory.CreateDirectory(
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"pdfproof-generator-{Guid.NewGuid():N}")).FullName;

    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
```

- [ ] **Step 2: Write the failing tests**

`tests/PdfProof.FixtureGenerator.Tests/GeneratorArgumentsTests.cs`:
```csharp
namespace PdfProof.FixtureGenerator.Tests;

public sealed class GeneratorArgumentsTests
{
    [Fact]
    public void Without_arguments_the_repository_defaults_are_used()
    {
        var (arguments, error) = GeneratorArguments.Parse([]);

        Assert.Null(error);
        Assert.Equal(Path.Combine("corpus", "generated"), arguments!.Source);
        Assert.Equal(Path.Combine("artifacts", "corpus"), arguments.Output);
        Assert.Null(arguments.Producers);
        Assert.Equal("typst", arguments.Typst);
    }

    [Fact]
    public void Every_option_is_read()
    {
        var (arguments, _) = GeneratorArguments.Parse(["--source", "src", "--output", "out", "--producers", "typst, selectpdf", "--typst", "tools/typst"]);

        Assert.Equal(("src", "out", "tools/typst"), (arguments!.Source, arguments.Output, arguments.Typst));
        Assert.Equal(["typst", "selectpdf"], arguments.Producers);
    }

    [Theory]
    [InlineData(new[] { "--unknown" }, "Unknown argument: --unknown")]
    [InlineData(new[] { "--output" }, "--output needs a value")]
    [InlineData(new[] { "--output", "--typst", "x" }, "--output needs a value")]
    public void Bad_arguments_are_reported(string[] args, string expected)
    {
        var (arguments, error) = GeneratorArguments.Parse(args);

        Assert.Null(arguments);
        Assert.Equal(expected, error);
    }
}
```

`tests/PdfProof.FixtureGenerator.Tests/GeneratorTests.cs`:
```csharp
using System.Text;

namespace PdfProof.FixtureGenerator.Tests;

public sealed class GeneratorTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly StringWriter _log = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task Each_fixture_and_a_manifest_are_written()
    {
        var exit = await Run(new FakeProducer("alpha"), new FakeProducer("beta", unavailable: "not here"));

        Assert.Equal(0, exit);
        Assert.Equal("%PDF invoice alpha", File.ReadAllText(_temp.Combine("out", "invoice", "alpha.pdf")));
        Assert.False(File.Exists(_temp.Combine("out", "invoice", "beta.pdf")));
        Assert.Contains("warning: beta is unavailable: not here", _log.ToString());
        Assert.Equal(
            """
            {
              "documents": [
                "invoice"
              ],
              "producers": [
                {
                  "name": "alpha",
                  "version": "1.0",
                  "status": "generated"
                },
                {
                  "name": "beta",
                  "status": "unavailable",
                  "reason": "not here"
                }
              ]
            }

            """.Replace("\r\n", "\n"),
            File.ReadAllText(_temp.Combine("out", "manifest.json")));
    }

    [Fact]
    public async Task A_failing_producer_fails_the_run_and_is_recorded()
    {
        var exit = await Run(new FakeProducer("alpha", fails: true));

        Assert.Equal(1, exit);
        Assert.Contains("\"status\": \"failed\"", File.ReadAllText(_temp.Combine("out", "manifest.json")));
        Assert.Contains("\"reason\": \"invoice: boom\"", File.ReadAllText(_temp.Combine("out", "manifest.json")));
        Assert.Contains("error: alpha failed on invoice: boom", _log.ToString());
    }

    [Fact]
    public async Task A_failing_best_effort_producer_is_only_a_warning()
    {
        var exit = await Run(new FakeProducer("alpha", fails: true, bestEffort: true));

        Assert.Equal(0, exit);
        Assert.Contains("warning: alpha failed on invoice: boom", _log.ToString());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task A_previous_fixture_never_stands_in_for_a_producer_that_is_unavailable_or_fails(bool unavailable, bool fails)
    {
        // Review Focus 4.
        Directory.CreateDirectory(_temp.Combine("out", "invoice"));
        File.WriteAllText(_temp.Combine("out", "invoice", "alpha.pdf"), "stale");

        await Run(new FakeProducer("alpha", unavailable: unavailable ? "gone" : null, fails: fails));

        Assert.False(File.Exists(_temp.Combine("out", "invoice", "alpha.pdf")));
    }

    [Fact]
    public void Producers_are_selected_by_name_in_name_order_and_unknown_names_are_reported()
    {
        IReadOnlyList<IProducer> all = [new FakeProducer("beta"), new FakeProducer("alpha"), new FakeProducer("gamma")];

        var selected = Producers.Select(all, ["gamma", "alpha", "delta"], out var unknown);

        Assert.Equal(["alpha", "gamma"], selected.Select(p => p.Name));
        Assert.Equal(["delta"], unknown);
        Assert.Equal(["alpha", "beta", "gamma"], Producers.Select(all, null, out _).Select(p => p.Name));
    }

    [Fact]
    public void Documents_are_the_folders_with_an_oracle_in_name_order()
    {
        foreach (var name in new[] { "report", "invoice", "draft" })
        {
            Directory.CreateDirectory(_temp.Combine("src", name));
        }

        File.WriteAllText(_temp.Combine("src", "report", "expected.semantics.json"), "{}");
        File.WriteAllText(_temp.Combine("src", "invoice", "expected.semantics.json"), "{}");

        var documents = CorpusDocument.Find(_temp.Combine("src"));

        Assert.Equal(["invoice", "report"], documents.Select(d => d.Name));
        Assert.Equal(_temp.Combine("src", "invoice", "invoice.html"), documents[0].HtmlPath);
        Assert.Equal(_temp.Combine("src", "invoice", "invoice.typ"), documents[0].TypstPath);
    }

    private Task<int> Run(params IProducer[] producers) =>
        new Generator(producers, _log).RunAsync([new CorpusDocument("invoice", _temp.Combine("src", "invoice"))], _temp.Combine("out"));

    private sealed class FakeProducer(string name, string? unavailable = null, bool fails = false, bool bestEffort = false) : IProducer
    {
        public string Name => name;

        public bool BestEffort => bestEffort;

        public string Version => "1.0";

        public Task<string?> CheckAsync() => Task.FromResult(unavailable);

        public Task<byte[]> RenderAsync(CorpusDocument document) =>
            fails ? throw new InvalidOperationException("boom") : Task.FromResult(Encoding.ASCII.GetBytes($"%PDF {document.Name} {name}"));
    }
}
```

- [ ] **Step 3: Run the tests to see them fail**

Run: `dotnet build tests/PdfProof.FixtureGenerator.Tests/PdfProof.FixtureGenerator.Tests.csproj`
Expected: build FAILS: `GeneratorArguments`, `Generator`, `IProducer`, `CorpusDocument` and `Producers` don't exist.

- [ ] **Step 4: Write the generator**

`tools/PdfProof.FixtureGenerator/IProducer.cs`:
```csharp
namespace PdfProof.FixtureGenerator;

// One way of turning a corpus document into a PDF. Name is the fixture's file name: <document>/<Name>.pdf.
internal interface IProducer
{
    string Name { get; }

    // Best-effort producers (LibreOffice) never fail the run (spec §10.2).
    bool BestEffort { get; }

    // The producer's version for the manifest; valid after CheckAsync returned null.
    string Version { get; }

    // Null when the producer can run on this machine; otherwise why not. Called once, before rendering.
    Task<string?> CheckAsync();

    Task<byte[]> RenderAsync(CorpusDocument document);
}
```

`tools/PdfProof.FixtureGenerator/CorpusDocument.cs`:
```csharp
namespace PdfProof.FixtureGenerator;

// A logical document of the generated corpus: corpus/generated/<Name>/ holds its HTML and Typst sources and its oracle
// (expected.semantics.json); its C# versions live in Documents/.
internal sealed record CorpusDocument(string Name, string Directory)
{
    public string HtmlPath => Path.Combine(Directory, Name + ".html");

    public string TypstPath => Path.Combine(Directory, Name + ".typ");

    // The folders under `source` that hold an oracle, in name order (ordinal).
    public static IReadOnlyList<CorpusDocument> Find(string source) =>
        System.IO.Directory.GetDirectories(source)
            .Where(folder => File.Exists(Path.Combine(folder, "expected.semantics.json")))
            .Select(folder => new CorpusDocument(Path.GetFileName(folder), Path.GetFullPath(folder)))
            .OrderBy(document => document.Name, StringComparer.Ordinal)
            .ToList();
}
```

`tools/PdfProof.FixtureGenerator/GeneratorArguments.cs`:
```csharp
namespace PdfProof.FixtureGenerator;

// The command line. Paths are relative to the working directory; the build runs from the repository root.
internal sealed record GeneratorArguments(string Source, string Output, IReadOnlyList<string>? Producers, string Typst)
{
    public const string Usage =
        "Usage: PdfProof.FixtureGenerator [--source <dir>] [--output <dir>] [--producers <name,name,...>] [--typst <path>]";

    public static (GeneratorArguments? Arguments, string? Error) Parse(IReadOnlyList<string> args)
    {
        var source = Path.Combine("corpus", "generated");
        var output = Path.Combine("artifacts", "corpus");
        var typst = "typst";
        IReadOnlyList<string>? producers = null;
        for (var i = 0; i < args.Count; i++)
        {
            var name = args[i];
            if (name is not ("--source" or "--output" or "--producers" or "--typst"))
            {
                return (null, $"Unknown argument: {name}");
            }

            if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                return (null, $"{name} needs a value");
            }

            var value = args[++i];
            switch (name)
            {
                case "--source":
                    source = value;
                    break;
                case "--output":
                    output = value;
                    break;
                case "--producers":
                    producers = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    break;
                default:
                    typst = value;
                    break;
            }
        }

        return (new GeneratorArguments(source, output, producers, typst), null);
    }
}
```

`tools/PdfProof.FixtureGenerator/Manifest.cs`:
```csharp
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
```

`tools/PdfProof.FixtureGenerator/Generator.cs`:
```csharp
namespace PdfProof.FixtureGenerator;

// Renders every corpus document with every producer into <output>/<document>/<producer>.pdf, writes manifest.json,
// and decides the exit code: 1 when a producer that isn't best effort failed, otherwise 0. An unavailable producer is
// a warning: each machine renders what it can, and CI requires the fixtures it needs (PDFPROOF_REQUIRE_FIXTURES).
internal sealed class Generator(IReadOnlyList<IProducer> producers, TextWriter log)
{
    public async Task<int> RunAsync(IReadOnlyList<CorpusDocument> documents, string output)
    {
        Directory.CreateDirectory(output);
        var entries = new List<ManifestEntry>();
        var failed = false;
        foreach (var producer in producers)
        {
            // A previous run's PDF must never stand in for this run's result.
            foreach (var document in documents)
            {
                var stale = FixturePath(output, document, producer);
                if (File.Exists(stale))
                {
                    File.Delete(stale);
                }
            }

            var unavailable = await producer.CheckAsync();
            if (unavailable is not null)
            {
                log.WriteLine($"warning: {producer.Name} is unavailable: {unavailable}");
                entries.Add(new ManifestEntry(producer.Name, null, "unavailable", unavailable));
                continue;
            }

            string? error = null;
            foreach (var document in documents)
            {
                try
                {
                    var pdf = await producer.RenderAsync(document);
                    var path = FixturePath(output, document, producer);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await File.WriteAllBytesAsync(path, pdf);
                    log.WriteLine($"{document.Name}/{producer.Name}.pdf: {pdf.Length} bytes");
                }
                catch (Exception ex)
                {
                    error ??= $"{document.Name}: {ex.Message}";
                    log.WriteLine($"{(producer.BestEffort ? "warning" : "error")}: {producer.Name} failed on {document.Name}: {ex.Message}");
                }
            }

            entries.Add(new ManifestEntry(producer.Name, producer.Version, error is null ? "generated" : "failed", error));
            failed |= error is not null && !producer.BestEffort;
        }

        Manifest.Write(Path.Combine(output, "manifest.json"), documents.Select(document => document.Name).ToList(), entries);
        return failed ? 1 : 0;
    }

    private static string FixturePath(string output, CorpusDocument document, IProducer producer) =>
        Path.Combine(output, document.Name, producer.Name + ".pdf");
}
```

`tools/PdfProof.FixtureGenerator/Producers.cs`:
```csharp
namespace PdfProof.FixtureGenerator;

internal static class Producers
{
    // Every producer. Tasks 5 and 6 add them; the corpus tests list the same names (CorpusProducer.All).
    public static IReadOnlyList<IProducer> All(string typst) => [];

    // The producers named (all when `names` is null), in name order; names that match no producer go to `unknown`.
    public static IReadOnlyList<IProducer> Select(IReadOnlyList<IProducer> all, IReadOnlyList<string>? names, out IReadOnlyList<string> unknown)
    {
        unknown = names is null ? [] : names.Where(name => all.All(producer => producer.Name != name)).ToList();
        return all
            .Where(producer => names is null || names.Contains(producer.Name))
            .OrderBy(producer => producer.Name, StringComparer.Ordinal)
            .ToList();
    }
}
```

`tools/PdfProof.FixtureGenerator/Program.cs`:
```csharp
using PdfProof.FixtureGenerator;

// Renders the generated corpus (corpus/generated/<document>/) with every producer available on this machine into
// artifacts/corpus/<document>/<producer>.pdf, plus manifest.json. Run by the GenerateFixtures build target.
var (arguments, error) = GeneratorArguments.Parse(args);
if (arguments is null)
{
    Console.Error.WriteLine(error);
    Console.Error.WriteLine(GeneratorArguments.Usage);
    return 2;
}

var all = Producers.All(arguments.Typst);
var selected = Producers.Select(all, arguments.Producers, out var unknown);
if (unknown.Count > 0)
{
    Console.Error.WriteLine($"Unknown producers: {string.Join(", ", unknown)}. Known: {string.Join(", ", all.Select(p => p.Name))}.");
    return 2;
}

var documents = Directory.Exists(arguments.Source) ? CorpusDocument.Find(arguments.Source) : [];
if (documents.Count == 0)
{
    Console.Error.WriteLine($"No corpus documents (folders with expected.semantics.json) in {arguments.Source}.");
    return 2;
}

return await new Generator(selected, Console.Out).RunAsync(documents, arguments.Output);
```

- [ ] **Step 5: Run the tests**

Run: `dotnet test --project tests/PdfProof.FixtureGenerator.Tests/PdfProof.FixtureGenerator.Tests.csproj`
Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
git add tools tests/PdfProof.FixtureGenerator.Tests PdfProof.slnx
git commit -m "Add the fixture generator's runner, arguments and manifest

tools/PdfProof.FixtureGenerator renders each corpus document with each producer
into artifacts/corpus and writes a deterministic manifest of versions and
statuses. Unavailable producers are warnings, failures fail the run unless best
effort, and a previous run's PDF never stands in for a missing result.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: The invoice document and the HTML and Typst producers

**Files:**
- Create: `corpus/generated/invoice/invoice.html`, `invoice.typ`, `expected.semantics.json`, `deviations.json`
- Create: `tools/PdfProof.FixtureGenerator/ExternalProcess.cs`, `Versions.cs`, and `Producers/ChromiumProducer.cs`, `Producers/SelectPdfProducer.cs`, `Producers/TypstProducer.cs`, `Producers/LibreOfficeProducer.cs`
- Create: `tests/PdfProof.FixtureGenerator.Tests/ProducerTests.cs`
- Modify: `tools/PdfProof.FixtureGenerator/Producers.cs`, `tools/PdfProof.FixtureGenerator/PdfProof.FixtureGenerator.csproj`, `Directory.Packages.props`

**Interfaces:**
- Consumes: `IProducer`, `CorpusDocument`, `Producers` (Task 4).
- Produces:
  - Producers named `chromium-tagged`, `chromium-untagged`, `selectpdf`, `typst` and `libreoffice` (best effort).
  - `ExternalProcess.RunAsync(string fileName, IReadOnlyList<string> arguments, string? workingDirectory = null, TimeSpan? timeout = null) -> Task<(int ExitCode, string Output, string Error)>`.
  - `Versions.Of(Type type) -> string`.
  - `LibreOfficeProducer(IReadOnlyList<string> candidates)` with `static DefaultCandidates()`.
  - The `invoice` corpus folder. Its oracle is what Task 8 compares against.

- [ ] **Step 1: Add the packages**

In `Directory.Packages.props`, add:
```xml
    <!-- The fixture generator's producers (tools/, never shipped). Pinned: a new version changes the fixtures. -->
    <PackageVersion Include="Select.HtmlToPdf.NetCore" Version="26.4.0" />
    <!-- Playwright pins the Chromium build it installs. -->
    <PackageVersion Include="Microsoft.Playwright" Version="1.63.0" />
```
In the generator's `.csproj`, add:
```xml
  <ItemGroup>
    <PackageReference Include="Select.HtmlToPdf.NetCore" />
    <PackageReference Include="Microsoft.Playwright" />
  </ItemGroup>
```

- [ ] **Step 2: Write the invoice folder**

The content is the same logical document in every source: neutral sample data, fixed where the producer allows.

`corpus/generated/invoice/invoice.html`:
```html
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>Invoice</title>
<style>
  body { font-family: Arial, Helvetica, sans-serif; font-size: 10pt; margin: 0; }
  h1 { font-size: 20pt; }
  h2 { font-size: 14pt; }
  table { border-collapse: collapse; }
  th, td { border: 1px solid #999; padding: 4px 8px; text-align: left; }
  .page-break { page-break-before: always; }
</style>
</head>
<body>
<h1>Invoice</h1>
<p>Questions? Visit <a href="https://example.com/support">our support site</a> or e-mail us.</p>
<p><a href="#terms">Read the terms and conditions</a></p>
<table>
  <thead><tr><th>Item</th><th>Qty</th><th>Amount</th></tr></thead>
  <tbody><tr><td>Consulting, senior</td><td>10</td><td>14 500,00 SEK</td></tr></tbody>
</table>
<h2 id="terms" class="page-break">Terms and conditions</h2>
<ul><li>Payment within 30 days.</li></ul>
</body>
</html>
```

`corpus/generated/invoice/invoice.typ`:
```typst
#set document(title: "Invoice", date: datetime(year: 2026, month: 1, day: 15))
#set text(size: 10pt, lang: "en")
#set page(paper: "a4", footer: context align(center)[Page #counter(page).display() of #counter(page).final().first()])

= Invoice

Questions? Visit #link("https://example.com/support")[our support site] or e-mail us.

#link(<terms>)[Read the terms and conditions]

#table(
  columns: 3,
  table.header[Item][Qty][Amount],
  [Consulting, senior], [10], [14 500,00 SEK],
)

#pagebreak()

== Terms and conditions <terms>

- Payment within 30 days.
```

`corpus/generated/invoice/expected.semantics.json` is the oracle in the JSON export format (spec §7.4). Plan 6 defines that format; if it renames a property, the oracle changes with it.
```json
{
  "formatVersion": 1,
  "metadata": { "title": "Invoice", "pageCount": 2 },
  "semantics": {
    "blocks": [
      { "type": "heading", "level": 1, "text": "Invoice", "page": 1 },
      { "type": "paragraph", "text": "Questions? Visit our support site or e-mail us.", "page": 1,
        "links": [{ "text": "our support site", "uri": "https://example.com/support" }] },
      { "type": "paragraph", "text": "Read the terms and conditions", "page": 1,
        "links": [{ "text": "Read the terms and conditions", "page": 2 }] },
      { "type": "table", "page": 1, "header": ["Item", "Qty", "Amount"],
        "rows": [["Consulting, senior", "10", "14 500,00 SEK"]] },
      { "type": "heading", "level": 2, "text": "Terms and conditions", "page": 2 },
      { "type": "list", "page": 2, "items": ["Payment within 30 days."] }
    ],
    "artifacts": [{ "role": "footer", "text": "Page {n} of {N}", "pages": "1-2" }]
  }
}
```

`corpus/generated/invoice/deviations.json` (known producer gaps: `{ "<producer>": "<reason>" }`; Task 8 fills it from real output if needed):
```json
{}
```

- [ ] **Step 3: Write the failing tests in `tests/PdfProof.FixtureGenerator.Tests/ProducerTests.cs`**

```csharp
namespace PdfProof.FixtureGenerator.Tests;

public sealed class ProducerTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void The_producers_have_the_names_the_corpus_tests_expect()
    {
        Assert.Equal(
            ["chromium-tagged", "chromium-untagged", "libreoffice", "selectpdf", "typst"],
            Producers.Select(Producers.All("typst"), null, out _).Select(p => p.Name));
    }

    [Fact]
    public async Task Typst_is_unavailable_when_its_cli_is_missing()
    {
        var reason = await new TypstProducer(_temp.Combine("no-typst")).CheckAsync();

        Assert.NotNull(reason);
        Assert.StartsWith("the Typst CLI", reason);
    }

    [Fact]
    public async Task LibreOffice_is_unavailable_when_not_installed_and_is_best_effort()
    {
        var missing = _temp.Combine("soffice");
        var producer = new LibreOfficeProducer([missing]);

        Assert.True(producer.BestEffort);
        Assert.Equal($"LibreOffice was not found (looked in: {missing})", await producer.CheckAsync());
    }

    [Fact]
    public async Task SelectPdf_runs_on_Windows_only()
    {
        Assert.Equal(OperatingSystem.IsWindows(), await new SelectPdfProducer().CheckAsync() is null);
    }

    [Fact]
    public async Task An_external_process_reports_its_exit_code_and_output()
    {
        var (exitCode, output, _) = await ExternalProcess.RunAsync("dotnet", ["--version"]);

        Assert.Equal(0, exitCode);
        Assert.Matches(@"^\d+\.\d+\.\d+", output.Trim());
    }

    [Fact]
    public void Versions_drop_the_commit_suffix()
    {
        Assert.DoesNotContain("+", Versions.Of(typeof(Microsoft.Playwright.IPlaywright)));
    }
}
```

- [ ] **Step 4: Run the tests to see them fail**

Run: `dotnet build tests/PdfProof.FixtureGenerator.Tests/PdfProof.FixtureGenerator.Tests.csproj`
Expected: build FAILS: `TypstProducer`, `LibreOfficeProducer`, `SelectPdfProducer`, `ExternalProcess` and `Versions` don't exist.

- [ ] **Step 5: Write the helpers**

`tools/PdfProof.FixtureGenerator/ExternalProcess.cs`:
```csharp
using System.Diagnostics;

namespace PdfProof.FixtureGenerator;

// Runs a command-line tool (Typst, LibreOffice) and captures its output. Arguments are passed as a list, so paths
// with spaces need no quoting. A tool that runs past the timeout (default 2 minutes) is killed.
internal static class ExternalProcess
{
    public static async Task<(int ExitCode, string Output, string Error)> RunAsync(
        string fileName, IReadOnlyList<string> arguments, string? workingDirectory = null, TimeSpan? timeout = null)
    {
        var start = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory ?? "",
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"{fileName} did not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        var limit = timeout ?? TimeSpan.FromMinutes(2);
        using var cancel = new CancellationTokenSource(limit);
        try
        {
            await process.WaitForExitAsync(cancel.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{fileName} did not finish within {limit.TotalSeconds} s.");
        }

        return (process.ExitCode, await output, await error);
    }
}
```

`tools/PdfProof.FixtureGenerator/Versions.cs`:
```csharp
using System.Reflection;

namespace PdfProof.FixtureGenerator;

internal static class Versions
{
    // The informational version of the assembly that defines `type`, without a "+commit" suffix.
    public static string Of(Type type)
    {
        var version = type.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? type.Assembly.GetName().Version?.ToString()
            ?? "unknown";
        var plus = version.IndexOf('+');
        return plus < 0 ? version : version[..plus];
    }
}
```

- [ ] **Step 6: Write the four producers**

`tools/PdfProof.FixtureGenerator/Producers/ChromiumProducer.cs`:
```csharp
using Microsoft.Playwright;

namespace PdfProof.FixtureGenerator;

// Chromium print-to-PDF through Playwright, tagged (with an outline) or untagged. The page footer comes from
// Chromium's footer template, as in applications that print HTML this way.
internal sealed class ChromiumProducer(bool tagged) : IProducer
{
    public string Name => tagged ? "chromium-tagged" : "chromium-untagged";

    public bool BestEffort => false;

    public string Version { get; private set; } = "";

    public async Task<string?> CheckAsync()
    {
        try
        {
            // Installs the Chromium build this Playwright version pins; does nothing when it is already installed.
            var exit = await Task.Run(() => Microsoft.Playwright.Program.Main(["install", "chromium"]));
            if (exit != 0)
            {
                return $"installing Chromium for Playwright failed (exit code {exit})";
            }

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync();
            Version = $"Chromium {browser.Version} (Playwright {Versions.Of(typeof(IPlaywright))})";
            return null;
        }
        catch (Exception ex) when (ex is PlaywrightException or InvalidOperationException)
        {
            return "Chromium could not be started: " + ex.Message;
        }
    }

    public async Task<byte[]> RenderAsync(CorpusDocument document)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync();
        await page.GotoAsync(new Uri(document.HtmlPath).AbsoluteUri);
        return await page.PdfAsync(new PagePdfOptions
        {
            Format = "A4",
            Tagged = tagged,
            Outline = tagged,
            DisplayHeaderFooter = true,
            HeaderTemplate = "<span></span>",
            FooterTemplate = "<div style='font-size:8pt;width:100%;text-align:center'>Page <span class='pageNumber'></span> of <span class='totalPages'></span></div>",
            Margin = new Margin { Top = "2cm", Bottom = "2cm", Left = "2cm", Right = "2cm" },
        });
    }
}
```

`tools/PdfProof.FixtureGenerator/Producers/SelectPdfProducer.cs`:
```csharp
using System.Drawing;
using System.Text.RegularExpressions;

namespace PdfProof.FixtureGenerator;

// SelectPdf Community (HTML to PDF, Windows only, at most 5 pages). The footer is a SelectPdf text section, and the
// title is set from <title> the way an application would set it.
internal sealed class SelectPdfProducer : IProducer
{
    public string Name => "selectpdf";

    public bool BestEffort => false;

    public string Version => "Select.HtmlToPdf.NetCore " + Versions.Of(typeof(SelectPdf.HtmlToPdf));

    public Task<string?> CheckAsync() => Task.FromResult(OperatingSystem.IsWindows() ? null : "SelectPdf runs on Windows only");

    public Task<byte[]> RenderAsync(CorpusDocument document)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("SelectPdf runs on Windows only.");
        }

        var html = File.ReadAllText(document.HtmlPath);
        var converter = new SelectPdf.HtmlToPdf();
        converter.Options.DisplayFooter = true;
        converter.Footer.Height = 30;
        converter.Footer.Add(new SelectPdf.PdfTextSection(0, 10, "Page {page_number} of {total_pages}", new Font("Arial", 8))
        {
            HorizontalAlign = SelectPdf.PdfTextHorizontalAlign.Center,
        });
        var pdf = converter.ConvertHtmlString(html, new Uri(document.Directory + Path.DirectorySeparatorChar).AbsoluteUri);
        try
        {
            pdf.DocumentInformation.Title = Title(html);
            return Task.FromResult(pdf.Save());
        }
        finally
        {
            pdf.Close();
        }
    }

    private static string Title(string html)
    {
        var match = Regex.Match(html, "<title>(.*?)</title>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }
}
```

`tools/PdfProof.FixtureGenerator/Producers/TypstProducer.cs`:
```csharp
using System.ComponentModel;

namespace PdfProof.FixtureGenerator;

// The Typst CLI. The build passes the pinned CLI it downloaded (--typst); without it, "typst" on PATH is used.
internal sealed class TypstProducer(string executable) : IProducer
{
    public string Name => "typst";

    public bool BestEffort => false;

    public string Version { get; private set; } = "";

    public async Task<string?> CheckAsync()
    {
        try
        {
            var (exit, output, error) = await ExternalProcess.RunAsync(executable, ["--version"]);
            if (exit != 0)
            {
                return $"the Typst CLI '{executable}' failed: {error.Trim()}";
            }

            Version = output.Trim();
            return null;
        }
        catch (Win32Exception ex)
        {
            return $"the Typst CLI '{executable}' was not found ({ex.Message}). Run ./build.ps1 --target=GenerateFixtures, which downloads the pinned version, or pass --typst <path>.";
        }
    }

    public async Task<byte[]> RenderAsync(CorpusDocument document)
    {
        var output = Path.Combine(Path.GetTempPath(), $"pdfproof-typst-{Guid.NewGuid():N}.pdf");
        try
        {
            var (exit, _, error) = await ExternalProcess.RunAsync(executable, ["compile", document.TypstPath, output], document.Directory);
            if (exit != 0)
            {
                throw new InvalidOperationException($"typst compile failed: {error.Trim()}");
            }

            return await File.ReadAllBytesAsync(output);
        }
        finally
        {
            if (File.Exists(output))
            {
                File.Delete(output);
            }
        }
    }
}
```

`tools/PdfProof.FixtureGenerator/Producers/LibreOfficeProducer.cs`:
```csharp
namespace PdfProof.FixtureGenerator;

// LibreOffice (HTML through Writer/Web to PDF). Best effort (spec §10.2): used when installed, never installed by the
// build, and a failure is only a warning.
internal sealed class LibreOfficeProducer(IReadOnlyList<string> candidates) : IProducer
{
    private string? _soffice;

    public string Name => "libreoffice";

    public bool BestEffort => true;

    public string Version { get; private set; } = "";

    // Where LibreOffice installs soffice by default.
    public static IReadOnlyList<string> DefaultCandidates()
    {
        if (OperatingSystem.IsWindows())
        {
            return
            [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LibreOffice", "program", "soffice.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LibreOffice", "program", "soffice.exe"),
            ];
        }

        return ["/usr/bin/soffice", "/usr/lib/libreoffice/program/soffice", "/Applications/LibreOffice.app/Contents/MacOS/soffice"];
    }

    public async Task<string?> CheckAsync()
    {
        _soffice = candidates.FirstOrDefault(File.Exists);
        if (_soffice is null)
        {
            return $"LibreOffice was not found (looked in: {string.Join(", ", candidates)})";
        }

        var (exit, output, _) = await ExternalProcess.RunAsync(_soffice, ["--version"]);
        Version = exit == 0 && output.Trim().Length > 0 ? output.Trim() : "LibreOffice (version unknown)";
        return null;
    }

    public async Task<byte[]> RenderAsync(CorpusDocument document)
    {
        var work = Directory.CreateTempSubdirectory("pdfproof-libreoffice-").FullName;
        try
        {
            // A private user profile, so a LibreOffice that is already running doesn't take over the conversion.
            var profile = new Uri(Path.Combine(work, "profile")).AbsoluteUri;
            var (exit, _, error) = await ExternalProcess.RunAsync(
                _soffice!,
                [$"-env:UserInstallation={profile}", "--headless", "--convert-to", "pdf", "--outdir", work, document.HtmlPath]);
            var pdf = Path.Combine(work, document.Name + ".pdf");
            if (exit != 0 || !File.Exists(pdf))
            {
                throw new InvalidOperationException($"soffice failed (exit code {exit}): {error.Trim()}");
            }

            return await File.ReadAllBytesAsync(pdf);
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }
}
```

Replace `Producers.All` in `Producers.cs`:
```csharp
    // Every producer. The corpus tests list the same names (CorpusProducer.All); Task 6 adds the C# producers.
    public static IReadOnlyList<IProducer> All(string typst) =>
    [
        new ChromiumProducer(tagged: false),
        new ChromiumProducer(tagged: true),
        new LibreOfficeProducer(LibreOfficeProducer.DefaultCandidates()),
        new SelectPdfProducer(),
        new TypstProducer(typst),
    ];
```

- [ ] **Step 7: Run the tests**

Run: `dotnet test --project tests/PdfProof.FixtureGenerator.Tests/PdfProof.FixtureGenerator.Tests.csproj`
Expected: all tests pass.

If the build reports CA1416 (platform compatibility) for `System.Drawing.Font` or SelectPdf calls, add `[SupportedOSPlatform("windows")]` to `SelectPdfProducer.RenderAsync`. Keep the `OperatingSystem.IsWindows()` guard.

- [ ] **Step 8: Render the invoice with these producers**

This machine has Typst 0.15.1 on PATH. Run from the repository root:
```bash
dotnet run --project tools/PdfProof.FixtureGenerator -- --producers chromium-tagged,chromium-untagged,selectpdf,typst,libreoffice
```
Expected:
- exit code 0;
- `artifacts/corpus/invoice/{chromium-tagged,chromium-untagged,selectpdf,typst}.pdf` exist;
- the log has `warning: libreoffice is unavailable: LibreOffice was not found …`;
- `artifacts/corpus/manifest.json` lists four `generated` producers with versions, and `libreoffice` as `unavailable`.

The first run installs Chromium for Playwright, which takes a minute.

- [ ] **Step 9: Commit**

```bash
git add Directory.Packages.props corpus tools tests/PdfProof.FixtureGenerator.Tests
git commit -m "Add the invoice corpus document and the Chromium, SelectPdf, Typst and LibreOffice producers

The invoice folder holds the HTML and Typst sources, the oracle and an empty
deviations file. Chromium renders tagged and untagged through Playwright, Typst
through its CLI, LibreOffice only when installed (best effort).

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: The QuestPDF and MigraDoc producers

**Files:**
- Create in `tools/PdfProof.FixtureGenerator/`: `Documents/ICSharpDocument.cs`, `Documents/InvoiceDocument.cs`, `Producers/QuestPdfProducer.cs`, `Producers/MigraDocProducer.cs`, `Producers/MigraDocFonts.cs`
- Modify: `tools/PdfProof.FixtureGenerator/Producers.cs`, the generator `.csproj`, `Directory.Packages.props`, `tests/PdfProof.FixtureGenerator.Tests/ProducerTests.cs`

**Interfaces:**
- Consumes: `IProducer`, `CorpusDocument`, `Versions` (Tasks 4–5).
- Produces:
  - Producers named `questpdf-tagged`, `questpdf-untagged` and `migradoc`.
  - `interface ICSharpDocument { byte[] QuestPdf(bool tagged); byte[] MigraDoc(); }`, and `CSharpDocuments.For(CorpusDocument) -> ICSharpDocument`, which throws for a document without a C# version.
  - `MigraDocFonts.Configure() -> string?`: null when PDFsharp has fonts, otherwise why not.

- [ ] **Step 1: Add the packages**

In `Directory.Packages.props`:
```xml
    <PackageVersion Include="QuestPDF" Version="2026.9.1" />
    <PackageVersion Include="PDFsharp-MigraDoc" Version="6.2.4" />
```
In the generator's `.csproj`, add to the package `<ItemGroup>`:
```xml
    <!-- QuestPDF runs under its Community license, which covers this open-source repository. -->
    <PackageReference Include="QuestPDF" />
    <PackageReference Include="PDFsharp-MigraDoc" />
```

- [ ] **Step 2: Write the failing tests (append to `ProducerTests.cs`)**

Change the expected list in `The_producers_have_the_names_the_corpus_tests_expect` to:
```csharp
            ["chromium-tagged", "chromium-untagged", "libreoffice", "migradoc", "questpdf-tagged", "questpdf-untagged", "selectpdf", "typst"],
```
Add:
```csharp
    [Fact]
    public async Task QuestPdf_renders_the_invoice_and_tags_it_on_request()
    {
        var invoice = new CorpusDocument("invoice", _temp.Path);

        var tagged = await Rendered(new QuestPdfProducer(tagged: true), invoice);
        var untagged = await Rendered(new QuestPdfProducer(tagged: false), invoice);

        Assert.Contains("/StructTreeRoot", tagged);
        Assert.DoesNotContain("/StructTreeRoot", untagged);
    }

    [Fact]
    public async Task MigraDoc_renders_the_invoice_when_fonts_are_available()
    {
        var producer = new MigraDocProducer();
        if (await producer.CheckAsync() is { } reason)
        {
            Assert.Skip(reason);
        }

        Assert.StartsWith("%PDF-", await Rendered(producer, new CorpusDocument("invoice", _temp.Path)));
    }

    [Fact]
    public void A_document_without_a_CSharp_version_is_reported()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => CSharpDocuments.For(new CorpusDocument("letter", _temp.Path)));

        Assert.Equal("There is no C# version of the corpus document 'letter'. Add one in tools/PdfProof.FixtureGenerator/Documents/.", ex.Message);
    }

    // The PDF as Latin-1 text, so tests can look for PDF keywords in it.
    private static async Task<string> Rendered(IProducer producer, CorpusDocument document)
    {
        Assert.Null(await producer.CheckAsync());
        return System.Text.Encoding.Latin1.GetString(await producer.RenderAsync(document));
    }
```

- [ ] **Step 3: Run the tests to see them fail**

Run: `dotnet build tests/PdfProof.FixtureGenerator.Tests/PdfProof.FixtureGenerator.Tests.csproj`
Expected: build FAILS: `QuestPdfProducer`, `MigraDocProducer` and `CSharpDocuments` don't exist.

- [ ] **Step 4: Write the C# invoice**

`tools/PdfProof.FixtureGenerator/Documents/ICSharpDocument.cs`:
```csharp
namespace PdfProof.FixtureGenerator;

// A corpus document written with the C# PDF libraries, matching its HTML and Typst sources.
internal interface ICSharpDocument
{
    byte[] QuestPdf(bool tagged);

    byte[] MigraDoc();
}

internal static class CSharpDocuments
{
    public static ICSharpDocument For(CorpusDocument document) => document.Name switch
    {
        "invoice" => new InvoiceDocument(),
        _ => throw new InvalidOperationException(
            $"There is no C# version of the corpus document '{document.Name}'. Add one in tools/PdfProof.FixtureGenerator/Documents/."),
    };
}
```

`tools/PdfProof.FixtureGenerator/Documents/InvoiceDocument.cs`:
```csharp
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PdfProof.FixtureGenerator;

// The invoice (corpus/generated/invoice/) for QuestPDF and MigraDoc: the same blocks, links and footer as invoice.html
// and invoice.typ. Dates are fixed; no producer string is set (QuestPDF writes none, and the fixture must show that).
// QuestPDF and MigraDoc both define Document and Unit, so those names are written in full.
internal sealed class InvoiceDocument : ICSharpDocument
{
    private static readonly DateTimeOffset Date = new(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);

    public byte[] QuestPdf(bool tagged)
    {
        var document = QuestPDF.Fluent.Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, QuestPDF.Infrastructure.Unit.Centimetre);
            page.DefaultTextStyle(style => style.FontSize(10));
            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
            page.Content().Column(column =>
            {
                column.Spacing(8);
                column.Item().SemanticHeading1().Text("Invoice").FontSize(20).Bold();
                column.Item().SemanticParagraph().Text(text =>
                {
                    text.Span("Questions? Visit ");
                    text.Hyperlink("our support site", "https://example.com/support");
                    text.Span(" or e-mail us.");
                });
                column.Item().SemanticParagraph().SectionLink("terms").Text("Read the terms and conditions");
                column.Item().SemanticTable().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn();
                        columns.RelativeColumn(2);
                    });
                    table.Header(header =>
                    {
                        header.Cell().Text("Item").Bold();
                        header.Cell().Text("Qty").Bold();
                        header.Cell().Text("Amount").Bold();
                    });
                    table.Cell().Text("Consulting, senior");
                    table.Cell().Text("10");
                    table.Cell().Text("14 500,00 SEK");
                });
                column.Item().PageBreak();
                column.Item().Section("terms").SemanticHeading2().Text("Terms and conditions").FontSize(14).Bold();
                column.Item().SemanticList().Column(list => list.Item().SemanticListItem().Row(row =>
                {
                    row.ConstantItem(12).SemanticListLabel().Text("-");
                    row.RelativeItem().SemanticListItemBody().Text("Payment within 30 days.");
                }));
            });
        }));
        document = document.WithMetadata(new DocumentMetadata { Title = "Invoice", Language = "en", CreationDate = Date, ModifiedDate = Date });
        if (tagged)
        {
            document = document.WithSettings(new DocumentSettings { PDFUA_Conformance = PDFUA_Conformance.PDFUA_1 });
        }

        return document.GeneratePdf();
    }

    public byte[] MigraDoc()
    {
        var document = new MigraDoc.DocumentObjectModel.Document();
        document.Info.Title = "Invoice";
        var section = document.AddSection();

        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.AddText("Page ");
        footer.AddPageField();
        footer.AddText(" of ");
        footer.AddNumPagesField();

        var title = section.AddParagraph("Invoice");
        title.Format.Font.Size = 20;
        title.Format.Font.Bold = true;

        var support = section.AddParagraph("Questions? Visit ");
        support.AddHyperlink("https://example.com/support", HyperlinkType.Url).AddText("our support site");
        support.AddText(" or e-mail us.");

        section.AddParagraph().AddHyperlink("terms").AddText("Read the terms and conditions");

        var table = section.AddTable();
        table.Borders.Width = 0.5;
        table.AddColumn("6cm");
        table.AddColumn("2cm");
        table.AddColumn("4cm");
        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Format.Font.Bold = true;
        header.Cells[0].AddParagraph("Item");
        header.Cells[1].AddParagraph("Qty");
        header.Cells[2].AddParagraph("Amount");
        var row = table.AddRow();
        row.Cells[0].AddParagraph("Consulting, senior");
        row.Cells[1].AddParagraph("10");
        row.Cells[2].AddParagraph("14 500,00 SEK");

        section.AddPageBreak();
        var terms = section.AddParagraph("Terms and conditions");
        terms.Format.Font.Size = 14;
        terms.Format.Font.Bold = true;
        terms.AddBookmark("terms");
        section.AddParagraph("- Payment within 30 days.");

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        renderer.PdfDocument.Info.CreationDate = Date.UtcDateTime;
        using var output = new MemoryStream();
        renderer.PdfDocument.Save(output);
        return output.ToArray();
    }
}
```

- [ ] **Step 5: Write the producers and the font setup**

`tools/PdfProof.FixtureGenerator/Producers/QuestPdfProducer.cs`:
```csharp
namespace PdfProof.FixtureGenerator;

// QuestPDF, untagged or tagged (PDF/UA-1). QuestPDF runs under its Community license, which covers this repository.
internal sealed class QuestPdfProducer(bool tagged) : IProducer
{
    public string Name => tagged ? "questpdf-tagged" : "questpdf-untagged";

    public bool BestEffort => false;

    public string Version => "QuestPDF " + Versions.Of(typeof(QuestPDF.Settings));

    public Task<string?> CheckAsync()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        return Task.FromResult<string?>(null);
    }

    public Task<byte[]> RenderAsync(CorpusDocument document) => Task.FromResult(CSharpDocuments.For(document).QuestPdf(tagged));
}
```

`tools/PdfProof.FixtureGenerator/Producers/MigraDocProducer.cs`:
```csharp
namespace PdfProof.FixtureGenerator;

// MigraDoc on PDFsharp 6. Untagged: PDFsharp doesn't write a structure tree.
internal sealed class MigraDocProducer : IProducer
{
    public string Name => "migradoc";

    public bool BestEffort => false;

    public string Version => "PDFsharp-MigraDoc " + Versions.Of(typeof(MigraDoc.DocumentObjectModel.Document));

    public Task<string?> CheckAsync() => Task.FromResult(MigraDocFonts.Configure());

    public Task<byte[]> RenderAsync(CorpusDocument document) => Task.FromResult(CSharpDocuments.For(document).MigraDoc());
}
```

`tools/PdfProof.FixtureGenerator/Producers/MigraDocFonts.cs`:
```csharp
using PdfSharp.Fonts;

namespace PdfProof.FixtureGenerator;

// PDFsharp 6 (the Core build) finds no fonts on its own. On Windows it may use the installed Windows fonts; elsewhere a
// resolver serves Liberation Sans or DejaVu Sans for every family MigraDoc asks for, its error font included. PDFsharp's
// font settings can be set only once per process, so the outcome is remembered.
internal static class MigraDocFonts
{
    private static readonly object Gate = new();
    private static bool _configured;
    private static string? _problem;

    // Null when fonts are set up; otherwise why not.
    public static string? Configure()
    {
        lock (Gate)
        {
            if (!_configured)
            {
                _configured = true;
                if (OperatingSystem.IsWindows())
                {
                    GlobalFontSettings.UseWindowsFontsUnderWindows = true;
                }
                else if (FileFontResolver.TryCreate() is { } resolver)
                {
                    GlobalFontSettings.FontResolver = resolver;
                }
                else
                {
                    _problem = "no Liberation Sans or DejaVu Sans font found under /usr/share/fonts/truetype";
                }
            }

            return _problem;
        }
    }

    private sealed class FileFontResolver(IReadOnlyDictionary<string, string> faces) : IFontResolver
    {
        public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) =>
            new((bold, italic) switch
            {
                (true, true) => "bolditalic",
                (true, false) => "bold",
                (false, true) => "italic",
                _ => "regular",
            });

        public byte[]? GetFont(string faceName) => File.ReadAllBytes(faces[faceName]);

        public static FileFontResolver? TryCreate()
        {
            string[][] families =
            [
                [
                    "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
                    "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf",
                    "/usr/share/fonts/truetype/liberation/LiberationSans-Italic.ttf",
                    "/usr/share/fonts/truetype/liberation/LiberationSans-BoldItalic.ttf",
                ],
                [
                    "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
                    "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
                    "/usr/share/fonts/truetype/dejavu/DejaVuSans-Oblique.ttf",
                    "/usr/share/fonts/truetype/dejavu/DejaVuSans-BoldOblique.ttf",
                ],
            ];
            var files = families.FirstOrDefault(family => family.All(File.Exists));
            return files is null
                ? null
                : new FileFontResolver(new Dictionary<string, string>
                {
                    ["regular"] = files[0],
                    ["bold"] = files[1],
                    ["italic"] = files[2],
                    ["bolditalic"] = files[3],
                });
        }
    }
}
```

Add the three producers to `Producers.All`, keeping the list alphabetical, and change its comment to `// Every producer. The corpus tests list the same names (CorpusProducer.All).`:
```csharp
        new MigraDocProducer(),
        new QuestPdfProducer(tagged: false),
        new QuestPdfProducer(tagged: true),
```

- [ ] **Step 6: Run the tests**

Run: `dotnet test --project tests/PdfProof.FixtureGenerator.Tests/PdfProof.FixtureGenerator.Tests.csproj`
Expected: all tests pass. The MigraDoc test skips only on a machine without the fonts.

If the `IFontResolver` members differ in nullability from PDFsharp 6.2.4's declaration (a CS8766/CS8767 warning, which fails the build), match the interface's declaration exactly.

- [ ] **Step 7: Render the invoice with every producer**

```bash
dotnet run --project tools/PdfProof.FixtureGenerator --
```
Expected:
- exit code 0;
- eight producers in `artifacts/corpus/manifest.json`: seven `generated`, and `libreoffice` `unavailable` here;
- `artifacts/corpus/invoice/` holds seven PDFs.

- [ ] **Step 8: Commit**

```bash
git add Directory.Packages.props tools tests/PdfProof.FixtureGenerator.Tests
git commit -m "Add the QuestPDF (tagged and untagged) and MigraDoc producers

The invoice is written once more in C#, for QuestPDF with semantic tags and for
MigraDoc. MigraDoc gets fonts from Windows, or from Liberation Sans or DejaVu Sans
elsewhere.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 7: The oracle and the text-level comparison

**Files:**
- Create: `tests/PdfProof.Corpus.Tests/PdfProof.Corpus.Tests.csproj`, `CorpusPaths.cs`, `Oracle.cs`, `OracleComparison.cs`, `OracleComparisonTests.cs` (all in `tests/PdfProof.Corpus.Tests/`)
- Modify: `PdfProof.slnx`, `Directory.Packages.props`

**Interfaces:**
- Consumes:
  - `PdfProofDocument` and its layout and structure models: `LayoutPage.Text`, `LayoutPage.Links`, `LayoutLink.Text`/`Uri`/`TargetPage`, `StructureModel.Root.Descendants()` (pre-order) and `StructureNode.Role`;
  - `RawPdf`, linked from `PdfProof.Tests`;
  - the invoice oracle (Task 5).
- Produces (internal, namespace `PdfProof.Corpus.Tests`):
  - `sealed record Oracle(string? Title, int? PageCount, IReadOnlyList<OracleBlock> Blocks, IReadOnlyList<OracleArtifact> Artifacts)` with `static Load(string path)` and `static Parse(string json)`. It throws `InvalidDataException` for an unknown block type.
  - `sealed record OracleBlock(string Type, int? Level, string? Text, int? Page, IReadOnlyList<string> Header, IReadOnlyList<IReadOnlyList<string>> Rows, IReadOnlyList<string> Items, IReadOnlyList<OracleLink> Links)` with `Texts` and `Role`.
  - `sealed record OracleLink(string Text, string? Uri, int? Page)` and `sealed record OracleArtifact(string Role, string Text)`.
  - `static class OracleComparison` with `Compare(Oracle oracle, PdfProofDocument document, bool checkRoles) -> IReadOnlyList<string>`: the mismatches, empty when the document matches.
  - `static class CorpusPaths` with `RepositoryRoot`, `GeneratedSources`, `GeneratedDocument(name)`, `Fixture(document, producer)` and `External(id)`.

- [ ] **Step 1: Create the project**

In `Directory.Packages.props`, add:
```xml
    <!-- Corpus tests on .NET Framework read the oracle with it; net10.0 has it in the box. -->
    <PackageVersion Include="System.Text.Json" Version="10.0.12" />
```
`tests/PdfProof.Corpus.Tests/PdfProof.Corpus.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="Polyfill" PrivateAssets="all" />
    <PackageReference Include="System.Text.Json" Condition="'$(TargetFramework)' == 'net48'" />
    <ProjectReference Include="../../src/PdfProof/PdfProof.csproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- The comparison's own tests build small PDFs with the same helper as PdfProof.Tests. -->
    <Compile Include="../PdfProof.Tests/RawPdf.cs" Link="RawPdf.cs" />
  </ItemGroup>

</Project>
```
Add `<Project Path="tests/PdfProof.Corpus.Tests/PdfProof.Corpus.Tests.csproj" />` to the `/tests/` folder of `PdfProof.slnx`.

`tests/PdfProof.Corpus.Tests/CorpusPaths.cs`:
```csharp
namespace PdfProof.Corpus.Tests;

// Where the corpus lives: the sources and oracles in corpus/, the generated and downloaded PDFs in artifacts/ (written
// by the GenerateFixtures and FetchCorpus build targets, never committed).
internal static class CorpusPaths
{
    public static string RepositoryRoot { get; } = FindRoot();

    public static string GeneratedSources => Path.Combine(RepositoryRoot, "corpus", "generated");

    public static string GeneratedDocument(string document) => Path.Combine(GeneratedSources, document);

    public static string Fixture(string document, string producer) =>
        Path.Combine(RepositoryRoot, "artifacts", "corpus", document, producer + ".pdf");

    public static string External(string id) => Path.Combine(RepositoryRoot, "artifacts", "external", id + ".pdf");

    // The nearest folder above the test binaries that holds PdfProof.slnx.
    private static string FindRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "PdfProof.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException($"No PdfProof.slnx above {AppContext.BaseDirectory}.");
    }
}
```

- [ ] **Step 2: Write the failing tests in `tests/PdfProof.Corpus.Tests/OracleComparisonTests.cs`**

```csharp
using PdfProof.Tests;

namespace PdfProof.Corpus.Tests;

public sealed class OracleComparisonTests
{
    private static readonly Oracle Expected = Oracle.Parse(
        """
        {
          "formatVersion": 1,
          "metadata": { "title": "Invoice", "pageCount": 2 },
          "semantics": {
            "blocks": [
              { "type": "heading", "level": 1, "text": "Invoice", "page": 1 },
              { "type": "paragraph", "text": "Visit our support site", "page": 1,
                "links": [{ "text": "Visit our support site", "uri": "https://example.com/support" }] },
              { "type": "paragraph", "text": "Read the terms and conditions", "page": 1,
                "links": [{ "text": "Read the terms and conditions", "page": 2 }] },
              { "type": "table", "page": 1, "header": ["Item", "Qty", "Amount"],
                "rows": [["Consulting, senior", "10", "14 500,00 SEK"]] },
              { "type": "heading", "level": 2, "text": "Terms and conditions", "page": 2 },
              { "type": "list", "page": 2, "items": ["Payment within 30 days."] }
            ],
            "artifacts": [{ "role": "footer", "text": "Page {n} of {N}", "pages": "1-2" }]
          }
        }
        """);

    [Fact]
    public void A_document_that_shows_everything_in_order_has_no_mismatches()
    {
        Assert.Empty(OracleComparison.Compare(Expected, Invoice(), checkRoles: false));
    }

    [Fact]
    public void A_different_title_is_a_mismatch()
    {
        Assert.Equal(["title: expected \"Invoice\", was \"Bill\""], OracleComparison.Compare(Expected, Invoice(title: "Bill"), checkRoles: false));
    }

    [Fact]
    public void Missing_text_is_reported_with_its_block_and_page()
    {
        Assert.Equal(
            ["block 5 (heading): \"Terms and conditions\" not found on page 2 after the previous block's text"],
            OracleComparison.Compare(Expected, Invoice(terms: "Terms"), checkRoles: false));
    }

    [Fact]
    public void Text_in_the_wrong_order_is_a_mismatch()
    {
        var swapped = Expected with { Blocks = [Expected.Blocks[0], Expected.Blocks[2], Expected.Blocks[1], .. Expected.Blocks.Skip(3)] };

        Assert.Equal(
            ["block 3 (paragraph): \"Visit our support site\" not found on page 1 after the previous block's text"],
            OracleComparison.Compare(swapped, Invoice(), checkRoles: false));
    }

    [Fact]
    public void A_link_to_the_wrong_target_is_a_mismatch()
    {
        Assert.Equal(
            ["block 2 (paragraph): no link \"Visit our support site\" to https://example.com/support on page 1"],
            OracleComparison.Compare(Expected, Invoice(support: "https://example.com/other"), checkRoles: false));
    }

    [Fact]
    public void A_missing_footer_is_reported_for_its_page()
    {
        Assert.Equal(
            ["page 2: footer \"Page 2 of 2\" not found"],
            OracleComparison.Compare(Expected, Invoice(footerOnSecondPage: false), checkRoles: false));
    }

    [Fact]
    public void Tagged_documents_must_declare_the_blocks_roles_in_order()
    {
        var oracle = Oracle.Parse(
            """
            { "semantics": { "blocks": [
              { "type": "heading", "level": 1, "text": "Invoice" },
              { "type": "paragraph", "text": "Hello" },
              { "type": "list", "items": ["Payment within 30 days."] } ] } }
            """);
        var asLevelTwo = Oracle.Parse(
            """
            { "semantics": { "blocks": [ { "type": "heading", "level": 2, "text": "Invoice" } ] } }
            """);

        Assert.Empty(OracleComparison.Compare(oracle, Tagged(), checkRoles: true));
        var mismatch = Assert.Single(OracleComparison.Compare(asLevelTwo, Tagged(), checkRoles: true));
        Assert.Equal("structure: no H2 element for block 1 (heading) after the previous block's; roles in order: Document H1 P L LI", mismatch);
    }

    [Fact]
    public void An_unknown_block_type_is_rejected()
    {
        var ex = Assert.Throws<InvalidDataException>(() => Oracle.Parse("""{ "semantics": { "blocks": [ { "type": "chart" } ] } }"""));

        Assert.Equal("Unknown block type \"chart\".", ex.Message);
    }

    [Fact]
    public void The_invoice_oracle_parses()
    {
        var oracle = Oracle.Load(Path.Combine(CorpusPaths.GeneratedDocument("invoice"), "expected.semantics.json"));

        Assert.Equal(("Invoice", (int?)2), (oracle.Title, oracle.PageCount));
        Assert.Equal(["heading", "paragraph", "paragraph", "table", "heading", "list"], oracle.Blocks.Select(b => b.Type));
        Assert.Equal(["Item", "Qty", "Amount", "Consulting, senior", "10", "14 500,00 SEK"], oracle.Blocks[3].Texts);
        Assert.Equal(["H1", "P", "P", "Table", "H2", "L"], oracle.Blocks.Select(b => b.Role));
    }

    // Two pages laid out like the oracle; each argument breaks one thing.
    private static PdfProofDocument Invoice(
        string title = "Invoice", string support = "https://example.com/support", string terms = "Terms and conditions", bool footerOnSecondPage = true)
    {
        var pdf = new RawPdf { Info = $"<< /Title ({title}) >>" };
        var first = pdf.AddPage(
            RawPdf.Text(50, 750, "Invoice", 18) +
            RawPdf.Text(50, 700, "Visit our support site") +
            RawPdf.Text(50, 680, "Read the terms and conditions") +
            RawPdf.Text(50, 640, "Item") + RawPdf.Text(250, 640, "Qty") + RawPdf.Text(350, 640, "Amount") +
            RawPdf.Text(50, 620, "Consulting, senior") + RawPdf.Text(250, 620, "10") + RawPdf.Text(350, 620, "14 500,00 SEK") +
            RawPdf.Text(270, 40, "Page 1 of 2", 10));
        var second = pdf.AddPage(
            RawPdf.Text(50, 750, terms, 14) +
            RawPdf.Text(50, 720, "Payment within 30 days.") +
            (footerOnSecondPage ? RawPdf.Text(270, 40, "Page 2 of 2", 10) : ""));
        pdf.Annotate(first, $"<< /Type /Annot /Subtype /Link /Rect [45 695 250 715] /A << /S /URI /URI ({support}) >> >>");
        pdf.Annotate(first, $"<< /Type /Annot /Subtype /Link /Rect [45 675 250 695] /A << /S /GoTo /D [{second} 0 R /Fit] >> >>");
        return PdfProofDocument.Load(pdf.Build());
    }

    private static PdfProofDocument Tagged()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(
            RawPdf.Marked("H1", 0, RawPdf.Text(50, 750, "Invoice", 18)) +
            RawPdf.Marked("P", 1, RawPdf.Text(50, 700, "Hello")) +
            RawPdf.Marked("LI", 2, RawPdf.Text(50, 650, "Payment within 30 days.")));
        var heading = pdf.Add($"<< /S /H1 /Pg {page} 0 R /K 0 >>");
        var paragraph = pdf.Add($"<< /S /P /Pg {page} 0 R /K 1 >>");
        var item = pdf.Add($"<< /S /LI /Pg {page} 0 R /K 2 >>");
        var list = pdf.Add($"<< /S /L /K [{item} 0 R] >>");
        var document = pdf.Add($"<< /S /Document /K [{heading} 0 R {paragraph} 0 R {list} 0 R] >>");
        pdf.Tag($"{document} 0 R");
        return PdfProofDocument.Load(pdf.Build());
    }
}
```

- [ ] **Step 3: Run the tests to see them fail**

Run: `dotnet build tests/PdfProof.Corpus.Tests/PdfProof.Corpus.Tests.csproj`
Expected: build FAILS: `Oracle` and `OracleComparison` don't exist.

- [ ] **Step 4: Write `tests/PdfProof.Corpus.Tests/Oracle.cs`**

```csharp
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
            ? artifactArray.EnumerateArray().Select(a => new OracleArtifact(String(a, "role") ?? "", String(a, "text") ?? "")).ToList()
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

    // The structure role a tagged producer should give the block. "H" accepts any heading level, for oracles that
    // leave the level out.
    public string Role => Type switch
    {
        "heading" => Level is { } level ? "H" + level.ToString(CultureInfo.InvariantCulture) : "H",
        "paragraph" => "P",
        "table" => "Table",
        "list" => "L",
        _ => "Figure",
    };
}

internal sealed record OracleLink(string Text, string? Uri, int? Page);

internal sealed record OracleArtifact(string Role, string Text);
```

- [ ] **Step 5: Write `tests/PdfProof.Corpus.Tests/OracleComparison.cs`**

```csharp
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PdfProof.Layout;

namespace PdfProof.Corpus.Tests;

// Compares a loaded document with its oracle at the level the layout and structure models allow until the semantic
// model exists (Plan 5): title and page count; every block's text found in order (on its page, when the oracle gives
// one); its links; for tagged producers, the blocks' structure roles in order; and the artifacts on every page. Text
// is compared with whitespace collapsed. Returns the mismatches; empty means the document matches.
internal static class OracleComparison
{
    public static IReadOnlyList<string> Compare(Oracle oracle, PdfProofDocument document, bool checkRoles)
    {
        var mismatches = new List<string>();
        var pages = document.Layout.Pages;
        if (oracle.Title is { } title && document.Metadata.Title != title)
        {
            mismatches.Add($"title: expected \"{title}\", was \"{document.Metadata.Title}\"");
        }

        if (oracle.PageCount is { } count && pages.Count != count)
        {
            mismatches.Add($"page count: expected {count}, was {pages.Count}");
        }

        // All page texts joined by a space; pageStart[i] is where page i + 1 begins.
        var texts = pages.Select(page => Normalize(page.Text)).ToList();
        var pageStart = new List<int>();
        var joined = new StringBuilder();
        foreach (var text in texts)
        {
            pageStart.Add(joined.Length);
            joined.Append(text).Append(' ');
        }

        var haystack = joined.ToString();
        var cursor = 0;
        for (var b = 0; b < oracle.Blocks.Count; b++)
        {
            var block = oracle.Blocks[b];
            var where = block.Page is { } onPage ? $" on page {onPage}" : "";
            var start = cursor;
            var end = haystack.Length;
            if (block.Page is { } page)
            {
                if (page < 1 || page > pages.Count)
                {
                    mismatches.Add($"block {b + 1} ({block.Type}): page {page} doesn't exist");
                    continue;
                }

                start = Math.Max(cursor, pageStart[page - 1]);
                end = pageStart[page - 1] + texts[page - 1].Length;
            }

            foreach (var text in block.Texts)
            {
                var needle = Normalize(text);
                var at = start <= end ? haystack.IndexOf(needle, start, StringComparison.Ordinal) : -1;
                if (at < 0 || at + needle.Length > end)
                {
                    mismatches.Add($"block {b + 1} ({block.Type}): \"{text}\" not found{where} after the previous block's text");
                    continue;
                }

                start = cursor = at + needle.Length;
            }

            foreach (var link in block.Links.Where(link => !HasLink(pages, block.Page, link)))
            {
                mismatches.Add($"block {b + 1} ({block.Type}): no link \"{link.Text}\" to {Target(link)}{where}");
            }
        }

        if (checkRoles)
        {
            Roles(oracle, document, mismatches);
        }

        foreach (var artifact in oracle.Artifacts)
        {
            for (var i = 0; i < pages.Count; i++)
            {
                var expected = Normalize(artifact.Text
                    .Replace("{n}", (i + 1).ToString(CultureInfo.InvariantCulture))
                    .Replace("{N}", pages.Count.ToString(CultureInfo.InvariantCulture)));
                if (texts[i].IndexOf(expected, StringComparison.Ordinal) < 0)
                {
                    mismatches.Add($"page {i + 1}: {artifact.Role} \"{expected}\" not found");
                }
            }
        }

        return mismatches;
    }

    private static bool HasLink(IReadOnlyList<LayoutPage> pages, int? page, OracleLink expected)
    {
        IEnumerable<LayoutPage> scope = page is { } number && number >= 1 && number <= pages.Count ? [pages[number - 1]] : pages;
        return scope
            .SelectMany(candidate => candidate.Links)
            .Any(link => Normalize(link.Text) == Normalize(expected.Text)
                && (expected.Uri is null || link.Uri == expected.Uri)
                && (expected.Page is null || link.TargetPage == expected.Page));
    }

    private static string Target(OracleLink link) => link.Uri ?? (link.Page is { } page ? $"page {page}" : "anywhere");

    // Tagged producers must declare the blocks' roles in document order; other elements may come in between.
    private static void Roles(Oracle oracle, PdfProofDocument document, List<string> mismatches)
    {
        var roles = document.Structure.Root.Descendants().Select(node => node.Role).ToList();
        var at = 0;
        for (var b = 0; b < oracle.Blocks.Count; b++)
        {
            var expected = oracle.Blocks[b].Role;
            var found = roles.FindIndex(at, role => expected == "H" ? IsHeading(role) : role == expected);
            if (found < 0)
            {
                mismatches.Add($"structure: no {expected} element for block {b + 1} ({oracle.Blocks[b].Type}) after the previous block's; roles in order: {string.Join(" ", roles)}");
                continue;
            }

            at = found + 1;
        }
    }

    private static bool IsHeading(string role) =>
        role == "H" || (role.Length == 2 && role[0] == 'H' && role[1] >= '1' && role[1] <= '6');

    private static string Normalize(string text) => Regex.Replace(text, @"\s+", " ").Trim();
}
```

- [ ] **Step 6: Run the tests**

Run: `dotnet test --project tests/PdfProof.Corpus.Tests/PdfProof.Corpus.Tests.csproj`
Expected: all tests pass on net48 and net10.0.

`Tagged_documents_must_declare_the_blocks_roles_in_order` relies on `Descendants()` being pre-order (Document, H1, P, L, LI). If it returns another order, the expected message must follow the order `Descendants()` documents. Read its XML doc before changing the test.

- [ ] **Step 7: Commit**

```bash
git add Directory.Packages.props PdfProof.slnx tests/PdfProof.Corpus.Tests
git commit -m "Compare documents with a corpus oracle at text level

PdfProof.Corpus.Tests reads expected.semantics.json and checks title, page
count, block text in order and on its page, links, the structure roles of tagged
documents, and per-page artifacts. Plan 5 replaces the text level with full
block comparison.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: The corpus matrix and known deviations

**Files:**
- Create in `tests/PdfProof.Corpus.Tests/`: `CorpusProducer.cs`, `Fixtures.cs`, `Deviations.cs`, `MatrixVerdict.cs`, `GeneratedCorpusTests.cs`, `MatrixVerdictTests.cs`, `FixturesTests.cs`
- Modify (only if the real run in Step 6 shows producer gaps): `corpus/generated/invoice/deviations.json`

**Interfaces:**
- Consumes: `Oracle`, `OracleComparison`, `CorpusPaths` (Task 7), the fixtures from Tasks 5–6, and `PdfDiagnostics.Profile.Name` (Task 1).
- Produces:
  - `sealed record CorpusProducer(string Name, bool Tagged, string ExpectedProfile, bool BestEffort = false)` with `static All`, listing the same names as the generator.
  - `static class Fixtures`:
    - `Require(string path, string howToGet, bool bestEffort = false) -> string`: skips the test, or fails it under `PDFPROOF_REQUIRE_FIXTURES=true` unless best effort;
    - `TryLoad(string path, List<string> mismatches) -> PdfProofDocument?`.
  - `static class Deviations` with `Load(string path) -> IReadOnlyDictionary<string, string>`.
  - `enum MatrixOutcome { Pass, KnownDeviation, Fail }` and `sealed record MatrixVerdict(MatrixOutcome Outcome, string Message)` with `static Of(producer, mismatches, deviations)`.

- [ ] **Step 1: Write the failing tests**

`tests/PdfProof.Corpus.Tests/MatrixVerdictTests.cs`:
```csharp
namespace PdfProof.Corpus.Tests;

public sealed class MatrixVerdictTests
{
    private static readonly IReadOnlyDictionary<string, string> Known = new Dictionary<string, string> { ["typst"] = "draws list bullets as text" };

    [Fact]
    public void No_mismatches_and_no_deviation_passes()
    {
        Assert.Equal(new MatrixVerdict(MatrixOutcome.Pass, "selectpdf matches the oracle."), MatrixVerdict.Of("selectpdf", [], Known));
    }

    [Fact]
    public void Mismatches_without_a_deviation_fail_and_list_every_mismatch()
    {
        var verdict = MatrixVerdict.Of("selectpdf", ["page 2: footer \"Page 2 of 2\" not found", "page count: expected 2, was 3"], Known);

        Assert.Equal(MatrixOutcome.Fail, verdict.Outcome);
        Assert.Equal(
            "selectpdf doesn't match the oracle (2 mismatches):\n- page 2: footer \"Page 2 of 2\" not found\n- page count: expected 2, was 3\n" +
            "Fix the cause, or record a known producer gap in deviations.json with the reason.",
            verdict.Message);
    }

    [Fact]
    public void Mismatches_with_a_deviation_are_a_known_deviation()
    {
        var verdict = MatrixVerdict.Of("typst", ["block 6 (list): \"x\" not found"], Known);

        Assert.Equal(MatrixOutcome.KnownDeviation, verdict.Outcome);
        Assert.Equal("typst: known deviation (draws list bullets as text):\n- block 6 (list): \"x\" not found", verdict.Message);
    }

    [Fact]
    public void A_deviation_that_no_longer_deviates_fails_until_it_is_removed()
    {
        var verdict = MatrixVerdict.Of("typst", [], Known);

        Assert.Equal(MatrixOutcome.Fail, verdict.Outcome);
        Assert.Equal("typst now matches the oracle, but deviations.json still lists it (\"draws list bullets as text\"). Remove the entry.", verdict.Message);
    }
}
```

`tests/PdfProof.Corpus.Tests/FixturesTests.cs`:
```csharp
namespace PdfProof.Corpus.Tests;

public sealed class FixturesTests
{
    [Fact]
    public void A_fixture_that_is_not_a_readable_pdf_becomes_a_mismatch_naming_the_file()
    {
        // Review Focus 5: e.g. a truncated artifact download.
        var path = Path.Combine(Path.GetTempPath(), $"pdfproof-truncated-{Guid.NewGuid():N}.pdf");
        File.WriteAllText(path, "%PDF-1.7\n1 0 obj\n<<");
        try
        {
            var mismatches = new List<string>();

            Assert.Null(Fixtures.TryLoad(path, mismatches));

            Assert.StartsWith($"{path} could not be loaded: ", Assert.Single(mismatches));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Deviations_are_read_from_their_file_and_a_missing_file_means_none()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pdfproof-deviations-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{ "typst": "draws list bullets as text" }""");
        try
        {
            Assert.Equal("draws list bullets as text", Deviations.Load(path)["typst"]);
            Assert.Empty(Deviations.Load(path + ".missing"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
```

`tests/PdfProof.Corpus.Tests/GeneratedCorpusTests.cs`:
```csharp
namespace PdfProof.Corpus.Tests;

// The corpus matrix (spec §10.2): every generated document × every producer, checked against the document's oracle.
// A case passes, is a known deviation (deviations.json, reason printed), or fails.
public sealed class GeneratedCorpusTests
{
    public static TheoryData<string, string> Cases()
    {
        var cases = new TheoryData<string, string>();
        foreach (var document in Documents())
        {
            foreach (var producer in CorpusProducer.All)
            {
                cases.Add(document, producer.Name);
            }
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Every_producer_matches_the_oracle_or_a_known_deviation(string document, string producerName)
    {
        var producer = CorpusProducer.All.Single(p => p.Name == producerName);
        var path = Fixtures.Require(CorpusPaths.Fixture(document, producer.Name), "Run ./build.ps1 --target=GenerateFixtures.", producer.BestEffort);
        var sources = CorpusPaths.GeneratedDocument(document);

        var mismatches = new List<string>();
        if (Fixtures.TryLoad(path, mismatches) is { } pdf)
        {
            if (pdf.Diagnostics.Profile.Name != producer.ExpectedProfile)
            {
                mismatches.Add($"profile: expected {producer.ExpectedProfile}, was {pdf.Diagnostics.Profile.Name}");
            }

            if (pdf.Structure.IsTagged != producer.Tagged)
            {
                mismatches.Add($"tagged: expected {producer.Tagged}, was {pdf.Structure.IsTagged}");
            }

            var oracle = Oracle.Load(Path.Combine(sources, "expected.semantics.json"));
            mismatches.AddRange(OracleComparison.Compare(oracle, pdf, checkRoles: producer.Tagged && pdf.Structure.IsTagged));
        }

        var verdict = MatrixVerdict.Of(producer.Name, mismatches, Deviations.Load(Path.Combine(sources, "deviations.json")));
        TestContext.Current.TestOutputHelper?.WriteLine(verdict.Message);
        Assert.True(verdict.Outcome != MatrixOutcome.Fail, verdict.Message);
    }

    [Fact]
    public void Every_deviation_names_a_corpus_producer()
    {
        foreach (var document in Documents())
        {
            foreach (var producer in Deviations.Load(Path.Combine(CorpusPaths.GeneratedDocument(document), "deviations.json")).Keys)
            {
                Assert.True(CorpusProducer.All.Any(p => p.Name == producer), $"{document}/deviations.json lists \"{producer}\", which is not a corpus producer.");
            }
        }
    }

    private static IEnumerable<string> Documents() =>
        Directory.GetDirectories(CorpusPaths.GeneratedSources)
            .Where(folder => File.Exists(Path.Combine(folder, "expected.semantics.json")))
            .Select(folder => Path.GetFileName(folder))
            .OrderBy(name => name, StringComparer.Ordinal);
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet build tests/PdfProof.Corpus.Tests/PdfProof.Corpus.Tests.csproj`
Expected: build FAILS: `MatrixVerdict`, `MatrixOutcome`, `Fixtures`, `Deviations` and `CorpusProducer` don't exist.

- [ ] **Step 3: Write the matrix types**

`tests/PdfProof.Corpus.Tests/CorpusProducer.cs`:
```csharp
namespace PdfProof.Corpus.Tests;

// Every producer of the generated corpus, under the names tools/PdfProof.FixtureGenerator uses, with what loading its
// output must show: tagging, and the profile detected from its /Producer and /Creator (probed in Plan 4).
internal sealed record CorpusProducer(string Name, bool Tagged, string ExpectedProfile, bool BestEffort = false)
{
    public static IReadOnlyList<CorpusProducer> All { get; } =
    [
        new("chromium-tagged", Tagged: true, "Chromium"),
        new("chromium-untagged", Tagged: false, "Chromium"),
        new("libreoffice", Tagged: false, "Default", BestEffort: true),
        new("migradoc", Tagged: false, "Default"),
        // QuestPDF writes no /Producer or /Creator, so it is never detected (spec §5.3).
        new("questpdf-tagged", Tagged: true, "Default"),
        new("questpdf-untagged", Tagged: false, "Default"),
        new("selectpdf", Tagged: false, "SelectPdf"),
        new("typst", Tagged: true, "Typst"),
    ];
}
```

`tests/PdfProof.Corpus.Tests/Fixtures.cs`:
```csharp
namespace PdfProof.Corpus.Tests;

// Fixtures are made by the build (GenerateFixtures, FetchCorpus), not committed. A missing one skips its test with a
// hint, or fails it when PDFPROOF_REQUIRE_FIXTURES=true (always set in CI). Best-effort producers only ever skip.
internal static class Fixtures
{
    public static bool Required => Environment.GetEnvironmentVariable("PDFPROOF_REQUIRE_FIXTURES") == "true";

    public static string Require(string path, string howToGet, bool bestEffort = false)
    {
        if (File.Exists(path))
        {
            return path;
        }

        var message = $"Missing {path}. {howToGet}";
        if (Required && !bestEffort)
        {
            Assert.Fail(message);
        }

        Assert.Skip(message);
        return path;
    }

    // A fixture PdfProof can't read is a mismatch, so the case fails with the file and the error instead of crashing.
    public static PdfProofDocument? TryLoad(string path, List<string> mismatches)
    {
        try
        {
            return PdfProofDocument.Load(path);
        }
        catch (PdfProofException ex)
        {
            mismatches.Add($"{path} could not be loaded: {ex.Message}");
            return null;
        }
    }
}
```

`tests/PdfProof.Corpus.Tests/Deviations.cs`:
```csharp
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
```

`tests/PdfProof.Corpus.Tests/MatrixVerdict.cs`:
```csharp
namespace PdfProof.Corpus.Tests;

internal enum MatrixOutcome
{
    Pass,
    KnownDeviation,
    Fail,
}

// The outcome of one matrix case. A known deviation that starts passing fails until its entry is removed, so
// deviations.json never hides a fixed gap (spec §10.2).
internal sealed record MatrixVerdict(MatrixOutcome Outcome, string Message)
{
    public static MatrixVerdict Of(string producer, IReadOnlyList<string> mismatches, IReadOnlyDictionary<string, string> deviations)
    {
        var known = deviations.TryGetValue(producer, out var reason);
        if (mismatches.Count == 0)
        {
            return known
                ? new MatrixVerdict(MatrixOutcome.Fail, $"{producer} now matches the oracle, but deviations.json still lists it (\"{reason}\"). Remove the entry.")
                : new MatrixVerdict(MatrixOutcome.Pass, $"{producer} matches the oracle.");
        }

        var list = string.Join("\n", mismatches.Select(mismatch => "- " + mismatch));
        return known
            ? new MatrixVerdict(MatrixOutcome.KnownDeviation, $"{producer}: known deviation ({reason}):\n{list}")
            : new MatrixVerdict(
                MatrixOutcome.Fail,
                $"{producer} doesn't match the oracle ({mismatches.Count} mismatches):\n{list}\nFix the cause, or record a known producer gap in deviations.json with the reason.");
    }
}
```

- [ ] **Step 4: Run the unit tests**

Run: `dotnet test --project tests/PdfProof.Corpus.Tests/PdfProof.Corpus.Tests.csproj`
Expected: all tests pass. The seven matrix cases with fixtures from Task 6 also run here; `libreoffice` is skipped. If a matrix case fails, go to Step 5 before committing.

- [ ] **Step 5: Judge every failing matrix case**

For each failing case, read its message and open the PDF's text with a small test or the debugger (`PdfProofDocument.Load(path).Layout.Pages[i].Text`). Then decide which of two cases it is:
- **A producer gap.** The producer really renders or tags differently, e.g. it drops a link or draws a footer as an image. Add an entry to `corpus/generated/invoice/deviations.json` naming the gap in one line, e.g. `"libreoffice": "LibreOffice's HTML import ignores the page footer"`.
- **A PdfProof bug.** Text is on the page but extracted wrongly, or a link exists but isn't matched. Stop and report it with the case's message. It gets its own failing test in `PdfProof.Tests` and a fix in the core before this task continues.

Never loosen the oracle or the comparison to make a case pass. From the probes, every case is expected to pass with `deviations.json` left as `{}`.

- [ ] **Step 6: Run the matrix as CI will**

```bash
PDFPROOF_REQUIRE_FIXTURES=true dotnet test --project tests/PdfProof.Corpus.Tests/PdfProof.Corpus.Tests.csproj
```
(PowerShell: `$env:PDFPROOF_REQUIRE_FIXTURES='true'; dotnet test --project tests/PdfProof.Corpus.Tests/PdfProof.Corpus.Tests.csproj; Remove-Item Env:PDFPROOF_REQUIRE_FIXTURES`.)
Expected: everything passes, and `libreoffice` is still skipped (best effort).

Then move `artifacts/corpus` aside and run once more without the variable. Expected: the seven matrix cases skip with `Missing …/artifacts/corpus/invoice/<producer>.pdf. Run ./build.ps1 --target=GenerateFixtures.`. Move the folder back.

- [ ] **Step 7: Commit**

```bash
git add tests/PdfProof.Corpus.Tests corpus
git commit -m "Run the corpus matrix: every producer against the invoice oracle

Each case passes, is a known deviation from deviations.json, or fails; a
deviation that stops deviating fails until removed. Detection and tagging are
checked per producer. Missing fixtures skip, or fail with
PDFPROOF_REQUIRE_FIXTURES=true; unreadable fixtures fail with the file named.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: The external corpus: pinned downloads, `FetchCorpus`, the IRS forms

**Files:**
- Create: `corpus/external/manifest.json`
- Create: `build/Build.Cake.Extensions/Downloads/PinnedDownload.cs`, `build/Build.Cake.Extensions/Corpus/ExternalCorpusManifest.cs`
- Create: `build/Build.Cake.Extensions.Tests/Downloads/PinnedDownloadTests.cs`, `build/Build.Cake.Extensions.Tests/Corpus/ExternalCorpusManifestTests.cs`
- Create: `build/Build/Tasks/CorpusTasks.cs`, `tests/PdfProof.Corpus.Tests/ExternalCorpusTests.cs`
- Modify: `build/Build/BuildParameters.cs`, `build/Build/Program.cs`, `build/Build/Build.csproj`

**Interfaces:**
- Consumes: `Fixtures`, `CorpusPaths` (Tasks 7–8); `LayoutFormField` (Task 3).
- Produces:
  - `Build.Cake.Extensions.Downloads.PinnedDownload(HttpClient http)` with:
    - `Task<string> GetAsync(Uri url, string sha256, string cacheDirectory, string fileName)`: returns `<cache>/<sha256>/<fileName>`;
    - `static string Sha256Of(string path)`;
    - `static HttpClient CreateHttpClient()`, which sets a User-Agent.
  - `Build.Cake.Extensions.Corpus.ExternalCorpusEntry(string Id, Uri Url, string Sha256, string License, string Source, string Purpose)`, and `ExternalCorpusManifest.Parse(string json)` / `Load(string path)`.
  - `BuildParameters` with `RequireFixtures`, `SkipGenerate`, `SkipFetch` and `Producers`, plus the paths `Corpus`, `External` and `Cache` under `artifacts/`.
  - The Cake target `FetchCorpus`, which writes `artifacts/external/<id>.pdf`.

- [ ] **Step 1: Write the manifest `corpus/external/manifest.json`**

The `irs-prior` URLs are stable; the `irs-pdf` URLs always serve the latest revision. Hashes were taken on 2026-10-11.
```json
[
  {
    "id": "irs-w4-2026",
    "url": "https://www.irs.gov/pub/irs-prior/fw4--2026.pdf",
    "sha256": "92444d8856ce55d9e25dca8b6d1420634fc68b11e1ab1f760916ea29ddd312b2",
    "license": "Public domain: a work of the United States federal government (17 U.S.C. 105)",
    "source": "Internal Revenue Service, Form W-4 (2026)",
    "purpose": "Tagged AcroForm made with a form designer: nested field tree, 48 fields on page 1, 5 pages"
  },
  {
    "id": "irs-w9-2024",
    "url": "https://www.irs.gov/pub/irs-prior/fw9--2024.pdf",
    "sha256": "2d420cbb4123dcf1fb82595b2359cfbb5d81f00b9df9d359fcc7af361d093f53",
    "license": "Public domain: a work of the United States federal government (17 U.S.C. 105)",
    "source": "Internal Revenue Service, Form W-9 (Rev. March 2024)",
    "purpose": "Tagged AcroForm made with a form designer: text fields and checkboxes, 23 fields on page 1, 6 pages"
  }
]
```

- [ ] **Step 2: Write the failing build-helper tests**

`build/Build.Cake.Extensions.Tests/Downloads/PinnedDownloadTests.cs`:
```csharp
using System.Net;
using System.Text;
using Build.Cake.Extensions.Downloads;

namespace Build.Cake.Extensions.Tests.Downloads;

public sealed class PinnedDownloadTests : IDisposable
{
    private static readonly byte[] Body = Encoding.ASCII.GetBytes("pinned content");

    private readonly string _cache = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), $"pdfproof-download-{Guid.NewGuid():N}")).FullName;

    public void Dispose() => Directory.Delete(_cache, recursive: true);

    [Fact]
    public async Task A_download_is_verified_stored_by_hash_and_reused()
    {
        var sha = Sha(Body);
        var handler = new StubHandler(Body);
        var download = new PinnedDownload(new HttpClient(handler));

        var first = await download.GetAsync(new Uri("https://example.com/a.pdf"), sha, _cache, "a.pdf");
        var second = await download.GetAsync(new Uri("https://example.com/a.pdf"), sha, _cache, "a.pdf");

        Assert.Equal(Path.Combine(_cache, sha, "a.pdf"), first);
        Assert.Equal(first, second);
        Assert.Equal(Body, File.ReadAllBytes(first));
        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task A_hash_mismatch_fails_with_both_hashes_and_keeps_nothing()
    {
        var expected = Sha(Encoding.ASCII.GetBytes("something else"));
        var download = new PinnedDownload(new HttpClient(new StubHandler(Body)));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            download.GetAsync(new Uri("https://example.com/a.pdf"), expected, _cache, "a.pdf"));

        Assert.Equal($"SHA-256 mismatch for https://example.com/a.pdf: expected {expected}, got {Sha(Body)}.", ex.Message);
        Assert.Empty(Directory.GetFiles(_cache, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task A_damaged_cached_file_is_downloaded_again()
    {
        var sha = Sha(Body);
        Directory.CreateDirectory(Path.Combine(_cache, sha));
        File.WriteAllText(Path.Combine(_cache, sha, "a.pdf"), "damaged");
        var handler = new StubHandler(Body);

        var path = await new PinnedDownload(new HttpClient(handler)).GetAsync(new Uri("https://example.com/a.pdf"), sha, _cache, "a.pdf");

        Assert.Equal(Body, File.ReadAllBytes(path));
        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task An_http_error_fails_the_download()
    {
        var download = new PinnedDownload(new HttpClient(new StubHandler(Body, HttpStatusCode.NotFound)));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            download.GetAsync(new Uri("https://example.com/a.pdf"), Sha(Body), _cache, "a.pdf"));
    }

    private static string Sha(byte[] data) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(data));

    private sealed class StubHandler(byte[] body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent(body) });
        }
    }
}
```
`build/Build.Cake.Extensions.Tests/Corpus/ExternalCorpusManifestTests.cs`:
```csharp
using Build.Cake.Extensions.Corpus;

namespace Build.Cake.Extensions.Tests.Corpus;

public sealed class ExternalCorpusManifestTests
{
    private const string Sha = "2d420cbb4123dcf1fb82595b2359cfbb5d81f00b9df9d359fcc7af361d093f53";

    [Fact]
    public void Entries_are_read_with_every_field()
    {
        var entry = Assert.Single(ExternalCorpusManifest.Parse(Json("sample-form", Sha, "Public domain")));

        Assert.Equal(
            new ExternalCorpusEntry("sample-form", new Uri("https://example.com/form.pdf"), Sha, "Public domain", "Example source", "A form"),
            entry);
    }

    [Theory]
    [InlineData("sample-form", Sha, "", "Entry 1 has no \"license\".")]
    [InlineData("Sample Form", Sha, "Public domain", "Entry \"Sample Form\": the id must be lowercase letters, digits and hyphens.")]
    [InlineData("sample-form", "ABC", "Public domain", "Entry \"sample-form\": sha256 must be 64 lowercase hex digits.")]
    public void Invalid_entries_are_rejected(string id, string sha, string license, string expected)
    {
        var ex = Assert.Throws<InvalidDataException>(() => ExternalCorpusManifest.Parse(Json(id, sha, license)));

        Assert.Equal(expected, ex.Message);
    }

    [Fact]
    public void An_id_listed_twice_is_rejected()
    {
        var one = Json("sample-form", Sha, "Public domain").Trim().TrimStart('[').TrimEnd(']');

        var ex = Assert.Throws<InvalidDataException>(() => ExternalCorpusManifest.Parse($"[{one},{one}]"));

        Assert.Equal("Entry \"sample-form\" is listed twice.", ex.Message);
    }

    private static string Json(string id, string sha, string license) =>
        $$"""
        [{ "id": "{{id}}", "url": "https://example.com/form.pdf", "sha256": "{{sha}}", "license": "{{license}}",
           "source": "Example source", "purpose": "A form" }]
        """;
}
```

- [ ] **Step 3: Run the tests to see them fail**

Run: `dotnet build build/Build.Cake.Extensions.Tests/Build.Cake.Extensions.Tests.csproj`
Expected: build FAILS: `PinnedDownload`, `ExternalCorpusManifest` and `ExternalCorpusEntry` don't exist.

- [ ] **Step 4: Write the helpers**

`build/Build.Cake.Extensions/Downloads/PinnedDownload.cs`:
```csharp
using System.Security.Cryptography;

namespace Build.Cake.Extensions.Downloads;

// Downloads a file pinned by SHA-256 into <cache>/<sha256>/<fileName> and reuses it while its hash matches. A
// mismatch fails with both hashes and leaves nothing behind: the pin, not the server, decides what the build uses.
public sealed class PinnedDownload(HttpClient http)
{
    // Some servers refuse requests without a User-Agent.
    public static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PdfProof-build/1.0 (+https://github.com/mgnslndh/PdfProof)");
        return client;
    }

    public async Task<string> GetAsync(Uri url, string sha256, string cacheDirectory, string fileName)
    {
        var expected = sha256.ToLowerInvariant();
        var folder = Path.Combine(cacheDirectory, expected);
        var path = Path.Combine(folder, fileName);
        if (File.Exists(path) && Sha256Of(path) == expected)
        {
            return path;
        }

        Directory.CreateDirectory(folder);
        var temporary = path + ".download";
        try
        {
            using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                await using var file = File.Create(temporary);
                await response.Content.CopyToAsync(file);
            }

            var actual = Sha256Of(temporary);
            if (actual != expected)
            {
                throw new InvalidOperationException($"SHA-256 mismatch for {url}: expected {expected}, got {actual}.");
            }

            File.Move(temporary, path, overwrite: true);
            return path;
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }

            if (!File.Exists(path) && Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Directory.Delete(folder);
            }
        }
    }

    public static string Sha256Of(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
```

`build/Build.Cake.Extensions/Corpus/ExternalCorpusManifest.cs`:
```csharp
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
```

- [ ] **Step 5: Run the build-helper tests**

Run: `dotnet test --project build/Build.Cake.Extensions.Tests/Build.Cake.Extensions.Tests.csproj`
Expected: all tests pass.

- [ ] **Step 6: Add the build parameters and the `FetchCorpus` target**

Replace `build/Build/BuildParameters.cs`:
```csharp
// The build's inputs, read once in Setup from the command line (--name=value, or --flag for booleans).
public sealed record BuildParameters(
    string Configuration, DirectoryPath Artifacts, bool RequireFixtures, bool SkipGenerate, bool SkipFetch, string? Producers)
{
    public DirectoryPath Packages => Artifacts.Combine("packages");

    // GenerateFixtures writes here: <document>/<producer>.pdf and manifest.json.
    public DirectoryPath Corpus => Artifacts.Combine("corpus");

    // FetchCorpus writes here: <id>.pdf.
    public DirectoryPath External => Artifacts.Combine("external");

    // Pinned downloads (external PDFs, the Typst CLI), kept between runs and cached in CI.
    public DirectoryPath Cache => Artifacts.Combine("cache");

    public static BuildParameters Create(ICakeContext context) => new(
        context.Argument("configuration", "Release"),
        context.MakeAbsolute(context.Directory("artifacts")),
        context.HasArgument("require-fixtures"),
        context.HasArgument("skip-generate"),
        context.HasArgument("skip-fetch"),
        context.HasArgument("producers") ? context.Argument<string>("producers") : null);
}
```
In `build/Build/Build.csproj`, add to the `<Using>` item group:
```xml
    <Using Include="Build.Cake.Extensions.Corpus" />
    <Using Include="Build.Cake.Extensions.Downloads" />
```
`build/Build/Tasks/CorpusTasks.cs`:
```csharp
public static partial class Program
{
    // Downloads the external corpus (corpus/external/manifest.json) into artifacts/external/<id>.pdf, verifying each
    // file's SHA-256. Downloads are cached in artifacts/cache by hash, so a run without changes downloads nothing.
    static void RegisterCorpusTasks()
    {
        Task("FetchCorpus")
            .WithCriteria<BuildParameters>((_, parameters) => !parameters.SkipFetch, "--skip-fetch was given")
            .Does<BuildParameters>(async parameters =>
            {
                using var http = PinnedDownload.CreateHttpClient();
                var download = new PinnedDownload(http);
                EnsureDirectoryExists(parameters.External);
                foreach (var entry in ExternalCorpusManifest.Load("corpus/external/manifest.json"))
                {
                    var cached = await download.GetAsync(entry.Url, entry.Sha256, parameters.Cache.FullPath, entry.Id + ".pdf");
                    CopyFile(cached, parameters.External.CombineWithFilePath(entry.Id + ".pdf"));
                    Information("{0}: {1} ({2})", entry.Id, entry.Source, entry.License);
                }
            });
    }
}
```
In `build/Build/Program.cs`, call `RegisterCorpusTasks();` after `RegisterTestTasks();`.

- [ ] **Step 7: Write the failing IRS tests in `tests/PdfProof.Corpus.Tests/ExternalCorpusTests.cs`**

These values come from the probes (PdfPig 0.1.16). The W-9 full name follows the form designer's `topmostSubform[0].Page1[0].…` convention; the W-4 test matches only the partial name, because its intermediate subform names weren't visible in the probe.

```csharp
using PdfProof.Layout;

namespace PdfProof.Corpus.Tests;

// Real-world PDFs pinned in corpus/external/manifest.json and downloaded by FetchCorpus. Code assertions for now;
// approved exports come with Plan 6.
public sealed class ExternalCorpusTests
{
    private const string HowToGet = "Run ./build.ps1 --target=FetchCorpus.";

    [Fact]
    public void The_W9_is_a_tagged_six_page_form_with_nested_fields()
    {
        var doc = PdfProofDocument.Load(Fixtures.Require(CorpusPaths.External("irs-w9-2024"), HowToGet));

        Assert.Equal((6, "Form W-9 (Rev. March 2024)"), (doc.Metadata.PageCount, doc.Metadata.Title));
        Assert.True(doc.Structure.IsTagged);
        Assert.DoesNotContain(doc.Diagnostics.Entries, e => e.Code == PdfDiagnosticCodes.FormFieldsUnreadable);
        var fields = doc.Layout.Pages[0].FormFields;
        Assert.Equal(23, fields.Count);
        var name = Assert.Single(fields, f => f.Name == "topmostSubform[0].Page1[0].f1_01[0]");
        Assert.Equal((LayoutFormFieldKind.Text, (string?)null, new LayoutBox(58.6, 674, 576, 688)), (name.Kind, name.Value, name.Box));
        var box = Assert.Single(fields, f => f.Name.EndsWith(".c1_1[0]", StringComparison.Ordinal));
        Assert.Equal((LayoutFormFieldKind.Checkbox, (bool?)false), (box.Kind, box.IsChecked));
    }

    [Fact]
    public void The_W4_is_a_tagged_five_page_form()
    {
        var doc = PdfProofDocument.Load(Fixtures.Require(CorpusPaths.External("irs-w4-2026"), HowToGet));

        Assert.Equal((5, "2026 Form W-4"), (doc.Metadata.PageCount, doc.Metadata.Title));
        Assert.True(doc.Structure.IsTagged);
        var fields = doc.Layout.Pages[0].FormFields;
        Assert.Equal(48, fields.Count);
        var first = Assert.Single(fields, f => f.Name.EndsWith(".f1_01[0]", StringComparison.Ordinal));
        Assert.Equal((LayoutFormFieldKind.Text, new LayoutBox(94.6, 698, 272.9, 712)), (first.Kind, first.Box));
    }
}
```

- [ ] **Step 8: Fetch the corpus and run the tests**

Run: `./build.ps1 --target=FetchCorpus`
Expected: two lines (`irs-w4-2026: …`, `irs-w9-2024: …`), and `artifacts/external/irs-w9-2024.pdf` and `irs-w4-2026.pdf` exist. A second run downloads nothing: the files come from `artifacts/cache`.

Run: `dotnet test --project tests/PdfProof.Corpus.Tests/PdfProof.Corpus.Tests.csproj`
Expected: all tests pass on net48 and net10.0.

If the W-9 full name differs from `topmostSubform[0].Page1[0].f1_01[0]`, check what the PDF's `/T` entries say:
- If they agree with what the reader produced, the test's expectation was wrong: fix it.
- If they don't agree, the walk in `FormFieldReader` is wrong: add a failing `FormFieldTests` case for that shape first.


- [ ] **Step 9: Commit**

```bash
git add corpus/external build tests/PdfProof.Corpus.Tests
git commit -m "Add the external corpus: IRS W-9 and W-4 pinned by URL and SHA-256

FetchCorpus downloads every manifest entry through a pinned, cached download and
fails on a hash mismatch. The manifest requires a license per entry. Corpus
tests check the forms' pages, titles, tagging and form fields.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: `GenerateFixtures`, the pinned Typst CLI and the `Default` chain

**Files:**
- Create: `build/Build.Cake.Extensions/Typst/TypstCli.cs`, `build/Build.Cake.Extensions.Tests/Typst/TypstCliTests.cs`, `build/Build/Tasks/FixtureTasks.cs`
- Modify: `build/Build/Tasks/TestTasks.cs`, `build/Build/Tasks/DefaultTasks.cs`, `build/Build/Program.cs`, `build/Build/Build.csproj`

**Interfaces:**
- Consumes: `PinnedDownload` and `BuildParameters` (Task 9), the generator's command line (Task 4).
- Produces:
  - `Build.Cake.Extensions.Typst.TypstCli` with:
    - `const string Version = "0.15.1"`;
    - `TypstAsset AssetFor(OSPlatform os, Architecture architecture)`, `Url(TypstAsset)`;
    - `Task<string> EnsureAsync(PinnedDownload download, string cacheDirectory)`, which returns the executable's path.
  - `sealed record TypstAsset(string FileName, string Sha256, string Executable)`.
  - The target `GenerateFixtures`.
  - `Test` sets `PDFPROOF_REQUIRE_FIXTURES=true` when `--require-fixtures` is given.
  - `Default` runs `GenerateFixtures → FetchCorpus → Build → Test → Pack`.

- [ ] **Step 1: Write the failing tests in `build/Build.Cake.Extensions.Tests/Typst/TypstCliTests.cs`**

```csharp
using System.Runtime.InteropServices;
using Build.Cake.Extensions.Typst;

namespace Build.Cake.Extensions.Tests.Typst;

public sealed class TypstCliTests
{
    [Fact]
    public void Windows_and_Linux_x64_have_pinned_release_assets()
    {
        Assert.Equal(
            new TypstAsset("typst-x86_64-pc-windows-msvc.zip", "19ce3551153c2fe7ee9fa2f95208310c8f4d3209fedb699e0333faf8913f6736", "typst-x86_64-pc-windows-msvc/typst.exe"),
            TypstCli.AssetFor(OSPlatform.Windows, Architecture.X64));
        Assert.Equal(
            new TypstAsset("typst-x86_64-unknown-linux-musl.tar.xz", "a6d077d0a95eed5a2eba715b2dae06be954f624ccbf85758a03f389ded33118c", "typst-x86_64-unknown-linux-musl/typst"),
            TypstCli.AssetFor(OSPlatform.Linux, Architecture.X64));
    }

    [Fact]
    public void The_asset_url_is_the_pinned_github_release()
    {
        Assert.Equal(
            new Uri("https://github.com/typst/typst/releases/download/v0.15.1/typst-x86_64-pc-windows-msvc.zip"),
            TypstCli.Url(TypstCli.AssetFor(OSPlatform.Windows, Architecture.X64)));
    }

    [Fact]
    public void Other_platforms_are_told_how_to_use_their_own_Typst()
    {
        var ex = Assert.Throws<PlatformNotSupportedException>(() => TypstCli.AssetFor(OSPlatform.OSX, Architecture.Arm64));

        Assert.Equal("No pinned Typst CLI for OSX Arm64. Install Typst 0.15.1 and put it on PATH; the generator then uses it.", ex.Message);
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet build build/Build.Cake.Extensions.Tests/Build.Cake.Extensions.Tests.csproj`
Expected: build FAILS: `TypstCli` and `TypstAsset` don't exist.

- [ ] **Step 3: Write `build/Build.Cake.Extensions/Typst/TypstCli.cs`**

```csharp
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Build.Cake.Extensions.Downloads;

namespace Build.Cake.Extensions.Typst;

public sealed record TypstAsset(string FileName, string Sha256, string Executable);

// The Typst CLI the fixture generator uses, pinned by version and per-platform SHA-256 (spec §10.2; OQ-2: Typstium is
// not on a package feed). Release archives hold one folder named like the archive, with the executable inside.
public static class TypstCli
{
    public const string Version = "0.15.1";

    public static TypstAsset AssetFor(OSPlatform os, Architecture architecture)
    {
        if (os == OSPlatform.Windows && architecture == Architecture.X64)
        {
            return new TypstAsset(
                "typst-x86_64-pc-windows-msvc.zip",
                "19ce3551153c2fe7ee9fa2f95208310c8f4d3209fedb699e0333faf8913f6736",
                "typst-x86_64-pc-windows-msvc/typst.exe");
        }

        if (os == OSPlatform.Linux && architecture == Architecture.X64)
        {
            return new TypstAsset(
                "typst-x86_64-unknown-linux-musl.tar.xz",
                "a6d077d0a95eed5a2eba715b2dae06be954f624ccbf85758a03f389ded33118c",
                "typst-x86_64-unknown-linux-musl/typst");
        }

        throw new PlatformNotSupportedException(
            $"No pinned Typst CLI for {os} {architecture}. Install Typst {Version} and put it on PATH; the generator then uses it.");
    }

    public static Uri Url(TypstAsset asset) => new($"https://github.com/typst/typst/releases/download/v{Version}/{asset.FileName}");

    // Downloads (or reuses) the pinned archive for this machine and extracts it once; returns the executable's path.
    public static async Task<string> EnsureAsync(PinnedDownload download, string cacheDirectory)
    {
        var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OSPlatform.Windows
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? OSPlatform.Linux
            : OSPlatform.OSX;
        var asset = AssetFor(os, RuntimeInformation.OSArchitecture);
        var archive = await download.GetAsync(Url(asset), asset.Sha256, cacheDirectory, asset.FileName);
        var folder = Path.Combine(Path.GetDirectoryName(archive)!, "extracted");
        var executable = Path.Combine(folder, asset.Executable);
        if (!File.Exists(executable))
        {
            Directory.CreateDirectory(folder);
            if (asset.FileName.EndsWith(".zip", StringComparison.Ordinal))
            {
                ZipFile.ExtractToDirectory(archive, folder, overwriteFiles: true);
            }
            else
            {
                // .tar.xz: the BCL reads tar but not xz, so the system tar extracts it.
                Extract("tar", ["-xJf", archive, "-C", folder]);
            }
        }

        return executable;
    }

    private static void Extract(string tool, IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo(tool) { UseShellExecute = false, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"{tool} did not start.");
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{tool} failed (exit code {process.ExitCode}): {error.Trim()}");
        }
    }
}
```

- [ ] **Step 4: Run the build-helper tests**

Run: `dotnet test --project build/Build.Cake.Extensions.Tests/Build.Cake.Extensions.Tests.csproj`
Expected: all tests pass.

- [ ] **Step 5: Add the target, the test variable and the chain**

In `build/Build/Build.csproj`, add `<Using Include="Build.Cake.Extensions.Typst" />`.

`build/Build/Tasks/FixtureTasks.cs`:
```csharp
public static partial class Program
{
    // Renders corpus/generated with every producer available on this machine into artifacts/corpus
    // (tools/PdfProof.FixtureGenerator). Typst is the pinned CLI; on a platform without a pin, the generator uses typst
    // from PATH. --producers=a,b renders only those producers.
    static void RegisterFixtureTasks()
    {
        Task("GenerateFixtures")
            .WithCriteria<BuildParameters>((_, parameters) => !parameters.SkipGenerate, "--skip-generate was given")
            .Does<BuildParameters>(async parameters =>
            {
                var arguments = new ProcessArgumentBuilder().Append("--output").AppendQuoted(parameters.Corpus.FullPath);
                try
                {
                    using var http = PinnedDownload.CreateHttpClient();
                    var typst = await TypstCli.EnsureAsync(new PinnedDownload(http), parameters.Cache.FullPath);
                    arguments.Append("--typst").AppendQuoted(typst);
                }
                catch (PlatformNotSupportedException ex)
                {
                    Warning(ex.Message);
                }

                if (parameters.Producers is { } producers)
                {
                    arguments.Append("--producers").AppendQuoted(producers);
                }

                DotNetRun(
                    "tools/PdfProof.FixtureGenerator/PdfProof.FixtureGenerator.csproj",
                    arguments,
                    new DotNetRunSettings { Configuration = parameters.Configuration });
            });
    }
}
```

Replace the body of `RegisterTestTasks` in `build/Build/Tasks/TestTasks.cs`:
```csharp
    // Every test project in the solution; on Windows that includes the net48 runs. With --require-fixtures, a missing
    // corpus fixture fails its test instead of skipping it (PDFPROOF_REQUIRE_FIXTURES, always set in CI).
    static void RegisterTestTasks()
    {
        Task("Test")
            .IsDependentOn("Build")
            .Does<BuildParameters>(parameters =>
                DotNetTest("PdfProof.slnx", new DotNetTestSettings
                {
                    PathType = DotNetTestPathType.Solution,
                    Configuration = parameters.Configuration,
                    NoBuild = true,
                    EnvironmentVariables = parameters.RequireFixtures
                        ? new Dictionary<string, string?> { ["PDFPROOF_REQUIRE_FIXTURES"] = "true" }
                        : new Dictionary<string, string?>(),
                }));
    }
```
If Cake's `EnvironmentVariables` is typed `IDictionary<string, string>` (no nullable value), drop the `?`.

Replace `RegisterDefaultTask` in `build/Build/Tasks/DefaultTasks.cs`:
```csharp
    // What CI runs on every platform, and what a local ./build.ps1 or ./build.sh runs: fixtures, external corpus,
    // build, tests, packages. --skip-generate and --skip-fetch leave out the first two (CI's build jobs get the
    // fixtures from the fixtures job).
    static void RegisterDefaultTask()
    {
        Task("Default")
            .IsDependentOn("GenerateFixtures")
            .IsDependentOn("FetchCorpus")
            .IsDependentOn("Test")
            .IsDependentOn("Pack");
    }
```

Update `build/Build/Program.cs`:
```csharp
// Builds, tests and packs PdfProof exactly as CI does; CI runs this build, so the two can't drift apart.
// Arguments: --target=<task> (Default = GenerateFixtures, FetchCorpus, BuildTests, Build, Test, Pack),
// --configuration=<c> (Release), --require-fixtures, --skip-generate, --skip-fetch, --producers=<a,b>.
var target = Argument("target", "Default");

Setup(context => BuildParameters.Create(context));

RegisterBuildTasks();
RegisterTestTasks();
RegisterFixtureTasks();
RegisterCorpusTasks();
RegisterPackTasks();
RegisterDefaultTask();

RunTarget(target);
```

- [ ] **Step 6: Run the targets**

Run: `./build.ps1 --target=GenerateFixtures`
Expected:
- the Typst archive is downloaded into `artifacts/cache/19ce…/` and extracted;
- the generator runs, and `artifacts/corpus/manifest.json` lists `typst` with version `typst 0.15.1 (…)` and seven `generated` producers.

A second run downloads nothing.

Run: `./build.ps1 --skip-generate --require-fixtures`
Expected: FetchCorpus, BuildTests, Build, Test (net48 + net10.0) and Pack succeed. The corpus matrix runs against the fixtures, and `libreoffice` is skipped.

Run: `./build.ps1`
Expected: the whole chain succeeds.

- [ ] **Step 7: Commit**

```bash
git add build
git commit -m "Add GenerateFixtures with a pinned Typst CLI, and run the corpus in the default build

The build downloads Typst 0.15.1 by SHA-256 for Windows and Linux x64 and passes
it to the generator. --require-fixtures sets PDFPROOF_REQUIRE_FIXTURES for the
tests; Default now runs GenerateFixtures, FetchCorpus, Test and Pack.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: CI fixtures job and `AGENTS.md`

**Files:**
- Modify: `.github/workflows/ci.yml`, `AGENTS.md`

**Interfaces:**
- Consumes: the Cake targets and flags (Tasks 9–10).
- Produces:
  - a `fixtures` job (windows-latest) that uploads the artifact `fixtures`;
  - the `build` jobs need `fixtures`, download it into `artifacts/corpus`, and run `--skip-generate --require-fixtures`.

- [ ] **Step 1: Replace `.github/workflows/ci.yml`**

The action pins:
- `actions/checkout`, `actions/setup-dotnet` and `actions/upload-artifact`: as before;
- `actions/cache` v6.1.0: `55cc8345863c7cc4c66a329aec7e433d2d1c52a9`;
- `actions/download-artifact` v8.0.2: `9000827ccba6bdab643e8b6fd33ac0654aef8333`.

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:

permissions:
  contents: read

jobs:
  fixtures:
    # Renders the generated corpus with every producer. SelectPdf needs Windows; the build jobs on every OS test
    # against these PDFs. Cached by the sources, the generator and the pinned producer versions.
    name: fixtures
    runs-on: windows-latest
    env:
      DOTNET_NOLOGO: 'true'
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
      - uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          global-json-file: global.json
      - id: cache
        uses: actions/cache@55cc8345863c7cc4c66a329aec7e433d2d1c52a9 # v6.1.0
        with:
          path: artifacts/corpus
          key: fixtures-${{ hashFiles('corpus/generated/**', 'tools/**', 'Directory.Packages.props', 'build/Build.Cake.Extensions/Typst/TypstCli.cs') }}
      - if: steps.cache.outputs.cache-hit != 'true'
        run: ./build.ps1 --target=GenerateFixtures
        shell: pwsh
      - uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
        with:
          name: fixtures
          path: artifacts/corpus
          if-no-files-found: error
          retention-days: 7

  build:
    name: build (${{ matrix.os }})
    needs: fixtures
    strategy:
      fail-fast: false
      matrix:
        os: [windows-latest, ubuntu-latest]
    runs-on: ${{ matrix.os }}
    env:
      DOTNET_NOLOGO: 'true'
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with:
          # MinVer computes the version from tags and history.
          fetch-depth: 0
      - uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          global-json-file: global.json
      - uses: actions/download-artifact@9000827ccba6bdab643e8b6fd33ac0654aef8333 # v8.0.2
        with:
          name: fixtures
          path: artifacts/corpus
      # The external corpus is pinned by SHA-256; the cache saves downloading it on every run.
      - uses: actions/cache@55cc8345863c7cc4c66a329aec7e433d2d1c52a9 # v6.1.0
        with:
          path: artifacts/cache
          key: external-${{ hashFiles('corpus/external/manifest.json') }}
      # The same steps as a local run, without rendering: external corpus, build tests, build, tests (net48 + net10.0
      # on Windows) with every fixture required, pack and package checks. Each platform uses its own bootstrap.
      - if: runner.os == 'Windows'
        run: ./build.ps1 --skip-generate --require-fixtures
        shell: pwsh
      - if: runner.os != 'Windows'
        run: ./build.sh --skip-generate --require-fixtures
        shell: bash
      - if: runner.os == 'Windows'
        uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
        with:
          name: packages
          path: artifacts/packages/*
          if-no-files-found: error
          retention-days: 7
```

- [ ] **Step 2: Update `AGENTS.md`**

Replace the `## Commands` code block with:
````markdown
```bash
./build.ps1                      # or ./build.sh: fixtures, external corpus, build tests, build, test, pack (what CI runs)
./build.ps1 --target=Test        # build and test only (corpus tests skip without fixtures)
./build.ps1 --target=GenerateFixtures [--producers=typst,selectpdf]  # render corpus/generated → artifacts/corpus
./build.ps1 --target=FetchCorpus # download the external corpus (pinned by SHA-256) → artifacts/external
./build.ps1 --skip-generate --skip-fetch --require-fixtures  # flags: skip the first two steps; fail on missing fixtures
dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj   # one test project
# record new public API (run after adding public members; then rebuild and review the appended lines)
dotnet build src/PdfProof/PdfProof.csproj 2>&1 | sed -n "s/.*error RS0016: Symbol '\([^']*\)' is not part of the declared public API.*/\1/p" | sort -u >> src/PdfProof/PublicAPI.Unshipped.txt
```

On Windows, tests run on net48 and net10.0; elsewhere on net10.0 only. SelectPdf fixtures can only be generated on
Windows; LibreOffice fixtures only where LibreOffice is installed (best effort).
````

In the `## Layout` table, replace the `tests/` row with these rows:
```markdown
| `tests/` | xUnit v3 on Microsoft.Testing.Platform; `PdfProof.Corpus.Tests` runs the corpus matrix and the external corpus |
| `tools/PdfProof.FixtureGenerator` | Renders corpus documents with each producer (SelectPdf, Chromium, Typst, QuestPDF, MigraDoc, LibreOffice); not packed |
| `corpus/generated/<document>/` | Sources (`.html`, `.typ`), the oracle `expected.semantics.json`, `deviations.json`; the C# versions are in the generator's `Documents/` |
| `corpus/external/manifest.json` | External PDFs: id, url, sha256, license, source, purpose |
```

In `## Rules`, replace the **Test PDFs** rule with:
```markdown
- **Test PDFs:** build them in code. Use `tests/PdfProof.Tests/RawPdf.cs` (raw PDF syntax: exact positions, clipping, rotation, annotations, outlines, marked content and structure trees: `RawPdf.Marked`, `RawPdf.Tag`; form fields: `RawPdf.Widget`, `RawPdf.FormFields`), PdfPig's `PdfDocumentBuilder`, or `EncryptedPdf.Encrypt` (PDFsharp, test-only) for encrypted PDFs. Don't commit generated PDF files.
```
Replace the **Corpus** rule with:
```markdown
- **Corpus:** generated PDFs are never committed; they go to `artifacts/`. Corpus sources use neutral data and fixed dates. A new producer gets a row in `tests/PdfProof.Corpus.Tests/CorpusProducer.cs` under the generator's name; a known producer gap goes in the document's `deviations.json` with a one-line reason, never into a looser oracle. External PDFs are listed with URL, SHA-256 and license, at a URL that doesn't change.
```

- [ ] **Step 3: Check the workflow and the whole build**

Run: `./build.ps1`
Expected: the whole chain succeeds locally.

Check the workflow file for indentation and the five action pins:
```bash
grep -n "uses:" .github/workflows/ci.yml
```
Expected: the checkout, setup-dotnet, cache, download-artifact and upload-artifact pins listed above, each with its version comment.

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/ci.yml AGENTS.md
git commit -m "Run a Windows fixtures job in CI and test every OS against its fixtures; update AGENTS.md

The fixtures job renders the corpus (cached by sources and pinned producers) and
uploads it; the build jobs download it, fetch the external corpus and run the
tests with PDFPROOF_REQUIRE_FIXTURES=true. AGENTS.md documents the new targets,
flags, projects and corpus rules.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Self-review notes

- **Spec coverage:**

  | Spec item | Task |
  |---|---|
  | §5.3 (amended): profiles as settings, options choose one, null profile = detect, password with detection | 1, 2 |
  | §5.2 step 2: detection from /Producer, then /Creator, checked against real producer strings | 1 (unit), 8 (real fixtures) |
  | §5.4: encrypted with no or wrong password gives a clear `PdfProofException` | 2 |
  | §5.1: form fields in the layout model (one per widget, on its page) | 3 |
  | §10.1, §10.2: generator, `invoice` sources and C# versions, fixed dates, `artifacts/corpus` with `manifest.json`, never committed | 4, 5, 6 |
  | §10.2 v1 producers: SelectPdf, Chromium tagged/untagged, Typst, QuestPDF tagged/untagged, PDFsharp/MigraDoc, LibreOffice best effort | 5, 6 |
  | §10.2: oracle, matrix (pass, known deviation, fail; a fixed deviation fails), text normalized, omitted properties ignored | 7, 8 |
  | §10.2: a missing fixture skips; `PDFPROOF_REQUIRE_FIXTURES=true` fails | 8, 10 |
  | §10.3: manifest with id/url/sha256/license/source/purpose, download into a cache, fail on hash mismatch, W-9 and W-4 | 9 |
  | §11.1: `GenerateFixtures`, `FetchCorpus`, `--require-fixtures`, `--producers`, `--skip-generate`, `--skip-fetch`; `Default` chain; pinned downloads and producer helpers in `Build.Cake.Extensions` | 9, 10 |
  | §11.2: `fixtures` job on windows-latest, cached, uploaded; test jobs download and run with `PDFPROOF_REQUIRE_FIXTURES=true` | 11 |
  | §12: `PdfProof.Corpus.Tests` on net48 and net10.0 | 7 |
  | §16.4: `AGENTS.md` updated with the new commands, projects and rules | 11 |

  Deliberately not in this plan:
  - Approved exports (`approved/<producer>.md|json`) and identical output across cultures and OS: Plan 6, which has the exporters.
  - `docs/corpus.md`: generated from approved outputs, so Plan 6 or 7.
  - The other v1 documents (`report`, `long-table`, `two-column`, `form`, `letter`, `special-text`): a later plan; the generator and the matrix take new document folders without code changes, except for C# versions.
  - The Gutenberg and arXiv external entries: added with a confirmed license per entry.
- **Placeholders:** none. Task 4's `Producers.All` returns an empty list until Task 5 fills it; that interim state is stated in both tasks.
- **Type consistency:**
  - Producer names are the same in `Producers.All` (Tasks 5–6), `ProducerTests`, and `CorpusProducer.All`: `chromium-tagged`, `chromium-untagged`, `libreoffice`, `migradoc`, `questpdf-tagged`, `questpdf-untagged`, `selectpdf`, `typst`.
  - `PdfDiagnostics.Profile` is a `PdfProfile`, compared by `Name` in the corpus tests.
  - `CorpusPaths.Fixture` matches the generator's `<output>/<document>/<producer>.pdf`, and `CorpusPaths.External` matches `FetchCorpus`'s `artifacts/external/<id>.pdf`.
  - `BuildParameters.Corpus` is the generator's `--output`.
- **Review Focus:** each of the five lines has its test in the owning task:
  1. Task 2, `The_password_never_appears_in_diagnostics_logs_or_the_options`;
  2. Task 3, `A_damaged_form_is_left_out_and_the_document_still_loads`;
  3. Task 3, `A_widget_without_a_page_is_left_out_and_counted`;
  4. Task 4, `A_previous_fixture_never_stands_in_for_a_producer_that_is_unavailable_or_fails`;
  5. Task 8, `A_fixture_that_is_not_a_readable_pdf_becomes_a_mismatch_naming_the_file`.
