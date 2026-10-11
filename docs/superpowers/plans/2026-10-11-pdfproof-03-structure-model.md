# PdfProof Plan 3: Structure Model Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `PdfProofDocument.Structure` exposes the Tagged PDF structure tree (roles after RoleMap, marked text, alt and actual text, language, table attributes, links) as an immutable `StructureModel`. The model is never null: an untagged PDF gets an empty root and `IsTagged == false`.

**Architecture:**
- **The tree is read in two phases.**
  1. `StructureTreeReader` walks PdfPig's tokens from `/StructTreeRoot` into a small internal *raw tree* (role as written, page, attributes, kids). It runs inside `PdfPigCall`.
  2. `StructureBuilder` maps the raw tree to the public model outside `PdfPigCall`: RoleMap, marked text, link matching, normalization.
- **Marked text is collected per page during the layout pass**, only when the document has a structure tree, so pages are never read twice.
- **The model is faithful:** element content keeps its document order (text runs, child elements, annotation references). Repairing producer quirks, such as QuestPDF's links on `P`, belongs to the semantic structure front end in Plan 5 (spec §6), not here.
- **A damaged structure tree doesn't fail the load.** The structure is left empty and a `StructureUnreadable` warning is recorded, the same rule as bookmarks.

**Tech Stack:** PdfPig 0.1.16 (`document.Structure`, tokens, `Page.GetMarkedContents()`), xUnit v3, `RawPdf` test helper.

**Spec:** `docs/superpowers/specs/2026-10-11-pdfproof-v1-design.md` (§3 D5, D6; §5.1; §5.4; §6 "Tags are trusted"; FR-MOD-10/11 in the original requirements)

## Where this plan fits

| Plan | Delivers |
|---|---|
| 1 Foundation, 2 Layout model | Done (PR #1, #2) |
| **3 Structure model (this plan)** | `StructureModel`, structure tree reader, marked text, RoleMap, links, attributes, diagnostics |
| 4 Fixture generator and corpus | Generator, `invoice` per producer, corpus matrix; AcroForm fields, encrypted PDFs |
| 5 Semantic passes | Candidate blocks; the structure front end, including producer quirks such as links on `P` |
| 6 Export | Export tree, scrubbing, Markdown and JSON (including the `structure` section) |
| 7 CLI, skill and `0.1.0-alpha` | `export`, `explain`, `skill …`, first pre-release |

## Global Constraints

- Library target frameworks: `netstandard2.0;net8.0;net10.0`. Tests: `net48;net10.0` on Windows, `net10.0` elsewhere.
- **netstandard2.0-safe code:** no `GeneratedRegex`, `[^1]`, `ToHashSet`, `KeyValuePair` deconstruction or `string.Join(char, …)`.
- `TreatWarningsAsErrors`, nullable enabled, and XML docs on every public member.
- Every public API change goes into `src/PdfProof/PublicAPI.Unshipped.txt`. Use the command in `AGENTS.md` after adding public members, then rebuild and review the appended lines.
- **Guard clauses and throws** use the shared `Throw` helper (`Throw.IfNull`, `Throw.ArgumentException`, …). Calls into PdfPig go through `PdfPigCall.Read`, and mapping to the model happens outside it.
- **Public model collections** are read-only copies (`ReadOnly.List`), because the model is shared between threads (spec D4).
- Deterministic output: invariant culture, document order, ordinal comparisons.
- **Test PDFs are built in code with `RawPdf`.** Never write `\u` or `\t` escapes in this plan's code: use `(char)0xAD`-style expressions, because escapes get mangled when files are written.
- Never write real company or customer names. Sample data is neutral.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Test command: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`. The whole build is `./build.ps1`.

## Review Focus

1. **A structure tree with cycles** (an element whose `/K` points back at an ancestor) or extreme depth must load in bounded time, without a stack overflow. Test in Task 6.
2. **A structure tree referencing a missing or unparseable object** must load the document with an empty structure and a `StructureUnreadable` warning. Pages and layout are unaffected. Test in Task 6.
3. **Marked content hidden by clipping** must not appear as structure text. The layout drops it, and the structure must agree. Test in Task 3.
4. **Artifacts** (`/Artifact` elements and artifact marked content: running headers, page numbers) must not appear in the structure. Test in Task 5.
5. **An OBJR pointing at a link whose rectangle doesn't match any layout link** (e.g. a rectangle with reversed corners) must still match when the rectangles are the same after normalization. An OBJR to a non-link annotation gives a reference with `Link == null`. Test in Task 4.

---

### Task 1: Public structure types and the untagged model

**Files:**
- Create in `src/PdfProof/Structure/`: `StructureContent.cs`, `StructureText.cs`, `StructureObjectReference.cs`, `StructureNode.cs`, `StructureModel.cs`
- Create: `tests/PdfProof.Tests/StructureTests.cs`
- Modify: `src/PdfProof/PdfProofDocument.cs`, `src/PdfProof/PublicAPI.Unshipped.txt`

**Interfaces:**
- Consumes: `ReadOnly.List` (Plan 2), `PdfProof.Layout.LayoutLink` (Plan 2).
- Produces:
  - `abstract class StructureContent`: `int? Page`; constructor `private protected`, so only the three types below derive from it.
  - `sealed class StructureText : StructureContent`: `string Text`, `int MarkedContentId`.
  - `sealed class StructureObjectReference : StructureContent`: `string Type`, `LayoutLink? Link`.
  - `sealed class StructureNode : StructureContent`:
    - `string Role`, `string? CustomRole`, `string? Alt`, `string? ActualText`, `string? Language`, `string? Scope`, `int RowSpan`, `int ColumnSpan`
    - `IReadOnlyList<StructureContent> Content`, `IReadOnlyList<StructureNode> Children`, `string Text`
    - `IEnumerable<StructureNode> Descendants()`
  - `sealed class StructureModel`: `bool IsTagged`, `StructureNode Root`; internal `StructureModel.Untagged`.
  - `PdfProofDocument.Structure : StructureModel`.

- [ ] **Step 1: Write the failing test**

`tests/PdfProof.Tests/StructureTests.cs`:
```csharp
using PdfProof.Structure;

namespace PdfProof.Tests;

public sealed class StructureTests
{
    [Fact]
    public void An_untagged_pdf_has_an_empty_structure_root_and_is_not_tagged()
    {
        var structure = PdfProofDocument.Load(RowsLayoutTests.Pdf(RawPdf.Text(50, 750, "Hello"))).Structure;

        Assert.False(structure.IsTagged);
        Assert.Equal("StructTreeRoot", structure.Root.Role);
        Assert.Empty(structure.Root.Content);
        Assert.Empty(structure.Root.Children);
        Assert.Equal("", structure.Root.Text);
        Assert.Null(structure.Root.Page);
        Assert.Empty(structure.Root.Descendants());
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: build FAILS with `CS0234`/`CS1061`: namespace `PdfProof.Structure` and `PdfProofDocument.Structure` do not exist.

- [ ] **Step 3: Implement the types**

`src/PdfProof/Structure/StructureContent.cs`:
```csharp
namespace PdfProof.Structure;

/// <summary>
/// One item of a structure element's content, in document order: a child element (<see cref="StructureNode"/>), a
/// run of marked text (<see cref="StructureText"/>) or a reference to an annotation
/// (<see cref="StructureObjectReference"/>).
/// </summary>
public abstract class StructureContent
{
    private protected StructureContent(int? page)
    {
        Page = page;
    }

    /// <summary>The page the content is on, when known.</summary>
    public int? Page { get; }
}
```

`src/PdfProof/Structure/StructureText.cs`:
```csharp
namespace PdfProof.Structure;

/// <summary>The text of one marked-content sequence, referenced from a structure element.</summary>
public sealed class StructureText : StructureContent
{
    internal StructureText(string text, int? page, int markedContentId)
        : base(page)
    {
        Text = text;
        MarkedContentId = markedContentId;
    }

    /// <summary>
    /// The sequence's visible text, normalized like the layout model: words joined by single spaces. Text hidden by
    /// clipping is left out.
    /// </summary>
    public string Text { get; }

    /// <summary>The marked-content identifier (MCID) of the sequence on its page.</summary>
    public int MarkedContentId { get; }
}
```

`src/PdfProof/Structure/StructureObjectReference.cs`:
```csharp
using PdfProof.Layout;

namespace PdfProof.Structure;

/// <summary>A reference from a structure element to an annotation (an OBJR), e.g. a link or a form widget.</summary>
public sealed class StructureObjectReference : StructureContent
{
    internal StructureObjectReference(string type, int? page, LayoutLink? link)
        : base(page)
    {
        Type = type;
        Link = link;
    }

    /// <summary>The annotation's subtype, e.g. <c>Link</c> or <c>Widget</c>; <c>Unknown</c> when it has none.</summary>
    public string Type { get; }

    /// <summary>
    /// For a link annotation: the same link as in <see cref="LayoutPage.Links"/> on its page (matched by rectangle),
    /// with its text and target. Otherwise <see langword="null"/>.
    /// </summary>
    public LayoutLink? Link { get; }
}
```

`src/PdfProof/Structure/StructureNode.cs`:
```csharp
namespace PdfProof.Structure;

/// <summary>A structure element of a Tagged PDF, e.g. a heading, paragraph, table cell or link.</summary>
public sealed class StructureNode : StructureContent
{
    internal StructureNode(
        string role,
        string? customRole,
        int? page,
        string? alt,
        string? actualText,
        string? language,
        string? scope,
        int rowSpan,
        int columnSpan,
        IReadOnlyList<StructureContent> content)
        : base(page)
    {
        Role = role;
        CustomRole = customRole;
        Alt = alt;
        ActualText = actualText;
        Language = language;
        Scope = scope;
        RowSpan = rowSpan;
        ColumnSpan = columnSpan;
        Content = ReadOnly.List(content);
        Children = ReadOnly.List(content.OfType<StructureNode>());
        Text = actualText ?? string.Join(" ", content.Select(TextOf).Where(text => text.Length > 0));
    }

    /// <summary>
    /// The standard structure type after <c>/RoleMap</c>, e.g. <c>Document</c>, <c>H1</c>, <c>P</c>, <c>Table</c>,
    /// <c>TR</c>, <c>TH</c>, <c>TD</c>, <c>L</c>, <c>LI</c>, <c>Link</c> or <c>Figure</c>. The root is
    /// <c>StructTreeRoot</c>.
    /// </summary>
    public string Role { get; }

    /// <summary>The role as written in the PDF, when <c>/RoleMap</c> mapped it to another role; otherwise <see langword="null"/>.</summary>
    public string? CustomRole { get; }

    /// <summary>The alternative description (<c>/Alt</c>), e.g. of a figure.</summary>
    public string? Alt { get; }

    /// <summary>The replacement text (<c>/ActualText</c>). When set, it is also <see cref="Text"/>.</summary>
    public string? ActualText { get; }

    /// <summary>The element's language (<c>/Lang</c>), e.g. <c>sv-SE</c>.</summary>
    public string? Language { get; }

    /// <summary>For a table header cell: <c>Row</c>, <c>Column</c> or <c>Both</c> (table attribute <c>/Scope</c>).</summary>
    public string? Scope { get; }

    /// <summary>For a table cell: the number of rows it spans (table attribute <c>/RowSpan</c>); 1 by default.</summary>
    public int RowSpan { get; }

    /// <summary>For a table cell: the number of columns it spans (table attribute <c>/ColSpan</c>); 1 by default.</summary>
    public int ColumnSpan { get; }

    /// <summary>The element's content in document order: child elements, marked text and annotation references.</summary>
    public IReadOnlyList<StructureContent> Content { get; }

    /// <summary>The child elements, in document order.</summary>
    public IReadOnlyList<StructureNode> Children { get; }

    /// <summary>
    /// The element's text: <see cref="ActualText"/> when set, otherwise the text of its content, in order, joined
    /// by single spaces.
    /// </summary>
    public string Text { get; }

    /// <summary>All elements below this one, depth first in document order, not including this element.</summary>
    public IEnumerable<StructureNode> Descendants()
    {
        foreach (var child in Children)
        {
            yield return child;
            foreach (var descendant in child.Descendants())
            {
                yield return descendant;
            }
        }
    }

    private static string TextOf(StructureContent content) => content switch
    {
        StructureText text => text.Text,
        StructureNode node => node.Text,
        _ => "",
    };
}
```

`src/PdfProof/Structure/StructureModel.cs`:
```csharp
namespace PdfProof.Structure;

/// <summary>
/// The logical structure tree of a Tagged PDF (PDF/UA): the producer's declared headings, paragraphs, tables, lists,
/// links and figures. Never <see langword="null"/>: an untagged PDF has an empty <see cref="Root"/>.
/// </summary>
public sealed class StructureModel
{
    internal StructureModel(StructureNode root)
    {
        Root = root;
        IsTagged = root.Children.Count > 0;
    }

    internal static StructureModel Untagged { get; } =
        new(new StructureNode("StructTreeRoot", null, null, null, null, null, null, 1, 1, []));

    /// <summary>
    /// <see langword="true"/> when the document has a structure tree with at least one element (not counting
    /// artifacts). <c>/MarkInfo</c> is not used; when it disagrees, <see cref="PdfProofDocument.Diagnostics"/>
    /// notes it.
    /// </summary>
    public bool IsTagged { get; }

    /// <summary>The tree's root, role <c>StructTreeRoot</c>; its children are the top-level elements.</summary>
    public StructureNode Root { get; }
}
```

In `src/PdfProof/PdfProofDocument.cs`:
- Add `using PdfProof.Structure;`.
- Add the constructor parameter `StructureModel structure` after `LayoutModel layout`, assign `Structure = structure;`, and add this property after `Layout`:
```csharp

    /// <summary>The Tagged PDF structure tree; empty, with <see cref="StructureModel.IsTagged"/> false, when the PDF isn't tagged.</summary>
    public StructureModel Structure { get; }
```
- In `Open`, pass `StructureModel.Untagged` as the structure in the `new PdfProofDocument(…)` call. Task 3 replaces it with the real structure.

Record the public API with the command in `AGENTS.md`.

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/PdfProof tests/PdfProof.Tests
git commit -m "Add the public structure model; untagged PDFs get an empty root

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `RawPdf` helpers for tagged PDFs

**Files:**
- Modify: `tests/PdfProof.Tests/RawPdf.cs`
- Create: `tests/PdfProof.Tests/RawPdfTaggingTests.cs`

**Interfaces:**
- Produces, on `RawPdf`:
  - `static string Marked(string tag, int mcid, string body)`: wraps content in `/tag <</MCID n>> BDC … EMC`.
  - `static string Artifact(string body)`: wraps content in `/Artifact BDC … EMC`.
  - `int Tag(string kids, string rootEntries = "", bool marked = true)`: adds a `/StructTreeRoot` with the given `/K` array content, plus `/MarkInfo << /Marked true >>` when `marked`, and returns the root's object number.

- [ ] **Step 1: Write the failing test**

These helpers are used by every later task. The test checks them through PdfPig directly, so it doesn't depend on any PdfProof code that's still to be written.

`tests/PdfProof.Tests/RawPdfTaggingTests.cs`:
```csharp
using UglyToad.PdfPig;
using UglyToad.PdfPig.Tokens;

namespace PdfProof.Tests;

public sealed class RawPdfTaggingTests
{
    [Fact]
    public void Tag_adds_a_structure_tree_root_and_mark_info_and_Marked_wraps_marked_content()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("P", 0, RawPdf.Text(50, 750, "Hello")) + RawPdf.Artifact(RawPdf.Text(50, 30, "Page 1")));
        var paragraph = pdf.Add($"<< /S /P /Pg {page} 0 R /K 0 >>");
        pdf.Tag($"{paragraph} 0 R");

        using var document = PdfDocument.Open(pdf.Build());

        var catalog = document.Structure.Catalog.CatalogDictionary;
        Assert.True(catalog.Data.ContainsKey("StructTreeRoot"));
        Assert.True(catalog.Data.ContainsKey("MarkInfo"));
        var marked = document.GetPage(1).GetMarkedContents();
        Assert.Contains(marked, m => m.MarkedContentIdentifier == 0 && string.Concat(m.Letters.Select(l => l.Value)) == "Hello");
        Assert.Contains(marked, m => m.IsArtifact);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: build FAILS with `CS0117`: `RawPdf` has no `Marked`, `Artifact` or `Tag`.

- [ ] **Step 3: Implement**

In `tests/PdfProof.Tests/RawPdf.cs`, add after the `Text` method:
```csharp

    /// A marked-content sequence with an MCID, as a Tagged PDF writes it: "/P <</MCID 0>> BDC ... EMC".
    public static string Marked(string tag, int mcid, string body) =>
        FormattableString.Invariant($"/{tag} <</MCID {mcid}>> BDC\n{body}EMC\n");

    /// An artifact sequence (running header, page number): "/Artifact BDC ... EMC".
    public static string Artifact(string body) => $"/Artifact BDC\n{body}EMC\n";

    /// Adds a /StructTreeRoot whose /K array holds `kids` (e.g. "12 0 R 13 0 R"), plus /MarkInfo /Marked true unless
    /// `marked` is false. Returns the root's object number.
    public int Tag(string kids, string rootEntries = "", bool marked = true)
    {
        var root = Add($"<< /Type /StructTreeRoot /K [{kids}] {rootEntries} >>");
        CatalogEntries += $" /StructTreeRoot {root} 0 R" + (marked ? " /MarkInfo << /Marked true >>" : "");
        return root;
    }
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add tests/PdfProof.Tests
git commit -m "Add RawPdf helpers for marked content and structure trees

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Read the structure tree: elements, roles, RoleMap and marked text

**Files:**
- Create: `src/PdfProof/Structure/RawStructure.cs`, `src/PdfProof/Structure/StructureTreeReader.cs`, `src/PdfProof/Structure/StructureBuilder.cs`, `src/PdfProof/Layout/MarkedContentText.cs`, `src/PdfProof/Layout/LayoutBuildResult.cs`
- Modify: `src/PdfProof/Layout/LayoutBuilder.cs`, `src/PdfProof/PdfProofDocument.cs`, `src/PdfProof/PdfDiagnosticCodes.cs`, `src/PdfProof/PublicAPI.Unshipped.txt`, `tests/PdfProof.Tests/StructureTests.cs`

**Interfaces:**
- Consumes: the Task 1 types, the Task 2 helpers, `LayoutBuilder` and `WordFactory` (Plan 2).
- Produces:
  - Internal raw tree (records):
    - `RawStructure(bool HasStructTreeRoot, bool Marked, IReadOnlyDictionary<string, string> RoleMap, IReadOnlyList<RawElement> Elements)` with `RawStructure.None`
    - `abstract record RawKid`
    - `RawElement(string Role, int? Page, string? Alt, string? ActualText, string? Language, string? Scope, int RowSpan, int ColumnSpan, IReadOnlyList<RawKid> Kids) : RawKid`
    - `RawMarkedContent(int? Page, int Mcid) : RawKid`
    - `RawObjectReference(int? Page, string? Subtype, double[]? Rectangle) : RawKid`
  - Internal `StructureTreeReader.Read(PdfDocument) -> RawStructure`. It runs inside `PdfPigCall`, and Task 3 covers elements and MCIDs; Tasks 4–5 add OBJRs and attributes.
  - Internal `StructureBuilder.Build(RawStructure, LayoutBuildResult, LayoutOptions) -> StructureModel`.
  - Internal `MarkedContentText.Collect(IReadOnlyList<MarkedContentElement>, LayoutOptions) -> IReadOnlyDictionary<int, string>` (MCID to text).
  - Internal `LayoutBuildResult(LayoutModel Model, IReadOnlyDictionary<int, IReadOnlyDictionary<int, string>> MarkedText)`.
  - `LayoutBuilder.Build(PdfDocument, LayoutOptions, DiagnosticsCollector, bool collectMarkedText) -> LayoutBuildResult`.
  - `PdfDiagnosticCodes.Structure = "Structure"`, recorded when tagged: `"<n> elements"`.

- [ ] **Step 1: Write the failing tests**

Add to `tests/PdfProof.Tests/StructureTests.cs`, inside the class:
```csharp

    [Fact]
    public void Elements_are_read_with_their_roles_marked_text_and_pages()
    {
        var pdf = new RawPdf();
        var first = pdf.AddPage(
            RawPdf.Marked("H1", 0, RawPdf.Text(50, 750, "Invoice", 18)) +
            RawPdf.Marked("P", 1, RawPdf.Text(50, 700, "Hello") + RawPdf.Text(90, 700, "world")));
        var second = pdf.AddPage(RawPdf.Marked("P", 0, RawPdf.Text(50, 750, "Terms apply.")));
        var heading = pdf.Add($"<< /S /H1 /Pg {first} 0 R /K 0 >>");
        var paragraph = pdf.Add($"<< /S /P /Pg {first} 0 R /K 1 >>");
        var terms = pdf.Add($"<< /S /P /Pg {second} 0 R /K 0 >>");
        var document = pdf.Add($"<< /S /Document /K [{heading} 0 R {paragraph} 0 R {terms} 0 R] >>");
        pdf.Tag($"{document} 0 R");

        var doc = PdfProofDocument.Load(pdf.Build());

        Assert.True(doc.Structure.IsTagged);
        var root = Assert.Single(doc.Structure.Root.Children);
        Assert.Equal("Document", root.Role);
        Assert.Equal(
            [("H1", "Invoice", (int?)1), ("P", "Hello world", 1), ("P", "Terms apply.", 2)],
            root.Children.Select(n => (n.Role, n.Text, n.Page)));
        var text = Assert.IsType<StructureText>(Assert.Single(root.Children[1].Content));
        Assert.Equal((1, (int?)1), (text.MarkedContentId, text.Page));
        Assert.Contains(doc.Diagnostics.Entries, e => e.ToString() == "Structure: 4 elements");
    }

    [Fact]
    public void RoleMap_maps_custom_roles_to_standard_ones_and_keeps_the_custom_name()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("Title", 0, RawPdf.Text(50, 750, "Invoice", 18)));
        var title = pdf.Add($"<< /S /Title /Pg {page} 0 R /K 0 >>");
        pdf.Tag($"{title} 0 R", "/RoleMap << /Title /Heading /Heading /H1 >>");

        var node = Assert.Single(PdfProofDocument.Load(pdf.Build()).Structure.Root.Children);

        Assert.Equal(("H1", "Title"), (node.Role, node.CustomRole));
    }

    [Fact]
    public void Marked_text_is_normalized_like_the_layout()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("P", 0, RawPdf.Text(50, 750, "e" + (char)0xAD + "mail", font: "F4")));
        pdf.Tag($"{pdf.Add($"<< /S /P /Pg {page} 0 R /K 0 >>")} 0 R");

        Assert.Equal("e-mail", PdfProofDocument.Load(pdf.Build()).Structure.Root.Children[0].Text);
    }

    [Fact]
    public void Marked_content_hidden_by_clipping_has_no_text_in_the_structure_either()
    {
        // Review Focus 3: the layout drops clipped text, and the structure must agree.
        var pdf = new RawPdf();
        var page = pdf.AddPage(
            RawPdf.Marked("P", 0, RawPdf.Text(50, 750, "Visible")) +
            "q 0 0 10 10 re W n\n" + RawPdf.Marked("P", 1, RawPdf.Text(50, 600, "Clipped away")) + "Q\n");
        var visible = pdf.Add($"<< /S /P /Pg {page} 0 R /K 0 >>");
        var clipped = pdf.Add($"<< /S /P /Pg {page} 0 R /K 1 >>");
        pdf.Tag($"{visible} 0 R {clipped} 0 R");

        var structure = PdfProofDocument.Load(pdf.Build()).Structure;

        Assert.Equal(["Visible", ""], structure.Root.Children.Select(n => n.Text));
        Assert.Empty(structure.Root.Children[1].Content);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: FAIL. `IsTagged` is false and `Root.Children` is empty, because the structure is never read. The build errors for `PdfDiagnosticCodes.Structure` until Step 3 adds it, so add the constant first if you want to see the assertion failures.

- [ ] **Step 3: Implement the raw tree and the reader**

In `src/PdfProof/PdfDiagnosticCodes.cs`, add after `BookmarksUnreadable`:
```csharp

    /// <summary>What the structure tree holds, for tagged documents.</summary>
    public const string Structure = "Structure";
```

`src/PdfProof/Structure/RawStructure.cs`:
```csharp
namespace PdfProof.Structure;

// The structure tree as PdfPig's tokens describe it: roles as written, pages resolved, attributes read. Built inside
// PdfPigCall by StructureTreeReader; StructureBuilder maps it to the public model outside.
internal sealed record RawStructure(
    bool HasStructTreeRoot,
    bool Marked,
    IReadOnlyDictionary<string, string> RoleMap,
    IReadOnlyList<RawElement> Elements)
{
    public static RawStructure None { get; } = new(false, false, new Dictionary<string, string>(), []);
}

internal abstract record RawKid;

internal sealed record RawElement(
    string Role,
    int? Page,
    string? Alt,
    string? ActualText,
    string? Language,
    string? Scope,
    int RowSpan,
    int ColumnSpan,
    IReadOnlyList<RawKid> Kids) : RawKid;

internal sealed record RawMarkedContent(int? Page, int Mcid) : RawKid;

internal sealed record RawObjectReference(int? Page, string? Subtype, double[]? Rectangle) : RawKid;
```

`src/PdfProof/Structure/StructureTreeReader.cs`:
```csharp
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Tokens;

namespace PdfProof.Structure;

// Reads the logical structure tree of a Tagged PDF (ISO 32000 section 14.7) from the catalog's /StructTreeRoot into a
// raw tree. PdfPig exposes marked content per page but not the tree. Runs inside PdfPigCall: everything here reads
// PdfPig tokens. Cycles and depth are bounded, so damaged trees load in bounded time.
internal sealed class StructureTreeReader
{
    private const int MaxDepth = 256;

    private readonly PdfDocument _document;
    private readonly Dictionary<IndirectReference, int> _pageNumbers = [];
    private readonly HashSet<IndirectReference> _visited = [];

    private StructureTreeReader(PdfDocument document)
    {
        _document = document;
    }

    public static RawStructure Read(PdfDocument document) => new StructureTreeReader(document).ReadRoot();

    private RawStructure ReadRoot()
    {
        var catalog = _document.Structure.Catalog.CatalogDictionary;
        var marked = Resolve(Get(catalog, "MarkInfo")) is DictionaryToken markInfo
            && Resolve(Get(markInfo, "Marked")) is BooleanToken { Data: true };
        if (Resolve(Get(catalog, "StructTreeRoot")) is not DictionaryToken root)
        {
            return RawStructure.None with { Marked = marked };
        }

        var roleMap = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Resolve(Get(root, "RoleMap")) is DictionaryToken map)
        {
            foreach (var entry in map.Data)
            {
                if (Resolve(entry.Value) is NameToken name)
                {
                    roleMap[entry.Key] = name.Data;
                }
            }
        }

        IndexPages(Get(catalog, "Pages"));
        var elements = Kids(root).Select(kid => ReadKid(kid, null, 0)).OfType<RawElement>().ToList();
        return new RawStructure(true, marked, roleMap, elements);
    }

    private RawKid? ReadKid(IToken kid, int? page, int depth)
    {
        if (depth > MaxDepth)
        {
            return null;
        }

        if (kid is IndirectReferenceToken reference && !_visited.Add(reference.Data))
        {
            return null; // cycle guard
        }

        switch (Resolve(kid))
        {
            case NumericToken mcid:
                return new RawMarkedContent(page, mcid.Int);
            case DictionaryToken dictionary when Name(dictionary, "Type") == "MCR":
                return Get(dictionary, "MCID") is NumericToken id ? new RawMarkedContent(PageOf(dictionary) ?? page, id.Int) : null;
            case DictionaryToken dictionary when Name(dictionary, "Type") == "OBJR":
                return null; // Task 4
            case DictionaryToken dictionary when Name(dictionary, "S") is { } role:
                return ReadElement(dictionary, role, page, depth);
            default:
                return null;
        }
    }

    private RawElement ReadElement(DictionaryToken element, string role, int? inheritedPage, int depth)
    {
        var page = PageOf(element) ?? inheritedPage;
        var kids = Kids(element).Select(kid => ReadKid(kid, page, depth + 1)).OfType<RawKid>().ToList();
        return new RawElement(role, page, null, null, null, null, 1, 1, kids);
    }

    // Page objects in page-tree order, so /Pg references resolve to page numbers.
    private void IndexPages(IToken? pages)
    {
        var ordered = new List<IndirectReference>();
        var seen = new HashSet<IndirectReference>();
        var stack = new Stack<IToken>();
        if (pages is not null)
        {
            stack.Push(pages);
        }

        while (stack.Count > 0)
        {
            if (stack.Pop() is not IndirectReferenceToken reference || !seen.Add(reference.Data))
            {
                continue;
            }

            if (Resolve(reference) is not DictionaryToken node)
            {
                continue;
            }

            if (Name(node, "Type") == "Page")
            {
                ordered.Add(reference.Data);
            }
            else if (Resolve(Get(node, "Kids")) is ArrayToken kids)
            {
                for (var i = kids.Data.Count - 1; i >= 0; i--)
                {
                    stack.Push(kids.Data[i]); // reversed, so pages pop in document order
                }
            }
        }

        for (var i = 0; i < ordered.Count; i++)
        {
            _pageNumbers[ordered[i]] = i + 1;
        }
    }

    private int? PageOf(DictionaryToken dictionary) =>
        Get(dictionary, "Pg") is IndirectReferenceToken reference && _pageNumbers.TryGetValue(reference.Data, out var number) ? number : null;

    private IEnumerable<IToken> Kids(DictionaryToken dictionary)
    {
        var kids = Get(dictionary, "K");
        return Resolve(kids) switch
        {
            null => [],
            ArrayToken array => array.Data,
            _ => [kids!], // keep an indirect reference, so ReadKid can guard against cycles
        };
    }

    private IToken? Resolve(IToken? token)
    {
        for (var i = 0; i < 32 && token is IndirectReferenceToken reference; i++)
        {
            token = _document.Structure.GetObject(reference.Data)?.Data;
        }

        return token;
    }

    private static IToken? Get(DictionaryToken dictionary, string key) =>
        dictionary.Data.TryGetValue(key, out var value) ? value : null;

    private string? Name(DictionaryToken dictionary, string key) =>
        Resolve(Get(dictionary, key)) is NameToken name ? name.Data : null;
}
```

- [ ] **Step 4: Collect marked text during the layout pass**

`src/PdfProof/Layout/LayoutBuildResult.cs`:
```csharp
namespace PdfProof.Layout;

// The layout model plus, for tagged documents, the marked text per page (page number -> MCID -> text), collected in
// the same page pass so pages are read once.
internal sealed record LayoutBuildResult(LayoutModel Model, IReadOnlyDictionary<int, IReadOnlyDictionary<int, string>> MarkedText);
```

`src/PdfProof/Layout/MarkedContentText.cs`:
```csharp
using System.Text.RegularExpressions;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Util;

namespace PdfProof.Layout;

// The visible text of each marked-content sequence on a page, by MCID. Artifacts are skipped. Letters hidden by
// clipping are already gone (PdfPig applies ClipPaths to marked content too).
internal static class MarkedContentText
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.CultureInvariant);

    public static IReadOnlyDictionary<int, string> Collect(IReadOnlyList<MarkedContentElement> elements, LayoutOptions options)
    {
        var texts = new Dictionary<int, string>();
        foreach (var element in elements)
        {
            Collect(element, options, texts);
        }

        return texts;
    }

    private static void Collect(MarkedContentElement element, LayoutOptions options, Dictionary<int, string> texts)
    {
        if (element.IsArtifact)
        {
            return;
        }

        if (element.MarkedContentIdentifier >= 0)
        {
            var text = element.ActualText is { } actual ? Whitespace.Replace(WordFactory.Normalize(actual, options), " ").Trim() : Text(Letters(element).ToList(), options);
            texts[element.MarkedContentIdentifier] = texts.TryGetValue(element.MarkedContentIdentifier, out var earlier) && earlier.Length > 0
                ? (text.Length > 0 ? earlier + " " + text : earlier)
                : text;
            return; // nested sequences are part of this MCID's content
        }

        foreach (var child in element.Children)
        {
            Collect(child, options, texts);
        }
    }

    // Horizontal text: the word extractor recovers spaces from gaps, like the layout. Rotated text: content order is
    // the reading order within one sequence, and the word extractor would scramble it.
    private static string Text(List<Letter> letters, LayoutOptions options)
    {
        if (letters.All(letter => letter.TextOrientation == TextOrientation.Horizontal))
        {
            var words = DefaultWordExtractor.Instance.GetWords(letters)
                .Select(word => WordFactory.Create(word, options))
                .OfType<LayoutWord>()
                .Select(word => word.Text);
            return string.Join(" ", words);
        }

        return Whitespace.Replace(WordFactory.Normalize(string.Concat(letters.Select(letter => letter.Value)), options), " ").Trim();
    }

    private static IEnumerable<Letter> Letters(MarkedContentElement element) =>
        element.Letters.Concat(element.Children.SelectMany(Letters));
}
```

In `src/PdfProof/Layout/LayoutBuilder.cs`:
1. Change `Build` to return `LayoutBuildResult` and take `bool collectMarkedText`:
```csharp
    public static LayoutBuildResult Build(PdfDocument document, LayoutOptions options, DiagnosticsCollector diagnostics, bool collectMarkedText)
    {
        var pageCount = PdfPigCall.Read(() => document.NumberOfPages);
        var pages = new List<LayoutPage>(pageCount);
        var markedText = new Dictionary<int, IReadOnlyDictionary<int, string>>();
        for (var number = 1; number <= pageCount; number++)
        {
            var pageNumber = number;
            var page = PdfPigCall.Read(() => document.GetPage(pageNumber), pageNumber);
            pages.Add(BuildPage(page, pageNumber, options, diagnostics));
            if (collectMarkedText)
            {
                var marked = PdfPigCall.Read(() => page.GetMarkedContents().ToList(), pageNumber);
                markedText[pageNumber] = MarkedContentText.Collect(marked, options);
            }
        }

        return new LayoutBuildResult(new LayoutModel(pages, Bookmarks(document, diagnostics)), markedText);
    }
```
2. The rest of `LayoutBuilder` is unchanged.

`src/PdfProof/Structure/StructureBuilder.cs`:
```csharp
using PdfProof.Layout;

namespace PdfProof.Structure;

// Maps the raw tree to the public model: roles through /RoleMap, marked text from the layout pass. Runs outside
// PdfPigCall, so a bug here surfaces as itself.
internal sealed class StructureBuilder
{
    private readonly RawStructure _raw;
    private readonly LayoutBuildResult _layout;
    private readonly LayoutOptions _options;

    private StructureBuilder(RawStructure raw, LayoutBuildResult layout, LayoutOptions options)
    {
        _raw = raw;
        _layout = layout;
        _options = options;
    }

    public static StructureModel Build(RawStructure raw, LayoutBuildResult layout, LayoutOptions options)
    {
        var builder = new StructureBuilder(raw, layout, options);
        var elements = raw.Elements.Select(builder.Element).OfType<StructureNode>().ToList();
        return new StructureModel(new StructureNode("StructTreeRoot", null, null, null, null, null, null, 1, 1, elements));
    }

    private StructureNode? Element(RawElement raw)
    {
        var role = MapRole(raw.Role);
        var content = new List<StructureContent>();
        foreach (var kid in raw.Kids)
        {
            if (Content(kid) is { } item)
            {
                content.Add(item);
            }
        }

        return new StructureNode(role, raw.Role == role ? null : raw.Role, raw.Page, null, null, null, null, 1, 1, content);
    }

    private StructureContent? Content(RawKid kid) => kid switch
    {
        RawElement element => Element(element),
        RawMarkedContent marked => MarkedText(marked),
        _ => null,
    };

    private StructureText? MarkedText(RawMarkedContent marked) =>
        marked.Page is { } page
        && _layout.MarkedText.TryGetValue(page, out var texts)
        && texts.TryGetValue(marked.Mcid, out var text)
        && text.Length > 0
            ? new StructureText(text, page, marked.Mcid)
            : null;

    // RoleMap can chain (Custom -> Custom2 -> P); stop at a role that isn't mapped further.
    private string MapRole(string role)
    {
        for (var i = 0; i < 10 && _raw.RoleMap.TryGetValue(role, out var mapped) && mapped != role; i++)
        {
            role = mapped;
        }

        return role;
    }
}
```

In `src/PdfProof/PdfProofDocument.cs`, in `Open`, replace the layout and document construction (from `var layout = LayoutBuilder.Build(…);` to the final `return`) with:
```csharp
        var raw = PdfPigCall.Read(() => StructureTreeReader.Read(document));
        var tagged = raw.Elements.Count > 0;
        var layoutResult = LayoutBuilder.Build(document, effective.Layout, diagnostics, collectMarkedText: tagged);
        var layout = layoutResult.Model;
        if (layout.Pages.Count > 0 && layout.Pages.All(page => page.Blocks.Count == 0))
        {
            diagnostics.Add(PdfDiagnosticCodes.NoTextLayer, null, "No page has extractable text; the PDF may be a scan. OCR is not supported.");
        }

        var structure = tagged ? StructureBuilder.Build(raw, layoutResult, effective.Layout) : StructureModel.Untagged;
        if (structure.IsTagged)
        {
            diagnostics.Add(PdfDiagnosticCodes.Structure, null, $"{structure.Root.Descendants().Count()} elements");
        }

        return new PdfProofDocument(metadata, layout, structure, diagnostics.ToDiagnostics(effective.Profile, detected));
```

Record the public API with the command in `AGENTS.md`. The expected new line is the `Structure` constant.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/PdfProof tests/PdfProof.Tests
git commit -m "Read the structure tree: elements, roles, RoleMap and marked text

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Mixed content and annotation references (links)

**Files:**
- Modify: `src/PdfProof/Structure/StructureTreeReader.cs`, `src/PdfProof/Structure/StructureBuilder.cs`, `tests/PdfProof.Tests/StructureTests.cs`

**Interfaces:**
- Consumes: the Task 3 reader and builder, and `LayoutPage.Links`.
- Produces:
  - The reader emits `RawObjectReference` for OBJR kids: page, subtype and `/Rect` as 4 doubles.
  - The builder maps it to a `StructureObjectReference` whose `Link` is the layout link on that page with the same normalized, rounded rectangle (tolerance 0.5 pt).

- [ ] **Step 1: Write the failing tests**

Add to `StructureTests`:
```csharp

    [Fact]
    public void Content_keeps_document_order_of_text_child_elements_and_link_references()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(
            RawPdf.Marked("P", 0, RawPdf.Text(50, 700, "Questions? Visit")) +
            RawPdf.Marked("Link", 1, RawPdf.Text(150, 700, "our support site")) +
            RawPdf.Marked("P", 2, RawPdf.Text(250, 700, "today.")));
        var annotation = pdf.Add("<< /Type /Annot /Subtype /Link /Rect [145 695 245 715] /A << /S /URI /URI (https://example.com/support) >> >>");
        pdf.Annotate(page, $"{annotation} 0 R");
        var link = pdf.Add($"<< /S /Link /Pg {page} 0 R /K [1 << /Type /OBJR /Obj {annotation} 0 R >>] >>");
        var paragraph = pdf.Add($"<< /S /P /Pg {page} 0 R /K [0 {link} 0 R 2] >>");
        pdf.Tag($"{paragraph} 0 R");

        var node = Assert.Single(PdfProofDocument.Load(pdf.Build()).Structure.Root.Children);

        Assert.Equal("Questions? Visit our support site today.", node.Text);
        Assert.Collection(
            node.Content,
            item => Assert.Equal("Questions? Visit", Assert.IsType<StructureText>(item).Text),
            item => Assert.Equal("Link", Assert.IsType<StructureNode>(item).Role),
            item => Assert.Equal("today.", Assert.IsType<StructureText>(item).Text));
        var linkNode = node.Children[0];
        var reference = Assert.Single(linkNode.Content.OfType<StructureObjectReference>());
        Assert.Equal("Link", reference.Type);
        Assert.Equal(("our support site", "https://example.com/support"), (reference.Link!.Text, reference.Link.Uri));
        Assert.Equal("our support site", linkNode.Text);
    }

    [Fact]
    public void A_link_annotation_on_a_paragraph_is_kept_where_the_producer_put_it()
    {
        // QuestPDF attaches link annotations to the enclosing P instead of a Link element. The structure model
        // shows that as it is; the semantic layer (Plan 5) repairs it.
        var pdf = new RawPdf();
        var first = pdf.AddPage(RawPdf.Marked("P", 0, RawPdf.Text(50, 700, "Read the terms")));
        var second = pdf.AddPage(RawPdf.Text(50, 750, "Terms"));
        var annotation = pdf.Add($"<< /Type /Annot /Subtype /Link /Rect [45 715 140 695] /A << /S /GoTo /D [{second} 0 R /Fit] >> >>");
        pdf.Annotate(first, $"{annotation} 0 R");
        var paragraph = pdf.Add($"<< /S /P /Pg {first} 0 R /K [0 << /Type /OBJR /Obj {annotation} 0 R >>] >>");
        pdf.Tag($"{paragraph} 0 R");

        var node = Assert.Single(PdfProofDocument.Load(pdf.Build()).Structure.Root.Children);

        Assert.Equal("P", node.Role);
        var reference = Assert.Single(node.Content.OfType<StructureObjectReference>());
        Assert.Equal(2, reference.Link!.TargetPage); // the rectangle's corners are reversed: Review Focus 5
    }

    [Fact]
    public void A_reference_to_a_non_link_annotation_has_no_link()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("P", 0, RawPdf.Text(50, 700, "Note")));
        var annotation = pdf.Add("<< /Type /Annot /Subtype /Text /Rect [300 300 320 320] /Contents (A note) >>");
        pdf.Annotate(page, $"{annotation} 0 R");
        var paragraph = pdf.Add($"<< /S /P /Pg {page} 0 R /K [0 << /Type /OBJR /Obj {annotation} 0 R >>] >>");
        pdf.Tag($"{paragraph} 0 R");

        var reference = Assert.Single(PdfProofDocument.Load(pdf.Build()).Structure.Root.Children[0].Content.OfType<StructureObjectReference>());

        Assert.Equal(("Text", (int?)1), (reference.Type, reference.Page));
        Assert.Null(reference.Link);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: FAIL. No `StructureObjectReference` is found, because OBJRs are skipped.

- [ ] **Step 3: Implement**

In `src/PdfProof/Structure/StructureTreeReader.cs`, replace `return null; // Task 4` with:
```csharp
                return ObjectReference(dictionary, page);
```
and add these methods:
```csharp

    private RawObjectReference ObjectReference(DictionaryToken objr, int? elementPage)
    {
        if (Resolve(Get(objr, "Obj")) is not DictionaryToken annotation)
        {
            return new RawObjectReference(PageOf(objr) ?? elementPage, null, null);
        }

        double[]? rectangle = null;
        if (Resolve(Get(annotation, "Rect")) is ArrayToken { Length: 4 } rect)
        {
            rectangle = rect.Data.Select(token => Resolve(token) is NumericToken number ? number.Double : double.NaN).ToArray();
        }

        return new RawObjectReference(PageOf(objr) ?? PageOf(annotation) ?? elementPage, Name(annotation, "Subtype"), rectangle);
    }
```
`PageOf(annotation)` reads the annotation's own `/P` (page) entry when it has one; `PageOf` looks at `/Pg`, so also try `/P`:
```csharp

    private int? AnnotationPage(DictionaryToken annotation) =>
        Get(annotation, "P") is IndirectReferenceToken reference && _pageNumbers.TryGetValue(reference.Data, out var number) ? number : null;
```
and use `PageOf(objr) ?? AnnotationPage(annotation) ?? elementPage` in `ObjectReference` instead of `PageOf(annotation)`.

In `src/PdfProof/Structure/StructureBuilder.cs`, add a case to `Content`:
```csharp
        RawObjectReference reference => new StructureObjectReference(reference.Subtype ?? "Unknown", reference.Page, Link(reference)),
```
and the method:
```csharp

    // The layout link on the same page with the same rectangle (normalized and rounded like the layout), so its text
    // and target resolve exactly as in LayoutPage.Links.
    private LayoutLink? Link(RawObjectReference reference)
    {
        if (reference.Subtype != "Link" || reference.Page is not { } page || page < 1 || page > _layout.Model.Pages.Count
            || reference.Rectangle is not { } r || r.Any(double.IsNaN))
        {
            return null;
        }

        var box = Geometry.Round(new LayoutBox(Math.Min(r[0], r[2]), Math.Min(r[1], r[3]), Math.Max(r[0], r[2]), Math.Max(r[1], r[3])), _options.Decimals);
        return _layout.Model.Pages[page - 1].Links.FirstOrDefault(link =>
            Math.Abs(link.Box.Left - box.Left) < 0.5 && Math.Abs(link.Box.Bottom - box.Bottom) < 0.5 &&
            Math.Abs(link.Box.Right - box.Right) < 0.5 && Math.Abs(link.Box.Top - box.Top) < 0.5);
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/PdfProof tests/PdfProof.Tests
git commit -m "Keep mixed content order and resolve annotation references to layout links

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Attributes, actual text, language and artifacts

**Files:**
- Modify: `src/PdfProof/Structure/StructureTreeReader.cs`, `src/PdfProof/Structure/StructureBuilder.cs`, `tests/PdfProof.Tests/StructureTests.cs`

**Interfaces:**
- Consumes: the Task 3–4 reader and builder.
- Produces:
  - The reader fills `RawElement` with `/Alt`, `/ActualText` and `/Lang` (text or hex strings), plus `/Scope`, `/RowSpan` and `/ColSpan` from the attribute objects in `/A` (a dictionary, or an array of dictionaries and revision numbers).
  - The builder:
    - normalizes `Alt` and `ActualText` with `WordFactory.Normalize` and collapses whitespace;
    - skips elements whose mapped role is `Artifact`;
    - drops `Alt` when it equals the element's text (some producers repeat the text).

- [ ] **Step 1: Write the failing tests**

Add to `StructureTests`:
```csharp

    [Fact]
    public void Alt_actual_text_and_language_are_read()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(
            RawPdf.Marked("Figure", 0, "q 40 0 0 40 50 600 cm /Im1 Do Q\n") +
            RawPdf.Marked("Span", 1, RawPdf.Text(50, 500, "Pg.")));
        var figure = pdf.Add($"<< /S /Figure /Pg {page} 0 R /K 0 /Alt (Company logo) >>");
        var span = pdf.Add($"<< /S /Span /Pg {page} 0 R /K 1 /ActualText (Page) /Lang (en-US) >>");
        pdf.Tag($"{figure} 0 R {span} 0 R");

        var children = PdfProofDocument.Load(pdf.Build()).Structure.Root.Children;

        Assert.Equal(("Figure", "Company logo", ""), (children[0].Role, children[0].Alt, children[0].Text));
        Assert.Equal(("Page", "Page", "en-US"), (children[1].ActualText, children[1].Text, children[1].Language));
    }

    [Fact]
    public void Table_attributes_scope_and_spans_are_read_from_the_attribute_objects()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("TH", 0, RawPdf.Text(50, 750, "Amount")) + RawPdf.Marked("TD", 1, RawPdf.Text(50, 730, "9 900,00 SEK")));
        var header = pdf.Add($"<< /S /TH /Pg {page} 0 R /K 0 /A << /O /Table /Scope /Column /ColSpan 2 >> >>");
        var cell = pdf.Add($"<< /S /TD /Pg {page} 0 R /K 1 /A [<< /O /Layout /Placement /Block >> 0 << /O /Table /RowSpan 3 >> 0] >>");
        var row = pdf.Add($"<< /S /TR /K [{header} 0 R {cell} 0 R] >>");
        pdf.Tag($"{pdf.Add($"<< /S /Table /K [{row} 0 R] >>")} 0 R");

        var cells = PdfProofDocument.Load(pdf.Build()).Structure.Root.Descendants().Where(n => n.Role is "TH" or "TD").ToList();

        Assert.Equal(("Column", 1, 2), (cells[0].Scope, cells[0].RowSpan, cells[0].ColumnSpan));
        Assert.Equal(((string?)null, 3, 1), (cells[1].Scope, cells[1].RowSpan, cells[1].ColumnSpan));
    }

    [Fact]
    public void Artifacts_are_left_out_of_the_structure()
    {
        // Review Focus 4: running headers and page numbers are artifacts, both as elements and as marked content.
        var pdf = new RawPdf();
        var page = pdf.AddPage(
            RawPdf.Marked("P", 0, RawPdf.Text(50, 750, "Body text")) +
            RawPdf.Artifact(RawPdf.Text(50, 30, "Page 1 of 1")) +
            RawPdf.Marked("Footer", 1, RawPdf.Text(400, 30, "Confidential")));
        var paragraph = pdf.Add($"<< /S /P /Pg {page} 0 R /K 0 >>");
        var footer = pdf.Add($"<< /S /Footer /Pg {page} 0 R /K 1 >>");
        pdf.Tag($"{paragraph} 0 R {footer} 0 R", "/RoleMap << /Footer /Artifact >>");

        var structure = PdfProofDocument.Load(pdf.Build()).Structure;

        var node = Assert.Single(structure.Root.Children);
        Assert.Equal("Body text", node.Text);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: FAIL. `Alt`, `ActualText`, `Language` and `Scope` are null, and the `Footer` element mapped to `Artifact` is still present.

- [ ] **Step 3: Implement**

In `src/PdfProof/Structure/StructureTreeReader.cs`, replace `ReadElement` with:
```csharp
    private RawElement ReadElement(DictionaryToken element, string role, int? inheritedPage, int depth)
    {
        var page = PageOf(element) ?? inheritedPage;
        var kids = Kids(element).Select(kid => ReadKid(kid, page, depth + 1)).OfType<RawKid>().ToList();
        var (scope, rowSpan, columnSpan) = TableAttributes(element);
        return new RawElement(
            role,
            page,
            Text(element, "Alt"),
            Text(element, "ActualText"),
            Text(element, "Lang"),
            scope,
            rowSpan,
            columnSpan,
            kids);
    }

    // /A holds one attribute object or an array of them (optionally followed by revision numbers). Table attributes
    // are those with /O /Table.
    private (string? Scope, int RowSpan, int ColumnSpan) TableAttributes(DictionaryToken element)
    {
        string? scope = null;
        var rowSpan = 1;
        var columnSpan = 1;
        var attributes = Resolve(Get(element, "A")) switch
        {
            DictionaryToken single => [single],
            ArrayToken array => array.Data.Select(Resolve).OfType<DictionaryToken>().ToList(),
            _ => new List<DictionaryToken>(),
        };
        foreach (var attribute in attributes.Where(a => Name(a, "O") == "Table"))
        {
            scope = Name(attribute, "Scope") ?? scope;
            rowSpan = Resolve(Get(attribute, "RowSpan")) is NumericToken rows && rows.Int > 0 ? rows.Int : rowSpan;
            columnSpan = Resolve(Get(attribute, "ColSpan")) is NumericToken columns && columns.Int > 0 ? columns.Int : columnSpan;
        }

        return (scope, rowSpan, columnSpan);
    }

    private string? Text(DictionaryToken dictionary, string key) => Resolve(Get(dictionary, key)) switch
    {
        StringToken text => text.Data,
        HexToken hex => hex.Data,
        _ => null,
    };
```

In `src/PdfProof/Structure/StructureBuilder.cs`, replace `Element` with:
```csharp
    private StructureNode? Element(RawElement raw)
    {
        var role = MapRole(raw.Role);
        if (role == "Artifact")
        {
            return null;
        }

        var content = new List<StructureContent>();
        foreach (var kid in raw.Kids)
        {
            if (Content(kid) is { } item)
            {
                content.Add(item);
            }
        }

        var actualText = Normalize(raw.ActualText);
        var alt = Normalize(raw.Alt);
        var node = new StructureNode(role, raw.Role == role ? null : raw.Role, raw.Page, alt, actualText, raw.Language, raw.Scope, raw.RowSpan, raw.ColumnSpan, content);
        return alt is not null && alt == node.Text
            ? new StructureNode(role, node.CustomRole, raw.Page, null, actualText, raw.Language, raw.Scope, raw.RowSpan, raw.ColumnSpan, content) // some producers repeat the text as /Alt
            : node;
    }

    private string? Normalize(string? text) =>
        text is null ? null : Whitespace.Replace(WordFactory.Normalize(text, _options), " ").Trim();
```
Add `using System.Text.RegularExpressions;` and the field `private static readonly Regex Whitespace = new(@"\s+", RegexOptions.CultureInvariant);` to `StructureBuilder`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/PdfProof tests/PdfProof.Tests
git commit -m "Read alt and actual text, language and table attributes; leave out artifacts

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Damaged trees, MarkInfo, and repository guidance

**Files:**
- Modify: `src/PdfProof/PdfProofDocument.cs`, `src/PdfProof/PdfDiagnosticCodes.cs`, `src/PdfProof/DiagnosticsCollector.cs`, `src/PdfProof/PublicAPI.Unshipped.txt`, `tests/PdfProof.Tests/StructureTests.cs`, `AGENTS.md`

**Interfaces:**
- Consumes: the Task 3–5 reader and builder.
- Produces:
  - `PdfDiagnosticCodes.StructureUnreadable` (warning): the tree couldn't be read and is left out.
  - `PdfDiagnosticCodes.MarkInfoMismatch` (debug): `/MarkInfo /Marked` disagrees with whether there is a structure tree.

- [ ] **Step 1: Write the failing tests**

Add to `StructureTests` (add `using Microsoft.Extensions.Logging;` at the top of the file):
```csharp

    [Fact]
    public void A_structure_tree_with_a_cycle_loads_in_bounded_time()
    {
        // Review Focus 1: an element whose /K points back at its parent.
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("P", 0, RawPdf.Text(50, 750, "Hello")));
        var parent = pdf.Reserve();
        var child = pdf.Add($"<< /S /P /Pg {page} 0 R /K [0 {parent} 0 R] >>");
        pdf.Set(parent, $"<< /S /Div /K [{child} 0 R] >>");
        pdf.Tag($"{parent} 0 R");

        var structure = PdfProofDocument.Load(pdf.Build()).Structure;

        Assert.Equal("Hello", Assert.Single(structure.Root.Descendants(), n => n.Role == "P").Text);
    }

    [Fact]
    public void A_very_deep_structure_tree_loads_without_overflowing_the_stack()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("P", 0, RawPdf.Text(50, 750, "Deep")));
        var current = pdf.Add($"<< /S /P /Pg {page} 0 R /K 0 >>");
        for (var i = 0; i < 2000; i++)
        {
            current = pdf.Add($"<< /S /Div /K [{current} 0 R] >>");
        }

        pdf.Tag($"{current} 0 R");

        var structure = PdfProofDocument.Load(pdf.Build()).Structure;

        Assert.True(structure.IsTagged); // elements below the depth limit (256) are left out
    }

    [Fact]
    public void A_structure_tree_referencing_a_missing_object_loads_with_StructureUnreadable()
    {
        // Review Focus 2: PdfPig throws for a reference to an object that doesn't exist.
        var logger = new ListLogger();
        var pdf = new RawPdf();
        pdf.AddPage(RawPdf.Marked("P", 0, RawPdf.Text(50, 750, "Hello")));
        pdf.Tag("999 0 R");

        var doc = PdfProofDocument.Load(pdf.Build(), PdfProofOptions.Default with { Logger = logger });

        Assert.Equal("Hello", doc.Layout.Pages[0].Text);
        Assert.False(doc.Structure.IsTagged);
        var entry = Assert.Single(doc.Diagnostics.Entries, e => e.Code == PdfDiagnosticCodes.StructureUnreadable);
        Assert.StartsWith("The structure tree could not be read and is left out: ", entry.Message);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.StartsWith("StructureUnreadable", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true, false, "/MarkInfo sets /Marked true, but the document has no structure tree with elements.")]
    [InlineData(false, true, "The document has a structure tree, but /MarkInfo doesn't set /Marked true.")]
    public void A_MarkInfo_flag_that_disagrees_with_the_structure_tree_is_noted(bool marked, bool tree, string message)
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("P", 0, RawPdf.Text(50, 750, "Hello")));
        if (tree)
        {
            pdf.Tag($"{pdf.Add($"<< /S /P /Pg {page} 0 R /K 0 >>")} 0 R", marked: marked);
        }
        else if (marked)
        {
            pdf.CatalogEntries += " /MarkInfo << /Marked true >>";
        }

        var entry = Assert.Single(PdfProofDocument.Load(pdf.Build()).Diagnostics.Entries, e => e.Code == PdfDiagnosticCodes.MarkInfoMismatch);

        Assert.Equal(message, entry.Message);
    }

    [Fact]
    public void A_consistently_tagged_document_has_no_MarkInfo_note()
    {
        var pdf = new RawPdf();
        var page = pdf.AddPage(RawPdf.Marked("P", 0, RawPdf.Text(50, 750, "Hello")));
        pdf.Tag($"{pdf.Add($"<< /S /P /Pg {page} 0 R /K 0 >>")} 0 R");

        Assert.DoesNotContain(PdfProofDocument.Load(pdf.Build()).Diagnostics.Entries, e => e.Code == PdfDiagnosticCodes.MarkInfoMismatch);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: build FAILS with `CS0117`: no `StructureUnreadable` or `MarkInfoMismatch`. Once the constants exist, the missing-object test fails with `PdfProofException`, the cycle and depth tests pass (Task 3's guards), and the MarkInfo tests fail.

- [ ] **Step 3: Implement**

In `src/PdfProof/PdfDiagnosticCodes.cs`, add after `Structure`:
```csharp

    /// <summary>The structure tree could not be read and is left out; pages and layout are unaffected. Logged as a warning.</summary>
    public const string StructureUnreadable = "StructureUnreadable";

    /// <summary><c>/MarkInfo /Marked</c> disagrees with whether the document has a structure tree.</summary>
    public const string MarkInfoMismatch = "MarkInfoMismatch";
```

In `src/PdfProof/DiagnosticsCollector.cs`, add `StructureUnreadable` to `IsWarning`:
```csharp
        code is PdfDiagnosticCodes.NoTextLayer or PdfDiagnosticCodes.BookmarksUnreadable or PdfDiagnosticCodes.StructureUnreadable;
```

In `src/PdfProof/PdfProofDocument.cs`:
- Replace `var raw = PdfPigCall.Read(() => StructureTreeReader.Read(document));` with `var raw = ReadStructure(document, diagnostics);`.
- After the `if (structure.IsTagged) { … }` block, add:
```csharp
        if (raw.HasStructTreeRoot || raw.Marked)
        {
            if (raw.Marked && !structure.IsTagged)
            {
                diagnostics.Add(PdfDiagnosticCodes.MarkInfoMismatch, null, "/MarkInfo sets /Marked true, but the document has no structure tree with elements.");
            }
            else if (!raw.Marked && structure.IsTagged)
            {
                diagnostics.Add(PdfDiagnosticCodes.MarkInfoMismatch, null, "The document has a structure tree, but /MarkInfo doesn't set /Marked true.");
            }
        }
```
- Add the method:
```csharp

    // The structure tree is secondary to page content: a tree the parser rejects is reported and left out, and the
    // document still loads.
    private static RawStructure ReadStructure(PdfDocument document, DiagnosticsCollector diagnostics)
    {
        try
        {
            return PdfPigCall.Read(() => StructureTreeReader.Read(document));
        }
        catch (PdfProofException ex)
        {
            diagnostics.Add(
                PdfDiagnosticCodes.StructureUnreadable,
                null,
                "The structure tree could not be read and is left out: " + (ex.InnerException ?? ex).Message);
            return RawStructure.None;
        }
    }
```

Record the public API with the command in `AGENTS.md`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: all tests pass.

- [ ] **Step 5: Update `AGENTS.md`**

In the `## Layout` table, replace the `src/PdfProof` row with:
```markdown
| `src/PdfProof` | Core: `PdfProofDocument`, options and profiles, diagnostics; `Layout/` (layout model and strategies); `Structure/` (Tagged PDF structure tree) (netstandard2.0; net8.0; net10.0) |
```
In the **Test PDFs** rule, add after "outlines": `, marked content and structure trees (`RawPdf.Marked`, `RawPdf.Tag`)`.

- [ ] **Step 6: Run the whole build**

Run: `./build.ps1`
Expected: BuildTests, Build, Test (net48 + net10.0) and Pack all succeed.

- [ ] **Step 7: Commit**

```bash
git add src/PdfProof tests/PdfProof.Tests AGENTS.md
git commit -m "Report unreadable structure trees and MarkInfo mismatches; update AGENTS.md

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Self-review notes

- **Spec coverage:**

  | Spec item | Task |
  |---|---|
  | §5.1 `StructureModel`, never null; empty root and `IsTagged` false when untagged (D6) | 1 |
  | §5.1 `IsTagged` from the tree, not `/MarkInfo`; mismatch noted in Diagnostics | 1, 6 |
  | Roles after RoleMap, marked text, pages (FR-MOD-10) | 3 |
  | Link targets through OBJR (FR-MOD-10) | 4 |
  | `/Alt`, `/ActualText` (FR-MOD-10) | 5 |
  | `/Lang`, table `/Scope`, row and column spans (FR-MOD-11) | 5 |
  | Several links per element (FR-MOD-11) | 4: every OBJR becomes its own reference |
  | Nested lists (FR-MOD-11) | Read as any other elements (`L` → `LI` → `L`); no special handling needed |
  | §5.4 damaged input: the structure is secondary, so it gets a diagnostic, not a failed load | 6 |
  | §6 producer quirks (links on `P`) | Plan 5 by design; Task 4 tests that the model shows them faithfully |

- **Placeholders:** none. The one interim value (`StructureModel.Untagged` in Task 1, before Task 3 reads the tree) is replaced in Task 3 and stated there.
