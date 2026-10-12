# PdfProof v1: design

| | |
|---|---|
| Status | Draft for review |
| Date | 2026-10-11 |
| Author | Magnus Lindhe (with Claude) |
| Scope | Everything up to and including 1.0.0 |
| License | MIT, open source from the first commit |

---

## 1. Purpose

PdfProof makes **generated PDFs testable**. It reads a PDF with [PdfPig](https://github.com/UglyToad/PdfPig) and
gives three views of it: layout (geometry), structure (the Tagged PDF tree) and **semantics** (headings, paragraphs,
tables, lists, figures, links). On top of that it offers:

- **Assertions** on meaning: `PdfAssert.That(doc).HasHeading("Terms").FollowedBy.Paragraph(containing: "30 days")`.
  They work in any test framework.
- **Snapshots**: deterministic, scrubbed Markdown and JSON that work with plain [Verify](https://github.com/VerifyTests/Verify).
- **Lint**: generic invariants that run on every generated document.
- **A CLI** that shows what the library sees.

**The primary use case:** an application generates a PDF on every test run, from HTML through a converter such as
SelectPdf, from Typst, or from a PDF library, and tests assert on the result. A test suite written against one
producer must keep passing, with few changes, after moving to another producer (for example from SelectPdf to
Typst). This requirement shapes most decisions below.

### Non-goals (v1)

- OCR of scanned PDFs. They are detected and reported (`NoTextLayer`), not read.
- Visual or pixel comparison.
- General "any document → Markdown for LLMs" conversion. Fidelity to what is visible and testable wins over prose
  quality.
- Creating, editing or repairing PDFs.

## 2. Evidence from the spike

An earlier throwaway spike showed that the approach works. Its findings shape this design:

| # | Finding | Consequence |
|---|---|---|
| E1 | The same PDF always produces identical output. Generated PDFs differ between runs in dates, IDs and printed values. | Determinism and scrubbing are core features (§7). |
| E2 | Volatile text has volatile geometry: a GUID changes its line's width. | Scrubbing text also scrubs the box of its element (§7.1). |
| E3 | SelectPdf (WebKit) draws the whole HTML document on every page and clips each page to its slice. | Clipping is on by default; images entirely off-page are dropped. |
| E4 | SelectPdf extracts every `-` as U+00AD (soft hyphen). | Text normalization is on by default. |
| E5 | No single layout heuristic handles both tables (row-wise) and multi-column pages (column-wise). | Pluggable layout strategies (`Rows`, `ReadingOrder`). |
| E6 | PdfPig's orientation label and rotated boxes are unreliable for vertical text. | Reading direction comes from baselines. |
| E7 | Tagged PDF gives exact semantics. QuestPDF tags with one setting; SelectPdf tags only with its Blink/Chromium engine and a commercial license. | The structure tree is a first-class source (§6). |
| E8 | Markdown snapshots are much easier to review than JSON. | Markdown is the main snapshot format. |
| E9 | Existing .NET PDF→Markdown converters are thin PdfPig wrappers; none handled SelectPdf output or kept links. | Build on PdfPig directly. |
| E10 | Verify 33.x requires a SponsorCheck license; 32.0.1 is the last MIT release. | No Verify integration package before v1 (§3). |

## 3. Decisions

| # | Decision | Choice |
|---|---|---|
| D1 | Packages in v1 | `PdfProof`, `PdfProof.Testing`, `PdfProof.Cli` (dotnet tool `pdfproof`) |
| D2 | After v1 | `PdfProof.Verify` (`VerifyPdf`, `.pdf` converter), `PdfProof.Testing.TUnit` (native `await Assert.That(doc)…`), `PdfProof.Markdown.Matching` (Markdown fragments as sequence patterns, Markdig) |
| D3 | Target frameworks | Libraries: `netstandard2.0;net8.0;net10.0`. `netstandard2.0` covers .NET Framework 4.8, and tests run on net48. CLI: `net8.0;net10.0`. |
| D4 | Root type | `PdfProofDocument`: the only thing you load. It is immutable, thread-safe, not `IDisposable`, and used by the core, CLI and tests alike. |
| D5 | Model names | `LayoutModel`, `StructureModel`, `SemanticModel`; element types carry their layer as a prefix (`LayoutLine`, `StructureNode`, `SemanticHeading`). |
| D6 | Untagged PDFs | `StructureModel` is never null. Its `Root` is empty and `IsTagged` is `false`. |
| D7 | Semantic model | Built by a fixed pipeline of named passes over a shared candidate-block list, from either the structure or the layout front end (§6). |
| D8 | Pass extensibility | Passes are configurable and can be switched off, but are not public extension points in v1. `ILayoutStrategy` is public. |
| D9 | Export | Scrub once, render twice: Markdown and JSON are rendered from the same scrubbed export tree (§7). |
| D10 | Default JSON | Metadata + semantics, without geometry. `ToJson(JsonExportOptions)` can add geometry, layout and structure. |
| D11 | Assertions | `PdfAssert.That(…)`, fluent, evaluated at every step on candidate sets. Chaining works in either order (§8). |
| D12 | Test frameworks | No adapter packages in v1. `PdfAssertionException` implements an `IAssertionException` declared in PdfProof (xUnit v3 matches it by name; verified first, §8.7). |
| D13 | Sequence patterns | In v1, as a builder. The Markdown-fragment front end comes after v1. |
| D14 | Logging | `Microsoft.Extensions.Logging.Abstractions` (`ILogger`, optional). |
| D15 | CLI | Spectre.Console.Cli and Spectre.Console, not System.CommandLine. Commands are thin and delegate to services. |
| D16 | Corpus | Generated by a fixture generator in the build. Generated PDFs are not committed. External PDFs are downloaded and pinned by SHA-256 (§10). |
| D17 | Build | Cake.Sdk (project-based) with `build.ps1` / `build.sh` bootstraps. CI runs the same targets (§11). |
| D18 | Versioning | MinVer (tag-based). Pre-releases to nuget.org from `0.1.0-alpha`. |
| D19 | Naming in the repo | Only neutral sample data (e.g. "Consulting, senior", `example.com`). No real company or customer names. |
| D20 | Agent skill for users | One Agent Skills `SKILL.md` skill teaching agents (Claude Code, Codex, Copilot) to test PDFs with PdfProof. It ships inside the CLI and is managed with `pdfproof skill install\|status\|uninstall` (§16). |
| D21 | Agent guidance for contributors | `AGENTS.md` at the repo root (read by Codex and Copilot); `CLAUDE.md` imports it, so there is one source of truth (§16.4). |
| D22 | After v1 | A Claude Code plugin marketplace in this repo (`/plugin marketplace add mgnslndh/PdfProof`) |

## 4. Architecture

### 4.1 Packages

```
PdfProof                 PdfPig, Microsoft.Extensions.Logging.Abstractions
  ▲
  ├── PdfProof.Testing   PdfProof
  │     ▲
  └── PdfProof.Cli       PdfProof, PdfProof.Testing, Spectre.Console.Cli, Spectre.Console
```

| Package | Contents |
|---|---|
| `PdfProof` | `PdfProofDocument`, options and profiles, layout model and strategies, structure reader, semantic passes, export tree, scrubbing, JSON and Markdown exporters, `VolatilityDetector` |
| `PdfProof.Testing` | Matching engine, `PdfAssert` API, narrowing sets, relations, sequence matcher, invariant rules, failure messages |
| `PdfProof.Cli` | `pdfproof export`, `lint`, `explain` |

### 4.2 Repository layout

```
AGENTS.md, CLAUDE.md                guidance for agents working on this repo (§16.4)
build.ps1, build.sh                 bootstraps (§11)
global.json                         SDK + msbuild-sdks: Cake.Sdk
Directory.Build.props / .targets    shared settings; packs assets/branding/brand/png/pdfproof-icon-128.png as icon.png
Directory.Packages.props            central package management, all versions pinned
PdfProof.slnx
src/
  PdfProof/                         Layout/ Structure/ Semantics/ (Passes/) Export/ Scrubbing/ Profiles/
  PdfProof.Testing/
  PdfProof.Cli/
tests/
  PdfProof.Tests/
  PdfProof.Testing.Tests/
  PdfProof.Corpus.Tests/
  PdfProof.Cli.Tests/
  PdfProof.Skill.Tests/             compiled sources of every C# snippet in the skill (§16.3)
skills/
  pdfproof/                         the agent skill: SKILL.md + references/ (§16)
tools/
  PdfProof.FixtureGenerator/        not packed
corpus/
  generated/<document>/             sources, expected.semantics.json, deviations.json, approved/
  external/                         manifest.json, approved/
build/                              Cake build (§11)
assets/branding/                    logo, icon, palette, render script
docs/
  schema/pdfproof-v1.json
  corpus.md                         generated "what to expect" page
```

### 4.3 Namespaces

`PdfProof` (entry points, options, profiles), `PdfProof.Layout`, `PdfProof.Structure`, `PdfProof.Semantics`,
`PdfProof.Export`, `PdfProof.Scrubbing`, `PdfProof.Testing`. Every public type name is unique across these
namespaces, so they can all be imported together.

## 5. Core: loading and the three models

### 5.1 Root type

```
PdfProofDocument
 ├─ Metadata     PdfMetadata       version, info dictionary (standard + custom), dates as DateTimeOffset?, page count
 ├─ Layout       LayoutModel       Pages → LayoutPage → LayoutBlock → LayoutLine → LayoutSpan;
 │                                 LayoutLink, LayoutImage, LayoutAnnotation, LayoutFormField (one per widget, on its
 │                                 page), bookmarks, fonts (embedded?)
 ├─ Structure    StructureModel    IsTagged, Root: StructureNode → Children (role after RoleMap, text, alt, actual
 │                                 text, lang, link targets, page)
 ├─ Semantics    SemanticModel     Source (Declared|Inferred), Blocks, Sections, Artifacts, Decisions (the decision
 │                                 log: pass, page, block, message); navigation helpers
 │                                 Headings, Paragraphs, Tables, Lists, Figures, Links
 └─ Diagnostics  PdfDiagnostics    profile used, decision log, warnings (e.g. NoTextLayer, clipped letter counts)
```

`StructureModel.IsTagged` is `true` when the document has a structure tree with at least one element. It doesn't
depend on the `/MarkInfo /Marked` flag; when the two disagree, `Diagnostics` notes it. `IsTagged` lives only here, and
the JSON `metadata.isTagged` is computed from it.

Coordinates are PDF user space (points, origin bottom-left). Boxes are `[left, bottom, right, top]`, rounded to a
configurable precision (default 0.1 pt), with `-0` normalized.

### 5.2 Loading pipeline

```
PdfProofDocument.Load(source, options?)        source: byte[] | Stream | file path | PdfPig PdfDocument
  1 Open         PdfPig; password from the options; ClipPaths when clipping is on
  2 Profile      the options' profile, or none given → detected from /Producer, then /Creator
  3 Layout       per page: letters → clip filter → normalization → words → ILayoutStrategy → blocks/lines/spans;
                 links, images (dropped when entirely off-page), annotations; rounding. Letters are discarded per page.
  4 Structure    structure tree reader → StructureModel
  5 Semantics    front end + passes (§6) → SemanticModel
  6 Diagnostics  every step writes to the decision log
```

- **Eager, page by page, then close.** PdfPig is closed before `Load` returns. Memory stays bounded for 500+ page
  documents because only models are kept.
- **Layout strategies:** `Rows` (default; deterministic; keeps table rows together) and `ReadingOrder` (Docstrum +
  reading-order detection; multi-column pages). Custom strategies implement `ILayoutStrategy`.
- **Rotated text** is grouped in its own frame, with reading direction from baselines, and the block carries an
  `Orientation`.
- **Fonts:** the subset prefix (`ABCDEF+`) is stripped. `IsBold` / `IsItalic` come from descriptor flags first, then
  the name. Whether a font is embedded is recorded, for rule PP009.
- **Text normalization** (default on): soft and Unicode hyphens become `-`, special spaces become a space, ligatures are
  expanded, zero-width characters are removed. Configurable and extensible with custom maps.

### 5.3 Options and profiles

```csharp
// How to read a PDF from a given producer: a named, complete set of settings.
public sealed record PdfProfile
{
    public static PdfProfile Default { get; }         // also SelectPdf, QuestPdf, Typst, Chromium
    public string Name { get; init; }
    public LayoutOptions Layout { get; init; }        // Strategy, ApplyClipping, Normalization, Precision, ColumnGapFactor, …
    public SemanticOptions Semantics { get; init; }   // Source (Auto|Structure|Layout), one options record per pass
}

// How to load this file.
public sealed record PdfProofOptions
{
    public PdfProfile? Profile { get; init; }         // null: detected from /Producer, then /Creator
    public string? Password { get; init; }
    public ILogger? Logger { get; init; }

    public static PdfProofOptions Default { get; }    // everything null: detect, no password, no logging
}
```

- **A profile is the settings, options choose one.** The settings come entirely from one profile: the detected one or
  the one given. They are never merged. A preset is adjusted with `with { … }`
  (`PdfProfile.SelectPdf with { Layout = … }`), and `Name` keeps saying which preset it started from.
- **Per-file inputs are not part of a profile.** The password belongs to a PDF, not to a producer, so it is a load
  option, as in PdfPig's `ParsingOptions` and iText's `ReaderProperties`. It works with detection:
  `Load(pdf, new() { Password = "…" })`.
- `Load(pdf)` is `Load(pdf, PdfProofOptions.Default)`. `Diagnostics` records the profile and how it was chosen:
  detected (naming the entry that matched), from options, or from options and adjusted.
- **v1 profiles:** `Default`, `SelectPdf`, `QuestPdf`, `Typst`, `Chromium`. QuestPDF writes no `/Producer` or
  `/Creator`, so `QuestPdf` is never detected and must be given.
- Scrubbing is **not** a load option. It belongs to export (§7), so one loaded document can be asserted on unscrubbed
  and snapshotted scrubbed.

### 5.4 Errors

| Situation | Behavior |
|---|---|
| Encrypted, no or wrong password | `PdfProofException` with a clear message |
| Corrupt PDF | `PdfProofException` wrapping PdfPig's error, with the page number where known |
| No text layer (scan) | Loads. `Diagnostics` reports `NoTextLayer`; invariant PP006 flags it. |

## 6. Semantic passes

Both front ends produce one list of **candidate blocks**. A candidate block has lines and styled runs, a page and box,
a `Kind` (`Unknown`, `Heading`, `Paragraph`, `Table`, `List`, `Figure`, `Artifact`), and whether the kind is
**Declared** (tags) or **Inferred**. Passes run in a fixed order, rewrite the list, and append to the decision log.
All passes are deterministic: stable sorts with explicit tie-breakers, invariant culture.

| # | Pass | Applies to | Does | Main options |
|---|---|---|---|---|
| 0a | Layout front end | untagged | Splits each `LayoutBlock` into candidates of kind `Unknown` where font size, whole-line boldness, cell structure or line spacing changes (producers such as MigraDoc and Typst put a heading, paragraphs and a table in one layout block); each split is logged | – |
| 0b | Structure front end | tagged | Maps roles (`H1–H6`, `H`, `P`, `L/LI/Lbl/LBody`, `Table/TR/TH/TD`, `Figure`, `Link`) to kinds; flattens containers (`Document`, `Part`, `Sect`, `Div`, `Art`, `Form`); fixes producer quirks: looks through `NonStruct`/`Span` (Chromium), folds a `P` inside `TH`/`TD`/`LI` into its cell or item and links on `P` to the text they cover (QuestPDF), strips `Lbl` from item text | – |
| 1 | ArtifactDetection | both | Running headers, footers, page numbers: text in the top/bottom band that repeats at the same position across pages, digits normalized (`Page {n} of {N}`). One-page documents use page-number patterns only. | `BandRatio` (0.1), `MinPageFraction` (0.5), `PageNumberPatterns` |
| 2 | CrossPageMerge | both | Merges a paragraph split by a page break (first part doesn't end a sentence, styles match) and a table split by a page break (columns align; repeated header row dropped) | `MergeParagraphs`, `MergeTables`, `ColumnTolerance` |
| 3 | HeadingClassification | untagged | Body size = most common size weighted by characters. Short blocks clearly larger, or bold, short and followed by body text, become headings. Levels from size clusters, largest = 1. | `MinSizeRatio` (1.15), `MaxLines` (2), `BoldShortLineIsHeading` |
| 4 | TableDetection | untagged (tagged: header normalization) | ≥ `MinRows` consecutive rows with ≥ 2 cells whose column edges align. Header = first row if its style differs or it is declared `TH`. | `MinRows` (2), `ColumnTolerance`, `HeaderDetection` |
| 5 | ListDetection | untagged | Bullets (`• – ▪ * -`), numbering (`1.`, `a)`, `i.`) or a **list marker shape** (a small filled path just left of the line, as HTML-to-PDF converters draw CSS bullets) at consistent indentation; nesting by indentation | `BulletGlyphs`, `NumberingPatterns` |
| 6 | LinkAttachment | both | Link annotations become inline `SemanticLink`s on the text they cover (letter centres inside the rectangle), or the tags' `OBJR` links | `MinOverlap` |
| 7 | Finalize | both | `Unknown` becomes paragraph; section tree from headings; order assigned; model frozen | – |

**Delivery:** Plan 5a builds the model, both front ends and passes 1, 3, 4, 5 (without nesting), 6 and 7, all exercised by the
`invoice` corpus document. Plan 5b adds pass 2 (CrossPageMerge), nested lists and layout figures, together with the corpus
documents that exercise them (`report`, `long-table`, `letter`).

**Rules:**

- **Tags are trusted.** In tagged documents, passes 3–5 don't reclassify. Passes 1, 2 and 6 still run, because tags
  often miss artifacts, page splits and link placement. This is what makes an untagged (SelectPdf) and a tagged
  (Typst) rendering of the same content come out in the same shape.
- **Sections.** A section runs from its heading to the next heading of the same or higher level when headings are
  declared, and to the next heading of any level when they are inferred (inferred levels are unreliable).
- **Origin.** Every semantic element knows its `Page`, its `Source` (`Declared`/`Inferred`) and an internal box used
  for spatial queries and messages. The box is exported only when geometry is requested.
- Every decision is logged (`"Terms and conditions": 14 pt vs body 10 pt → heading L2`), which feeds `explain`,
  failure messages and `ILogger`.

## 7. Export

### 7.1 Scrub once, render twice

```
PdfProofDocument ─▶ ExportTree.Build(sections, includeGeometry) ─▶ Scrubber(ScrubOptions) ─┬─▶ JsonWriter       ─▶ .json
                    internal, immutable, fixed order                one placeholder context    └─▶ MarkdownRenderer ─▶ .md
```

- Scrub paths follow the JSON shape (`metadata.custom.*`, `semantics.blocks[*].text`; wildcards `[*]`, `*`, `**`) and
  apply to Markdown as well, since Markdown is rendered from the same tree.
- Placeholders: `{Date_1}`, `{Guid_1}`, `{Version}`, `{InvoiceNo_1}`. Equal values get the same placeholder across the
  whole document. Numbering follows tree order (metadata first, then blocks in document order).
- **Geometry scrubbing** (default on): when text inside an element is scrubbed, its box becomes `"{Scrubbed}"`.
- `ScrubOptions.Default`: creation and modified dates; version numbers in producer and creator; dates and GUIDs in
  custom info. Opt-in: `.ScrubInlineDates(formats?)` (ISO and sv-SE by default), `.ScrubInlineGuids()`,
  `.ScrubValues(regex, name, path?)`, `.ScrubPath(path, name)`. `ScrubOptions.None` disables scrubbing.
  Content scrubbing is opt-in because a printed date may be exactly what a test asserts.

### 7.2 Entry points

```csharp
doc.ToMarkdown();                 doc.ToMarkdown(MarkdownExportOptions options);
doc.ToJson();                     doc.ToJson(JsonExportOptions options);
PdfMarkdown.Export(byte[] | Stream, MarkdownExportOptions? options = null);   // one-liners for plain Verify
PdfJson.Export(byte[] | Stream, JsonExportOptions? options = null);
```

```csharp
[Fact]
public Task Invoice() =>
    Verify(PdfMarkdown.Export(Render(order), MarkdownExportOptions.Default.ScrubInlineDates()), "md");
```

### 7.3 Markdown

```markdown
---
title: Invoice
producer: SelectPdf {Version}
pages: 2
tagged: false
semantics: inferred
artifacts:
  - footer: "Page {n} of {N}"
---

<!-- page 1 -->

# Invoice

Questions? Visit [our support site](https://example.com/support) or e-mail us.

[Read the terms and conditions](#page-2)

| Item | Qty | Amount |
| --- | --- | --- |
| Consulting, senior | 10 | 14 500,00 SEK |

<!-- page 2 -->

## Terms and conditions

- Payment within 30 days.

![Signature](sha256:0b0d67c1e6cdf899)

<!-- bookmarks -->
- Invoice → page 1
  - Terms and conditions → page 2
```

- GFM; one paragraph per line (no hard wraps); unpadded table cells.
- **Emphasis off by default**, because bold/italic come from fonts and differ between producers.
- Artifacts appear once, deduplicated, in the front matter.
- A page marker precedes the first block that *starts* on that page. A block merged across pages appears once.
- Escaping is a fixed rule set (`\ * _ [ ] | #`, and `-`, `+`, `1.`, `>` at line start). In cells, `|` becomes `\|`
  and line breaks become `<br>`. Spanned cells repeat empty. A table without a detected header gets an empty header
  row.
- Images: `![alt or "image W×H"](sha256:<16 hex>)`. Internal links: `[text](#page-n)`.
- Bookmarks and form fields go in HTML-comment-delimited sections at the end.
- `MarkdownExportOptions` switches: front matter, page markers, images, bookmarks, form fields, artifacts, emphasis.

### 7.4 JSON

```json
{
  "formatVersion": 1,
  "metadata": { "title": "Invoice", "producer": "SelectPdf {Version}", "pageCount": 2, "isTagged": false },
  "semantics": {
    "source": "inferred",
    "blocks": [
      { "type": "heading", "level": 1, "text": "Invoice", "page": 1 },
      { "type": "paragraph", "text": "Questions? Visit our support site or e-mail us.", "page": 1,
        "links": [{ "text": "our support site", "uri": "https://example.com/support" }] },
      { "type": "table", "page": 1, "header": ["Item", "Qty", "Amount"],
        "rows": [["Consulting, senior", "10", "14 500,00 SEK"]] }
    ],
    "artifacts": [{ "role": "footer", "text": "Page {n} of {N}", "pages": "1-2" }]
  }
}
```

- `JsonExportOptions`: `Sections` (`Metadata | Semantics` by default; also `Layout`, `Structure`, `Diagnostics`, `All`),
  `IncludeGeometry` (default `false`), `Scrub`.
- A `"type"` property comes first on every block. camelCase, 2-space indent, nulls and empty collections omitted, boxes
  and cell rows on one line.
- Written by a hand-written writer on `Utf8JsonWriter`, with no serializer and no reflection: exact formatting control,
  AOT- and trim-safe.
- `formatVersion: 1`, with a JSON Schema at `docs/schema/pdfproof-v1.json` that tests validate against.

### 7.5 Determinism and output stability

- UTF-8 without BOM, `\n`, a trailing newline, non-ASCII written literally, invariant culture everywhere, no unordered
  iteration, no timestamps.
- Corpus approval tests run under sv-SE and invariant culture, on Windows (net48, net10.0) and Linux (net10.0), and
  assert identical output.
- **Changed output breaks consumers' snapshots.** Any change to approved output fails CI until re-approved, and the PR
  adds an "Output changes" changelog entry. Output changes ship only in minor or major versions after 1.0.

### 7.6 Volatility detection

`VolatilityDetector.Detect(generate, options, runs = 2)` generates a document several times (1.1 s apart, because PDF
dates have one-second resolution). It exports each run with geometry and diffs the JSON. It reports:
- paths that vary before scrubbing
- paths that still vary after scrubbing
- suggested scrub patterns

## 8. `PdfProof.Testing`

### 8.1 Layers

| Layer | Lives in | What |
|---|---|---|
| Navigation | `PdfProof` | Typed access on the models (`doc.Semantics.Tables[0].Rows`). Usable with any assertion library. |
| Matching engine | `PdfProof.Testing` | Text matching, candidate sets, relations, sequence matcher, rules, candidate ranking, messages |
| `PdfAssert` | `PdfProof.Testing` | Fluent API; throws `PdfAssertionException` |

### 8.2 Entry point and types

```csharp
PdfAssert.That(PdfProofDocument doc, PdfAssertOptions? options = null)   // recommended: load once, share
PdfAssert.That(byte[] pdf, PdfProofOptions? load = null, PdfAssertOptions? options = null)
PdfAssert.That(Stream pdf, PdfProofOptions? load = null, PdfAssertOptions? options = null)
```

| Type | Returned by | Offers |
|---|---|---|
| `DocumentScope` | `That`, scope methods | Scopes: `OnPage`, `OnPages`, `InSection`, `InHeader`, `InFooter`. Positive: `ContainsText`, `MatchesText`, `HasHeading`, `HasParagraph`, `HasTable`, `HasList`, `HasLink`, `HasFigure`, `HasHeadingsInOrder`, `HasSequence`, `MatchesExactly`. Negative: `DoesNotContainText`, `HasNoHeading`, `HasNoLink`, `HasNoTable`. Document: `HasPageCount`, `HasTitle`, `HasLanguage`, `IsTagged`, `PassesInvariants`. |
| `EveryPageScope` | `OnEveryPage()` | The same scopes and positive assertions, evaluated per page |
| `HeadingSet`, `ParagraphSet`, `TableSet`, `RowSet`, `ListSet`, `LinkSet`, `FigureSet` | `Has…` | Narrowing (`OnPage`, `InSection`, `AtLevel`, …), relations (`FollowedBy.X`, `PrecededBy.X`, `Before`, `After`, `.Section`), counts (`Exactly`, `AtLeast`), results (`Single()`, `All`) |

Type-specific narrowing:
- `TableSet`: `WithHeader(…)`, `HasRow(params string[])`, `HasRow(r => r["Item"] == "…")` → `RowSet.WithCell(column, value)`, `HasRowCount(n)`, `Column(name)`
- `LinkSet`: `WithUri(string | Regex)`, `ToPage(n)`, `IsExternal()`

### 8.3 Rules

1. **Evaluate at each step, on sets.** Every call narrows the candidate set immediately and throws when it becomes
   empty. `That(doc).OnPage(1).HasHeading("Hej")` and `That(doc).HasHeading("Hej").OnPage(1)` are equivalent, and no
   chain can be left "unfinished".
2. **Negatives only on scopes.** Element-set types have no `DoesNot…` methods, so ambiguous negations don't compile.
3. **Text matching:** a positional string means exact after normalization (whitespace collapsed, text normalization
   applied, case-sensitive). `containing:` means substring. `matching:` takes a regex. `PdfAssertOptions` sets case
   sensitivity, candidate count and excerpt length.
4. **Heading levels are ignored unless asked for** (`AtLevel`), because inferred and declared levels can differ
   between producers.
5. **Relations skip artifacts and page breaks.** `FollowedBy` means the next body block.

### 8.4 Examples

```csharp
var doc = PdfProofDocument.Load(await generator.RenderAsync(order));

PdfAssert.That(doc).HasHeadingsInOrder("Invoice", "Order lines", "Terms and conditions");
PdfAssert.That(doc).HasHeading("Terms and conditions").OnPage(2);
PdfAssert.That(doc).HasLink("Read the terms and conditions").ToPage(2);
PdfAssert.That(doc).OnPage(1).DoesNotContainText("Terms and conditions");
PdfAssert.That(doc).OnEveryPage().InFooter().MatchesText(@"Page \d+ of \d+");

PdfAssert.That(doc).HasTable(withHeader: ["Item", "Qty", "Amount"])
    .HasRow(r => r["Item"] == "Consulting, senior").WithCell("Amount", "14 500,00 SEK");

PdfAssert.That(doc).HasHeading("Terms and conditions")
    .FollowedBy.Paragraph(containing: "Payment within 30 days");
PdfAssert.That(doc).InSection("Terms and conditions").HasParagraph(containing: "30 days");

PdfAssert.That(doc).PassesInvariants();
```

### 8.5 Sequence patterns

A sequence is a regular expression over blocks. Page breaks and artifacts are invisible to it.

```csharp
PdfAssert.That(doc).HasSequence(s => s
    .Heading("Order lines")
    .Table(t => t.WithHeader("Item", "Qty", "Amount").WithRowCount(3))
    .Paragraph(matching: @"^Total: [\d ]+,\d\d SEK$"));

PdfAssert.That(doc).InSection("Terms and conditions").MatchesExactly(s => s
    .OneOrMore(b => b.Paragraph())
    .Optional(b => b.Figure(alt: "Signature")));
```

- Steps: `Heading`, `Paragraph`, `Table`, `List`, `Figure`, `Any`, `Gap()`, `Gap(max: n)`, `Optional`, `OneOrMore`,
  `ZeroOrMore`.
- `HasSequence` matches anywhere in the scope. `MatchesExactly` must match the whole scope.
- The matcher backtracks and reports the **longest partial match**.

### 8.6 Invariants

```csharp
public interface IPdfRule
{
    string Id { get; }
    string Title { get; }
    IEnumerable<PdfRuleViolation> Check(PdfProofDocument doc);
}

PdfAssert.That(doc).PassesInvariants(o => o.Suppress("PP007").Add(new MyRule()));
```

| Id | Rule | v1 |
|---|---|---|
| PP001 | No visible text outside the page or crop box, and none clipped at the page edge | ✓ |
| PP002 | No overlapping text lines | ✓ |
| PP003 | Internal links and bookmarks point to existing pages | ✓ |
| PP004 | URIs are absolute with an allowed scheme | ✓ |
| PP005 | No U+FFFD or unmapped glyphs | ✓ |
| PP006 | The document has a text layer | ✓ |
| PP007 | No empty pages | ✓ |
| PP008 | Page numbering ("Page n of N", configurable patterns) matches the page count | ✓ |
| PP009 | Fonts are embedded | ✓ |
| PP010 | Accessibility basics when tagged (language, title, figure alt, table headers) | after v1 |

The same rules power `pdfproof lint`.

### 8.7 Failures

- `PdfAssertionException : Exception` implements `PdfProof.Testing.IAssertionException`, declared in PdfProof.
  xUnit v3 is expected to treat exceptions implementing an interface named `IAssertionException` as assertion
  failures. **The first implementation task proves this with a test.** If it fails, a small `PdfProof.Testing.Xunit`
  package is added. TUnit, NUnit and MSTest fail a test on any exception, and their `Assert.Multiple` scopes collect
  ours.
- Messages, approval-tested for every assertion kind:
  1. **Expectation** in domain terms, including the scope.
  2. **Narrowing trail:** how many candidates each step kept, and where.
  3. **Closest candidates:** top 3 by normalized edit distance, with page.
  4. **Excerpt:** the Markdown of the scope, at most 15 lines, centred on the closest candidate.
  5. **Notes:** inferred or declared semantics, normalization applied, letters removed by clipping, profile used.

```
Expected a table with header [Item | Qty | Amount] to have a row where Item = "Consulting, senior"
with Amount = "14 500,00 SEK", but Amount was "14 500,00 kr" (page 1).

Closest rows:
  | Consulting, senior | 10 | 14 500,00 kr |
Notes: semantics inferred from layout (the PDF is not tagged); profile SelectPdf.
```

## 9. CLI (`pdfproof`)

| Command | Options | Output / exit code |
|---|---|---|
| `export <file>` | `--format md\|json`, `--sections …`, `--geometry`, `--profile auto\|selectpdf\|questpdf\|typst\|chromium\|default`, `--layout rows\|reading-order`, `--scrub default\|none\|inline`, `-o, --output <path>` | Plain content to stdout, or to a file with `--output`. Diagnostics to stderr. 0, or 2 on error. |
| `lint <files or globs>` | `--suppress PP007,…`, `--format text\|json\|github` | 0 clean, 1 violations, 2 error. `github` emits workflow annotations. |
| `explain <file>` | `--page n`, `--pass <name>` | Semantic outline and the decision log, grouped by pass |
| `skill install` | `--agent claude\|agents\|all`, `--user`, `--project <dir>`, `--force` | Installs the agent skill matching this tool's version (§16.2) |
| `skill status` | `--user`, `--project <dir>` | Table of installed copies: location, version, state |
| `skill uninstall` | `--agent claude\|agents\|all`, `--user`, `--project <dir>`, `--force` | Removes only the files PdfProof installed |

- **Spectre.Console.Cli** for commands, settings and help. **Spectre.Console** renders `lint` and `explain` output
  (tables, trees, rules, colors).
- **The command decides the audience.** `export` is for machines, `explain` is for humans, and `lint` serves both
  (rich text by default, `--format json|github` for machines). No flag turns one into the other.
- `export` writes exactly what `ToMarkdown()` / `ToJson()` return to stdout, with no markup, ANSI codes or layout,
  whether stdout is a terminal or redirected. Piping, redirection and diffing need no flags:
  `pdfproof export a.pdf > a.md`, `pdfproof export a.pdf | code -`.
- `export` writes diagnostics and a short summary (profile, tagged or not, letters removed by clipping, warnings) to
  **stderr** through Spectre.Console, so they never end up in redirected output. With `--output`, stdout is free and
  the summary is rendered there as a table.
- **Commands are thin.** Each parses and validates settings, calls a service, and renders the result. Logic lives in
  `PdfProof` / `PdfProof.Testing`, or in small CLI services (`ExportService`, `LintService`, `ExplainRenderer`) that
  are tested without the console (Spectre.Console.Testing for rendering).
- Targets `net8.0;net10.0`, `RollForward=Major`, packed as a dotnet tool.

## 10. Corpus

### 10.1 Why generated

The primary use case is a PDF that is **generated over and over and tested over and over**, not a static PDF
committed to a repository. The generated corpus mirrors that: every build renders the same logical documents with
each producer and runs the same semantic tests against the fresh output. It also tests the producer-migration promise
directly: **one oracle per document, which every producer must satisfy.**

### 10.2 Generated corpus (Tier 1)

```
corpus/generated/invoice/
  invoice.html                 used by SelectPdf and Chromium
  invoice.typ                  used by Typst
  expected.semantics.json      hand-written oracle (same format as the JSON export)
  deviations.json              known producer gaps: { "<producer>": "<reason>" }
  approved/<producer>.md|json  approved exports (committed)
tools/PdfProof.FixtureGenerator/Documents/InvoiceDocument.cs   QuestPDF and MigraDoc versions
```

- **Comparing against the oracle:** the block list must match in order. Properties missing from the oracle are
  ignored (omit `level` to accept any heading level). Text is compared after normalization. Links are compared by text
  and target, tables by header and rows, list items by text, artifacts by role, text and page range.
- **The corpus test is a matrix** of document × producer: pass, known deviation, or fail. A known deviation that
  starts passing fails the test until its entry is removed.
- **Fixture sources use fixed dates and IDs.** The bytes still differ (`/ID`, CreationDate), but the comparison uses
  scrubbed semantics.
- **v1 documents:**

  | Document | Exercises |
  |---|---|
  | `invoice` | Table, external and internal links, page-number footer, 2 pages |
  | `report` | H1–H3, a paragraph across a page break, nested lists, figure with alt text |
  | `long-table` | A table across 3 pages with a repeated header |
  | `two-column` | Reading order |
  | `form` | AcroForm fields |
  | `letter` | Running header and footer, right-aligned date |
  | `special-text` | Soft hyphens, ligatures, NBSP, å/ä/ö, rotated text |

- **v1 producers:**

  | Producer | Platform | Pinned by |
  |---|---|---|
  | SelectPdf Community | Windows | NuGet version |
  | Chromium (tagged and untagged) | any | Playwright version |
  | Typst | any | Exact CLI version + SHA-256 (or Typstium once on a package feed) |
  | QuestPDF (tagged and untagged) | any | NuGet version |
  | PDFsharp/MigraDoc | any | NuGet version |
  | LibreOffice | any | Best effort, never fails the build |

- Generated PDFs go to `artifacts/corpus/` with a `manifest.json` of producer versions, and are **not committed**.
  A missing fixture skips its test with a warning. With `PDFPROOF_REQUIRE_FIXTURES=true` (always set in CI), it fails.

### 10.3 External corpus (Tier 2)

- `corpus/external/manifest.json` entries: `id`, `url`, `sha256`, `license`, `source`, `purpose`. The build downloads
  them into a cache and fails on a hash mismatch. Approved exports go in `corpus/external/approved/`.
- **v1 set:** IRS W-9 and W-4 (US federal works, public domain), one Project Gutenberg PDF, one CC-BY arXiv paper.
  IRS forms are pinned by their `irs-prior` URLs (`https://www.irs.gov/pub/irs-prior/fw9--2024.pdf`): the
  `irs-pdf` URLs always serve the latest revision, so their hash changes when a form is revised.
  veraPDF corpus and PDF/UA Reference Suite files are added only after their licenses are confirmed.
- Nothing is added without a license recorded in the manifest. Govdocs1 is excluded (known malware).
- `docs/corpus.md` is generated from the manifest and approved outputs: the "what to expect" page.

### 10.4 Labelled datasets (Tier 3, after v1)

DocLayNet (CDLA-Permissive-1.0) and the ICDAR 2013 table competition set may be used later to measure the precision
and recall of heading and table inference. They are opt-in and not part of CI.

## 11. Build, CI and releases

### 11.1 Cake build

Same structure as Typstium's Cake.Sdk build:

```
build.ps1 / build.sh                dotnet run --project build/Build/Build.csproj -- <args>
build/
  Build.slnx
  Directory.Build.props             imports root props; net10.0; IsPackable=false
  Build/
    Build.csproj                    <Project Sdk="Cake.Sdk">, RunWorkingDirectory = repo root
    Program.cs                      Setup → BuildParameters; Register*Tasks(); RunTarget
    BuildParameters.cs              --configuration, --require-fixtures, --producers=…, --skip-generate, --skip-fetch
    Tasks/FixtureTasks.cs CorpusTasks.cs BuildTasks.cs TestTasks.cs PackTasks.cs DefaultTasks.cs
  Build.Cake.Extensions/            PdfProof-agnostic helpers: pinned downloads (url + sha256), producer detection
  Build.Cake.Extensions.Tests/
```

| Target | Does |
|---|---|
| `GenerateFixtures` | Renders every corpus document with every producer available on this OS; warns about unavailable ones |
| `FetchCorpus` | Downloads and verifies external PDFs |
| `Build`, `Test` | Build; unit, testing, corpus, CLI and skill tests (net48 on Windows only) |
| `CheckSkill` | Fails when a C# snippet in `skills/pdfproof/` differs from its region in `PdfProof.Skill.Tests` (§16.3); part of `Test` |
| `Pack`, `Publish` | NuGet packages and the tool; publish on a version tag |
| `Default` | `GenerateFixtures → FetchCorpus → Build → Test` |

### 11.2 CI

- `ci.yml`:
  1. `fixtures` (windows-latest): runs `GenerateFixtures` and uploads `artifacts/corpus`. Cached by a hash of the
     fixture sources and pinned producer versions.
  2. `test` (windows: net48 and net10.0; ubuntu: net10.0): downloads the fixtures, runs `FetchCorpus` and `Test` with
     `PDFPROOF_REQUIRE_FIXTURES=true`.
  3. `pack`.
- `release.yml`: on a `v*` tag, runs `Pack` and publishes to nuget.org.
- Each job calls `build.ps1` or `build.sh`, so CI and local runs execute the same code.

### 11.3 Packaging

- MinVer versions; deterministic builds, SourceLink, `.snupkg` symbols.
- A README per package; the icon `assets/branding/brand/png/pdfproof-icon-128.png` packed as `icon.png`.
- `Microsoft.CodeAnalysis.PublicApiAnalyzers` from the first commit (`PublicAPI.Shipped.txt` / `Unshipped.txt`).
- Reserve the `PdfProof.*` ID prefix on nuget.org before the first pre-release.

### 11.4 Branding

- **Files:** `assets/branding/brand/`: icon (`pdfproof-icon.svg`, 256×256 tile), logos with dark and light wordmark
  (outlined and editable), social preview, and PNG renders.
- **Palette:** ink navy `#14284B`, proof green `#15965A`, paper `#F7F9FC`, light text `#F0F6FC`.
- **PNGs are committed**, so the build never renders them.
- **Usage guide:** `assets/README.md` documents which file to use where, and the `<picture>` light/dark snippet for
  the README.

### 11.5 Pre-release milestones

This spec covers all of v1. Each milestone gets its own implementation plan. The first plan covers the repository
skeleton (Cake build, CI, packaging), the fixture generator with the `invoice` document, and `0.1.0-alpha`.

| Version | Contains | Exit criteria |
|---|---|---|
| `0.1.0-alpha` | Core, `export`, `explain`, `skill install\|status\|uninstall`, the skill's exploring and snapshot parts | The invoice gives the same semantics from SelectPdf and Typst. Real-world SelectPdf documents evaluated privately, with an agent using the skill. |
| `0.2.0-alpha` | `PdfProof.Testing` (assertions, relations, invariants), `lint`, the skill's assertion parts | Tests written against real generated documents |
| `0.3.0-beta` | Sequences, polished failure messages, API review | Full v1 corpus matrix green or covered by known deviations |
| `1.0.0` | API frozen, JSON Schema v1, docs and cookbook | A suite written against SelectPdf output passes against Typst output with few changes |

## 12. Testing the library

| Project | Covers | Targets |
|---|---|---|
| `PdfProof.Tests` | Layout, structure reader, each pass (with a fake-layout builder), exporters, scrubbing, volatility | net48 (Windows), net10.0 |
| `PdfProof.Testing.Tests` | Narrowing and ordering rules, relations, sequences, invariants, failure messages (approval-tested), the xUnit `IAssertionException` check | net48, net10.0 |
| `PdfProof.Corpus.Tests` | Producer matrix against the oracles, approved exports, identical output across cultures and OS | net48, net10.0 |
| `PdfProof.Cli.Tests` | Commands end to end, exit codes, CLI services, rendering (Spectre.Console.Testing), skill install/status/uninstall | net10.0 |
| `PdfProof.Skill.Tests` | Every C# snippet in the skill, compiled and passing | net10.0 |

- **Test stack:** xUnit v3 on Microsoft.Testing.Platform. TUnit can't run on net48.
- **Approval tests:** Verify 32.0.1 (MIT), as a dev-only dependency.
- **Coverage:** ≥ 90 % line coverage of `PdfProof`.
- **Performance targets:** a typical 2-page document loads and exports in < 50 ms warm; a 6-page complex form in
  < 500 ms. A BenchmarkDotNet project tracks this; it doesn't gate CI.

## 13. Developer experience

| Id | Requirement |
|---|---|
| DX-1 | One line to a snapshot: `Verify(PdfMarkdown.Export(bytes), "md")` |
| DX-2 | One line to an assertion: `PdfAssert.That(doc).HasHeading("Terms").OnPage(2)` |
| DX-3 | Discoverable: fluent API in IntelliSense, options as records with `with`, no DI required |
| DX-4 | Explainable: every heuristic has a name, an option and a log line; `pdfproof explain` shows them |
| DX-5 | The CLI, the exporters and the assertions see the same `PdfProofDocument` |
| DX-6 | Getting started in ≤ 5 minutes: install, copy a sample test, get a green test and a reviewed snapshot |

**Recommended testing strategy** (ships with the docs):
1. Test business rules (totals, VAT, which clauses appear) before the PDF exists, on the view model or HTML.
2. Use PdfProof for what the conversion is responsible for: page placement, headers, footers, page numbers, links,
   bookmarks, nothing clipped or lost, metadata, plus invariants on every document.
3. Make generation deterministic in tests (inject `TimeProvider` and ID generators); scrubbing is the safety net.
4. Prefer semantic assertions and a few Markdown snapshots over many large snapshots. Semantic assertions survive a
   change of producer; snapshots need re-approval.
5. Prefer tagged output where the producer supports it.

## 14. Open questions

| Id | Question | Needed by |
|---|---|---|
| OQ-1 | Does xUnit v3 treat a foreign `IAssertionException` as an assertion failure? (§8.7) | First implementation task |
| OQ-2 | ~~Can Typstium be consumed as a package by the fixture generator, or do we call the Typst CLI?~~ Answered in Plan 4: Typstium is not on a package feed, so the build downloads the Typst CLI pinned by version and SHA-256. | `0.1.0-alpha` |
| OQ-3 | Licenses of the veraPDF corpus and the PDF/UA Reference Suite files | Before adding them |
| OQ-4 | Default layout strategy per profile: is `Rows` right for SelectPdf documents with multi-column sections? | Private evaluation of `0.1.0-alpha` |
| OQ-5 | Verify integration package after v1: Verify version range and license stance | After v1 |
| OQ-6 | Which project folder does Codex read skills from? `.agents/skills/` is reported by third parties, not yet confirmed in OpenAI's docs (§16.2) | Plan 7 |

## 15. Risks

| Risk | Mitigation |
|---|---|
| PdfPig upgrades change extraction | Pinned versions, approval-tested corpus, "Output changes" changelog |
| Layout inference fails on real documents | Named passes with options and logs, the corpus matrix, private pre-release evaluation, tagged output where possible |
| Producers float (Chromium, LibreOffice) | Pinned versions; LibreOffice best effort only |
| Fixture generation is Windows-bound (SelectPdf) | Separate Windows job; missing fixtures fail only in CI |
| Sequence and relation semantics get subtle | Rules in §8.3, approval-tested messages, longest-partial-match diagnostics |
| Snapshots approved blindly | Guidance in §13, semantic assertions as the main tool |
| A stale agent skill teaches agents a wrong API | Snippets compiled from tests (§16.3); skill version stamped and checked by `skill status` |

## 16. Agent support

Agents (Claude Code, Codex, GitHub Copilot) are a primary way people will write PDF tests. PdfProof ships guidance for
two audiences:

- an **agent skill** for people who *use* PdfProof (§16.1–16.3)
- **`AGENTS.md`** for agents that *work on* this repository (§16.4)

### 16.1 The skill

It uses the open Agent Skills format: a folder with `SKILL.md` (YAML front matter with `name` and `description`,
then Markdown instructions) and reference files the agent loads only when needed.

```
skills/pdfproof/
  SKILL.md                 the workflow and the rules; short
  references/
    assertions.md          PdfAssert: scopes, narrowing, relations, sequences, invariants
    snapshots.md           plain Verify + PdfMarkdown/PdfJson, scrubbing, VolatilityDetector
    cli.md                 export, explain, lint
    producers.md           profiles, tagged vs untagged, tests that survive a change of producer
    failures.md            reading a PdfProof failure message and what to do next
```

**Description (what makes agents pick it):** use when writing, fixing or reviewing .NET tests for generated PDFs with
PdfProof, when asked to check what a generated PDF contains, or when moving PDF generation to another producer.

**The workflow:**
1. **Look before asserting.** Run `pdfproof export <file>` and `pdfproof explain <file>` to see the semantic view the
   assertions will run against.
2. **Make generation deterministic** (inject `TimeProvider` and ID generators). Scrubbing is the safety net.
3. **Assert on meaning** with `PdfAssert`. Add a few Markdown snapshots through plain Verify.
4. **On failure**, read the narrowing trail, closest candidates and excerpt before changing the test or the code.

**The rules:**
- Don't assert geometry, fonts or heading levels unless the task asks for it.
- Prefer `containing:` over exact text for paragraphs.
- Load a document once and share it.
- Never weaken an assertion just to make it pass.

**Content by milestone:**
- `0.1.0-alpha`: exploring with the CLI, and snapshots.
- `0.2.0-alpha`: assertions and lint.
- `0.3.0-beta`: sequences.

### 16.2 Distribution: `pdfproof skill`

The skill's files are embedded in `PdfProof.Cli`, so the installed skill always matches the tool's version.

| Command | Behavior |
|---|---|
| `skill install` | Writes the skill to `.claude/skills/pdfproof/` (Claude Code; Copilot reads it too) and/or `.agents/skills/pdfproof/` (Codex; Copilot reads it too). `--agent claude\|agents\|all` (default `all`). Project folder by default: the nearest ancestor with `.git`, or `--project <dir>`. `--user` uses `~/.claude/skills/` and `~/.agents/skills/` instead. Re-running it upgrades or downgrades to the tool's version. |
| `skill status` | Shows a Spectre table: location, agents served, installed version, tool version, and a state: `Current`, `Outdated`, `Newer than tool`, `Modified` or `Not installed`. Exit code 0. |
| `skill uninstall` | Removes the installed files and then the folder if it is empty. Never touches other skills. |

- **Manifest:** each installed copy contains `.pdfproof-skill.json` with the tool version and a SHA-256 hash for every
  file written. `status` compares against it, and `uninstall` deletes only the files listed in it.
- **Local edits are protected:** if an installed file no longer matches its hash, `install` and `uninstall` stop and
  name the files. `--force` overrides.
- **Thin commands:** the logic lives in a `SkillInstaller` service that works on a root directory and is tested with
  temporary folders. The commands only parse settings and render results.
- **OQ-6:** the Codex project folder (`.agents/skills/`) must be confirmed against OpenAI's documentation before
  release.

### 16.3 Examples can't drift

Every C# snippet in the skill is the body of a region in `tests/PdfProof.Skill.Tests`, a test project that compiles
and passes. In the Markdown, a snippet is marked `<!-- snippet: name -->` before its code block. The `CheckSkill` build
target (`Build.Cake.Extensions`) fails when a marked block differs from its `#region name`, or when a region or marker
has no counterpart. Agents copy examples literally, so a stale example is worse than none.

### 16.4 `AGENTS.md` for contributors

- **`AGENTS.md`** at the repository root. It covers:
  - what PdfProof is, and where the spec and plans live
  - the build commands
  - the repository layout
  - the hard rules: no real company or customer names; `PublicAPI.Unshipped.txt` for every public API change; XML
    docs; TDD; deterministic output; re-approval plus an "Output changes" entry for changed approved output; no
    committed generated PDFs; licenses for external PDFs; net48 compatibility through Polyfill
- **`CLAUDE.md`** contains only `@AGENTS.md`.
- Every plan that adds commands, projects or rules updates `AGENTS.md` in the same change.
