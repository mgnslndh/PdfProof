# PdfProof Plan 5a: Semantic Model Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `PdfProofDocument.Semantics` exposes what a PDF means: headings with levels, paragraphs with inline links and emphasis, tables with header and rows, lists, figures, sections, and running headers and footers as artifacts. It is built from the tags of a tagged PDF or inferred from the layout of an untagged one. Every decision is logged. The `invoice` corpus document comes out the same from all seven producers.

**Architecture:**
- **Pipeline.** A front end turns the document into an internal list of *candidate blocks*. Named passes then rewrite the list in a fixed order, each logging its decisions. `Finalizer` turns the candidates into the immutable public `SemanticModel`.
- **The layout front end** (untagged):
  - groups each layout block's lines into rows of cells (lines on one baseline);
  - splits the block where font size, whole-line boldness, cell structure or line spacing changes, or after a short line (spec §6, row 0a, amended).
- **The structure front end** (tagged) maps roles to kinds and handles the producer quirks: containers, `NonStruct`, `P` inside cells and items, links on `P`, and `Lbl`.
- **The passes:** ArtifactDetection, HeadingClassification, TableDetection, ListDetection, LinkAttachment and Finalize. CrossPageMerge, nested lists and layout figures are Plan 5b.
- **List markers.** CSS bullets drawn as shapes are read as list markers. They are collected during the existing layout page pass and handed to the semantic layer internally; the public layout model doesn't change.
- **Runs.** Inline content is a list of `SemanticRun`s (text, bold, italic, optional link). `Text` is the runs joined.

**Tech Stack:** PdfPig 0.1.16 (`Page.Paths` for list markers), the existing layout and structure models, xUnit v3, `RawPdf`.

**Spec:** `docs/superpowers/specs/2026-10-11-pdfproof-v1-design.md` (§3 D5, D7, D8; §5.1; §5.3; §6 as amended in `ea6eccc`; §10.2)

## Where this plan fits

| Plan | Delivers |
|---|---|
| 1–4 | Done (PR #1–#4): foundation, layout, structure, fixture generator and corpus |
| **5a Semantic model (this plan)** | `SemanticModel`, both front ends, passes 1, 3, 4, 5 (flat lists), 6, 7; decision log; corpus comparison on full block lists |
| 5b Semantic passes, continued | CrossPageMerge, nested lists, layout figures; corpus documents `report`, `long-table`, `letter` |
| 6 Export | Export tree, scrubbing, Markdown and JSON, approved exports |
| 7 CLI, skill and `0.1.0-alpha` | `export`, `explain`, `skill …`, first pre-release |

**Decisions made while brainstorming this plan:**
- 5a/5b split.
- The layout front end splits layout blocks (MigraDoc and Typst put a heading, paragraphs and a table in one block).
- CSS bullets are read as marker shapes, internally.
- Inline content is runs.
- The decision log is structured (`SemanticModel.Decisions`).

**Probe results this plan relies on** (the seven invoice fixtures, 2026-10-12):

| Producer | Layout blocks on page 1 | Header row | List bullet | Footer |
|---|---|---|---|---|
| SelectPdf | one per paragraph; table rows in their own blocks | bold | a 2.3 pt filled square at x 14.5, text at x 23.2 | 8 pt, y 18 |
| Chromium | same as SelectPdf | bold | a 3 pt filled square at x 75, text at x 86.2 | 8 pt, y 22 (template; untagged even in tagged output) |
| QuestPDF | heading, two paragraphs, one block with both table rows | bold | text "- " | 10 pt, y 66 |
| MigraDoc | **one block** for the heading, both paragraphs and the table | bold | text "-" without a space ("-Payment") | 10 pt, y 44 |
| Typst (tagged) | heading and first paragraph in one block | not bold (tags give TH) | text "• " (tags give `Lbl`) | 10 pt, y 50 |

Heading sizes against body text: SelectPdf 15.7 and 11 against 7.6 (page scaled); MigraDoc, QuestPDF and Chromium 20 and 14 against 10; Typst 14 and 12 against 10.

## Global Constraints

- Library target frameworks: `netstandard2.0;net8.0;net10.0`. Tests: `net48;net10.0` on Windows, `net10.0` elsewhere.
- **net48/netstandard2.0-safe code in `src/`:** no `GeneratedRegex`, `[^1]` or ranges (`text[..30]`), `ToHashSet`, `KeyValuePair` deconstruction, `string.Join(char, …)` or `Math.Clamp`.
- `TreatWarningsAsErrors`, nullable enabled, XML docs on every public member.
- **Every public API change goes into `src/PdfProof/PublicAPI.Unshipped.txt`.** After adding public members, run from the repository root (bash):
  ```bash
  dotnet build src/PdfProof/PdfProof.csproj 2>&1 | sed -n "s/.*error RS0016: Symbol '\([^']*\)' is not part of the declared public API.*/\1/p" | sort -u >> src/PdfProof/PublicAPI.Unshipped.txt
  ```
  Then rebuild; expect 0 errors, and check that the appended lines are only the members you added.
- **Throwing:** guard clauses use the shared `Throw` helper. Calls into PdfPig go through `PdfPigCall.Read`; the semantic layer reads only PdfProof's own models, never PdfPig.
- **Public model collections** are read-only copies (`ReadOnly.List`). Public model types are immutable; the internal candidates are mutable while the passes run.
- **Deterministic output:** invariant culture for every number in a message (use `SemanticText.Number`), explicit sort orders, no unordered iteration.
- **Test PDFs are built in code** with `RawPdf`. Never write `\u`, `\t` or similar escapes in this plan's code; non-ASCII characters such as `•` and `→` are written literally. After writing a file, scan it for control or format characters.
- Never write real company or customer names. Sample data is neutral.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Test commands: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj` and `dotnet test --project tests/PdfProof.Corpus.Tests/PdfProof.Corpus.Tests.csproj`. The whole build is `./build.ps1`.

## Review Focus

1. **A page with no text, or a document with no pages,** gives an empty model without throwing: no blocks, no artifacts, `Source` set. Test in Task 2.
2. **The same link text twice in a block** (two links, or a link and plain text): each link attaches to its own occurrence, and none of them twice. Test in Task 7.
3. **Culture:** decision messages and artifact texts use the invariant culture, so the same numbers appear under sv-SE (`14 pt vs body 10.5 pt`, not `10,5`). Test in Task 4.
4. **A two-part footer in the bottom band** (left text, right page number: one row with two cells) becomes a footer artifact, not a table. Test in Task 3.
5. **Tagged marked content whose MCID has no collected text or style** (an MCID missing on its page) gives plain runs or nothing, without throwing. Test in Task 8.

---

### Task 1: The public semantic types, options and an empty model

**Files:**
- Create in `src/PdfProof/Semantics/`:
  - the model types: `SemanticSource.cs`, `SemanticLink.cs`, `SemanticRun.cs`, `SemanticBlock.cs`, `SemanticHeading.cs`, `SemanticParagraph.cs`, `SemanticCell.cs`, `SemanticRow.cs`, `SemanticTable.cs`, `SemanticListItem.cs`, `SemanticList.cs`, `SemanticFigure.cs`, `SemanticArtifactRole.cs`, `SemanticArtifact.cs`, `SemanticPass.cs`, `SemanticDecision.cs`, `SemanticSection.cs`, `SemanticLinkedText.cs`, `SemanticModel.cs`;
  - the options: `SemanticSourceOption.cs`, `SemanticOptions.cs`, `ArtifactDetectionOptions.cs`, `HeadingClassificationOptions.cs`, `TableDetectionOptions.cs`, `ListDetectionOptions.cs`, `LinkAttachmentOptions.cs`;
  - the internals: `SemanticText.cs`, `SemanticBuilder.cs`.
- Create: `tests/PdfProof.Tests/SemanticModelTests.cs`
- Modify: `src/PdfProof/PdfProfile.cs`, `src/PdfProof/PdfProofDocument.cs`, `src/PdfProof/PublicAPI.Unshipped.txt`

**Interfaces:**
- Consumes: `PdfProfile` (Plan 4), `LayoutBox`, `ReadOnly.List`, `Throw`.
- Produces (public, namespace `PdfProof.Semantics`):
  - `SemanticModel`: `Source`, `Blocks`, `Sections`, `Artifacts`, `Decisions`, and the helpers `Headings`, `Paragraphs`, `Tables`, `Lists`, `Figures`, `Links`. Constructor: `internal SemanticModel(SemanticSource, IReadOnlyList<SemanticBlock>, IReadOnlyList<SemanticSection>, IReadOnlyList<SemanticArtifact>, IReadOnlyList<SemanticDecision>)`.
  - `SemanticBlock` (abstract): `Page`, `Source`, `Text`, and `internal LayoutBox Box`. The kinds and their internal constructors:
    - `SemanticHeading(int page, SemanticSource source, int level, IReadOnlyList<SemanticRun> runs, LayoutBox box)`
    - `SemanticParagraph(int page, SemanticSource source, IReadOnlyList<SemanticRun> runs, LayoutBox box)`
    - `SemanticTable(int page, SemanticSource source, SemanticRow? header, IReadOnlyList<SemanticRow> rows, LayoutBox box)`
    - `SemanticList(int page, SemanticSource source, IReadOnlyList<SemanticListItem> items, LayoutBox box)`
    - `SemanticFigure(int page, SemanticSource source, string? alt, LayoutBox box)`
  - The inline and table parts: `SemanticRow(IReadOnlyList<SemanticCell>)`, `SemanticCell(IReadOnlyList<SemanticRun>)`, `SemanticListItem(IReadOnlyList<SemanticRun>)`, `SemanticRun(string text, bool isBold, bool isItalic, SemanticLink? link)`, `SemanticLink(string? uri, int? targetPage)`.
  - The rest of the model:
    - `SemanticArtifact(SemanticArtifactRole role, string text, IReadOnlyList<int> pages)`
    - `SemanticSection(SemanticHeading heading, IReadOnlyList<SemanticBlock> blocks, IReadOnlyList<SemanticSection> sections)`
    - `SemanticDecision(SemanticPass pass, int? page, SemanticBlock? block, string message)`
    - `SemanticLinkedText(SemanticBlock block, string text, SemanticLink link)`
  - Enums: `SemanticSource { Declared, Inferred }`, `SemanticArtifactRole { Header, Footer }`, `SemanticPass { LayoutFrontEnd, StructureFrontEnd, ArtifactDetection, HeadingClassification, TableDetection, ListDetection, LinkAttachment, Finalize }`.
  - `SemanticOptions` with:
    - `Source` (`SemanticSourceOption { Auto, Structure, Layout }`);
    - `ParagraphGapFactor` (1.0) and `ShortLineRatio` (0.8);
    - one record per pass: `Artifacts`, `Headings`, `Tables`, `Lists`, `Links`. Every pass record has `Enabled` (default true).
  - `PdfProfile.Semantics : SemanticOptions` (default `SemanticOptions.Default`), and `PdfProofDocument.Semantics : SemanticModel`.
  - Internal: `SemanticText` (`Join`, `Concat`, `Normalize`, `Link`, `Plain`, `Number`, `Excerpt`), and `SemanticBuilder.Build(LayoutBuildResult layout, StructureModel structure, SemanticOptions options, DiagnosticsCollector diagnostics) -> SemanticModel`. For now `Build` returns an empty model with `Source` from tagging; Task 2 fills it in.

- [ ] **Step 1: Write the failing tests in `tests/PdfProof.Tests/SemanticModelTests.cs`**

```csharp
using PdfProof.Semantics;

namespace PdfProof.Tests;

public sealed class SemanticModelTests
{
    [Fact]
    public void Every_document_has_a_semantic_model_whose_source_follows_the_tags()
    {
        var untagged = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 750, "Hello")));

        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("P", 0, RawPdf.Text(50, 750, "Hello")));
        pdf.Tag($"{pdf.Add($"<< /S /P /Pg {page} 0 R /K 0 >>")} 0 R");
        var tagged = PdfProofDocument.Load(pdf.Build());

        Assert.Equal(SemanticSource.Inferred, untagged.Semantics.Source);
        Assert.Equal(SemanticSource.Declared, tagged.Semantics.Source);
    }

    [Fact]
    public void The_model_collections_are_read_only()
    {
        var semantics = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 750, "Hello"))).Semantics;

        Assert.Throws<NotSupportedException>(() => ((IList<SemanticBlock>)semantics.Blocks).Add(null!));
        Assert.Throws<NotSupportedException>(() => ((IList<SemanticArtifact>)semantics.Artifacts).Add(null!));
        Assert.Throws<NotSupportedException>(() => ((IList<SemanticDecision>)semantics.Decisions).Add(null!));
    }

    [Fact]
    public void Semantic_options_have_the_spec_defaults_and_belong_to_the_profile()
    {
        var options = SemanticOptions.Default;

        Assert.Same(options, PdfProfile.Default.Semantics);
        Assert.Equal((SemanticSourceOption.Auto, 1.0, 0.8), (options.Source, options.ParagraphGapFactor, options.ShortLineRatio));
        Assert.Equal((true, 0.1, 0.5), (options.Artifacts.Enabled, options.Artifacts.BandRatio, options.Artifacts.MinPageFraction));
        Assert.NotEmpty(options.Artifacts.PageNumberPatterns);
        Assert.Equal((true, 1.15, 2, true), (options.Headings.Enabled, options.Headings.MinSizeRatio, options.Headings.MaxLines, options.Headings.BoldShortLineIsHeading));
        Assert.Equal((true, 2, 3.0), (options.Tables.Enabled, options.Tables.MinRows, options.Tables.ColumnTolerance));
        Assert.Equal(["•", "◦", "▪", "–", "*", "-"], options.Lists.BulletGlyphs);
        Assert.Equal((true, true, 3.0), (options.Lists.Enabled, options.Lists.UseMarkerShapes, options.Lists.IndentTolerance));
        Assert.True(options.Links.Enabled);
    }

    [Fact]
    public void Invalid_semantic_options_are_rejected()
    {
        Assert.Equal("ParagraphGapFactor", Assert.Throws<ArgumentOutOfRangeException>(() => SemanticOptions.Default with { ParagraphGapFactor = -1 }).ParamName);
        Assert.Equal("ShortLineRatio", Assert.Throws<ArgumentOutOfRangeException>(() => SemanticOptions.Default with { ShortLineRatio = 1.5 }).ParamName);
        Assert.Equal("Artifacts", Assert.Throws<ArgumentNullException>(() => SemanticOptions.Default with { Artifacts = null! }).ParamName);
        Assert.Equal("PageNumberPatterns", Assert.Throws<ArgumentException>(() => ArtifactDetectionOptions.Default with { PageNumberPatterns = ["("] }).ParamName);
        Assert.Equal("MinRows", Assert.Throws<ArgumentOutOfRangeException>(() => TableDetectionOptions.Default with { MinRows = 1 }).ParamName);
        Assert.Equal("BulletGlyphs", Assert.Throws<ArgumentException>(() => ListDetectionOptions.Default with { BulletGlyphs = [""] }).ParamName);
    }

    [Fact]
    public void Option_lists_are_copied()
    {
        var glyphs = new List<string> { "•" };
        var options = ListDetectionOptions.Default with { BulletGlyphs = glyphs };
        glyphs.Add("-");

        Assert.Equal(["•"], options.BulletGlyphs);
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet build tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: build FAILS: the namespace `PdfProof.Semantics` and `PdfProofDocument.Semantics` don't exist.

- [ ] **Step 3: Write the model types**

`src/PdfProof/Semantics/SemanticSource.cs`:
```csharp
namespace PdfProof.Semantics;

/// <summary>Where a semantic element's kind comes from.</summary>
public enum SemanticSource
{
    /// <summary>Declared by the PDF's tags (Tagged PDF).</summary>
    Declared,

    /// <summary>Inferred from the layout.</summary>
    Inferred,
}
```

`src/PdfProof/Semantics/SemanticLink.cs`:
```csharp
namespace PdfProof.Semantics;

/// <summary>Where a link goes: an external URI or a page of the same document.</summary>
public sealed class SemanticLink
{
    internal SemanticLink(string? uri, int? targetPage)
    {
        Uri = uri;
        TargetPage = targetPage;
    }

    /// <summary>The external URI, or <see langword="null"/> for a link within the document.</summary>
    public string? Uri { get; }

    /// <summary>The page number the link goes to, or <see langword="null"/> for an external link.</summary>
    public int? TargetPage { get; }

    /// <summary>The URI, or <c>page n</c>.</summary>
    public override string ToString() => Uri ?? (TargetPage is { } page ? "page " + page.ToString(System.Globalization.CultureInfo.InvariantCulture) : "(no target)");

    internal static bool Same(SemanticLink? a, SemanticLink? b) =>
        a is null ? b is null : b is not null && a.Uri == b.Uri && a.TargetPage == b.TargetPage;
}
```

`src/PdfProof/Semantics/SemanticRun.cs`:
```csharp
namespace PdfProof.Semantics;

/// <summary>A piece of inline text with one style and at most one link.</summary>
public sealed class SemanticRun
{
    internal SemanticRun(string text, bool isBold, bool isItalic, SemanticLink? link)
    {
        Text = text;
        IsBold = isBold;
        IsItalic = isItalic;
        Link = link;
    }

    /// <summary>The run's text, spaces included: a block's text is its runs joined.</summary>
    public string Text { get; }

    /// <summary>The text is bold.</summary>
    public bool IsBold { get; }

    /// <summary>The text is italic.</summary>
    public bool IsItalic { get; }

    /// <summary>The link the text belongs to, or <see langword="null"/>.</summary>
    public SemanticLink? Link { get; }
}
```

`src/PdfProof/Semantics/SemanticBlock.cs`:
```csharp
using PdfProof.Layout;

namespace PdfProof.Semantics;

/// <summary>A body block in reading order: a heading, paragraph, table, list or figure. Artifacts are not blocks.</summary>
public abstract class SemanticBlock
{
    private protected SemanticBlock(int page, SemanticSource source, string text, LayoutBox box)
    {
        Page = page;
        Source = source;
        Text = text;
        Box = box;
    }

    /// <summary>The page the block starts on.</summary>
    public int Page { get; }

    /// <summary>Whether the block's kind was declared by tags or inferred from the layout.</summary>
    public SemanticSource Source { get; }

    /// <summary>
    /// The block's text: runs joined for headings and paragraphs; for a table, cells joined by <c>" | "</c> and rows
    /// by <c>\n</c>; for a list, items joined by <c>\n</c>; for a figure, its alternative text or empty.
    /// </summary>
    public string Text { get; }

    // Where the block is, for spatial queries and geometry export. Default (all zero) when the block comes from tags.
    internal LayoutBox Box { get; }
}
```

`src/PdfProof/Semantics/SemanticHeading.cs`:
```csharp
using PdfProof.Layout;

namespace PdfProof.Semantics;

/// <summary>A heading.</summary>
public sealed class SemanticHeading : SemanticBlock
{
    internal SemanticHeading(int page, SemanticSource source, int level, IReadOnlyList<SemanticRun> runs, LayoutBox box)
        : base(page, source, SemanticText.Join(runs), box)
    {
        Level = level;
        Runs = ReadOnly.List(runs);
    }

    /// <summary>The level, 1 for the top level. Inferred levels come from font sizes, so they can differ between producers.</summary>
    public int Level { get; }

    /// <summary>The heading's text as runs.</summary>
    public IReadOnlyList<SemanticRun> Runs { get; }
}
```

`src/PdfProof/Semantics/SemanticParagraph.cs`:
```csharp
using PdfProof.Layout;

namespace PdfProof.Semantics;

/// <summary>A paragraph.</summary>
public sealed class SemanticParagraph : SemanticBlock
{
    internal SemanticParagraph(int page, SemanticSource source, IReadOnlyList<SemanticRun> runs, LayoutBox box)
        : base(page, source, SemanticText.Join(runs), box)
    {
        Runs = ReadOnly.List(runs);
    }

    /// <summary>The paragraph's text as runs.</summary>
    public IReadOnlyList<SemanticRun> Runs { get; }
}
```

`src/PdfProof/Semantics/SemanticCell.cs`:
```csharp
namespace PdfProof.Semantics;

/// <summary>A table cell. An empty cell has no runs and empty text.</summary>
public sealed class SemanticCell
{
    internal SemanticCell(IReadOnlyList<SemanticRun> runs)
    {
        Runs = ReadOnly.List(runs);
        Text = SemanticText.Join(runs);
    }

    /// <summary>The cell's text.</summary>
    public string Text { get; }

    /// <summary>The cell's text as runs.</summary>
    public IReadOnlyList<SemanticRun> Runs { get; }
}
```

`src/PdfProof/Semantics/SemanticRow.cs`:
```csharp
namespace PdfProof.Semantics;

/// <summary>A table row: one cell per column.</summary>
public sealed class SemanticRow
{
    internal SemanticRow(IReadOnlyList<SemanticCell> cells)
    {
        Cells = ReadOnly.List(cells);
    }

    /// <summary>The cells, left to right.</summary>
    public IReadOnlyList<SemanticCell> Cells { get; }
}
```

`src/PdfProof/Semantics/SemanticTable.cs`:
```csharp
using PdfProof.Layout;

namespace PdfProof.Semantics;

/// <summary>A table: an optional header row and the body rows.</summary>
public sealed class SemanticTable : SemanticBlock
{
    internal SemanticTable(int page, SemanticSource source, SemanticRow? header, IReadOnlyList<SemanticRow> rows, LayoutBox box)
        : base(page, source, Join(header, rows), box)
    {
        Header = header;
        Rows = ReadOnly.List(rows);
    }

    /// <summary>The header row, or <see langword="null"/> when the table has none.</summary>
    public SemanticRow? Header { get; }

    /// <summary>The body rows, top to bottom.</summary>
    public IReadOnlyList<SemanticRow> Rows { get; }

    private static string Join(SemanticRow? header, IReadOnlyList<SemanticRow> rows) =>
        string.Join("\n", (header is null ? rows : new[] { header }.Concat(rows)).Select(row => string.Join(" | ", row.Cells.Select(cell => cell.Text))));
}
```

`src/PdfProof/Semantics/SemanticListItem.cs`:
```csharp
namespace PdfProof.Semantics;

/// <summary>A list item, without its bullet or number.</summary>
public sealed class SemanticListItem
{
    internal SemanticListItem(IReadOnlyList<SemanticRun> runs)
    {
        Runs = ReadOnly.List(runs);
        Text = SemanticText.Join(runs);
    }

    /// <summary>The item's text.</summary>
    public string Text { get; }

    /// <summary>The item's text as runs.</summary>
    public IReadOnlyList<SemanticRun> Runs { get; }
}
```

`src/PdfProof/Semantics/SemanticList.cs`:
```csharp
using PdfProof.Layout;

namespace PdfProof.Semantics;

/// <summary>A bulleted or numbered list.</summary>
public sealed class SemanticList : SemanticBlock
{
    internal SemanticList(int page, SemanticSource source, IReadOnlyList<SemanticListItem> items, LayoutBox box)
        : base(page, source, string.Join("\n", items.Select(item => item.Text)), box)
    {
        Items = ReadOnly.List(items);
    }

    /// <summary>The items, in order.</summary>
    public IReadOnlyList<SemanticListItem> Items { get; }
}
```

`src/PdfProof/Semantics/SemanticFigure.cs`:
```csharp
using PdfProof.Layout;

namespace PdfProof.Semantics;

/// <summary>A figure (an image or drawing) declared by tags.</summary>
public sealed class SemanticFigure : SemanticBlock
{
    internal SemanticFigure(int page, SemanticSource source, string? alt, LayoutBox box)
        : base(page, source, alt ?? "", box)
    {
        Alt = alt;
    }

    /// <summary>The figure's alternative text, or <see langword="null"/> when it has none.</summary>
    public string? Alt { get; }
}
```

`src/PdfProof/Semantics/SemanticArtifactRole.cs`:
```csharp
namespace PdfProof.Semantics;

/// <summary>Where an artifact sits on the page.</summary>
public enum SemanticArtifactRole
{
    /// <summary>A running header in the top band of the page.</summary>
    Header,

    /// <summary>A running footer or page number in the bottom band of the page.</summary>
    Footer,
}
```

`src/PdfProof/Semantics/SemanticArtifact.cs`:
```csharp
namespace PdfProof.Semantics;

/// <summary>
/// Text that repeats as page furniture rather than content: a running header or footer, or a page number. It is not
/// part of <see cref="SemanticModel.Blocks"/>.
/// </summary>
public sealed class SemanticArtifact
{
    internal SemanticArtifact(SemanticArtifactRole role, string text, IReadOnlyList<int> pages)
    {
        Role = role;
        Text = text;
        Pages = ReadOnly.List(pages);
    }

    /// <summary>Header or footer.</summary>
    public SemanticArtifactRole Role { get; }

    /// <summary>The text, with the page number written <c>{n}</c> and the page count <c>{N}</c>: <c>Page {n} of {N}</c>.</summary>
    public string Text { get; }

    /// <summary>The pages it appears on, ascending.</summary>
    public IReadOnlyList<int> Pages { get; }
}
```

`src/PdfProof/Semantics/SemanticPass.cs`:
```csharp
namespace PdfProof.Semantics;

/// <summary>The steps that build the semantic model, in the order they run.</summary>
public enum SemanticPass
{
    /// <summary>Turns layout blocks into candidate blocks (untagged documents).</summary>
    LayoutFrontEnd,

    /// <summary>Turns the structure tree into candidate blocks (tagged documents).</summary>
    StructureFrontEnd,

    /// <summary>Finds running headers, footers and page numbers.</summary>
    ArtifactDetection,

    /// <summary>Finds headings and their levels from font size and weight (untagged documents).</summary>
    HeadingClassification,

    /// <summary>Finds tables from aligned rows of cells (untagged documents).</summary>
    TableDetection,

    /// <summary>Finds lists from bullets, numbering and marker shapes (untagged documents).</summary>
    ListDetection,

    /// <summary>Attaches link annotations to the text they cover.</summary>
    LinkAttachment,

    /// <summary>Turns the remaining candidates into paragraphs and builds the sections.</summary>
    Finalize,
}
```

`src/PdfProof/Semantics/SemanticDecision.cs`:
```csharp
using System.Globalization;

namespace PdfProof.Semantics;

/// <summary>One logged decision of a semantic pass: what it decided and, where it concerns one block, which.</summary>
public sealed class SemanticDecision
{
    internal SemanticDecision(SemanticPass pass, int? page, SemanticBlock? block, string message)
    {
        Pass = pass;
        Page = page;
        Block = block;
        Message = message;
    }

    /// <summary>The pass that decided.</summary>
    public SemanticPass Pass { get; }

    /// <summary>The page the decision is about, or <see langword="null"/> for the whole document.</summary>
    public int? Page { get; }

    /// <summary>The block the decision is about, or <see langword="null"/>.</summary>
    public SemanticBlock? Block { get; }

    /// <summary>The decision, in plain words.</summary>
    public string Message { get; }

    /// <summary>The decision as one line: <c>Pass: Message</c> or <c>Pass (page n): Message</c>.</summary>
    public override string ToString() =>
        Page is { } page ? $"{Pass} (page {page.ToString(CultureInfo.InvariantCulture)}): {Message}" : $"{Pass}: {Message}";
}
```

`src/PdfProof/Semantics/SemanticSection.cs`:
```csharp
namespace PdfProof.Semantics;

/// <summary>
/// A heading and what belongs to it. With declared headings a section runs to the next heading of the same or a
/// higher level, and holds the lower-level sections as <see cref="Sections"/>; with inferred headings it runs to the
/// next heading of any level (inferred levels are unreliable), so sections don't nest.
/// </summary>
public sealed class SemanticSection
{
    internal SemanticSection(SemanticHeading heading, IReadOnlyList<SemanticBlock> blocks, IReadOnlyList<SemanticSection> sections)
    {
        Heading = heading;
        Blocks = ReadOnly.List(blocks);
        Sections = ReadOnly.List(sections);
    }

    /// <summary>The section's heading.</summary>
    public SemanticHeading Heading { get; }

    /// <summary>The blocks after the heading and before the first subsection or the next section.</summary>
    public IReadOnlyList<SemanticBlock> Blocks { get; }

    /// <summary>The subsections.</summary>
    public IReadOnlyList<SemanticSection> Sections { get; }
}
```

`src/PdfProof/Semantics/SemanticLinkedText.cs`:
```csharp
namespace PdfProof.Semantics;

/// <summary>Linked text and the block it is in, as listed by <see cref="SemanticModel.Links"/>.</summary>
public sealed class SemanticLinkedText
{
    internal SemanticLinkedText(SemanticBlock block, string text, SemanticLink link)
    {
        Block = block;
        Text = text;
        Link = link;
    }

    /// <summary>The block the linked text is in.</summary>
    public SemanticBlock Block { get; }

    /// <summary>The linked text.</summary>
    public string Text { get; }

    /// <summary>Where the link goes.</summary>
    public SemanticLink Link { get; }
}
```

`src/PdfProof/Semantics/SemanticModel.cs`:
```csharp
namespace PdfProof.Semantics;

/// <summary>
/// What the PDF means: its blocks in reading order, the sections they form, the page furniture left out of them, and
/// every decision taken to get there. Built from the tags of a tagged PDF, otherwise inferred from the layout.
/// </summary>
public sealed class SemanticModel
{
    internal SemanticModel(
        SemanticSource source,
        IReadOnlyList<SemanticBlock> blocks,
        IReadOnlyList<SemanticSection> sections,
        IReadOnlyList<SemanticArtifact> artifacts,
        IReadOnlyList<SemanticDecision> decisions)
    {
        Source = source;
        Blocks = ReadOnly.List(blocks);
        Sections = ReadOnly.List(sections);
        Artifacts = ReadOnly.List(artifacts);
        Decisions = ReadOnly.List(decisions);
        Headings = ReadOnly.List(blocks.OfType<SemanticHeading>());
        Paragraphs = ReadOnly.List(blocks.OfType<SemanticParagraph>());
        Tables = ReadOnly.List(blocks.OfType<SemanticTable>());
        Lists = ReadOnly.List(blocks.OfType<SemanticList>());
        Figures = ReadOnly.List(blocks.OfType<SemanticFigure>());
        Links = ReadOnly.List(blocks.SelectMany(LinkedTexts));
    }

    /// <summary>Declared when the blocks come from tags, inferred when they come from the layout.</summary>
    public SemanticSource Source { get; }

    /// <summary>The body blocks in reading order. Artifacts are not included.</summary>
    public IReadOnlyList<SemanticBlock> Blocks { get; }

    /// <summary>The sections formed by the headings; blocks before the first heading belong to no section.</summary>
    public IReadOnlyList<SemanticSection> Sections { get; }

    /// <summary>Running headers, footers and page numbers.</summary>
    public IReadOnlyList<SemanticArtifact> Artifacts { get; }

    /// <summary>Every decision the passes logged, in the order they were taken.</summary>
    public IReadOnlyList<SemanticDecision> Decisions { get; }

    /// <summary>The headings, in reading order.</summary>
    public IReadOnlyList<SemanticHeading> Headings { get; }

    /// <summary>The paragraphs, in reading order.</summary>
    public IReadOnlyList<SemanticParagraph> Paragraphs { get; }

    /// <summary>The tables, in reading order.</summary>
    public IReadOnlyList<SemanticTable> Tables { get; }

    /// <summary>The lists, in reading order.</summary>
    public IReadOnlyList<SemanticList> Lists { get; }

    /// <summary>The figures, in reading order.</summary>
    public IReadOnlyList<SemanticFigure> Figures { get; }

    /// <summary>Every linked text, with its block, in reading order.</summary>
    public IReadOnlyList<SemanticLinkedText> Links { get; }

    private static IEnumerable<SemanticLinkedText> LinkedTexts(SemanticBlock block)
    {
        IEnumerable<SemanticRun> runs = block switch
        {
            SemanticHeading heading => heading.Runs,
            SemanticParagraph paragraph => paragraph.Runs,
            SemanticList list => list.Items.SelectMany(item => item.Runs),
            SemanticTable table => (table.Header is { } header ? new[] { header } : []).Concat(table.Rows).SelectMany(row => row.Cells).SelectMany(cell => cell.Runs),
            _ => [],
        };
        return runs.Where(run => run.Link is not null).Select(run => new SemanticLinkedText(block, run.Text, run.Link!));
    }
}
```

- [ ] **Step 4: Write the options**

`src/PdfProof/Semantics/SemanticSourceOption.cs`:
```csharp
namespace PdfProof.Semantics;

/// <summary>Which front end builds the semantic model.</summary>
public enum SemanticSourceOption
{
    /// <summary>The structure tree when the document is tagged, otherwise the layout.</summary>
    Auto,

    /// <summary>Always the structure tree; an untagged document gets an empty model.</summary>
    Structure,

    /// <summary>Always the layout, even for a tagged document.</summary>
    Layout,
}
```

`src/PdfProof/Semantics/SemanticOptions.cs`:
```csharp
namespace PdfProof.Semantics;

/// <summary>
/// How the semantic model is built: the front end, how the layout front end splits blocks, and one options record per
/// pass. Part of a <see cref="PdfProfile"/>; start from <see cref="Default"/> and adjust with <c>with</c>.
/// </summary>
public sealed record SemanticOptions
{
    /// <summary>The default semantic options.</summary>
    public static SemanticOptions Default { get; } = new();

    /// <summary>Which front end builds the model. Default <see cref="SemanticSourceOption.Auto"/>.</summary>
    public SemanticSourceOption Source { get; init; } = SemanticSourceOption.Auto;

    /// <summary>
    /// Layout front end: a vertical gap between two lines of a layout block larger than this many font sizes starts a
    /// new block. Default 1.0.
    /// </summary>
    public double ParagraphGapFactor
    {
        get => _paragraphGapFactor;
        init => _paragraphGapFactor = Throw.IfOutOfRange(value, 0, 100, nameof(ParagraphGapFactor));
    }

    /// <summary>
    /// Layout front end: a line that ends before this fraction of the page's text width ends its paragraph, so one-line
    /// paragraphs in the same layout block are told apart. Between 0 and 1; default 0.8.
    /// </summary>
    public double ShortLineRatio
    {
        get => _shortLineRatio;
        init => _shortLineRatio = Throw.IfOutOfRange(value, 0, 1, nameof(ShortLineRatio));
    }

    /// <summary>Running headers, footers and page numbers.</summary>
    public ArtifactDetectionOptions Artifacts
    {
        get => _artifacts;
        init => _artifacts = Throw.IfNull(value, nameof(Artifacts));
    }

    /// <summary>Headings in untagged documents.</summary>
    public HeadingClassificationOptions Headings
    {
        get => _headings;
        init => _headings = Throw.IfNull(value, nameof(Headings));
    }

    /// <summary>Tables in untagged documents.</summary>
    public TableDetectionOptions Tables
    {
        get => _tables;
        init => _tables = Throw.IfNull(value, nameof(Tables));
    }

    /// <summary>Lists in untagged documents.</summary>
    public ListDetectionOptions Lists
    {
        get => _lists;
        init => _lists = Throw.IfNull(value, nameof(Lists));
    }

    /// <summary>Links attached to the text they cover.</summary>
    public LinkAttachmentOptions Links
    {
        get => _links;
        init => _links = Throw.IfNull(value, nameof(Links));
    }

    private readonly double _paragraphGapFactor = 1.0;
    private readonly double _shortLineRatio = 0.8;
    private readonly ArtifactDetectionOptions _artifacts = ArtifactDetectionOptions.Default;
    private readonly HeadingClassificationOptions _headings = HeadingClassificationOptions.Default;
    private readonly TableDetectionOptions _tables = TableDetectionOptions.Default;
    private readonly ListDetectionOptions _lists = ListDetectionOptions.Default;
    private readonly LinkAttachmentOptions _links = LinkAttachmentOptions.Default;
}
```

`Throw.IfOutOfRange` with a `double` min and max: if `Throw.cs` only has the `int`/`uint` overloads, add this to `src/Shared/Throw/Throw.PdfProof.cs` (it is PdfProof's own helper, so not in `Throw.cs`):
```csharp
    /// <summary>Throws an <see cref="ArgumentOutOfRangeException"/> when a number is NaN or outside a range.</summary>
    /// <param name="argument">The number.</param>
    /// <param name="min">The smallest allowed value.</param>
    /// <param name="max">The largest allowed value.</param>
    /// <param name="paramName">The parameter's name.</param>
    /// <returns>The number.</returns>
    public static double IfOutOfRange(double argument, double min, double max, string paramName)
    {
        if (double.IsNaN(argument) || argument < min || argument > max)
        {
            ArgumentOutOfRangeException(paramName, argument, $"Must be between {min.ToString(System.Globalization.CultureInfo.InvariantCulture)} and {max.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
        }

        return argument;
    }
```

`src/PdfProof/Semantics/ArtifactDetectionOptions.cs`:
```csharp
using System.Text.RegularExpressions;

namespace PdfProof.Semantics;

/// <summary>
/// ArtifactDetection: text in the top or bottom band of the page that repeats at the same place on enough pages (page
/// numbers compared as <c>{n}</c> and <c>{N}</c>), or that matches a page-number pattern, is a header or footer, not
/// content.
/// </summary>
public sealed record ArtifactDetectionOptions
{
    /// <summary>The default options.</summary>
    public static ArtifactDetectionOptions Default { get; } = new();

    /// <summary>Run the pass. Default on.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>The height of the top and bottom bands, as a fraction of the page height. Default 0.1.</summary>
    public double BandRatio
    {
        get => _bandRatio;
        init => _bandRatio = Throw.IfOutOfRange(value, 0, 0.5, nameof(BandRatio));
    }

    /// <summary>The fraction of the pages a text must repeat on (and at least two pages). Default 0.5.</summary>
    public double MinPageFraction
    {
        get => _minPageFraction;
        init => _minPageFraction = Throw.IfOutOfRange(value, 0, 1, nameof(MinPageFraction));
    }

    /// <summary>
    /// Regular expressions (invariant culture) for a whole band line that is a page number, also on one-page
    /// documents. Default: <c>Page 3</c>, <c>Page 3 of 7</c> (also Swedish and German), <c>3 / 7</c>, <c>- 3 -</c>, <c>3</c>.
    /// </summary>
    public IReadOnlyList<string> PageNumberPatterns
    {
        get => _pageNumberPatterns;
        init => _pageNumberPatterns = Patterns(value);
    }

    private readonly double _bandRatio = 0.1;
    private readonly double _minPageFraction = 0.5;
    private readonly IReadOnlyList<string> _pageNumberPatterns = ReadOnly.List(
    [
        @"(?i)^(page|sida|seite)\s+\d+(\s+(of|av|von)\s+\d+)?$",
        @"^\d+\s*/\s*\d+$",
        @"^[-–]\s*\d+\s*[-–]$",
        @"^\d+$",
    ]);

    // A copy, each pattern checked once so a bad one fails here instead of while loading.
    private static IReadOnlyList<string> Patterns(IReadOnlyList<string> value)
    {
        Throw.IfNull(value, nameof(PageNumberPatterns));
        foreach (var pattern in value)
        {
            try
            {
                _ = new Regex(pattern, RegexOptions.CultureInvariant);
            }
            catch (ArgumentException ex)
            {
                Throw.ArgumentException(nameof(PageNumberPatterns), $"Not a valid regular expression: {pattern}", ex);
            }
        }

        return ReadOnly.List(value);
    }
}
```

`src/PdfProof/Semantics/HeadingClassificationOptions.cs`:
```csharp
namespace PdfProof.Semantics;

/// <summary>
/// HeadingClassification (untagged documents): a short block clearly larger than the body text, or bold, short and
/// followed by body text, is a heading. Levels come from size clusters, the largest being level 1.
/// </summary>
public sealed record HeadingClassificationOptions
{
    /// <summary>The default options.</summary>
    public static HeadingClassificationOptions Default { get; } = new();

    /// <summary>Run the pass. Default on.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>How much larger than the body text a heading must be. Default 1.15.</summary>
    public double MinSizeRatio
    {
        get => _minSizeRatio;
        init => _minSizeRatio = Throw.IfOutOfRange(value, 1, 10, nameof(MinSizeRatio));
    }

    /// <summary>The most lines a heading can have. Default 2.</summary>
    public int MaxLines
    {
        get => _maxLines;
        init => _maxLines = Throw.IfOutOfRange(value, 1, 20, nameof(MaxLines));
    }

    /// <summary>A bold, short block at body size that is followed by body text is a heading. Default on.</summary>
    public bool BoldShortLineIsHeading { get; init; } = true;

    private readonly double _minSizeRatio = 1.15;
    private readonly int _maxLines = 2;
}
```

`src/PdfProof/Semantics/TableDetectionOptions.cs`:
```csharp
namespace PdfProof.Semantics;

/// <summary>
/// TableDetection (untagged documents): consecutive rows of at least two cells whose columns line up are a table; the
/// first row is its header when its style differs from the next row's.
/// </summary>
public sealed record TableDetectionOptions
{
    /// <summary>The default options.</summary>
    public static TableDetectionOptions Default { get; } = new();

    /// <summary>Run the pass. Default on.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>The fewest rows a table has, header included. At least 2; default 2.</summary>
    public int MinRows
    {
        get => _minRows;
        init => _minRows = Throw.IfOutOfRange(value, 2, 1000, nameof(MinRows));
    }

    /// <summary>How far apart, in points, cells may be and still share a column. Default 3.</summary>
    public double ColumnTolerance
    {
        get => _columnTolerance;
        init => _columnTolerance = Throw.IfOutOfRange(value, 0, 100, nameof(ColumnTolerance));
    }

    private readonly int _minRows = 2;
    private readonly double _columnTolerance = 3;
}
```

`src/PdfProof/Semantics/ListDetectionOptions.cs`:
```csharp
using System.Text.RegularExpressions;

namespace PdfProof.Semantics;

/// <summary>
/// ListDetection (untagged documents): consecutive blocks at the same indentation that start with a bullet, a number,
/// or a small marker shape just left of the text (how HTML-to-PDF converters draw CSS bullets) are a list. The bullet
/// or number is left out of the item text.
/// </summary>
public sealed record ListDetectionOptions
{
    /// <summary>The default options.</summary>
    public static ListDetectionOptions Default { get; } = new();

    /// <summary>Run the pass. Default on.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Text bullets. A <c>-</c> followed by a digit is a minus sign, not a bullet. Default <c>• ◦ ▪ – * -</c>.</summary>
    public IReadOnlyList<string> BulletGlyphs
    {
        get => _bulletGlyphs;
        init => _bulletGlyphs = Glyphs(value);
    }

    /// <summary>Regular expressions for item numbers at the start of a line. Default <c>1.</c>, <c>1)</c>, <c>a.</c>, <c>a)</c>, <c>iv.</c>.</summary>
    public IReadOnlyList<string> NumberingPatterns
    {
        get => _numberingPatterns;
        init => _numberingPatterns = Patterns(value);
    }

    /// <summary>Read small filled shapes just left of a line as bullets. Default on.</summary>
    public bool UseMarkerShapes { get; init; } = true;

    /// <summary>How far apart, in points, item starts may be and still belong to one list. Default 3.</summary>
    public double IndentTolerance
    {
        get => _indentTolerance;
        init => _indentTolerance = Throw.IfOutOfRange(value, 0, 100, nameof(IndentTolerance));
    }

    private readonly IReadOnlyList<string> _bulletGlyphs = ReadOnly.List(["•", "◦", "▪", "–", "*", "-"]);
    private readonly IReadOnlyList<string> _numberingPatterns = ReadOnly.List([@"^\d{1,3}[.)]", @"^[a-z][.)]", @"^(?i:[ivxlc]{1,6})[.)]"]);
    private readonly double _indentTolerance = 3;

    private static IReadOnlyList<string> Glyphs(IReadOnlyList<string> value)
    {
        Throw.IfNull(value, nameof(BulletGlyphs));
        if (value.Any(string.IsNullOrWhiteSpace))
        {
            Throw.ArgumentException(nameof(BulletGlyphs), "A bullet glyph must not be empty or whitespace.");
        }

        return ReadOnly.List(value);
    }

    private static IReadOnlyList<string> Patterns(IReadOnlyList<string> value)
    {
        Throw.IfNull(value, nameof(NumberingPatterns));
        foreach (var pattern in value)
        {
            try
            {
                _ = new Regex(pattern, RegexOptions.CultureInvariant);
            }
            catch (ArgumentException ex)
            {
                Throw.ArgumentException(nameof(NumberingPatterns), $"Not a valid regular expression: {pattern}", ex);
            }
        }

        return ReadOnly.List(value);
    }
}
```

`src/PdfProof/Semantics/LinkAttachmentOptions.cs`:
```csharp
namespace PdfProof.Semantics;

/// <summary>LinkAttachment: link annotations become links on the runs of the text they cover.</summary>
public sealed record LinkAttachmentOptions
{
    /// <summary>The default options.</summary>
    public static LinkAttachmentOptions Default { get; } = new();

    /// <summary>Run the pass. Default on.</summary>
    public bool Enabled { get; init; } = true;
}
```

- [ ] **Step 5: Write the internals and wire the model in**

`src/PdfProof/Semantics/SemanticText.cs`:
```csharp
using System.Globalization;

namespace PdfProof.Semantics;

// Building and reshaping runs, and formatting for decision messages.
internal static class SemanticText
{
    public static string Join(IEnumerable<SemanticRun> runs) => string.Concat(runs.Select(run => run.Text));

    public static SemanticRun Plain(string text) => new(text, false, false, null);

    // Pieces joined by one space; then normalized. The space is bold, italic or linked only when the runs on both sides
    // are, so it merges into a styled stretch but never extends one ("Plain **bold** words", not "**bold **words").
    public static IReadOnlyList<SemanticRun> Concat(IEnumerable<IReadOnlyList<SemanticRun>> pieces)
    {
        var runs = new List<SemanticRun>();
        foreach (var piece in pieces.Where(piece => piece.Any(run => run.Text.Trim().Length > 0)))
        {
            if (runs.Count > 0)
            {
                var before = runs[runs.Count - 1];
                var after = piece.First(run => run.Text.Trim().Length > 0);
                runs.Add(new SemanticRun(
                    " ",
                    before.IsBold && after.IsBold,
                    before.IsItalic && after.IsItalic,
                    SemanticLink.Same(before.Link, after.Link) ? before.Link : null));
            }

            runs.AddRange(piece);
        }

        return Normalize(runs);
    }

    // Collapses whitespace, trims the ends, merges neighbours with the same style and link, drops empty runs.
    public static IReadOnlyList<SemanticRun> Normalize(IEnumerable<SemanticRun> runs)
    {
        var result = new List<SemanticRun>();
        foreach (var run in runs)
        {
            var text = Collapse(run.Text);
            if (result.Count > 0 && result[result.Count - 1].Text.EndsWith(" ", StringComparison.Ordinal) && text.StartsWith(" ", StringComparison.Ordinal))
            {
                text = text.Substring(1);
            }

            if (text.Length == 0)
            {
                continue;
            }

            if (result.Count > 0 && result[result.Count - 1] is var last && last.IsBold == run.IsBold && last.IsItalic == run.IsItalic && SemanticLink.Same(last.Link, run.Link))
            {
                result[result.Count - 1] = new SemanticRun(last.Text + text, last.IsBold, last.IsItalic, last.Link);
            }
            else
            {
                result.Add(new SemanticRun(text, run.IsBold, run.IsItalic, run.Link));
            }
        }

        if (result.Count > 0)
        {
            result[0] = With(result[0], result[0].Text.TrimStart());
            result[result.Count - 1] = With(result[result.Count - 1], result[result.Count - 1].Text.TrimEnd());
        }

        return result.Where(run => run.Text.Length > 0).ToList();
    }

    // The runs with the text from `start` (length `length`) linked; the runs around it keep their style.
    public static IReadOnlyList<SemanticRun> Link(IReadOnlyList<SemanticRun> runs, int start, int length, SemanticLink link)
    {
        var result = new List<SemanticRun>();
        var offset = 0;
        var end = start + length;
        foreach (var run in runs)
        {
            var runStart = offset;
            var runEnd = offset + run.Text.Length;
            offset = runEnd;
            if (runEnd <= start || runStart >= end)
            {
                result.Add(run);
                continue;
            }

            var from = Math.Max(start, runStart) - runStart;
            var to = Math.Min(end, runEnd) - runStart;
            if (from > 0)
            {
                result.Add(With(run, run.Text.Substring(0, from)));
            }

            result.Add(new SemanticRun(run.Text.Substring(from, to - from), run.IsBold, run.IsItalic, link));
            if (to < run.Text.Length)
            {
                result.Add(With(run, run.Text.Substring(to)));
            }
        }

        return Normalize(result);
    }

    public static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    public static string Excerpt(string text) => text.Length <= 30 ? text : text.Substring(0, 30) + "...";

    private static SemanticRun With(SemanticRun run, string text) => new(text, run.IsBold, run.IsItalic, run.Link);

    private static string Collapse(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length);
        foreach (var c in text)
        {
            var space = char.IsWhiteSpace(c);
            if (!space || builder.Length == 0 || builder[builder.Length - 1] != ' ')
            {
                builder.Append(space ? ' ' : c);
            }
        }

        return builder.ToString();
    }
}
```

`src/PdfProof/Semantics/SemanticBuilder.cs` (interim: Task 2 replaces the body):
```csharp
using PdfProof.Layout;
using PdfProof.Structure;

namespace PdfProof.Semantics;

// Builds the semantic model: a front end, the passes in their fixed order, then Finalizer (spec §6).
internal static class SemanticBuilder
{
    public static SemanticModel Build(LayoutBuildResult layout, StructureModel structure, SemanticOptions options, DiagnosticsCollector diagnostics)
    {
        var declared = options.Source switch
        {
            SemanticSourceOption.Structure => true,
            SemanticSourceOption.Layout => false,
            _ => structure.IsTagged,
        };
        return new SemanticModel(declared ? SemanticSource.Declared : SemanticSource.Inferred, [], [], [], []);
    }
}
```

In `src/PdfProof/PdfProfile.cs`, add after `Layout`:
```csharp
    /// <summary>How the semantic model is built.</summary>
    public SemanticOptions Semantics
    {
        get => _semantics;
        init => _semantics = Throw.IfNull(value, nameof(Semantics));
    }
```
Also add `private readonly SemanticOptions _semantics = SemanticOptions.Default;` and `using PdfProof.Semantics;`.

In `src/PdfProof/PdfProofDocument.cs`:
- add `using PdfProof.Semantics;`;
- add a `SemanticModel semantics` constructor parameter after `structure`, and the property:
```csharp
    /// <summary>What the PDF means: headings, paragraphs, tables, lists, figures, links and sections, from tags or inferred from the layout.</summary>
    public SemanticModel Semantics { get; }
```
- in `Open`, after the structure diagnostics, add `var semantics = SemanticBuilder.Build(layoutResult, structure, profile.Semantics, diagnostics);`, and pass `semantics` to the constructor.

- [ ] **Step 6: Record the public API and run the tests**

Run the RS0016 command and rebuild. Expected appended lines:
- every public member of the types above;
- `PdfProof.PdfProfile.Semantics.get/init`;
- `PdfProof.PdfProofDocument.Semantics.get`.

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: all tests pass on net48 and net10.0.

- [ ] **Step 7: Commit**

```bash
git add src tests/PdfProof.Tests
git commit -m "Add the public semantic model types and options

PdfProofDocument.Semantics exposes blocks, sections, artifacts and decisions,
with navigation helpers; PdfProfile.Semantics holds the front-end choice and one
options record per pass. The model is still empty; the pipeline follows.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Candidates, the layout front end and the pipeline

**Files:**
- Create: `src/PdfProof/Semantics/Candidate.cs`, `src/PdfProof/Semantics/SemanticContext.cs`, `src/PdfProof/Semantics/LayoutFrontEnd.cs`, `src/PdfProof/Semantics/Finalizer.cs`, `tests/PdfProof.Tests/LayoutFrontEndTests.cs`
- Modify: `src/PdfProof/Semantics/SemanticBuilder.cs`, `src/PdfProof/DiagnosticsCollector.cs`, `src/PdfProof/PdfDiagnosticCodes.cs`, `src/PdfProof/PublicAPI.Unshipped.txt`

**Interfaces:**
- Consumes: `LayoutModel`, `LayoutBuildResult`, `SemanticText`, `SemanticModel` (Task 1).
- Produces:
  - Internal types:
    - `CandidateKind { Unknown, Heading, Paragraph, Table, List, Figure }`;
    - `CandidateCell(IReadOnlyList<SemanticRun> Runs, LayoutBox Box)` with `Text`;
    - `CandidateLine(IReadOnlyList<CandidateCell> Cells, LayoutBox Box, double Size, bool IsBold)` with `Text`;
    - `Candidate(int page, IReadOnlyList<CandidateLine> lines, bool declared)`, with settable `Lines`, `Kind`, `Level`, `HeaderRows` and `Alt`, plus `PendingLinks`, `Box`, `Text` and `IsSingleCell`.
  - `SemanticContext(LayoutBuildResult layout, SemanticOptions options, DiagnosticsCollector diagnostics)` with `Layout`, `Build`, `Options`, `Candidates`, `Artifacts`, `Decide(SemanticPass, int? page, Candidate?, string)`.
  - `LayoutFrontEnd.Build(SemanticContext) -> List<Candidate>`, and `LayoutFrontEnd.Rows(IReadOnlyList<LayoutLine>) -> List<CandidateLine>` (lines on one baseline become one row; reused by ArtifactDetection).
  - `Finalizer.Build(SemanticContext, SemanticSource) -> SemanticModel` and `Finalizer.Runs(Candidate)`.
  - `DiagnosticsCollector.Log(string message)` (Debug, no entry), and `PdfDiagnosticCodes.Semantics = "Semantics"` with the summary `Semantics: inferred, 5 blocks, 0 artifacts`.

- [ ] **Step 1: Write the failing tests in `tests/PdfProof.Tests/LayoutFrontEndTests.cs`**

```csharp
using System.Globalization;
using PdfProof.Semantics;

namespace PdfProof.Tests;

public sealed class LayoutFrontEndTests
{
    // One layout block holding a heading, two one-line paragraphs and a two-row table, as MigraDoc writes it.
    internal static byte[] OneBlockInvoice()
    {
        var pdf = new RawPdf();
        pdf.AddPage(
            RawPdf.Text(70, 760, "Invoice", 20, "F2") +
            RawPdf.Text(70, 750, "Questions? Visit our support site or e-mail us.", 10) +
            RawPdf.Text(70, 738, "Read the terms and conditions", 10) +
            RawPdf.Text(70, 726, "Item", 10, "F2") + RawPdf.Text(250, 726, "Qty", 10, "F2") + RawPdf.Text(400, 726, "Amount", 10, "F2") +
            RawPdf.Text(70, 714, "Consulting, senior", 10) + RawPdf.Text(250, 714, "10", 10) + RawPdf.Text(400, 714, "14 500,00 SEK", 10));
        return pdf.Build();
    }

    [Fact]
    public void A_layout_block_is_split_where_size_boldness_cells_or_a_short_line_change()
    {
        var doc = PdfProofDocument.Load(OneBlockInvoice());

        Assert.Single(doc.Layout.Pages[0].Blocks); // the premise: one layout block
        Assert.Equal(
            ["Invoice", "Questions? Visit our support site or e-mail us.", "Read the terms and conditions", "Item Qty Amount", "Consulting, senior 10 14 500,00 SEK"],
            doc.Semantics.Blocks.Select(b => b.Text));
        Assert.Equal(
            [
                "LayoutFrontEnd (page 1): block 1: split before \"Questions? Visit our support s...\": size 20 → 10",
                "LayoutFrontEnd (page 1): block 1: split before \"Read the terms and conditions\": a short line ends a paragraph",
                "LayoutFrontEnd (page 1): block 1: split before \"Item Qty Amount\": cells 1 → 3",
                "LayoutFrontEnd (page 1): block 1: split before \"Consulting, senior 10 14 500,0...\": bold → regular",
            ],
            doc.Semantics.Decisions.Where(d => d.Pass == SemanticPass.LayoutFrontEnd).Select(d => d.ToString()));
    }

    [Fact]
    public void Unclassified_candidates_become_paragraphs_with_runs_from_the_spans()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 750, "Plain", 12) + RawPdf.Text(85, 750, "bold", 12, "F2") + RawPdf.Text(115, 750, "words", 12)));

        var paragraph = Assert.IsType<SemanticParagraph>(Assert.Single(doc.Semantics.Blocks));
        Assert.Equal("Plain bold words", paragraph.Text);
        Assert.Equal([("Plain ", false), ("bold", true), (" words", false)], paragraph.Runs.Select(r => (r.Text, r.IsBold)));
        Assert.Equal((1, SemanticSource.Inferred), (paragraph.Page, paragraph.Source));
    }

    [Fact]
    public void A_gap_larger_than_the_paragraph_gap_factor_splits_a_block()
    {
        var profile = PdfProfile.Default with { Semantics = SemanticOptions.Default with { ParagraphGapFactor = 0.1 } };
        var text = RawPdf.Text(50, 750, "A first line that runs on for quite a while here", 10) + RawPdf.Text(50, 738, "A second line that runs on for quite a while here", 10);

        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(text), new PdfProofOptions { Profile = profile });

        Assert.Equal(2, doc.Semantics.Blocks.Count);
        Assert.Contains(doc.Semantics.Decisions, d => d.Message.StartsWith("block 1: split before \"A second line", StringComparison.Ordinal) && d.Message.Contains(": gap "));
    }

    [Fact]
    public void An_empty_document_gives_an_empty_model()
    {
        // Review Focus 1.
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(""));

        Assert.Empty(doc.Semantics.Blocks);
        Assert.Empty(doc.Semantics.Artifacts);
        Assert.Equal(SemanticSource.Inferred, doc.Semantics.Source);
        Assert.Contains(doc.Diagnostics.Entries, e => e.ToString() == "Semantics: inferred, 0 blocks, 0 artifacts");
    }

    [Fact]
    public void The_semantic_summary_is_a_diagnostic_and_decisions_are_logged()
    {
        var logger = new ListLogger();

        var doc = PdfProofDocument.Load(OneBlockInvoice(), new PdfProofOptions { Logger = logger });

        Assert.Contains(doc.Diagnostics.Entries, e => e.ToString() == "Semantics: inferred, 5 blocks, 0 artifacts");
        Assert.Contains(logger.Entries, e => e.Level == Microsoft.Extensions.Logging.LogLevel.Debug && e.Message.StartsWith("LayoutFrontEnd (page 1): block 1: split before", StringComparison.Ordinal));
    }
}
```

Task 5 updates `A_layout_block_is_split…` and `The_semantic_summary_is_a_diagnostic…`, once the two table rows become one table block.

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: the five new tests FAIL. The blocks are empty, there are no decisions and no `Semantics` diagnostic.

- [ ] **Step 3: Write `src/PdfProof/Semantics/Candidate.cs`**

```csharp
using PdfProof.Layout;

namespace PdfProof.Semantics;

internal enum CandidateKind
{
    Unknown,
    Heading,
    Paragraph,
    Table,
    List,
    Figure,
}

// A cell of a candidate line: its runs and where it is. A paragraph or heading line has one cell.
internal sealed record CandidateCell(IReadOnlyList<SemanticRun> Runs, LayoutBox Box)
{
    public string Text => SemanticText.Join(Runs);
}

// A visual row (layout front end) or one structural row, item or paragraph (structure front end). Size and IsBold
// describe the row's text: the size with the most characters, and whether every span is bold.
internal sealed record CandidateLine(IReadOnlyList<CandidateCell> Cells, LayoutBox Box, double Size, bool IsBold)
{
    public string Text => string.Join(" ", Cells.Select(cell => cell.Text).Where(text => text.Length > 0));
}

// A block on its way through the passes. Mutable: passes reclassify and reshape candidates; Finalizer freezes them.
internal sealed class Candidate(int page, IReadOnlyList<CandidateLine> lines, bool declared)
{
    public int Page { get; } = page;

    public bool Declared { get; } = declared;

    public IReadOnlyList<CandidateLine> Lines { get; set; } = lines;

    public CandidateKind Kind { get; set; }

    public int? Level { get; set; }

    // Table candidates: how many of the first lines are header rows (0 or 1).
    public int HeaderRows { get; set; }

    public string? Alt { get; set; }

    // Tagged documents: link annotations on the element itself, placed on its text by LinkAttachment.
    public List<LayoutLink> PendingLinks { get; } = [];

    public LayoutBox Box => Lines.Count == 0 || Lines.All(line => line.Box == default) ? default : Geometry.Union(Lines.Where(line => line.Box != default).Select(line => line.Box));

    public string Text => string.Join(" ", Lines.Select(line => line.Text).Where(text => text.Length > 0));

    public bool IsSingleCell => Lines.All(line => line.Cells.Count == 1);
}
```

- [ ] **Step 4: Write `src/PdfProof/Semantics/SemanticContext.cs`**

```csharp
using PdfProof.Layout;

namespace PdfProof.Semantics;

// What the passes share: the layout they read, the candidates they rewrite, the artifacts and decisions they record.
internal sealed class SemanticContext(LayoutBuildResult layout, SemanticOptions options, DiagnosticsCollector diagnostics)
{
    public LayoutModel Layout => layout.Model;

    public LayoutBuildResult Build => layout;

    public SemanticOptions Options => options;

    public List<Candidate> Candidates { get; } = [];

    public List<SemanticArtifact> Artifacts { get; } = [];

    public List<(SemanticPass Pass, int? Page, Candidate? Candidate, string Message)> Decisions { get; } = [];

    public void Decide(SemanticPass pass, int? page, Candidate? candidate, string message)
    {
        Decisions.Add((pass, page, candidate, message));
        diagnostics.Log(new SemanticDecision(pass, page, null, message).ToString());
    }
}
```

In `src/PdfProof/DiagnosticsCollector.cs`, add:
```csharp
    // Logs at Debug without recording an entry: semantic decisions live in SemanticModel.Decisions.
    public void Log(string message) => logger?.Log(LogLevel.Debug, "{Decision}", message);
```
In `src/PdfProof/PdfDiagnosticCodes.cs`, add:
```csharp
    /// <summary>What the semantic model holds: its source and how many blocks and artifacts.</summary>
    public const string Semantics = "Semantics";
```

- [ ] **Step 5: Write `src/PdfProof/Semantics/LayoutFrontEnd.cs`**

```csharp
using PdfProof.Layout;

namespace PdfProof.Semantics;

// Untagged documents (spec §6, row 0a): each layout block becomes one or more candidates. Lines on one baseline form a
// row of cells. A block is split where font size, whole-line boldness or cell structure changes, where the gap between
// rows is larger than ParagraphGapFactor font sizes, or after a short line (a one-line paragraph followed by another).
internal static class LayoutFrontEnd
{
    public static List<Candidate> Build(SemanticContext context)
    {
        var candidates = new List<Candidate>();
        foreach (var page in context.Layout.Pages)
        {
            var textRight = page.Lines.Count == 0 ? 0 : page.Lines.Max(line => line.Box.Right);
            for (var b = 0; b < page.Blocks.Count; b++)
            {
                var block = page.Blocks[b];
                var rows = Rows(block.Lines);
                var current = new List<CandidateLine>();
                foreach (var row in rows)
                {
                    if (current.Count > 0 && SplitReason(current[current.Count - 1], row, block.Box.Left, textRight, context.Options) is { } reason)
                    {
                        candidates.Add(new Candidate(page.Number, current, declared: false));
                        context.Decide(SemanticPass.LayoutFrontEnd, page.Number, null, $"block {b + 1}: split before \"{SemanticText.Excerpt(row.Text)}\": {reason}");
                        current = [];
                    }

                    current.Add(row);
                }

                if (current.Count > 0)
                {
                    candidates.Add(new Candidate(page.Number, current, declared: false));
                }
            }
        }

        return candidates;
    }

    // Lines whose vertical centres are within half a line height of each other form one row; cells left to right.
    public static List<CandidateLine> Rows(IReadOnlyList<LayoutLine> lines)
    {
        var rows = new List<List<LayoutLine>>();
        foreach (var line in lines)
        {
            var row = rows.Count > 0 ? rows[rows.Count - 1] : null;
            if (row is not null && SameRow(row[0], line))
            {
                row.Add(line);
            }
            else
            {
                rows.Add([line]);
            }
        }

        return rows.Select(Row).ToList();
    }

    private static bool SameRow(LayoutLine a, LayoutLine b) =>
        Math.Abs((a.Box.Top + a.Box.Bottom) / 2 - (b.Box.Top + b.Box.Bottom) / 2) <= Math.Min(a.Box.Height, b.Box.Height) / 2;

    private static CandidateLine Row(List<LayoutLine> lines)
    {
        var ordered = lines.OrderBy(line => line.Box.Left).ToList();
        var cells = ordered.Select(line => new CandidateCell(Runs(line), line.Box)).ToList();
        var spans = ordered.SelectMany(line => line.Spans).ToList();
        var size = spans.GroupBy(span => span.Size).OrderByDescending(group => group.Sum(span => span.Text.Length)).ThenByDescending(group => group.Key).First().Key;
        return new CandidateLine(cells, Geometry.Union(ordered.Select(line => line.Box)), size, spans.All(span => span.IsBold));
    }

    private static IReadOnlyList<SemanticRun> Runs(LayoutLine line) =>
        SemanticText.Concat(line.Spans.Select(span => (IReadOnlyList<SemanticRun>)[new SemanticRun(span.Text, span.IsBold, span.IsItalic, null)]));

    private static string? SplitReason(CandidateLine previous, CandidateLine next, double blockLeft, double textRight, SemanticOptions options)
    {
        if ((previous.Cells.Count > 1) != (next.Cells.Count > 1))
        {
            return $"cells {previous.Cells.Count} → {next.Cells.Count}";
        }

        if (Math.Abs(previous.Size - next.Size) > 0.5)
        {
            return $"size {SemanticText.Number(previous.Size)} → {SemanticText.Number(next.Size)}";
        }

        if (previous.IsBold != next.IsBold)
        {
            return previous.IsBold ? "bold → regular" : "regular → bold";
        }

        var gap = previous.Box.Bottom - next.Box.Top;
        if (gap > options.ParagraphGapFactor * Math.Min(previous.Size, next.Size))
        {
            return $"gap {SemanticText.Number(gap)} pt";
        }

        if (previous.Cells.Count == 1 && previous.Box.Right < blockLeft + options.ShortLineRatio * (textRight - blockLeft))
        {
            return "a short line ends a paragraph";
        }

        return null;
    }
}
```

- [ ] **Step 6: Write `src/PdfProof/Semantics/Finalizer.cs` and the pipeline**

`src/PdfProof/Semantics/Finalizer.cs`:
```csharp
namespace PdfProof.Semantics;

// The last pass (spec §6, pass 7): unclassified candidates become paragraphs, candidates become immutable blocks,
// decisions point at their blocks, and sections are built from the headings.
internal static class Finalizer
{
    public static SemanticModel Build(SemanticContext context, SemanticSource source)
    {
        var blocks = new List<SemanticBlock>();
        var byCandidate = new Dictionary<Candidate, SemanticBlock>();
        foreach (var candidate in context.Candidates)
        {
            var block = Block(candidate);
            blocks.Add(block);
            byCandidate[candidate] = block;
        }

        var decisions = context.Decisions
            .Select(d => new SemanticDecision(d.Pass, d.Page, d.Candidate is { } c && byCandidate.TryGetValue(c, out var b) ? b : null, d.Message))
            .ToList();
        var artifacts = context.Artifacts.OrderBy(a => a.Role).ThenBy(a => a.Pages[0]).ThenBy(a => a.Text, StringComparer.Ordinal).ToList();
        return new SemanticModel(source, blocks, Sections(blocks, source), artifacts, decisions);
    }

    // A candidate's lines as one run list, lines and cells joined by a space.
    public static IReadOnlyList<SemanticRun> Runs(Candidate candidate) =>
        SemanticText.Concat(candidate.Lines.SelectMany(line => line.Cells).Select(cell => cell.Runs));

    private static SemanticBlock Block(Candidate candidate)
    {
        var source = candidate.Declared ? SemanticSource.Declared : SemanticSource.Inferred;
        return candidate.Kind switch
        {
            _ => new SemanticParagraph(candidate.Page, source, Runs(candidate), candidate.Box),
        };
    }

    private static IReadOnlyList<SemanticSection> Sections(IReadOnlyList<SemanticBlock> blocks, SemanticSource source) => [];
}
```
Tasks 4, 5, 6 and 8 add the `Heading`, `Table`, `List` and `Figure` cases to `Block`, and Task 4 replaces `Sections`.

Replace the body of `SemanticBuilder.Build`:
```csharp
    public static SemanticModel Build(LayoutBuildResult layout, StructureModel structure, SemanticOptions options, DiagnosticsCollector diagnostics)
    {
        var declared = options.Source switch
        {
            SemanticSourceOption.Structure => true,
            SemanticSourceOption.Layout => false,
            _ => structure.IsTagged,
        };
        var context = new SemanticContext(layout, options, diagnostics);

        // The structure front end comes in Task 8; until then tagged documents use the layout front end too.
        context.Candidates.AddRange(LayoutFrontEnd.Build(context));

        var source = declared ? SemanticSource.Declared : SemanticSource.Inferred;
        var model = Finalizer.Build(context, source);
        diagnostics.Add(
            PdfDiagnosticCodes.Semantics,
            null,
            $"{(declared ? "declared" : "inferred")}, {model.Blocks.Count} blocks, {model.Artifacts.Count} artifacts");
        return model;
    }
```

- [ ] **Step 7: Record the public API and run the tests**

Run the RS0016 command (it adds the `PdfDiagnosticCodes.Semantics` constant) and rebuild.

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: all tests pass on net48 and net10.0.

If `A_layout_block_is_split…` fails only on the premise `Assert.Single(...Blocks)`, the Rows strategy split the lines into several layout blocks. In that case, move the lines closer together (smaller baseline steps) until it's one block; don't change the strategy. If a split reason differs, check it against the rule order in `SplitReason`: cells, then size, boldness, gap, and finally the short-line rule.

- [ ] **Step 8: Commit**

```bash
git add src tests/PdfProof.Tests
git commit -m "Build semantic candidates from the layout and finalize them as paragraphs

The layout front end groups each block's lines into rows of cells and splits the
block where size, boldness, cell structure or spacing changes, or after a short
line, logging every split. Unclassified candidates become paragraphs with runs
from the spans; a Semantics diagnostic summarizes the model.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: ArtifactDetection

**Files:**
- Create: `src/PdfProof/Semantics/ArtifactDetector.cs`, `tests/PdfProof.Tests/ArtifactDetectionTests.cs`
- Modify: `src/PdfProof/Semantics/SemanticBuilder.cs`

**Interfaces:**
- Consumes: `SemanticContext`, `LayoutFrontEnd.Rows`, `ArtifactDetectionOptions` (Tasks 1–2).
- Produces: `ArtifactDetector.Run(SemanticContext)`, which adds `SemanticArtifact`s and removes the artifact text from the candidates.
  - It reads the band rows from the **layout** in both modes, so footers that tags leave out (Chromium's footer template) are found in tagged documents too.
  - Untagged candidate lines are removed by matching box; tagged ones by matching text on the page.

- [ ] **Step 1: Write the failing tests in `tests/PdfProof.Tests/ArtifactDetectionTests.cs`**

```csharp
using PdfProof.Semantics;

namespace PdfProof.Tests;

public sealed class ArtifactDetectionTests
{
    [Fact]
    public void A_repeating_page_number_footer_is_an_artifact_with_placeholders_and_not_a_block()
    {
        var doc = PdfProofDocument.Load(Pages(n => RawPdf.Text(50, 700, $"Body text on page {n}.") + RawPdf.Text(270, 40, $"Page {n} of 2", 10), 2));

        var artifact = Assert.Single(doc.Semantics.Artifacts);
        Assert.Equal((SemanticArtifactRole.Footer, "Page {n} of {N}"), (artifact.Role, artifact.Text));
        Assert.Equal([1, 2], artifact.Pages);
        Assert.Equal(["Body text on page 1.", "Body text on page 2."], doc.Semantics.Blocks.Select(b => b.Text));
        Assert.Contains(doc.Semantics.Decisions, d => d.ToString() == "ArtifactDetection: footer \"Page {n} of {N}\" repeats on pages 1, 2");
    }

    [Fact]
    public void A_running_header_repeating_on_every_page_is_an_artifact()
    {
        var doc = PdfProofDocument.Load(Pages(n => RawPdf.Text(50, 810, "Quarterly report", 10) + RawPdf.Text(50, 700, $"Content {n}."), 3));

        var artifact = Assert.Single(doc.Semantics.Artifacts);
        Assert.Equal((SemanticArtifactRole.Header, "Quarterly report"), (artifact.Role, artifact.Text));
        Assert.Equal([1, 2, 3], artifact.Pages);
    }

    [Fact]
    public void A_page_number_on_a_one_page_document_is_an_artifact_by_its_pattern()
    {
        var doc = PdfProofDocument.Load(Pages(_ => RawPdf.Text(50, 700, "Only page.") + RawPdf.Text(290, 40, "Page 1", 10), 1));

        var artifact = Assert.Single(doc.Semantics.Artifacts);
        Assert.Equal("Page {n}", artifact.Text);
        Assert.Equal([1], artifact.Pages);
        Assert.Contains(doc.Semantics.Decisions, d => d.ToString() == "ArtifactDetection (page 1): footer \"Page {n}\" is a page number");
    }

    [Fact]
    public void Band_text_that_neither_repeats_nor_is_a_page_number_stays_content()
    {
        var doc = PdfProofDocument.Load(Pages(n => RawPdf.Text(50, 700, $"Content {n}.") + (n == 2 ? RawPdf.Text(50, 40, "Total: 10", 10) : ""), 2));

        Assert.Empty(doc.Semantics.Artifacts);
        Assert.Contains(doc.Semantics.Blocks, b => b.Text == "Total: 10");
    }

    [Fact]
    public void A_two_part_footer_is_an_artifact_and_not_a_table()
    {
        // Review Focus 4: left text and right page number form one row of two cells.
        var doc = PdfProofDocument.Load(Pages(n => RawPdf.Text(50, 700, $"Content {n}.") + RawPdf.Text(50, 40, "Quarterly report", 10) + RawPdf.Text(500, 40, $"{n}", 10), 2));

        var artifact = Assert.Single(doc.Semantics.Artifacts);
        Assert.Equal("Quarterly report {n}", artifact.Text);
        Assert.Equal([1, 2], artifact.Pages);
        Assert.Equal(["Content 1.", "Content 2."], doc.Semantics.Blocks.Select(b => b.Text));
    }

    [Fact]
    public void The_pass_can_be_switched_off()
    {
        var profile = PdfProfile.Default with { Semantics = SemanticOptions.Default with { Artifacts = ArtifactDetectionOptions.Default with { Enabled = false } } };

        var doc = PdfProofDocument.Load(Pages(n => RawPdf.Text(50, 700, $"Body {n}.") + RawPdf.Text(270, 40, $"Page {n} of 2", 10), 2), new PdfProofOptions { Profile = profile });

        Assert.Empty(doc.Semantics.Artifacts);
        Assert.Contains(doc.Semantics.Blocks, b => b.Text == "Page 1 of 2");
    }

    internal static byte[] Pages(Func<int, string> content, int count)
    {
        var pdf = new RawPdf();
        for (var n = 1; n <= count; n++)
        {
            pdf.AddPage(content(n));
        }

        return pdf.Build();
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: the new tests FAIL: `Artifacts` is empty, and the footers are blocks. `Band_text_that_neither_repeats…` passes already; it guards against over-eager detection.

- [ ] **Step 3: Write `src/PdfProof/Semantics/ArtifactDetector.cs`**

```csharp
using System.Globalization;
using System.Text.RegularExpressions;
using PdfProof.Layout;

namespace PdfProof.Semantics;

// Spec §6, pass 1. Rows in the top or bottom band of the page are artifacts when the same text (page numbers written
// {n} and {N}) repeats at the same height on enough pages, or when a row is a page number by pattern. The rows come
// from the layout in both modes: tags often leave footers out. Their text is then removed from the candidates.
internal static class ArtifactDetector
{
    private static readonly Regex Digits = new(@"\d+", RegexOptions.CultureInvariant);

    public static void Run(SemanticContext context)
    {
        var options = context.Options.Artifacts;
        var pages = context.Layout.Pages;
        var patterns = options.PageNumberPatterns.Select(pattern => new Regex(pattern, RegexOptions.CultureInvariant)).ToList();

        var band = new List<(int Page, SemanticArtifactRole Role, CandidateLine Row, string Text)>();
        foreach (var page in pages)
        {
            foreach (var row in LayoutFrontEnd.Rows(page.Lines))
            {
                SemanticArtifactRole? role = row.Box.Bottom >= page.Height * (1 - options.BandRatio) ? SemanticArtifactRole.Header
                    : row.Box.Top <= page.Height * options.BandRatio ? SemanticArtifactRole.Footer
                    : null;
                if (role is { } r)
                {
                    band.Add((page.Number, r, row, Placeholders(row.Text, page.Number, pages.Count)));
                }
            }
        }

        var needed = Math.Max(2, (int)Math.Ceiling(options.MinPageFraction * pages.Count));
        var found = new List<(int Page, SemanticArtifactRole Role, CandidateLine Row, string Text)>();
        foreach (var group in band.GroupBy(item => (item.Role, item.Text, Height: Math.Round(item.Row.Box.Top / 2))))
        {
            var onPages = group.Select(item => item.Page).Distinct().OrderBy(page => page).ToList();
            if (onPages.Count >= needed)
            {
                found.AddRange(group);
                context.Decide(
                    SemanticPass.ArtifactDetection,
                    null,
                    null,
                    $"{Name(group.Key.Role)} \"{group.Key.Text}\" repeats on pages {string.Join(", ", onPages.Select(p => p.ToString(CultureInfo.InvariantCulture)))}");
            }
        }

        foreach (var item in band.Where(item => !found.Contains(item) && patterns.Any(pattern => pattern.IsMatch(item.Row.Text.Trim()))))
        {
            found.Add(item);
            context.Decide(SemanticPass.ArtifactDetection, item.Page, null, $"{Name(item.Role)} \"{item.Text}\" is a page number");
        }

        foreach (var artifact in found.GroupBy(item => (item.Role, item.Text)))
        {
            context.Artifacts.Add(new SemanticArtifact(artifact.Key.Role, artifact.Key.Text, artifact.Select(item => item.Page).Distinct().OrderBy(page => page).ToList()));
        }

        Remove(context, found);
    }

    // The page number becomes {n}; after it, the page count becomes {N}. Other numbers stay.
    private static string Placeholders(string text, int page, int count)
    {
        var pageText = page.ToString(CultureInfo.InvariantCulture);
        var countText = count.ToString(CultureInfo.InvariantCulture);
        var seenPage = false;
        return Digits.Replace(text, match =>
        {
            if (!seenPage && match.Value == pageText)
            {
                seenPage = true;
                return "{n}";
            }

            return seenPage && match.Value == countText ? "{N}" : match.Value;
        });
    }

    private static string Name(SemanticArtifactRole role) => role == SemanticArtifactRole.Header ? "header" : "footer";

    // Untagged candidates lose the rows with the artifact's box; tagged candidates (no boxes) the lines with its text.
    private static void Remove(SemanticContext context, List<(int Page, SemanticArtifactRole Role, CandidateLine Row, string Text)> found)
    {
        foreach (var candidate in context.Candidates.ToList())
        {
            var mine = found.Where(item => item.Page == candidate.Page).ToList();
            if (mine.Count == 0)
            {
                continue;
            }

            var kept = candidate.Lines
                .Where(line => !mine.Any(item => line.Box != default ? line.Box == item.Row.Box : line.Text == item.Row.Text))
                .ToList();
            if (kept.Count == 0)
            {
                context.Candidates.Remove(candidate);
            }
            else if (kept.Count != candidate.Lines.Count)
            {
                candidate.Lines = kept;
            }
        }
    }
}
```

In `SemanticBuilder.Build`, after the front end:
```csharp
        if (options.Artifacts.Enabled)
        {
            ArtifactDetector.Run(context);
        }
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: all tests pass on net48 and net10.0.

`A_two_part_footer…`: its row is one `CandidateLine` with two cells, and its text joins them with a space ("Quarterly report 1"). If the layout front end puts the footer's two lines into separate candidates, the removal still works per row box, because `LayoutFrontEnd.Rows` builds the same rows in both places.

- [ ] **Step 5: Commit**

```bash
git add src tests/PdfProof.Tests
git commit -m "Detect running headers, footers and page numbers as artifacts

Band rows that repeat at the same height on enough pages, with page numbers
written {n} and {N}, or that match a page-number pattern, become artifacts and
leave the body. Band rows come from the layout in both modes, so footers that tags
leave out are found too.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 4: HeadingClassification and sections

**Files:**
- Create: `src/PdfProof/Semantics/HeadingClassifier.cs`, `tests/PdfProof.Tests/HeadingClassificationTests.cs`
- Modify: `src/PdfProof/Semantics/Finalizer.cs` (the `Heading` case, `Sections`), `src/PdfProof/Semantics/SemanticBuilder.cs`, `tests/PdfProof.Tests/RawPdf.cs` (the `size` parameter of `Text` becomes a `double`)

**Interfaces:**
- Consumes: `Candidate`, `SemanticContext`, `HeadingClassificationOptions`, `Finalizer` (Tasks 1–2).
- Produces:
  - `HeadingClassifier.Run(SemanticContext)`: untagged documents only. It sets `Kind = Heading` and `Level`, and logs the body text size and each heading.
  - `Finalizer` turns heading candidates into `SemanticHeading` and builds `SemanticModel.Sections`:
    - declared headings nest by level;
    - inferred headings give flat sections.

- [ ] **Step 1: Let `RawPdf.Text` take a fractional size**

In `tests/PdfProof.Tests/RawPdf.cs`, change `int size = 12` to `double size = 12` in `Text`. Existing callers pass integers, which convert implicitly, and the text is already formatted with `FormattableString.Invariant`.

- [ ] **Step 2: Write the failing tests in `tests/PdfProof.Tests/HeadingClassificationTests.cs`**

```csharp
using System.Globalization;
using PdfProof.Semantics;

namespace PdfProof.Tests;

public sealed class HeadingClassificationTests
{
    [Fact]
    public void Larger_short_blocks_are_headings_with_levels_from_their_sizes()
    {
        var doc = PdfProofDocument.Load(ArtifactDetectionTests.Pages(n => n == 1
            ? RawPdf.Text(50, 750, "Invoice", 18) + RawPdf.Text(50, 700, "Questions? Visit our support site.", 12)
            : RawPdf.Text(50, 750, "Terms and conditions", 14) + RawPdf.Text(50, 700, "Payment within 30 days.", 12), 2));

        Assert.Equal([("Invoice", 1), ("Terms and conditions", 2)], doc.Semantics.Headings.Select(h => (h.Text, h.Level)));
        Assert.Equal(
            [
                "HeadingClassification: body text size 12 pt",
                "HeadingClassification (page 1): \"Invoice\": 18 pt vs body 12 pt → heading L1",
                "HeadingClassification (page 2): \"Terms and conditions\": 14 pt vs body 12 pt → heading L2",
            ],
            doc.Semantics.Decisions.Where(d => d.Pass == SemanticPass.HeadingClassification).Select(d => d.ToString()));
        Assert.Same(doc.Semantics.Headings[0], doc.Semantics.Decisions.Single(d => d.Message.StartsWith("\"Invoice\"", StringComparison.Ordinal)).Block);
    }

    [Fact]
    public void A_bold_short_block_followed_by_body_text_is_a_heading_below_the_larger_ones()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(
            RawPdf.Text(50, 750, "Invoice", 18) +
            RawPdf.Text(50, 700, "Payment terms", 12, "F2") +
            RawPdf.Text(50, 650, "Payment within 30 days of the invoice date.", 12)));

        Assert.Equal([("Invoice", 1), ("Payment terms", 2)], doc.Semantics.Headings.Select(h => (h.Text, h.Level)));
        Assert.Contains(doc.Semantics.Decisions, d => d.Message == "\"Payment terms\": bold, short and followed by body text → heading L2");
    }

    [Fact]
    public void A_bold_sentence_is_not_a_heading()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(
            RawPdf.Text(50, 700, "Payment is due now.", 12, "F2") +
            RawPdf.Text(50, 650, "Contact us with any questions about the invoice.", 12)));

        Assert.Empty(doc.Semantics.Headings);
    }

    [Fact]
    public void A_large_block_with_more_lines_than_MaxLines_is_not_a_heading()
    {
        // ShortLineRatio 0 keeps the three equal lines in one candidate; two body lines keep the body size at 12 pt.
        const string Line = "Large text that runs on and on";
        var profile = PdfProfile.Default with { Semantics = SemanticOptions.Default with { ShortLineRatio = 0 } };
        var doc = PdfProofDocument.Load(
            RowsLayoutTests.Pdf(
                RawPdf.Text(50, 750, Line, 18) + RawPdf.Text(50, 730, Line, 18) + RawPdf.Text(50, 710, Line, 18) +
                RawPdf.Text(50, 600, "Body text at the usual size, long enough to set the body size.", 12) +
                RawPdf.Text(50, 586, "More body text at the usual size, so that it outweighs the large.", 12)),
            new PdfProofOptions { Profile = profile });

        Assert.Empty(doc.Semantics.Headings);
    }

    [Fact]
    public void Inferred_headings_give_flat_sections()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(
            RawPdf.Text(50, 750, "Invoice", 18) +
            RawPdf.Text(50, 700, "Payment terms", 12, "F2") +
            RawPdf.Text(50, 650, "Payment within 30 days of the invoice date.", 12)));

        Assert.Equal(
            [("Invoice", 0, 0), ("Payment terms", 1, 0)],
            doc.Semantics.Sections.Select(s => (s.Heading.Text, s.Blocks.Count, s.Sections.Count)));
    }

    [Fact]
    public void Sizes_in_decisions_use_the_invariant_culture()
    {
        // Review Focus 3.
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("sv-SE");
        try
        {
            var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 750, "Invoice", 15.5) + RawPdf.Text(50, 700, "Body text at the usual size.", 10.5)));

            Assert.Contains(doc.Semantics.Decisions, d => d.Message == "\"Invoice\": 15.5 pt vs body 10.5 pt → heading L1");
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void The_pass_can_be_switched_off()
    {
        var profile = PdfProfile.Default with { Semantics = SemanticOptions.Default with { Headings = HeadingClassificationOptions.Default with { Enabled = false } } };

        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 750, "Invoice", 18) + RawPdf.Text(50, 700, "Body.", 12)), new PdfProofOptions { Profile = profile });

        Assert.Empty(doc.Semantics.Headings);
    }
}
```

- [ ] **Step 3: Run the tests to see them fail**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: the new tests FAIL, except the two that expect no headings: there are no headings and no sections yet.

- [ ] **Step 4: Write `src/PdfProof/Semantics/HeadingClassifier.cs`**

```csharp
namespace PdfProof.Semantics;

// Spec §6, pass 3 (untagged documents). The body text size is the size with the most characters. A one-cell candidate
// of at most MaxLines lines is a heading when it is MinSizeRatio larger than the body text, or when it is bold, doesn't
// end a sentence and is followed by text that isn't bold. Levels come from size clusters (0.5 pt), the largest being 1.
internal static class HeadingClassifier
{
    public static void Run(SemanticContext context)
    {
        var options = context.Options.Headings;
        var candidates = context.Candidates;
        var lines = candidates.SelectMany(candidate => candidate.Lines).Where(line => line.Text.Length > 0).ToList();
        if (lines.Count == 0)
        {
            return;
        }

        var body = lines.GroupBy(line => line.Size).OrderByDescending(group => group.Sum(line => line.Text.Length)).ThenBy(group => group.Key).First().Key;
        context.Decide(SemanticPass.HeadingClassification, null, null, $"body text size {SemanticText.Number(body)} pt");

        var headings = new List<(Candidate Candidate, double Size, string Reason)>();
        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (candidate.Kind != CandidateKind.Unknown || !candidate.IsSingleCell || candidate.Lines.Count == 0 || candidate.Lines.Count > options.MaxLines)
            {
                continue;
            }

            var size = candidate.Lines.Max(line => line.Size);
            if (size >= body * options.MinSizeRatio)
            {
                headings.Add((candidate, size, $"{SemanticText.Number(size)} pt vs body {SemanticText.Number(body)} pt"));
            }
            else if (options.BoldShortLineIsHeading
                && candidate.Lines.All(line => line.IsBold)
                && !candidate.Text.TrimEnd().EndsWith(".", StringComparison.Ordinal)
                && i + 1 < candidates.Count
                && candidates[i + 1].Lines.Any(line => !line.IsBold))
            {
                headings.Add((candidate, size, "bold, short and followed by body text"));
            }
        }

        var clusters = Clusters(headings.Select(heading => heading.Size));
        foreach (var (candidate, size, reason) in headings)
        {
            candidate.Kind = CandidateKind.Heading;
            candidate.Level = clusters.FindIndex(cluster => Math.Abs(cluster - size) <= 0.5) + 1;
            context.Decide(SemanticPass.HeadingClassification, candidate.Page, candidate, $"\"{SemanticText.Excerpt(candidate.Text)}\": {reason} → heading L{candidate.Level}");
        }
    }

    // Distinct sizes, largest first; a size within 0.5 pt of the previous cluster joins it.
    private static List<double> Clusters(IEnumerable<double> sizes)
    {
        var clusters = new List<double>();
        foreach (var size in sizes.OrderByDescending(size => size))
        {
            if (clusters.Count == 0 || clusters[clusters.Count - 1] - size > 0.5)
            {
                clusters.Add(size);
            }
        }

        return clusters;
    }
}
```

`candidate.Level` is an `int?`; interpolated, an integer formats the same in every culture.

- [ ] **Step 5: Headings and sections in `Finalizer`, and the pass in the pipeline**

In `Finalizer.Block`, add the case before the default:
```csharp
            CandidateKind.Heading => new SemanticHeading(candidate.Page, source, candidate.Level ?? 1, Runs(candidate), candidate.Box),
```
Replace `Sections`:
```csharp
    // Declared headings nest: a section runs to the next heading of the same or a higher level. Inferred headings
    // don't (their levels are unreliable): a section runs to the next heading of any level (spec §6, rules).
    private static IReadOnlyList<SemanticSection> Sections(IReadOnlyList<SemanticBlock> blocks, SemanticSource source)
    {
        var roots = new List<SectionBuilder>();
        var open = new List<SectionBuilder>();
        foreach (var block in blocks)
        {
            if (block is SemanticHeading heading)
            {
                if (source == SemanticSource.Inferred)
                {
                    open.Clear();
                }
                else
                {
                    while (open.Count > 0 && open[open.Count - 1].Heading.Level >= heading.Level)
                    {
                        open.RemoveAt(open.Count - 1);
                    }
                }

                var section = new SectionBuilder(heading);
                (open.Count == 0 ? roots : open[open.Count - 1].Children).Add(section);
                open.Add(section);
            }
            else if (open.Count > 0)
            {
                open[open.Count - 1].Blocks.Add(block);
            }
        }

        return roots.Select(section => section.Build()).ToList();
    }

    private sealed class SectionBuilder(SemanticHeading heading)
    {
        public SemanticHeading Heading { get; } = heading;

        public List<SemanticBlock> Blocks { get; } = [];

        public List<SectionBuilder> Children { get; } = [];

        public SemanticSection Build() => new(Heading, Blocks, Children.Select(child => child.Build()).ToList());
    }
```
In `SemanticBuilder.Build`, after ArtifactDetection:
```csharp
        if (!declared && options.Headings.Enabled)
        {
            HeadingClassifier.Run(context);
        }
```

- [ ] **Step 6: Run the tests**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: all tests pass on net48 and net10.0. Task 2's tests are unaffected: they check block texts and counts, not kinds.

- [ ] **Step 7: Commit**

```bash
git add src tests/PdfProof.Tests
git commit -m "Classify headings in untagged documents and build sections

Short blocks clearly larger than the body text, or bold, short and followed by
body text, become headings with levels from size clusters. Sections nest for
declared headings and stay flat for inferred ones. Decisions use the invariant
culture.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: TableDetection

**Files:**
- Create: `src/PdfProof/Semantics/TableDetector.cs`, `tests/PdfProof.Tests/TableDetectionTests.cs`
- Modify: `src/PdfProof/Semantics/Finalizer.cs` (the `Table` case), `src/PdfProof/Semantics/SemanticBuilder.cs`, `tests/PdfProof.Tests/LayoutFrontEndTests.cs`

**Interfaces:**
- Consumes: `Candidate`, `SemanticContext`, `TableDetectionOptions` (Tasks 1–2).
- Produces: `TableDetector.Run(SemanticContext)` for untagged documents.
  - Consecutive candidates made only of multi-cell rows (same page) become one `Table` candidate. Its lines are rows with one cell per column (missing cells are empty), and `HeaderRows` is 1 when the first row's boldness differs from the second's.
  - Columns are the merged horizontal extents of the cells, so left-, right- and centre-aligned cells all line up. A table needs at least two columns with a cell in every row.
  - `Finalizer` turns a `Table` candidate into a `SemanticTable`.

- [ ] **Step 1: Write the failing tests in `tests/PdfProof.Tests/TableDetectionTests.cs`**

```csharp
using PdfProof.Semantics;

namespace PdfProof.Tests;

public sealed class TableDetectionTests
{
    [Fact]
    public void Aligned_rows_of_cells_are_a_table_with_a_header_row_by_style()
    {
        var doc = PdfProofDocument.Load(LayoutFrontEndTests.OneBlockInvoice());

        var table = Assert.Single(doc.Semantics.Tables);
        Assert.Equal(["Item", "Qty", "Amount"], table.Header!.Cells.Select(c => c.Text));
        Assert.Equal([["Consulting, senior", "10", "14 500,00 SEK"]], table.Rows.Select(r => r.Cells.Select(c => c.Text).ToArray()));
        Assert.Equal("Item | Qty | Amount\nConsulting, senior | 10 | 14 500,00 SEK", table.Text);
        Assert.Contains(doc.Semantics.Decisions, d => d.ToString() == "TableDetection (page 1): 2 rows × 3 columns, header row by style");
    }

    [Fact]
    public void Right_aligned_numbers_share_a_column_with_their_header()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(
            RawPdf.Text(50, 700, "Item", 12, "F2") + RawPdf.Text(400, 700, "Amount", 12, "F2") +
            RawPdf.Text(50, 685, "Consulting, senior", 12) + RawPdf.Text(380, 685, "14 500,00 SEK", 12) +
            RawPdf.Text(50, 670, "Travel", 12) + RawPdf.Text(420, 670, "900,00", 12)));

        var table = Assert.Single(doc.Semantics.Tables);
        Assert.Equal(["Item", "Amount"], table.Header!.Cells.Select(c => c.Text));
        Assert.Equal([["Consulting, senior", "14 500,00 SEK"], ["Travel", "900,00"]], table.Rows.Select(r => r.Cells.Select(c => c.Text).ToArray()));
    }

    [Fact]
    public void A_single_row_of_cells_is_not_a_table()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 700, "Total:", 12) + RawPdf.Text(400, 700, "14 500,00 SEK", 12)));

        Assert.Empty(doc.Semantics.Tables);
        Assert.Equal("Total: 14 500,00 SEK", Assert.IsType<SemanticParagraph>(Assert.Single(doc.Semantics.Blocks)).Text);
    }

    [Fact]
    public void Rows_whose_cells_share_no_column_are_not_a_table()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(
            RawPdf.Text(50, 700, "North", 12) + RawPdf.Text(250, 700, "East", 12) +
            RawPdf.Text(150, 685, "South", 12) + RawPdf.Text(350, 685, "West", 12)));

        Assert.Empty(doc.Semantics.Tables);
    }

    [Fact]
    public void Rows_of_the_same_style_have_no_header()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(
            RawPdf.Text(50, 700, "Consulting", 12) + RawPdf.Text(300, 700, "10", 12) +
            RawPdf.Text(50, 685, "Travel", 12) + RawPdf.Text(300, 685, "2", 12)));

        var table = Assert.Single(doc.Semantics.Tables);
        Assert.Null(table.Header);
        Assert.Equal(2, table.Rows.Count);
    }

    [Fact]
    public void The_pass_can_be_switched_off()
    {
        var profile = PdfProfile.Default with { Semantics = SemanticOptions.Default with { Tables = TableDetectionOptions.Default with { Enabled = false } } };

        var doc = PdfProofDocument.Load(LayoutFrontEndTests.OneBlockInvoice(), new PdfProofOptions { Profile = profile });

        Assert.Empty(doc.Semantics.Tables);
    }
}
```

In `LayoutFrontEndTests`, the two tests that load `OneBlockInvoice()` now see one table instead of two rows:
- in `A_layout_block_is_split…`, the expected block texts become `["Invoice", "Questions? Visit our support site or e-mail us.", "Read the terms and conditions", "Item | Qty | Amount\nConsulting, senior | 10 | 14 500,00 SEK"]`;
- in `The_semantic_summary_is_a_diagnostic…`, the summary becomes `"Semantics: inferred, 4 blocks, 0 artifacts"`.

The decisions they check are unchanged.

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: the table tests FAIL (no tables), and so do the two updated `LayoutFrontEndTests`. The tests that expect no table pass already.

- [ ] **Step 3: Write `src/PdfProof/Semantics/TableDetector.cs`**

```csharp
using PdfProof.Layout;

namespace PdfProof.Semantics;

// Spec §6, pass 4 (untagged documents). Consecutive candidates on one page made only of rows with two or more cells
// are a table when there are MinRows rows and at least two columns hold a cell in every row. Columns are the merged
// horizontal extents of all cells (ColumnTolerance apart), so left-, right- and centre-aligned cells line up; two cells
// of one row in the same column means the rows don't align. The first row is the header when its boldness differs
// from the second row's.
internal static class TableDetector
{
    public static void Run(SemanticContext context)
    {
        var options = context.Options.Tables;
        var candidates = context.Candidates;
        for (var i = 0; i < candidates.Count; i++)
        {
            if (!IsRows(candidates[i]))
            {
                continue;
            }

            var j = i;
            var rows = new List<CandidateLine>();
            while (j < candidates.Count && IsRows(candidates[j]) && candidates[j].Page == candidates[i].Page)
            {
                rows.AddRange(candidates[j].Lines);
                j++;
            }

            if (rows.Count >= options.MinRows && Align(rows, options.ColumnTolerance) is { } aligned)
            {
                var header = rows[0].IsBold != rows[1].IsBold ? 1 : 0;
                var table = new Candidate(candidates[i].Page, aligned, declared: false) { Kind = CandidateKind.Table, HeaderRows = header };
                candidates.RemoveRange(i, j - i);
                candidates.Insert(i, table);
                context.Decide(
                    SemanticPass.TableDetection,
                    table.Page,
                    table,
                    $"{rows.Count} rows × {aligned[0].Cells.Count} columns" + (header == 1 ? ", header row by style" : ""));
            }
            else
            {
                i = j - 1;
            }
        }
    }

    private static bool IsRows(Candidate candidate) =>
        candidate.Kind == CandidateKind.Unknown && candidate.Lines.Count > 0 && candidate.Lines.All(line => line.Cells.Count >= 2);

    // The rows with one cell per column, or null when they don't line up.
    private static List<CandidateLine>? Align(List<CandidateLine> rows, double tolerance)
    {
        var columns = new List<(double Left, double Right)>();
        foreach (var cell in rows.SelectMany(row => row.Cells).OrderBy(cell => cell.Box.Left))
        {
            if (columns.Count > 0 && cell.Box.Left <= columns[columns.Count - 1].Right + tolerance)
            {
                var last = columns[columns.Count - 1];
                columns[columns.Count - 1] = (last.Left, Math.Max(last.Right, cell.Box.Right));
            }
            else
            {
                columns.Add((cell.Box.Left, cell.Box.Right));
            }
        }

        var aligned = new List<CandidateLine>();
        var filled = new int[columns.Count];
        foreach (var row in rows)
        {
            var cells = new CandidateCell?[columns.Count];
            foreach (var cell in row.Cells)
            {
                var column = columns.FindIndex(c => cell.Box.Left >= c.Left - tolerance && cell.Box.Right <= c.Right + tolerance);
                if (column < 0 || cells[column] is not null)
                {
                    return null;
                }

                cells[column] = cell;
                filled[column]++;
            }

            aligned.Add(row with { Cells = cells.Select(cell => cell ?? new CandidateCell([], default)).ToList() });
        }

        return filled.Count(count => count == rows.Count) >= 2 ? aligned : null;
    }
}
```

In `Finalizer.Block`, add:
```csharp
            CandidateKind.Table => new SemanticTable(
                candidate.Page,
                source,
                candidate.HeaderRows > 0 ? Row(candidate.Lines[0]) : null,
                candidate.Lines.Skip(candidate.HeaderRows).Select(Row).ToList(),
                candidate.Box),
```
and the helper:
```csharp
    private static SemanticRow Row(CandidateLine line) =>
        new(line.Cells.Select(cell => new SemanticCell(SemanticText.Normalize(cell.Runs))).ToList());
```
In `SemanticBuilder.Build`, after HeadingClassification:
```csharp
        if (!declared && options.Tables.Enabled)
        {
            TableDetector.Run(context);
        }
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: all tests pass on net48 and net10.0.

`Right_aligned_numbers…` depends on the cells' extents overlapping: "Amount" 400–440 against "14 500,00 SEK" 380–455 and "900,00" 420–455. If the Rows strategy merges "Consulting, senior" with its amount into one line (a gap below `ColumnGapFactor` × size), move the amounts further right. The test is about columns, not line grouping.

- [ ] **Step 5: Commit**

```bash
git add src tests/PdfProof.Tests
git commit -m "Detect tables in untagged documents from aligned rows of cells

Consecutive multi-cell rows whose cells line up in at least two full columns
become a table, with a header row when the first row's boldness differs. Column
extents are merged, so right-aligned numbers share their header's column.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: List markers and ListDetection

**Files:**
- Create: `src/PdfProof/Semantics/ListDetector.cs`, `tests/PdfProof.Tests/ListDetectionTests.cs`
- Modify: `src/PdfProof/Layout/LayoutBuildResult.cs`, `src/PdfProof/Layout/LayoutBuilder.cs`, `src/PdfProof/Semantics/Finalizer.cs` (the `List` case), `src/PdfProof/Semantics/SemanticBuilder.cs`, `tests/PdfProof.Tests/RawPdf.cs`

**Interfaces:**
- Consumes: `Candidate`, `SemanticContext`, `ListDetectionOptions` (Tasks 1–2), the layout page pass.
- Produces:
  - **Marker collection:** `LayoutBuildResult.Markers : IReadOnlyDictionary<int, IReadOnlyList<LayoutBox>>`. It holds the boxes of the small paths on each page (at most 12 × 12 pt, at least 0.5 pt, inside the crop box), deduplicated and ordered top-down. It's collected in the existing page pass and isn't part of the public layout model.
  - **`ListDetector.Run(SemanticContext)`** (untagged documents only):
    - **Which candidates:** consecutive one-cell candidates on one page that start with an item marker at the same indentation, within `IndentTolerance`, become one `List` candidate with one line per item.
    - **Item markers:**
      - a text bullet followed by whitespace or a letter (but `-` followed by a digit is a minus sign);
      - a numbering pattern followed by whitespace;
      - a marker shape that fits the line: at most 0.6 × the font size, ending at most 2 font sizes left of the text, and vertically within the line.
    - **Continuation lines** (lines that don't start an item) join the previous item. Bullets and numbers are stripped from the item text.
  - `Finalizer` turns a `List` candidate into a `SemanticList`.
  - `RawPdf.Square(double x, double y, double size)`: a filled square, as a content-stream fragment.

- [ ] **Step 1: Add the square helper to `tests/PdfProof.Tests/RawPdf.cs`**

```csharp
    /// A filled square of `size` points with its lower-left corner at (x, y), as HTML-to-PDF converters draw CSS bullets.
    public static string Square(double x, double y, double size) =>
        FormattableString.Invariant($"{x} {y} {size} {size} re f\n");
```

- [ ] **Step 2: Write the failing tests in `tests/PdfProof.Tests/ListDetectionTests.cs`**

```csharp
using PdfProof.Semantics;

namespace PdfProof.Tests;

public sealed class ListDetectionTests
{
    [Fact]
    public void Lines_starting_with_a_text_bullet_are_a_list_without_the_bullets()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 700, "- First item", 12) + RawPdf.Text(50, 685, "- Second item", 12)));

        var list = Assert.Single(doc.Semantics.Lists);
        Assert.Equal(["First item", "Second item"], list.Items.Select(i => i.Text));
        Assert.Contains(doc.Semantics.Decisions, d => d.ToString() == "ListDetection (page 1): 2 items (bullet \"-\")");
    }

    [Fact]
    public void A_bullet_directly_followed_by_a_letter_is_still_a_bullet()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 700, "-Payment within 30 days.", 12)));

        Assert.Equal(["Payment within 30 days."], Assert.Single(doc.Semantics.Lists).Items.Select(i => i.Text));
    }

    [Fact]
    public void A_minus_sign_before_a_number_is_not_a_bullet()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 700, "-5 degrees outside.", 12)));

        Assert.Empty(doc.Semantics.Lists);
    }

    [Fact]
    public void Numbered_lines_are_a_list()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 700, "1. First", 12) + RawPdf.Text(50, 685, "2. Second", 12)));

        var list = Assert.Single(doc.Semantics.Lists);
        Assert.Equal(["First", "Second"], list.Items.Select(i => i.Text));
        Assert.Contains(doc.Semantics.Decisions, d => d.Message == "2 items (numbering)");
    }

    [Fact]
    public void A_small_square_just_left_of_a_line_is_a_bullet()
    {
        // How SelectPdf and Chromium draw a CSS list bullet: a 2-3 pt filled square left of the item text.
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Square(40, 700, 3) + RawPdf.Text(50, 698, "Payment within 30 days.", 12)));

        Assert.Equal(["Payment within 30 days."], Assert.Single(doc.Semantics.Lists).Items.Select(i => i.Text));
        Assert.Contains(doc.Semantics.Decisions, d => d.Message == "1 items (marker shapes)");
    }

    [Fact]
    public void A_square_elsewhere_on_the_page_is_not_a_bullet()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Square(300, 500, 3) + RawPdf.Text(50, 698, "Payment within 30 days.", 12)));

        Assert.Empty(doc.Semantics.Lists);
    }

    [Fact]
    public void A_line_without_a_bullet_continues_the_item_above()
    {
        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(
            RawPdf.Text(50, 700, "- A first item long enough to wrap onto the next line of text", 12) +
            RawPdf.Text(58, 686, "and it continues here.", 12)));

        Assert.Equal(["A first item long enough to wrap onto the next line of text and it continues here."], Assert.Single(doc.Semantics.Lists).Items.Select(i => i.Text));
    }

    [Fact]
    public void The_pass_can_be_switched_off()
    {
        var profile = PdfProfile.Default with { Semantics = SemanticOptions.Default with { Lists = ListDetectionOptions.Default with { Enabled = false } } };

        var doc = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 700, "- First item", 12)), new PdfProofOptions { Profile = profile });

        Assert.Empty(doc.Semantics.Lists);
    }
}
```

- [ ] **Step 3: Run the tests to see them fail**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: the list tests FAIL (no lists), except the two that expect none.

- [ ] **Step 4: Collect the markers in the layout pass**

Replace `src/PdfProof/Layout/LayoutBuildResult.cs`:
```csharp
namespace PdfProof.Layout;

// The layout model plus what only the structure and semantic layers use, collected in the same page pass so pages are
// read once: for tagged documents the marked text per page (page number -> MCID -> text), and for every document the
// boxes of small paths per page, the candidates for list markers drawn as shapes.
internal sealed record LayoutBuildResult(
    LayoutModel Model,
    IReadOnlyDictionary<int, IReadOnlyDictionary<int, string>> MarkedText,
    IReadOnlyDictionary<int, IReadOnlyList<LayoutBox>> Markers);
```
In `LayoutBuilder.Build`:
- add `var markers = new Dictionary<int, IReadOnlyList<LayoutBox>>();` next to `markedText`;
- in the page loop, after `pages.Add(…)`, add `markers[pageNumber] = Markers(page, pageNumber, options);`;
- pass `markers` as the new last argument of `new LayoutBuildResult(…)`.

Add the helper (it needs `using UglyToad.PdfPig.Core;`, already imported):
```csharp
    // Small paths (a bullet is a few points across): candidates for list markers drawn as shapes. Paths outside the
    // visible page (HTML converters draw the whole document on every page) are left out.
    private static IReadOnlyList<LayoutBox> Markers(Page page, int number, LayoutOptions options)
    {
        var visible = page.CropBox.Bounds;
        return PdfPigCall.Read(() => page.Paths.Select(path => path.GetBoundingRectangle()).ToList(), number)
            .Where(rectangle => rectangle is { } r && Geometry.Intersects(r, visible))
            .Select(rectangle => Geometry.Round(Geometry.Bounds(rectangle!.Value), options.Decimals))
            .Where(box => box.Width >= 0.5 && box.Height >= 0.5 && box.Width <= 12 && box.Height <= 12)
            .Distinct()
            .OrderByDescending(box => box.Top).ThenBy(box => box.Left)
            .ToList();
    }
```

- [ ] **Step 5: Write `src/PdfProof/Semantics/ListDetector.cs`**

```csharp
using System.Text.RegularExpressions;
using PdfProof.Layout;

namespace PdfProof.Semantics;

// Spec §6, pass 5 (untagged documents; nesting comes in Plan 5b). Consecutive one-cell candidates on one page whose
// first line starts an item, at the same indentation, form a list. An item starts with a text bullet, a number, or a
// marker shape (a small path just left of the line, how HTML-to-PDF converters draw CSS bullets). Lines that don't
// start an item continue the one above. Bullets and numbers are left out of the item text.
internal static class ListDetector
{
    public static void Run(SemanticContext context)
    {
        var options = context.Options.Lists;
        var numbering = options.NumberingPatterns.Select(pattern => new Regex(pattern, RegexOptions.CultureInvariant)).ToList();
        var candidates = context.Candidates;

        (int Length, string How)? Start(CandidateLine line, int page)
        {
            var text = line.Text;
            foreach (var glyph in options.BulletGlyphs)
            {
                if (text.Length > glyph.Length && text.StartsWith(glyph, StringComparison.Ordinal))
                {
                    var next = text[glyph.Length];
                    if (glyph == "-" && char.IsDigit(next))
                    {
                        continue;
                    }

                    if (char.IsWhiteSpace(next) || char.IsLetter(next))
                    {
                        return (glyph.Length, $"bullet \"{glyph}\"");
                    }
                }
            }

            foreach (var pattern in numbering)
            {
                var match = pattern.Match(text);
                if (match.Success && match.Index == 0 && text.Length > match.Length && char.IsWhiteSpace(text[match.Length]))
                {
                    return (match.Length, "numbering");
                }
            }

            if (options.UseMarkerShapes && context.Build.Markers.TryGetValue(page, out var markers) && markers.Any(marker => Fits(marker, line)))
            {
                return (0, "marker shapes");
            }

            return null;
        }

        bool Eligible(Candidate candidate) => candidate.Kind == CandidateKind.Unknown && candidate.IsSingleCell && candidate.Lines.Count > 0;

        for (var i = 0; i < candidates.Count; i++)
        {
            var first = candidates[i];
            if (!Eligible(first) || Start(first.Lines[0], first.Page) is null)
            {
                continue;
            }

            var left = first.Lines[0].Box.Left;
            var items = new List<List<CandidateLine>>();
            var how = new SortedSet<string>(StringComparer.Ordinal);
            var j = i;
            while (j < candidates.Count
                && Eligible(candidates[j])
                && candidates[j].Page == first.Page
                && Start(candidates[j].Lines[0], first.Page) is not null
                && Math.Abs(candidates[j].Lines[0].Box.Left - left) <= options.IndentTolerance)
            {
                foreach (var line in candidates[j].Lines)
                {
                    if (Start(line, first.Page) is { } start)
                    {
                        how.Add(start.How);
                        items.Add([Strip(line, start.Length)]);
                    }
                    else
                    {
                        items[items.Count - 1].Add(line);
                    }
                }

                j++;
            }

            var list = new Candidate(first.Page, items.Select(Item).ToList(), declared: false) { Kind = CandidateKind.List };
            candidates.RemoveRange(i, j - i);
            candidates.Insert(i, list);
            context.Decide(SemanticPass.ListDetection, list.Page, list, $"{items.Count} items ({string.Join(", ", how)})");
        }
    }

    // A marker shape fits a line when it is at most 0.6 font sizes across, ends left of the text by at most two font
    // sizes, and its centre is within the line's height.
    private static bool Fits(LayoutBox marker, CandidateLine line)
    {
        var size = line.Size;
        var centre = (marker.Top + marker.Bottom) / 2;
        return marker.Width <= 0.6 * size
            && marker.Height <= 0.6 * size
            && marker.Right <= line.Box.Left + 0.5
            && line.Box.Left - marker.Right <= 2 * size
            && centre >= line.Box.Bottom - 0.2 * size
            && centre <= line.Box.Top + 0.2 * size;
    }

    // The line without its first `length` characters (a bullet or number) and the whitespace after them.
    private static CandidateLine Strip(CandidateLine line, int length)
    {
        if (length == 0)
        {
            return line;
        }

        var cell = line.Cells[0];
        var runs = new List<SemanticRun>();
        var skip = length;
        foreach (var run in cell.Runs)
        {
            if (skip >= run.Text.Length)
            {
                skip -= run.Text.Length;
                continue;
            }

            runs.Add(new SemanticRun(run.Text.Substring(skip), run.IsBold, run.IsItalic, run.Link));
            skip = 0;
        }

        return line with { Cells = [cell with { Runs = SemanticText.Normalize(runs) }] };
    }

    // An item's lines as one line with one cell.
    private static CandidateLine Item(List<CandidateLine> lines) => new(
        [new CandidateCell(SemanticText.Concat(lines.Select(line => line.Cells[0].Runs)), Geometry.Union(lines.Select(line => line.Box)))],
        Geometry.Union(lines.Select(line => line.Box)),
        lines[0].Size,
        lines.All(line => line.IsBold));
}
```

In `Finalizer.Block`, add:
```csharp
            CandidateKind.List => new SemanticList(
                candidate.Page,
                source,
                candidate.Lines.Select(line => new SemanticListItem(SemanticText.Concat(line.Cells.Select(cell => cell.Runs)))).ToList(),
                candidate.Box),
```
In `SemanticBuilder.Build`, after TableDetection:
```csharp
        if (!declared && options.Lists.Enabled)
        {
            ListDetector.Run(context);
        }
```

- [ ] **Step 6: Run the tests**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: all tests pass on net48 and net10.0.

- **If `A_small_square…` fails because no marker was collected,** check how PdfPig reports `re f` paths. `GetBoundingRectangle()` returns null for a path it can't measure, and those are skipped. A `RawPdf` square drawn as `x y w h re f` must come back with its 3 × 3 box. If it doesn't, print `page.Paths` in a scratch test before changing the filter.
- **If `A_line_without_a_bullet_continues…` comes out as two lists or a list plus a paragraph,** the layout front end split the two lines. The first line must be the widest on the page, so the short-line rule doesn't apply. Lengthen it rather than change the rule.

- [ ] **Step 7: Commit**

```bash
git add src tests/PdfProof.Tests
git commit -m "Detect lists from text bullets, numbering and marker shapes

The layout pass now collects small paths per page (internally), and ListDetection
reads one just left of a line as a bullet, which is how SelectPdf and Chromium draw
CSS list bullets. Continuation lines join their item; bullets and numbers leave the
item text, and a minus sign before a digit is not a bullet.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 7: LinkAttachment

**Files:**
- Create: `src/PdfProof/Semantics/LinkAttacher.cs`, `tests/PdfProof.Tests/LinkAttachmentTests.cs`
- Modify: `src/PdfProof/Semantics/SemanticBuilder.cs`

**Interfaces:**
- Consumes: `Candidate`, `SemanticContext`, `SemanticText.Link`, `LayoutLink`, `LinkAttachmentOptions` (Tasks 1–6).
- Produces: `LinkAttacher.Run(SemanticContext)`:
  - **Untagged:** each layout link goes, in the page's link order, to the first candidate overlapping its rectangle whose cell under the rectangle contains the link's text at a still-unlinked position.
  - **Tagged:** each candidate's `PendingLinks` go to the first unlinked occurrence of their text in the candidate.
  - **Line merging:** paragraph and heading candidates first have their lines merged into one, so a link that wraps across two lines still matches.
  - **Logging:** every link is logged, including those without text and those whose text isn't found.

- [ ] **Step 1: Write the failing tests in `tests/PdfProof.Tests/LinkAttachmentTests.cs`**

```csharp
using PdfProof.Semantics;

namespace PdfProof.Tests;

public sealed class LinkAttachmentTests
{
    [Fact]
    public void A_link_becomes_a_run_on_the_text_under_it()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Text(50, 700, "Questions? Visit") + RawPdf.Text(145, 700, "our support site") + RawPdf.Text(236, 700, "or e-mail us."));
        pdf.Annotate(page, "<< /Type /Annot /Subtype /Link /Rect [143 695 230 715] /A << /S /URI /URI (https://example.com/support) >> >>");

        var doc = PdfProofDocument.Load(pdf.Build());

        var paragraph = Assert.IsType<SemanticParagraph>(Assert.Single(doc.Semantics.Blocks));
        Assert.Equal("Questions? Visit our support site or e-mail us.", paragraph.Text);
        Assert.Equal(
            [("Questions? Visit ", (string?)null), ("our support site", "https://example.com/support"), (" or e-mail us.", null)],
            paragraph.Runs.Select(r => (r.Text, r.Link?.Uri)));
        var linked = Assert.Single(doc.Semantics.Links);
        Assert.Equal(("our support site", "https://example.com/support"), (linked.Text, linked.Link.Uri));
        Assert.Same(paragraph, linked.Block);
        Assert.Contains(doc.Semantics.Decisions, d => d.ToString() == "LinkAttachment (page 1): \"our support site\" → https://example.com/support");
    }

    [Fact]
    public void An_internal_link_has_its_target_page()
    {
        var pdf = new RawPdf();
        var first = pdf.AddPage(RawPdf.Text(50, 700, "Read the terms and conditions"));
        var second = pdf.AddPage(RawPdf.Text(50, 750, "Terms"));
        pdf.Annotate(first, $"<< /Type /Annot /Subtype /Link /Rect [45 695 250 715] /A << /S /GoTo /D [{second} 0 R /Fit] >> >>");

        var doc = PdfProofDocument.Load(pdf.Build());

        var run = Assert.Single(doc.Semantics.Paragraphs[0].Runs);
        Assert.Equal(("Read the terms and conditions", (int?)2, (string?)null), (run.Text, run.Link?.TargetPage, run.Link?.Uri));
    }

    [Fact]
    public void Two_links_with_the_same_text_each_take_their_own_occurrence()
    {
        // Review Focus 2.
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Text(50, 700, "See terms") + RawPdf.Text(112, 700, "here") + RawPdf.Text(143, 700, "and pricing") + RawPdf.Text(209, 700, "here"));
        pdf.Annotate(page, "<< /Type /Annot /Subtype /Link /Rect [110 695 138 712] /A << /S /URI /URI (https://example.com/terms) >> >>");
        pdf.Annotate(page, "<< /Type /Annot /Subtype /Link /Rect [207 695 235 712] /A << /S /URI /URI (https://example.com/pricing) >> >>");

        var doc = PdfProofDocument.Load(pdf.Build());

        Assert.Equal(
            [("See terms ", (string?)null), ("here", "https://example.com/terms"), (" and pricing ", null), ("here", "https://example.com/pricing")],
            doc.Semantics.Paragraphs[0].Runs.Select(r => (r.Text, r.Link?.Uri)));
    }

    [Fact]
    public void A_link_in_a_table_cell_links_that_cell()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(
            RawPdf.Text(70, 726, "Item", 10, "F2") + RawPdf.Text(250, 726, "Qty", 10, "F2") +
            RawPdf.Text(70, 714, "Consulting, senior", 10) + RawPdf.Text(250, 714, "10", 10));
        pdf.Annotate(page, "<< /Type /Annot /Subtype /Link /Rect [65 710 160 724] /A << /S /URI /URI (https://example.com/rates) >> >>");

        var table = Assert.Single(PdfProofDocument.Load(pdf.Build()).Semantics.Tables);

        Assert.Equal("https://example.com/rates", Assert.Single(table.Rows[0].Cells[0].Runs).Link?.Uri);
        Assert.Null(table.Rows[0].Cells[1].Runs[0].Link);
    }

    [Fact]
    public void A_link_without_text_is_logged_and_left_out()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Text(50, 700, "Body text."));
        pdf.Annotate(page, "<< /Type /Annot /Subtype /Link /Rect [300 300 350 320] /A << /S /URI /URI (https://example.com) >> >>");

        var doc = PdfProofDocument.Load(pdf.Build());

        Assert.Empty(doc.Semantics.Links);
        Assert.Contains(doc.Semantics.Decisions, d => d.ToString() == "LinkAttachment (page 1): a link to https://example.com covers no text and is left out");
    }

    [Fact]
    public void The_pass_can_be_switched_off()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Text(50, 700, "Read the terms"));
        pdf.Annotate(page, "<< /Type /Annot /Subtype /Link /Rect [45 695 250 715] /A << /S /URI /URI (https://example.com) >> >>");
        var profile = PdfProfile.Default with { Semantics = SemanticOptions.Default with { Links = LinkAttachmentOptions.Default with { Enabled = false } } };

        var doc = PdfProofDocument.Load(pdf.Build(), new PdfProofOptions { Profile = profile });

        Assert.Empty(doc.Semantics.Links);
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: the new tests FAIL (no link runs), except `The_pass_can_be_switched_off`.

- [ ] **Step 3: Write `src/PdfProof/Semantics/LinkAttacher.cs`**

```csharp
using PdfProof.Layout;

namespace PdfProof.Semantics;

// Spec §6, pass 6. A link becomes a link run on the text it covers. Untagged: each layout link (in the page's link
// order, top to bottom then left to right) goes to the first candidate overlapping its rectangle whose cell under the
// rectangle contains the link's text at a still-unlinked position; the layout already gives a link the text of the
// words under it. Tagged: links the structure front end couldn't place (annotations on a P, as QuestPDF writes them)
// go to the first unlinked occurrence of their text in their element.
internal static class LinkAttacher
{
    public static void Run(SemanticContext context)
    {
        foreach (var candidate in context.Candidates.Where(c => c.Kind is CandidateKind.Unknown or CandidateKind.Paragraph or CandidateKind.Heading && c.Lines.Count > 1))
        {
            // One line, so a link that wraps from one line to the next still matches.
            candidate.Lines = [new CandidateLine(
                [new CandidateCell(Finalizer.Runs(candidate), candidate.Box)],
                candidate.Box,
                candidate.Lines[0].Size,
                candidate.Lines.All(line => line.IsBold))];
        }

        foreach (var page in context.Layout.Pages)
        {
            var onPage = context.Candidates.Where(c => !c.Declared && c.Page == page.Number).ToList();
            foreach (var link in page.Links)
            {
                Place(context, page.Number, link, onPage.Where(c => Overlaps(c.Box, link.Box)), useBoxes: true);
            }
        }

        foreach (var candidate in context.Candidates.Where(c => c.Declared))
        {
            foreach (var link in candidate.PendingLinks)
            {
                Place(context, candidate.Page, link, [candidate], useBoxes: false);
            }
        }
    }

    private static void Place(SemanticContext context, int page, LayoutLink link, IEnumerable<Candidate> candidates, bool useBoxes)
    {
        var target = new SemanticLink(link.Uri, link.TargetPage);
        var text = link.Text.Trim();
        if (text.Length == 0)
        {
            context.Decide(SemanticPass.LinkAttachment, page, null, $"a link to {target} covers no text and is left out");
            return;
        }

        foreach (var candidate in candidates)
        {
            if (Attach(candidate, link, text, target, useBoxes))
            {
                context.Decide(SemanticPass.LinkAttachment, page, candidate, $"\"{SemanticText.Excerpt(text)}\" → {target}");
                return;
            }
        }

        context.Decide(SemanticPass.LinkAttachment, page, null, $"\"{SemanticText.Excerpt(text)}\" → {target}: the text was not found in a block and the link is left out");
    }

    private static bool Attach(Candidate candidate, LayoutLink link, string text, SemanticLink target, bool useBoxes)
    {
        var lines = candidate.Lines.ToList();
        for (var l = 0; l < lines.Count; l++)
        {
            var cells = lines[l].Cells.ToList();
            for (var c = 0; c < cells.Count; c++)
            {
                if (useBoxes && cells[c].Box != default && !Overlaps(cells[c].Box, link.Box))
                {
                    continue;
                }

                var at = Unlinked(cells[c].Runs, text);
                if (at >= 0)
                {
                    cells[c] = cells[c] with { Runs = SemanticText.Link(cells[c].Runs, at, text.Length, target) };
                    lines[l] = lines[l] with { Cells = cells };
                    candidate.Lines = lines;
                    return true;
                }
            }
        }

        return false;
    }

    // The first position of `text` whose characters are all in runs without a link, or -1.
    private static int Unlinked(IReadOnlyList<SemanticRun> runs, string text)
    {
        var joined = SemanticText.Join(runs);
        var linked = new bool[joined.Length];
        var offset = 0;
        foreach (var run in runs)
        {
            for (var i = 0; i < run.Text.Length; i++)
            {
                linked[offset + i] = run.Link is not null;
            }

            offset += run.Text.Length;
        }

        for (var at = joined.IndexOf(text, StringComparison.Ordinal); at >= 0; at = joined.IndexOf(text, at + 1, StringComparison.Ordinal))
        {
            var free = true;
            for (var i = at; i < at + text.Length && free; i++)
            {
                free = !linked[i];
            }

            if (free)
            {
                return at;
            }
        }

        return -1;
    }

    private static bool Overlaps(LayoutBox a, LayoutBox b) =>
        a.Left < b.Right && a.Right > b.Left && a.Bottom < b.Top && a.Top > b.Bottom;
}
```

In `SemanticBuilder.Build`, after ListDetection:
```csharp
        if (options.Links.Enabled)
        {
            LinkAttacher.Run(context);
        }
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: all tests pass on net48 and net10.0.

`Two_links_with_the_same_text…` depends on the two rectangles covering only their "here": the word centres are about 124 and 221, and the other words' centres fall outside. If the layout reports different link texts, print `doc.Layout.Pages[0].Links` before moving the rectangles.

- [ ] **Step 5: Commit**

```bash
git add src tests/PdfProof.Tests
git commit -m "Attach links to the text they cover as link runs

Untagged layout links go to the first overlapping block whose text contains them
at a still-unlinked position, so repeated link texts each get their own
occurrence; links in table cells link their cell. Links without text are logged
and left out.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: The structure front end

**Files:**
- Create: `src/PdfProof/Semantics/StructureFrontEnd.cs`, `tests/PdfProof.Tests/StructureFrontEndTests.cs`
- Modify:
  - layout: `src/PdfProof/Layout/MarkedContentText.cs`, `src/PdfProof/Layout/LayoutBuildResult.cs`, `src/PdfProof/Layout/LayoutBuilder.cs`;
  - structure: `src/PdfProof/Structure/StructureBuilder.cs`;
  - semantics: `src/PdfProof/Semantics/Finalizer.cs` (the `Figure` case), `src/PdfProof/Semantics/SemanticBuilder.cs`.

**Interfaces:**
- Consumes: `StructureModel`, `StructureNode`, `StructureText`, `StructureObjectReference` (Plan 3); `Candidate`, `SemanticContext` (Task 2); `LinkAttacher` (Task 7) places links that tags put on `P`.
- Produces:
  - **Marked text with style:** `internal sealed record MarkedText(string Text, bool IsBold, bool IsItalic)`. `MarkedContentText.Collect` returns `IReadOnlyDictionary<int, MarkedText>`, and `LayoutBuildResult.MarkedText` holds those per page. The style is "every word bold" and "every word italic".
  - **`StructureFrontEnd.Build(StructureModel, SemanticContext) -> List<Candidate>`**, used when the source is declared:
    - **Roles to kinds:**
      - `H`/`H1`–`H6` become Heading, with the level from the digit (`H` is 1);
      - `P`, `Link`, `Caption`, `Quote`, `Code` and `Note` become Paragraph;
      - `L` becomes List, with nested lists flattened (nesting comes in 5b, and each flattening is logged);
      - `Table` becomes Table, with header rows from `THead`, or a first row of `TH` cells;
      - `Figure`/`Formula` becomes Figure, with `Alt` or `ActualText`;
      - containers (`Document`, `Part`, `Sect`, `Div`, `Art`, `Aside`, `BlockQuote`, `NonStruct`, `Private`, `TOC`, `TOCI`, `Index`, `Form`, `DocumentFragment`) are walked through.
    - **Inline content** follows the content order:
      - text becomes runs with its style;
      - child elements (`Span`, `NonStruct`, a `P` inside a cell or item, …) are folded in;
      - a `Link` child's text gets the link from its `OBJR`;
      - an `OBJR` link on any other element goes to `PendingLinks`;
      - `Lbl` is left out.
    - Elements with no text and no figure role give no candidate.

- [ ] **Step 1: Write the failing tests in `tests/PdfProof.Tests/StructureFrontEndTests.cs`**

```csharp
using PdfProof.Semantics;

namespace PdfProof.Tests;

public sealed class StructureFrontEndTests
{
    [Fact]
    public void Tagged_headings_and_paragraphs_are_declared_and_their_sections_nest()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(
            RawPdf.Marked("H1", 0, RawPdf.Text(50, 750, "Invoice", 18)) + RawPdf.Marked("P", 1, RawPdf.Text(50, 700, "Hello")) +
            RawPdf.Marked("H2", 2, RawPdf.Text(50, 650, "Terms", 14)) + RawPdf.Marked("P", 3, RawPdf.Text(50, 600, "Pay soon.")));
        var kids = new[] { "H1", "P", "H2", "P" }.Select((role, mcid) => pdf.Add($"<< /S /{role} /Pg {page} 0 R /K {mcid} >>")).ToList();
        pdf.Tag($"{pdf.Add($"<< /S /Document /K [{string.Join(" ", kids.Select(k => $"{k} 0 R"))}] >>")} 0 R");

        var semantics = PdfProofDocument.Load(pdf.Build()).Semantics;

        Assert.Equal(SemanticSource.Declared, semantics.Source);
        Assert.All(semantics.Blocks, b => Assert.Equal(SemanticSource.Declared, b.Source));
        Assert.Equal([("Invoice", 1), ("Terms", 2)], semantics.Headings.Select(h => (h.Text, h.Level)));
        var section = Assert.Single(semantics.Sections);
        Assert.Equal(("Invoice", new[] { "Hello" }), (section.Heading.Text, section.Blocks.Select(b => b.Text).ToArray()));
        Assert.Equal(("Terms", new[] { "Pay soon." }), (section.Sections[0].Heading.Text, section.Sections[0].Blocks.Select(b => b.Text).ToArray()));
    }

    [Fact]
    public void Containers_and_wrappers_are_looked_through_and_a_Link_child_links_its_text()
    {
        // Chromium and Typst: P > NonStruct (text), Link (text + OBJR), NonStruct (text), inside Sect and Div.
        var pdf = new RawPdf();
        var page = pdf.AddPage(
            RawPdf.Marked("Span", 0, RawPdf.Text(50, 700, "Questions? Visit")) +
            RawPdf.Marked("Link", 1, RawPdf.Text(145, 700, "our support site")) +
            RawPdf.Marked("Span", 2, RawPdf.Text(236, 700, "or e-mail us.")));
        var annotation = pdf.Add("<< /Type /Annot /Subtype /Link /Rect [143 695 230 715] /A << /S /URI /URI (https://example.com/support) >> >>");
        pdf.Annotate(page, $"{annotation} 0 R");
        var before = pdf.Add($"<< /S /NonStruct /Pg {page} 0 R /K 0 >>");
        var link = pdf.Add($"<< /S /Link /Pg {page} 0 R /K [1 << /Type /OBJR /Obj {annotation} 0 R >>] >>");
        var after = pdf.Add($"<< /S /NonStruct /Pg {page} 0 R /K 2 >>");
        var paragraph = pdf.Add($"<< /S /P /Pg {page} 0 R /K [{before} 0 R {link} 0 R {after} 0 R] >>");
        var div = pdf.Add($"<< /S /Div /K [{paragraph} 0 R] >>");
        pdf.Tag($"{pdf.Add($"<< /S /Sect /K [{div} 0 R] >>")} 0 R");

        var block = Assert.IsType<SemanticParagraph>(Assert.Single(PdfProofDocument.Load(pdf.Build()).Semantics.Blocks));

        Assert.Equal(
            [("Questions? Visit ", (string?)null), ("our support site", "https://example.com/support"), (" or e-mail us.", null)],
            block.Runs.Select(r => (r.Text, r.Link?.Uri)));
    }

    [Fact]
    public void A_link_annotation_on_a_paragraph_is_placed_on_its_text()
    {
        // QuestPDF: P > [text, OBJR] with no Link element.
        var pdf = new RawPdf();
        var first = pdf.AddPage(RawPdf.Marked("P", 0, RawPdf.Text(50, 700, "Read the terms")));
        var second = pdf.AddPage(RawPdf.Text(50, 750, "Terms"));
        var annotation = pdf.Add($"<< /Type /Annot /Subtype /Link /Rect [45 715 140 695] /A << /S /GoTo /D [{second} 0 R /Fit] >> >>");
        pdf.Annotate(first, $"{annotation} 0 R");
        pdf.Tag($"{pdf.Add($"<< /S /P /Pg {first} 0 R /K [0 << /Type /OBJR /Obj {annotation} 0 R >>] >>")} 0 R");

        var semantics = PdfProofDocument.Load(pdf.Build()).Semantics;

        var run = Assert.Single(semantics.Paragraphs[0].Runs);
        Assert.Equal(("Read the terms", (int?)2), (run.Text, run.Link?.TargetPage));
    }

    [Fact]
    public void A_tagged_table_takes_its_header_from_THead_or_TH_and_folds_paragraphs_in_cells()
    {
        // QuestPDF writes Table > THead/TBody > TR > TH/TD > P; Chromium writes Table > TR > TH/TD.
        var pdf = new RawPdf();
        var page = pdf.AddPage(
            RawPdf.Marked("P", 0, RawPdf.Text(50, 700, "Item")) + RawPdf.Marked("P", 1, RawPdf.Text(200, 700, "Qty")) +
            RawPdf.Marked("P", 2, RawPdf.Text(50, 680, "Consulting, senior")) + RawPdf.Marked("P", 3, RawPdf.Text(200, 680, "10")));
        string Cell(string role, int mcid) => $"{pdf.Add($"<< /S /{role} /K [{pdf.Add($"<< /S /P /Pg {page} 0 R /K {mcid} >>")} 0 R] >>")} 0 R";
        var head = pdf.Add($"<< /S /THead /K [{pdf.Add($"<< /S /TR /K [{Cell("TH", 0)} {Cell("TH", 1)}] >>")} 0 R] >>");
        var body = pdf.Add($"<< /S /TBody /K [{pdf.Add($"<< /S /TR /K [{Cell("TD", 2)} {Cell("TD", 3)}] >>")} 0 R] >>");
        pdf.Tag($"{pdf.Add($"<< /S /Table /K [{head} 0 R {body} 0 R] >>")} 0 R");

        var table = Assert.Single(PdfProofDocument.Load(pdf.Build()).Semantics.Tables);

        Assert.Equal(["Item", "Qty"], table.Header!.Cells.Select(c => c.Text));
        Assert.Equal([["Consulting, senior", "10"]], table.Rows.Select(r => r.Cells.Select(c => c.Text).ToArray()));
        Assert.Equal(SemanticSource.Declared, table.Source);
    }

    [Fact]
    public void A_tagged_list_item_leaves_out_its_label()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("Lbl", 0, RawPdf.Text(50, 700, "•")) + RawPdf.Marked("LBody", 1, RawPdf.Text(60, 700, "Payment within 30 days.")));
        var label = pdf.Add($"<< /S /Lbl /Pg {page} 0 R /K 0 >>");
        var itemBody = pdf.Add($"<< /S /LBody /Pg {page} 0 R /K 1 >>");
        var item = pdf.Add($"<< /S /LI /K [{label} 0 R {itemBody} 0 R] >>");
        pdf.Tag($"{pdf.Add($"<< /S /L /K [{item} 0 R] >>")} 0 R");

        var list = Assert.Single(PdfProofDocument.Load(pdf.Build()).Semantics.Lists);

        Assert.Equal(["Payment within 30 days."], list.Items.Select(i => i.Text));
    }

    [Fact]
    public void A_tagged_figure_keeps_its_alternative_text()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("Figure", 0, "q 20 0 0 20 50 700 cm /Im1 Do Q\n"));
        pdf.Tag($"{pdf.Add($"<< /S /Figure /Pg {page} 0 R /Alt (Signature) /K 0 >>")} 0 R");

        var figure = Assert.Single(PdfProofDocument.Load(pdf.Build()).Semantics.Figures);

        Assert.Equal(("Signature", "Signature", 1), (figure.Alt, figure.Text, figure.Page));
    }

    [Fact]
    public void Bold_marked_content_gives_bold_runs()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("P", 0, RawPdf.Text(50, 700, "Important", 12, "F2")));
        pdf.Tag($"{pdf.Add($"<< /S /P /Pg {page} 0 R /K 0 >>")} 0 R");

        Assert.True(Assert.Single(PdfProofDocument.Load(pdf.Build()).Semantics.Paragraphs[0].Runs).IsBold);
    }

    [Fact]
    public void An_element_whose_marked_content_is_missing_gives_no_block_and_no_error()
    {
        // Review Focus 5: MCID 5 is not on the page.
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("P", 0, RawPdf.Text(50, 700, "Kept")));
        var kept = pdf.Add($"<< /S /P /Pg {page} 0 R /K 0 >>");
        var missing = pdf.Add($"<< /S /P /Pg {page} 0 R /K 5 >>");
        pdf.Tag($"{kept} 0 R {missing} 0 R");

        Assert.Equal(["Kept"], PdfProofDocument.Load(pdf.Build()).Semantics.Blocks.Select(b => b.Text));
    }

    [Fact]
    public void A_footer_left_out_of_the_tags_is_still_an_artifact()
    {
        // Chromium's footer template is drawn outside the structure tree.
        var pdf = new RawPdf();
        var pages = Enumerable.Range(1, 2).Select(n => pdf.AddPage(RawPdf.Marked("P", 0, RawPdf.Text(50, 700, $"Content {n}.")) + RawPdf.Text(270, 40, $"Page {n} of 2", 10))).ToList();
        pdf.Tag(string.Join(" ", pages.Select(p => $"{pdf.Add($"<< /S /P /Pg {p} 0 R /K 0 >>")} 0 R")));

        var semantics = PdfProofDocument.Load(pdf.Build()).Semantics;

        Assert.Equal("Page {n} of {N}", Assert.Single(semantics.Artifacts).Text);
        Assert.Equal(["Content 1.", "Content 2."], semantics.Blocks.Select(b => b.Text));
    }

    [Fact]
    public void The_layout_source_option_ignores_the_tags()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("H1", 0, RawPdf.Text(50, 750, "Invoice", 18)) + RawPdf.Marked("P", 1, RawPdf.Text(50, 700, "Hello there, this is body text.")));
        pdf.Tag($"{pdf.Add($"<< /S /H1 /Pg {page} 0 R /K 0 >>")} 0 R {pdf.Add($"<< /S /P /Pg {page} 0 R /K 1 >>")} 0 R");
        var profile = PdfProfile.Default with { Semantics = SemanticOptions.Default with { Source = SemanticSourceOption.Layout } };

        var semantics = PdfProofDocument.Load(pdf.Build(), new PdfProofOptions { Profile = profile }).Semantics;

        Assert.Equal(SemanticSource.Inferred, semantics.Source);
        Assert.Equal(SemanticSource.Inferred, Assert.Single(semantics.Headings).Source);
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: the new tests FAIL. Tagged documents still go through the layout front end, so blocks are `Inferred`, there are no sections from tags, no header from `THead`, and labels stay in the text. `The_layout_source_option…` passes already.

- [ ] **Step 3: Collect the style of marked content**

Replace `src/PdfProof/Layout/MarkedContentText.cs` with:
```csharp
using System.Text.RegularExpressions;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Util;

namespace PdfProof.Layout;

// The visible text of a marked-content sequence and whether all its words are bold or italic.
internal sealed record MarkedText(string Text, bool IsBold, bool IsItalic);

// The visible text and style of each marked-content sequence on a page, by MCID. Artifacts are skipped. Letters hidden
// by clipping are already gone (PdfPig applies ClipPaths to marked content too).
internal static class MarkedContentText
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.CultureInvariant);

    public static IReadOnlyDictionary<int, MarkedText> Collect(IReadOnlyList<MarkedContentElement> elements, LayoutOptions options)
    {
        // Iterative, so deeply nested marked content can't overflow the stack.
        var texts = new Dictionary<int, MarkedText>();
        var pending = new Stack<MarkedContentElement>();
        for (var i = elements.Count - 1; i >= 0; i--)
        {
            pending.Push(elements[i]);
        }

        while (pending.Count > 0)
        {
            var element = pending.Pop();
            if (element.IsArtifact)
            {
                continue;
            }

            if (element.MarkedContentIdentifier >= 0)
            {
                var letters = Letters(element);
                var words = Words(letters, options);
                var text = element.ActualText is { } actual
                    ? Whitespace.Replace(WordFactory.Normalize(actual, options), " ").Trim()
                    : Text(letters, words, options);
                var marked = new MarkedText(text, words.Count > 0 && words.All(word => word.IsBold), words.Count > 0 && words.All(word => word.IsItalic));
                texts[element.MarkedContentIdentifier] = texts.TryGetValue(element.MarkedContentIdentifier, out var earlier) && earlier.Text.Length > 0
                    ? new MarkedText(text.Length > 0 ? earlier.Text + " " + text : earlier.Text, earlier.IsBold && marked.IsBold, earlier.IsItalic && marked.IsItalic)
                    : marked;
                continue; // nested sequences are part of this MCID's content (ISO 32000 doesn't allow nested MCIDs)
            }

            for (var i = element.Children.Count - 1; i >= 0; i--)
            {
                pending.Push(element.Children[i]);
            }
        }

        return texts;
    }

    // Horizontal words, recovered from gaps like the layout; none for rotated text.
    private static List<LayoutWord> Words(List<Letter> letters, LayoutOptions options) =>
        letters.All(letter => letter.TextOrientation == TextOrientation.Horizontal)
            ? DefaultWordExtractor.Instance.GetWords(letters).Select(word => WordFactory.Create(word, options)).OfType<LayoutWord>().ToList()
            : [];

    // Horizontal text: the words joined. Rotated text: content order is the reading order within one sequence, and the
    // word extractor would scramble it.
    private static string Text(List<Letter> letters, List<LayoutWord> words, LayoutOptions options) =>
        letters.All(letter => letter.TextOrientation == TextOrientation.Horizontal)
            ? string.Join(" ", words.Select(word => word.Text))
            : Whitespace.Replace(WordFactory.Normalize(string.Concat(letters.Select(letter => letter.Value)), options), " ").Trim();

    // A sequence's letters in content order (its own, then those of nested sequences), leaving out nested
    // artifacts. Iterative, so deep nesting can't overflow the stack.
    private static List<Letter> Letters(MarkedContentElement root)
    {
        var letters = new List<Letter>();
        var pending = new Stack<MarkedContentElement>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var element = pending.Pop();
            if (element.IsArtifact)
            {
                continue;
            }

            letters.AddRange(element.Letters);
            for (var i = element.Children.Count - 1; i >= 0; i--)
            {
                pending.Push(element.Children[i]);
            }
        }

        return letters;
    }
}
```
Then update the uses:
- in `LayoutBuildResult`, change the `MarkedText` parameter to `IReadOnlyDictionary<int, IReadOnlyDictionary<int, MarkedText>> MarkedText`;
- in `LayoutBuilder.Build`, change the `markedText` dictionary to the same value type;
- in `StructureBuilder.MarkedText`, use `text.Text` where it used `text` (the `TryGetValue` result is now a `MarkedText`):
```csharp
    private StructureText? MarkedText(RawMarkedContent marked) =>
        marked.Page is { } page
        && _layout.MarkedText.TryGetValue(page, out var texts)
        && texts.TryGetValue(marked.Mcid, out var text)
        && text.Text.Length > 0
            ? new StructureText(text.Text, page, marked.Mcid)
            : null;
```

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: the structure tests from Plan 3 still pass. This step is a refactoring, so the new Task 8 tests still fail.

- [ ] **Step 4: Write `src/PdfProof/Semantics/StructureFrontEnd.cs`**

```csharp
using PdfProof.Layout;
using PdfProof.Structure;

namespace PdfProof.Semantics;

// Tagged documents (spec §6, row 0b): structure elements become candidates with declared kinds. Containers are walked
// through; inline content is folded into runs in content order. Producer quirks: Chromium wraps text in NonStruct,
// QuestPDF puts a P inside every table cell and list item and its link annotations on the P, list labels are Lbl
// elements. Tags are trusted: these candidates are not reclassified.
internal static class StructureFrontEnd
{
    private static readonly HashSet<string> Containers = new(StringComparer.Ordinal)
    {
        "StructTreeRoot", "Document", "DocumentFragment", "Part", "Sect", "Div", "Art", "Aside", "BlockQuote", "NonStruct",
        "Private", "TOC", "TOCI", "Index", "Form",
    };

    private static readonly HashSet<string> Paragraphs = new(StringComparer.Ordinal) { "P", "Link", "Caption", "Quote", "Code", "Note" };

    public static List<Candidate> Build(StructureModel structure, SemanticContext context)
    {
        var candidates = new List<Candidate>();
        Walk(structure.Root, candidates, context);
        return candidates;
    }

    private static void Walk(StructureNode node, List<Candidate> candidates, SemanticContext context)
    {
        var role = node.Role;
        if (HeadingLevel(role) is { } level)
        {
            if (Text(node, context, CandidateKind.Heading) is { } heading)
            {
                heading.Level = level;
                candidates.Add(heading);
            }
        }
        else if (Paragraphs.Contains(role))
        {
            if (Text(node, context, CandidateKind.Paragraph) is { } paragraph)
            {
                candidates.Add(paragraph);
            }
        }
        else if (role == "L")
        {
            if (List(node, context) is { } list)
            {
                candidates.Add(list);
            }
        }
        else if (role == "Table")
        {
            if (Table(node, context) is { } table)
            {
                candidates.Add(table);
            }
        }
        else if (role is "Figure" or "Formula")
        {
            candidates.Add(new Candidate(Page(node), [], declared: true) { Kind = CandidateKind.Figure, Alt = node.Alt ?? node.ActualText });
        }
        else if (Containers.Contains(role) || !node.Content.OfType<StructureText>().Any())
        {
            foreach (var child in node.Children)
            {
                Walk(child, candidates, context);
            }
        }
        else if (Text(node, context, CandidateKind.Paragraph) is { } other)
        {
            candidates.Add(other); // an unknown role with text of its own reads as a paragraph
        }
    }

    private static int? HeadingLevel(string role) =>
        role == "H" ? 1
        : role.Length == 2 && role[0] == 'H' && role[1] >= '1' && role[1] <= '6' ? role[1] - '0'
        : null;

    private static Candidate? Text(StructureNode node, SemanticContext context, CandidateKind kind)
    {
        var pending = new List<LayoutLink>();
        var runs = Runs(node, context, pending, null);
        if (runs.Count == 0)
        {
            return null;
        }

        var candidate = new Candidate(Page(node), [Line(runs)], declared: true) { Kind = kind };
        candidate.PendingLinks.AddRange(pending);
        return candidate;
    }

    private static Candidate? List(StructureNode node, SemanticContext context)
    {
        var pending = new List<LayoutLink>();
        var items = new List<CandidateLine>();
        Items(node, context, pending, items);
        if (items.Count == 0)
        {
            return null;
        }

        var list = new Candidate(Page(node), items, declared: true) { Kind = CandidateKind.List };
        list.PendingLinks.AddRange(pending);
        return list;
    }

    // Each LI is an item. A nested list is flattened into the items after its parent item (nesting is Plan 5b).
    private static void Items(StructureNode list, SemanticContext context, List<LayoutLink> pending, List<CandidateLine> items)
    {
        foreach (var item in list.Children.Where(child => child.Role == "LI"))
        {
            var runs = Runs(item, context, pending, null);
            if (runs.Count > 0)
            {
                items.Add(Line(runs));
            }

            foreach (var nested in item.Children.Where(child => child.Role == "L"))
            {
                context.Decide(SemanticPass.StructureFrontEnd, Page(nested), null, "a nested list is flattened into its parent list");
                Items(nested, context, pending, items);
            }
        }
    }

    private static Candidate? Table(StructureNode node, SemanticContext context)
    {
        var pending = new List<LayoutLink>();
        var rows = new List<(StructureNode Row, bool InHead)>();
        Rows(node, inHead: false, rows);
        var lines = new List<CandidateLine>();
        var header = 0;
        foreach (var (row, inHead) in rows)
        {
            var cells = row.Children.Where(child => child.Role is "TH" or "TD").ToList();
            if (cells.Count == 0)
            {
                continue;
            }

            if (lines.Count == 0 && (inHead || cells.All(cell => cell.Role == "TH")))
            {
                header = 1;
            }

            lines.Add(new CandidateLine(cells.Select(cell => new CandidateCell(Runs(cell, context, pending, null), default)).ToList(), default, 0, false));
        }

        if (lines.Count == 0)
        {
            return null;
        }

        var table = new Candidate(Page(node), lines, declared: true) { Kind = CandidateKind.Table, HeaderRows = header };
        table.PendingLinks.AddRange(pending);
        return table;
    }

    private static void Rows(StructureNode node, bool inHead, List<(StructureNode Row, bool InHead)> rows)
    {
        foreach (var child in node.Children)
        {
            if (child.Role == "TR")
            {
                rows.Add((child, inHead));
            }
            else if (child.Role is "THead" or "TBody" or "TFoot")
            {
                Rows(child, inHead || child.Role == "THead", rows);
            }
        }
    }

    // The element's inline content as runs, in content order. A Link child gives its text the link from its OBJR; an
    // OBJR link on another element is left for LinkAttachment. Lbl (list labels) and nested lists are left out.
    private static IReadOnlyList<SemanticRun> Runs(StructureNode node, SemanticContext context, List<LayoutLink> pending, SemanticLink? link)
    {
        var pieces = new List<IReadOnlyList<SemanticRun>>();
        foreach (var content in node.Content)
        {
            switch (content)
            {
                case StructureText text:
                    var (bold, italic) = Style(context, text);
                    pieces.Add([new SemanticRun(text.Text, bold, italic, link)]);
                    break;
                case StructureNode { Role: "Lbl" or "L" }:
                    break;
                case StructureNode child:
                    pieces.Add(Runs(child, context, pending, child.Role == "Link" ? LinkOf(child) ?? link : link));
                    break;
                case StructureObjectReference { Link: { } layoutLink } when node.Role != "Link":
                    pending.Add(layoutLink);
                    break;
            }
        }

        return SemanticText.Concat(pieces);
    }

    private static (bool IsBold, bool IsItalic) Style(SemanticContext context, StructureText text) =>
        text.Page is { } page && context.Build.MarkedText.TryGetValue(page, out var texts) && texts.TryGetValue(text.MarkedContentId, out var marked)
            ? (marked.IsBold, marked.IsItalic)
            : (false, false);

    private static SemanticLink? LinkOf(StructureNode link) =>
        link.Content.OfType<StructureObjectReference>().Select(reference => reference.Link).FirstOrDefault(found => found is not null) is { } found
            ? new SemanticLink(found.Uri, found.TargetPage)
            : null;

    private static CandidateLine Line(IReadOnlyList<SemanticRun> runs) => new([new CandidateCell(runs, default)], default, 0, false);

    // The element's page, or the first page found in its content.
    private static int Page(StructureNode node)
    {
        if (node.Page is { } page)
        {
            return page;
        }

        foreach (var content in node.Content)
        {
            var found = content is StructureNode child ? Page(child) : content.Page ?? 0;
            if (found > 0)
            {
                return found;
            }
        }

        return 1;
    }
}
```

The structure model's depth is bounded (256), so the recursion in `Walk`, `Runs` and `Page` is too.

- [ ] **Step 5: Use the structure front end, and finalize figures**

In `SemanticBuilder.Build`, replace the interim front-end lines with:
```csharp
        context.Candidates.AddRange(declared ? StructureFrontEnd.Build(structure, context) : LayoutFrontEnd.Build(context));
```
In `Finalizer.Block`, add:
```csharp
            CandidateKind.Figure => new SemanticFigure(candidate.Page, source, candidate.Alt, candidate.Box),
```

- [ ] **Step 6: Run the tests**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: all tests pass on net48 and net10.0, including Plan 3's structure tests and `SemanticModelTests`.

If `A_tagged_list_item_leaves_out_its_label` fails because the bullet "•" can't be written in WinAnsi (it extracts as another character or nothing), keep the test and use `-` as the label: the test is about the `Lbl` element, not the glyph.

- [ ] **Step 7: Commit**

```bash
git add src tests/PdfProof.Tests
git commit -m "Build the semantic model of tagged documents from the structure tree

Roles become declared headings, paragraphs, tables, lists and figures; containers
and wrappers are looked through, paragraphs inside cells and items are folded in,
Link elements link their text, link annotations on a P are placed on its text,
and list labels are left out. Runs take bold and italic from the marked content.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: The corpus compares full block lists

**Files:**
- Modify: `tests/PdfProof.Corpus.Tests/Oracle.cs`, `tests/PdfProof.Corpus.Tests/OracleComparison.cs`, `tests/PdfProof.Corpus.Tests/OracleComparisonTests.cs`, `tests/PdfProof.Corpus.Tests/GeneratedCorpusTests.cs`, `tests/PdfProof.Corpus.Tests/ExternalCorpusTests.cs`, `AGENTS.md`
- Modify (only if the real run in Step 5 shows producer gaps): `corpus/generated/invoice/deviations.json`

**Interfaces:**
- Consumes: `PdfProofDocument.Semantics` (Tasks 1–8), the oracle and matrix (Plan 4).
- Produces: `OracleComparison.Compare(Oracle oracle, PdfProofDocument document) -> IReadOnlyList<string>`, comparing:
  - title and page count;
  - the block count and each block's type, page, level, text, table header and rows, list items and links;
  - each oracle artifact by role, text and page range.

  `OracleArtifact` gains `Pages` (the oracle's `"pages"`, e.g. `"1-2"`). `OracleBlock.Role` and the `checkRoles` parameter are removed.

- [ ] **Step 1: Rewrite the comparison tests in `tests/PdfProof.Corpus.Tests/OracleComparisonTests.cs`**

Keep the `Expected` oracle and the tests `An_unknown_block_type_is_rejected` and `The_invoice_oracle_parses`. In the latter, delete the `Role` assertion. Replace the rest:

```csharp
    [Fact]
    public void A_document_with_the_same_blocks_links_and_footer_has_no_mismatches()
    {
        Assert.Empty(OracleComparison.Compare(Expected, Invoice()));
    }

    [Fact]
    public void A_different_title_is_a_mismatch()
    {
        Assert.Equal(["title: expected \"Invoice\", was \"Bill\""], OracleComparison.Compare(Expected, Invoice(title: "Bill")));
    }

    [Fact]
    public void A_different_text_is_reported_with_its_block()
    {
        Assert.Equal(
            ["block 5 (heading): expected text \"Terms and conditions\", found \"Terms\""],
            OracleComparison.Compare(Expected, Invoice(terms: "Terms")));
    }

    [Fact]
    public void Blocks_in_another_order_are_a_mismatch()
    {
        var swapped = Expected with { Blocks = [Expected.Blocks[0], Expected.Blocks[2], Expected.Blocks[1], .. Expected.Blocks.Skip(3)] };

        Assert.Contains(
            "block 2 (paragraph): expected text \"Read the terms and conditions\", found \"Visit our support site\"",
            OracleComparison.Compare(swapped, Invoice()));
    }

    [Fact]
    public void A_missing_table_header_is_a_mismatch()
    {
        Assert.Contains(
            "block 4 (table): expected header [Item | Qty | Amount], found none",
            OracleComparison.Compare(Expected, Invoice(boldHeader: false)));
    }

    [Fact]
    public void A_link_to_the_wrong_target_is_a_mismatch()
    {
        Assert.Equal(
            ["block 2 (paragraph): no link \"Visit our support site\" to https://example.com/support"],
            OracleComparison.Compare(Expected, Invoice(support: "https://example.com/other")));
    }

    [Fact]
    public void An_artifact_on_other_pages_than_expected_is_a_mismatch()
    {
        Assert.Equal(
            ["artifact: expected footer \"Page {n} of {N}\" on pages 1-2, found footer \"Page {n} of {N}\" on pages 1"],
            OracleComparison.Compare(Expected, Invoice(footerOnSecondPage: false)));
    }

    // Two pages laid out like the oracle; each argument breaks one thing.
    private static PdfProofDocument Invoice(
        string title = "Invoice", string support = "https://example.com/support", string terms = "Terms and conditions",
        bool footerOnSecondPage = true, bool boldHeader = true)
    {
        var header = boldHeader ? "F2" : "F1";
        var pdf = new RawPdf { Info = $"<< /Title ({title}) >>" };
        var first = pdf.AddPage(
            RawPdf.Text(50, 750, "Invoice", 18) +
            RawPdf.Text(50, 700, "Visit our support site") +
            RawPdf.Text(50, 680, "Read the terms and conditions") +
            RawPdf.Text(50, 640, "Item", 12, header) + RawPdf.Text(250, 640, "Qty", 12, header) + RawPdf.Text(350, 640, "Amount", 12, header) +
            RawPdf.Text(50, 620, "Consulting, senior") + RawPdf.Text(250, 620, "10") + RawPdf.Text(350, 620, "14 500,00 SEK") +
            RawPdf.Text(270, 40, "Page 1 of 2", 10));
        var second = pdf.AddPage(
            RawPdf.Text(50, 750, terms, 14) +
            RawPdf.Text(50, 720, "- Payment within 30 days.") +
            (footerOnSecondPage ? RawPdf.Text(270, 40, "Page 2 of 2", 10) : ""));
        pdf.Annotate(first, $"<< /Type /Annot /Subtype /Link /Rect [45 695 250 715] /A << /S /URI /URI ({support}) >> >>");
        pdf.Annotate(first, $"<< /Type /Annot /Subtype /Link /Rect [45 675 250 695] /A << /S /GoTo /D [{second} 0 R /Fit] >> >>");
        return PdfProofDocument.Load(pdf.Build());
    }
```
Delete the old `Tagged()` helper and `Tagged_documents_must_declare…`, and every remaining `checkRoles:` argument.

In the footer case, a footer only on page 1 of a two-page document is no longer repeating. It is still a page number by pattern ("Page 1 of 2" matches), so it is found on page 1 only, hence the expected message.

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet build tests/PdfProof.Corpus.Tests/PdfProof.Corpus.Tests.csproj`
Expected: build FAILS: `Compare` still takes `checkRoles`.

- [ ] **Step 3: Update `Oracle.cs` and rewrite `OracleComparison.cs`**

In `Oracle.cs`:
- the artifacts become `new OracleArtifact(String(a, "role") ?? "", String(a, "text") ?? "", String(a, "pages"))`, and the record is `internal sealed record OracleArtifact(string Role, string Text, string? Pages);`;
- delete `OracleBlock.Role`.

Replace `tests/PdfProof.Corpus.Tests/OracleComparison.cs`:
```csharp
using System.Globalization;
using System.Text.RegularExpressions;
using PdfProof.Semantics;

namespace PdfProof.Corpus.Tests;

// Compares a loaded document's semantic model with its oracle (spec §10.2): title and page count; the blocks in order
// by type, page, level, text, table header and rows, list items and links; and the artifacts by role, text and pages.
// What the oracle leaves out is not compared. Text is compared with whitespace collapsed. Returns the mismatches.
internal static class OracleComparison
{
    public static IReadOnlyList<string> Compare(Oracle oracle, PdfProofDocument document)
    {
        var mismatches = new List<string>();
        if (oracle.Title is { } title && document.Metadata.Title != title)
        {
            mismatches.Add($"title: expected \"{title}\", was \"{document.Metadata.Title}\"");
        }

        if (oracle.PageCount is { } count && document.Metadata.PageCount != count)
        {
            mismatches.Add($"page count: expected {count}, was {document.Metadata.PageCount}");
        }

        var blocks = document.Semantics.Blocks;
        if (blocks.Count != oracle.Blocks.Count)
        {
            mismatches.Add($"blocks: expected {oracle.Blocks.Count}, found {blocks.Count}: {string.Join(", ", blocks.Select(b => $"{Kind(b)} \"{Excerpt(b.Text)}\""))}");
        }

        for (var i = 0; i < Math.Min(blocks.Count, oracle.Blocks.Count); i++)
        {
            Block(i + 1, oracle.Blocks[i], blocks[i], mismatches);
        }

        foreach (var expected in oracle.Artifacts)
        {
            var role = expected.Role.ToLowerInvariant();
            var match = document.Semantics.Artifacts.FirstOrDefault(a => Name(a.Role) == role && Normalize(a.Text) == Normalize(expected.Text));
            if (match is null)
            {
                mismatches.Add($"artifact: expected {role} \"{expected.Text}\", found {Found(document.Semantics.Artifacts)}");
            }
            else if (expected.Pages is { } pages && Range(match.Pages) != pages)
            {
                mismatches.Add($"artifact: expected {role} \"{expected.Text}\" on pages {pages}, found {Name(match.Role)} \"{match.Text}\" on pages {Range(match.Pages)}");
            }
        }

        return mismatches;
    }

    private static void Block(int number, OracleBlock expected, SemanticBlock actual, List<string> mismatches)
    {
        var where = $"block {number} ({expected.Type})";
        if (Kind(actual) != expected.Type)
        {
            mismatches.Add($"{where}: found {Kind(actual)} \"{Excerpt(actual.Text)}\"");
            return;
        }

        if (expected.Page is { } page && actual.Page != page)
        {
            mismatches.Add($"{where}: expected page {page}, found page {actual.Page}");
        }

        switch (actual)
        {
            case SemanticHeading heading:
                if (expected.Level is { } level && heading.Level != level)
                {
                    mismatches.Add($"{where}: expected level {level}, found {heading.Level}");
                }

                Text(where, expected.Text, heading.Text, mismatches);
                break;
            case SemanticParagraph paragraph:
                Text(where, expected.Text, paragraph.Text, mismatches);
                break;
            case SemanticTable table:
                if (expected.Header.Count > 0)
                {
                    var header = table.Header is null ? "none" : Cells(table.Header);
                    if (header != Join(expected.Header))
                    {
                        mismatches.Add($"{where}: expected header {Join(expected.Header)}, found {header}");
                    }
                }

                var rows = table.Rows.Select(Cells).ToList();
                var expectedRows = expected.Rows.Select(Join).ToList();
                if (expectedRows.Count > 0 && !rows.SequenceEqual(expectedRows))
                {
                    mismatches.Add($"{where}: expected rows {string.Join(", ", expectedRows)}, found {string.Join(", ", rows)}");
                }

                break;
            case SemanticList list:
                var items = list.Items.Select(item => Normalize(item.Text)).ToList();
                if (expected.Items.Count > 0 && !items.SequenceEqual(expected.Items.Select(Normalize)))
                {
                    mismatches.Add($"{where}: expected items [{string.Join(" | ", expected.Items)}], found [{string.Join(" | ", items)}]");
                }

                break;
        }

        var runs = Runs(actual).Where(run => run.Link is not null).ToList();
        foreach (var link in expected.Links.Where(link => !runs.Any(run =>
            Normalize(run.Text) == Normalize(link.Text)
            && (link.Uri is null || run.Link!.Uri == link.Uri)
            && (link.Page is null || run.Link!.TargetPage == link.Page))))
        {
            mismatches.Add($"{where}: no link \"{link.Text}\" to {link.Uri ?? (link.Page is { } target ? $"page {target}" : "anywhere")}");
        }
    }

    private static void Text(string where, string? expected, string actual, List<string> mismatches)
    {
        if (expected is not null && Normalize(expected) != Normalize(actual))
        {
            mismatches.Add($"{where}: expected text \"{expected}\", found \"{actual}\"");
        }
    }

    private static IEnumerable<SemanticRun> Runs(SemanticBlock block) => block switch
    {
        SemanticHeading heading => heading.Runs,
        SemanticParagraph paragraph => paragraph.Runs,
        SemanticList list => list.Items.SelectMany(item => item.Runs),
        SemanticTable table => (table.Header is { } header ? new[] { header } : []).Concat(table.Rows).SelectMany(row => row.Cells).SelectMany(cell => cell.Runs),
        _ => [],
    };

    private static string Kind(SemanticBlock block) => block switch
    {
        SemanticHeading => "heading",
        SemanticParagraph => "paragraph",
        SemanticTable => "table",
        SemanticList => "list",
        _ => "figure",
    };

    private static string Name(SemanticArtifactRole role) => role == SemanticArtifactRole.Header ? "header" : "footer";

    private static string Found(IReadOnlyList<SemanticArtifact> artifacts) =>
        artifacts.Count == 0 ? "none" : string.Join(", ", artifacts.Select(a => $"{Name(a.Role)} \"{a.Text}\""));

    private static string Cells(SemanticRow row) => Join(row.Cells.Select(cell => cell.Text).ToList());

    private static string Join(IReadOnlyList<string> cells) => "[" + string.Join(" | ", cells.Select(Normalize)) + "]";

    // Pages as ranges: 1, 2, 3, 5 → "1-3,5".
    private static string Range(IReadOnlyList<int> pages)
    {
        var parts = new List<string>();
        for (var i = 0; i < pages.Count; i++)
        {
            var start = pages[i];
            while (i + 1 < pages.Count && pages[i + 1] == pages[i] + 1)
            {
                i++;
            }

            parts.Add(start == pages[i] ? start.ToString(CultureInfo.InvariantCulture) : $"{start.ToString(CultureInfo.InvariantCulture)}-{pages[i].ToString(CultureInfo.InvariantCulture)}");
        }

        return string.Join(",", parts);
    }

    private static string Excerpt(string text) => text.Length <= 40 ? text : text.Substring(0, 40) + "...";

    private static string Normalize(string text) => Regex.Replace(text, @"\s+", " ").Trim();
}
```
In `GeneratedCorpusTests`, call `OracleComparison.Compare(oracle, pdf)`, with no `checkRoles`.

- [ ] **Step 4: Run the comparison tests**

Run: `dotnet test --project tests/PdfProof.Corpus.Tests/PdfProof.Corpus.Tests.csproj`
Expected: the comparison and verdict tests pass. The matrix cases run against the fixtures from Plan 4 (regenerate them with `./build.ps1 --target=GenerateFixtures` if `artifacts/corpus` is missing).

A matrix case may fail here; Step 5 decides what each failure is.

- [ ] **Step 5: Judge every failing matrix case**

From the probes, all seven producers should match the invoice oracle with `deviations.json` left as `{}`. For each failing case, read its mismatches and look at the semantic model of that fixture (`doc.Semantics.Blocks`, `Decisions`). Then decide which of three cases it is:
- **A PdfProof bug or a missing heuristic.** The information is in the layout or tags, but a pass misses it: a split, a header row, a marker or a link. Write a failing `RawPdf` test in `PdfProof.Tests` that reproduces the shape, fix the pass, and record a ledger ruling.
- **A producer gap.** The producer doesn't put the information in the PDF at all. Add a one-line reason to `deviations.json`.
- **An oracle mistake.** Fix the oracle, and record why in the commit message.

Never loosen the comparison to make a case pass.

Then run the matrix as CI does:
```bash
PDFPROOF_REQUIRE_FIXTURES=true dotnet test --project tests/PdfProof.Corpus.Tests/PdfProof.Corpus.Tests.csproj
```
Expected: everything passes; `libreoffice` is skipped.

- [ ] **Step 6: Semantic checks on the IRS forms**

In `ExternalCorpusTests`, add to `The_W9_is_a_tagged_six_page_form_with_nested_fields`:
```csharp
        Assert.Equal(PdfProof.Semantics.SemanticSource.Declared, doc.Semantics.Source);
        Assert.Contains(doc.Semantics.Headings, h => h.Text == "Form W-9");
        Assert.True(doc.Semantics.Blocks.Count > 20);
```
and to `The_W4_is_a_tagged_five_page_form`:
```csharp
        Assert.Equal(PdfProof.Semantics.SemanticSource.Declared, doc.Semantics.Source);
        Assert.NotEmpty(doc.Semantics.Headings);
```
Run: `./build.ps1 --target=FetchCorpus` if `artifacts/external` is missing, then `dotnet test --project tests/PdfProof.Corpus.Tests/PdfProof.Corpus.Tests.csproj`.
Expected: all tests pass. If the W-9's first heading text differs (the probe showed `H1 [Form W-9]`), check the structure tree's text before changing the assertion.

- [ ] **Step 7: Update `AGENTS.md` and run the whole build**

In the `## Layout` table, replace the `src/PdfProof` row with:
```markdown
| `src/PdfProof` | Core: `PdfProofDocument`, options and profiles, diagnostics; `Layout/` (layout model and strategies); `Structure/` (Tagged PDF structure tree); `Semantics/` (semantic model: front ends, passes, decision log) (netstandard2.0; net8.0; net10.0) |
```
Run: `./build.ps1 --require-fixtures`
Expected: the whole chain succeeds; the corpus matrix passes with `libreoffice` skipped.

- [ ] **Step 8: Commit**

```bash
git add tests/PdfProof.Corpus.Tests corpus AGENTS.md
git commit -m "Compare the corpus against the semantic model

The oracle comparison now checks the blocks in order (type, page, level, text,
table header and rows, list items, links) and the artifacts by role, text and
page range. The IRS forms get basic semantic checks. AGENTS.md lists Semantics/.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Self-review notes

- **Spec coverage:**

  | Spec item | Task |
  |---|---|
  | §5.1 `SemanticModel`: `Source`, `Blocks`, `Sections`, `Artifacts`, `Decisions`, navigation helpers | 1, 2, 4 |
  | §5.3 `PdfProfile.Semantics`: source, one options record per pass, passes can be switched off (D8) | 1, and each pass's task |
  | §6 row 0a: layout front end with block splitting | 2 |
  | §6 row 0b: structure front end with producer quirks | 8 |
  | §6 pass 1: ArtifactDetection (band, repetition, page-number patterns, one-page documents) | 3 |
  | §6 pass 3: HeadingClassification (body size, `MinSizeRatio`, `MaxLines`, bold short lines, levels from clusters) | 4 |
  | §6 pass 4: TableDetection (untagged), header normalization (tagged) | 5, 8 |
  | §6 pass 5: ListDetection with glyphs, numbering and marker shapes (flat) | 6 |
  | §6 pass 6: LinkAttachment (geometry for untagged, `OBJR` for tagged) | 7, 8 |
  | §6 pass 7: Finalize (`Unknown` becomes paragraph, sections, order, frozen model) | 2, 4 |
  | §6 rules: tags are trusted (passes 3–5 skip declared documents), sections by source, origin (`Page`, `Source`, internal box), every decision logged | 2–8 |
  | §10.2: corpus comparison on full block lists, artifacts with page ranges | 9 |

  Deliberately not in this plan:
  - pass 2 CrossPageMerge, nested lists and layout figures: 5b, with the `report`, `long-table` and `letter` documents;
  - geometry export of block boxes: Plan 6;
  - `explain` grouping decisions by pass: Plan 7.
- **Placeholders:** none. Task 2's `SemanticBuilder` uses the layout front end for tagged documents until Task 8; both tasks say so.
- **Type consistency:**
  - `Candidate`, `CandidateLine` and `CandidateCell` are defined in Task 2 and used unchanged through Task 8.
  - `LayoutBuildResult` gains `Markers` (Task 6) and changes `MarkedText`'s value type (Task 8); `LayoutBuilder` is the only constructor call.
  - `SemanticText.Concat`, `Normalize` and `Link` (Task 1) are the only ways runs are built.
- **Review Focus:** each line has its test in the owning task:
  1. Task 2, `An_empty_document_gives_an_empty_model`;
  2. Task 7, `Two_links_with_the_same_text…`;
  3. Task 4, `Sizes_in_decisions_use_the_invariant_culture`;
  4. Task 3, `A_two_part_footer…`;
  5. Task 8, `An_element_whose_marked_content_is_missing…`.
