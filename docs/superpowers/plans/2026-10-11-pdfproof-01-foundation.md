# PdfProof Plan 1 — Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Set up a repository that builds, tests and packs PdfProof the same way locally and in CI, and that contains its first real behavior: `PdfProofDocument.Load` reading document metadata, plus proof that `PdfAssertionException` shows up in xUnit v3 as an assertion failure.

**Architecture:**
- **Libraries:** two libraries (`PdfProof`, `PdfProof.Testing`) targeting `netstandard2.0;net8.0;net10.0`. Test projects use xUnit v3 on Microsoft.Testing.Platform and run on net48 (Windows only) and net10.0.
- **Build:** a project-based Cake.Sdk build in `build/`, started by `build.ps1` / `build.sh`, runs Build → Test → Pack. Pack checks every package for its icon, README and symbols.
- **CI:** GitHub Actions calls the same bootstraps on Windows and Ubuntu.

**Tech Stack:** .NET SDK 10.0.401, PdfPig 0.1.16, Polyfill, Microsoft.CodeAnalysis.PublicApiAnalyzers, xUnit v3 3.2.2 (MTP), Cake.Sdk 6.3.0, MinVer, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-10-11-pdfproof-v1-design.md` (§3 D3, D4, D12, D17, D18; §4; §5.1; §5.4; §8.7; §11; §12)

## Where this plan fits

The spec's first milestone (`0.1.0-alpha`, spec §11.5) is split into seven plans. Each one leaves the repository green and shippable:

| Plan | Delivers |
|---|---|
| **1 Foundation (this plan)** | Repo settings, `PdfProofDocument.Load` + metadata, `PdfAssertionException` + xUnit proof (OQ-1), Cake build, packaging, CI, `AGENTS.md` |
| 2 Layout model | Port of the spike's layout: letters → words → lines → blocks, `Rows`/`ReadingOrder`, rotation, clipping, normalization, links, images, fonts, options and profiles. **Also decide which load errors become `PdfProofException`:** today `PdfProofDocument.Open` wraps every exception except `OutOfMemoryException`, so a bug in PdfProof itself would be reported as "the input could not be read as a PDF". Once page-by-page loading runs inside that `try`, wrap only parser errors (e.g. PdfPig's `PdfDocumentFormatException`, and whatever else PdfPig is shown to throw on bad input) and let programming errors through (review finding from Plan 1). |
| 3 Structure model | `StructureModel` (empty root when untagged), structure tree reader, RoleMap, MCIDs, OBJR links |
| 4 Fixture generator and corpus | `tools/PdfProof.FixtureGenerator`, the `invoice` document for each producer, `GenerateFixtures`/`FetchCorpus` Cake targets, the CI fixtures job, the corpus test matrix |
| 5 Semantic passes | Candidate blocks, passes 0–7, `SemanticModel`, decision log |
| 6 Export | Export tree, scrubbing, Markdown and JSON writers, JSON Schema v1, determinism tests |
| 7 CLI, skill and `0.1.0-alpha` | `pdfproof export`/`explain`/`skill install\|status\|uninstall` (Spectre.Console.Cli), the agent skill's exploring and snapshot parts with `CheckSkill`, `release.yml`, first pre-release |

## Global Constraints

- Library target frameworks: `netstandard2.0;net8.0;net10.0`. Test target frameworks: `net48;net10.0` on Windows, `net10.0` elsewhere.
- .NET SDK `10.0.401` (`rollForward: latestFeature`). The test runner is `Microsoft.Testing.Platform` (global.json).
- Pinned package versions in `Directory.Packages.props` (central package management). No floating versions.
- `TreatWarningsAsErrors`, `Nullable` enabled, `LangVersion` latest. XML docs are required on every public API in `src/` (`GenerateDocumentationFile`).
- Every public API change is recorded in `PublicAPI.Unshipped.txt` (PublicApiAnalyzers).
- Never write the name of the author's employer, or real company or customer names, anywhere in the repository (spec D19). Sample data stays neutral.
- Line endings are LF in the repository (`.gitattributes`). `build.sh` and `build.ps1` are executable and LF.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- From Task 6 on, `AGENTS.md` is updated in the same change whenever a task adds commands, projects or rules (spec §16.4).

## Review Focus

1. **Culture:** loading under a culture with a decimal comma (sv-SE) must still report the PDF version as `1.7`, never `1,7`. Test in Task 1.
2. **Input that isn't a PDF**, including an empty array, must throw `PdfProofException` with a readable message, not a raw PdfPig exception. Test in Task 1.
3. **Streams:** a non-seekable stream (network, pipe) must load. Loading reads from the stream's current position, and the caller's stream must stay open. Tests in Task 1.
4. **A PDF without an info dictionary** must load, with all info entries `null`. Test in Task 1.
5. **Running the build from another working directory** (`../PdfProof/build.sh` from a sibling folder) must behave exactly as from the root. Check in Task 3.

---

### Task 1: Repository settings and `PdfProofDocument.Load` with metadata

**Files:**
- Create: `.gitattributes`, `.editorconfig`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `PdfProof.slnx`
- Create: `src/PdfProof/PdfProof.csproj`, `src/PdfProof/PdfProofDocument.cs`, `src/PdfProof/PdfMetadata.cs`, `src/PdfProof/PdfProofException.cs`, `src/PdfProof/PublicAPI.Shipped.txt`, `src/PdfProof/PublicAPI.Unshipped.txt`
- Create: `tests/Directory.Build.props`, `tests/PdfProof.Tests/PdfProof.Tests.csproj`, `tests/PdfProof.Tests/TestPdfs.cs`, `tests/PdfProof.Tests/PdfProofDocumentLoadTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `PdfProof.PdfProofDocument` with `static Load(byte[] pdf)`, `static Load(Stream pdf)`, `static Load(string path)`, and `PdfMetadata Metadata { get; }`.
  - `PdfProof.PdfMetadata` with `string PdfVersion`, `int PageCount`, `string? Title`, `Author`, `Subject`, `Keywords`, `Creator`, `Producer`, and `DateTimeOffset? CreationDate`, `ModifiedDate`.
  - `PdfProof.PdfProofException : Exception` with constructors `(string message)` and `(string message, Exception? innerException)`.
  - Test helper `PdfProof.Tests.TestPdfs.Create(int pages = 1, string? title = null, string? author = null, bool includeInfo = true) -> byte[]`.

- [ ] **Step 1: Write the repository settings**

`.gitattributes`:
```gitattributes
* text=auto eol=lf
*.cmd text eol=crlf
*.ps1 text eol=crlf
# Run directly on Linux and macOS through its shebang, which a CR would break.
build.ps1 text eol=lf
build.sh text eol=lf
*.png binary
*.pdf binary
*.ttf binary
*.otf binary
*.nupkg binary
*.snupkg binary
```

`.editorconfig`:
```editorconfig
root = true

[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
trim_trailing_whitespace = true
indent_style = space
indent_size = 4

[*.{json,yml,yaml,xml,csproj,props,targets,slnx}]
indent_size = 2

[*.md]
trim_trailing_whitespace = false

[*.cs]
csharp_style_namespace_declarations = file_scoped:suggestion
csharp_style_var_for_built_in_types = true:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
dotnet_style_qualification_for_field = false:suggestion
dotnet_naming_rule.private_fields_underscore.symbols = private_fields
dotnet_naming_rule.private_fields_underscore.style = underscore_prefix
dotnet_naming_rule.private_fields_underscore.severity = suggestion
dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private
dotnet_naming_style.underscore_prefix.capitalization = camel_case
dotnet_naming_style.underscore_prefix.required_prefix = _
```

`global.json`:
```json
{
  "sdk": {
    "version": "10.0.401",
    "rollForward": "latestFeature"
  },
  "msbuild-sdks": {
    "Cake.Sdk": "6.3.0"
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

`Directory.Build.props`:
```xml
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
    <!-- Polyfill (source-only) brings modern APIs such as ArgumentNullException.ThrowIfNull to netstandard2.0. -->
    <PolyUseEmbeddedAttribute>true</PolyUseEmbeddedAttribute>
    <PolyArgumentExceptions>true</PolyArgumentExceptions>
    <Authors>Magnus Lindhe</Authors>
    <Copyright>Copyright (c) 2026 Magnus Lindhe</Copyright>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/mgnslndh/PdfProof</PackageProjectUrl>
    <RepositoryUrl>https://github.com/mgnslndh/PdfProof</RepositoryUrl>
    <PackageTags>pdf;testing;assertions;snapshot;markdown;json;pdfpig</PackageTags>
  </PropertyGroup>
</Project>
```

`Directory.Packages.props`:
```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- A PdfPig upgrade can change extraction and every consumer's snapshots: upgrade deliberately (spec §15). -->
    <PackageVersion Include="PdfPig" Version="0.1.16" />
    <PackageVersion Include="Polyfill" Version="11.4.3" />
    <PackageVersion Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" Version="5.6.0" />
    <!-- xunit.v3 3.2.x: what Verify.XunitV3 32.0.1 (the last MIT Verify) is built against; used from Plan 2 on. -->
    <PackageVersion Include="xunit.v3" Version="3.2.2" />
  </ItemGroup>
</Project>
```

`PdfProof.slnx`:
```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/PdfProof/PdfProof.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/PdfProof.Tests/PdfProof.Tests.csproj" />
  </Folder>
</Solution>
```

`tests/Directory.Build.props`:
```xml
<Project>
  <!-- The repository's settings, then what every test project shares. -->
  <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />
  <PropertyGroup>
    <!-- net48 proves the netstandard2.0 build works on .NET Framework; it only runs on Windows. -->
    <TargetFrameworks>net10.0</TargetFrameworks>
    <TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('Windows'))">net48;net10.0</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
    <TestingPlatformShowTestsFailure>true</TestingPlatformShowTestsFailure>
  </PropertyGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create the library and test projects (no code yet)**

`src/PdfProof/PdfProof.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>netstandard2.0;net8.0;net10.0</TargetFrameworks>
    <Description>Reads a PDF into layout, structure and semantic models, and exports deterministic Markdown and JSON for tests and snapshots.</Description>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <IsAotCompatible Condition="$([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'net8.0'))">true</IsAotCompatible>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="PdfPig" />
    <PackageReference Include="Polyfill" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <AdditionalFiles Include="PublicAPI.Shipped.txt" />
    <AdditionalFiles Include="PublicAPI.Unshipped.txt" />
  </ItemGroup>

</Project>
```

`src/PdfProof/PublicAPI.Shipped.txt` and `src/PdfProof/PublicAPI.Unshipped.txt`, each containing exactly one line:
```
#nullable enable
```

`tests/PdfProof.Tests/PdfProof.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="Polyfill" PrivateAssets="all" />
    <ProjectReference Include="../../src/PdfProof/PdfProof.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Write the test helper that builds PDFs in memory**

The tests build their PDFs with PdfPig's writer, so this plan needs no fixture files.

`tests/PdfProof.Tests/TestPdfs.cs`:
```csharp
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace PdfProof.Tests;

// Small PDFs written by PdfPig, so tests don't depend on fixture files.
internal static class TestPdfs
{
    public static byte[] Create(int pages = 1, string? title = null, string? author = null, bool includeInfo = true)
    {
        var builder = new PdfDocumentBuilder { IncludeDocumentInformation = includeInfo };
        builder.DocumentInformation.Title = title;
        builder.DocumentInformation.Author = author;

        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        for (var number = 1; number <= pages; number++)
        {
            var page = builder.AddPage(PageSize.A4);
            page.AddText($"Page {number}", 12, new PdfPoint(50, 750), font);
        }

        return builder.Build();
    }
}
```

- [ ] **Step 4: Write the failing tests**

`tests/PdfProof.Tests/PdfProofDocumentLoadTests.cs`:
```csharp
using System.Globalization;

namespace PdfProof.Tests;

public sealed class PdfProofDocumentLoadTests
{
    [Fact]
    public void Load_reads_the_page_count_and_the_info_dictionary()
    {
        var doc = PdfProofDocument.Load(TestPdfs.Create(pages: 2, title: "Invoice", author: "Accounts"));

        Assert.Equal(2, doc.Metadata.PageCount);
        Assert.Equal("Invoice", doc.Metadata.Title);
        Assert.Equal("Accounts", doc.Metadata.Author);
        Assert.Equal("PdfPig", doc.Metadata.Producer);
    }

    [Fact]
    public void Pdf_version_uses_a_dot_in_every_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("sv-SE");
        try
        {
            var doc = PdfProofDocument.Load(TestPdfs.Create());

            Assert.Matches(@"^\d\.\d$", doc.Metadata.PdfVersion);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void A_pdf_without_an_info_dictionary_loads_with_null_entries()
    {
        var doc = PdfProofDocument.Load(TestPdfs.Create(includeInfo: false));

        Assert.Equal(1, doc.Metadata.PageCount);
        Assert.Null(doc.Metadata.Title);
        Assert.Null(doc.Metadata.Author);
        Assert.Null(doc.Metadata.CreationDate);
    }

    [Fact]
    public void Load_reads_a_non_seekable_stream()
    {
        using var stream = new NonSeekableStream(TestPdfs.Create(pages: 3));

        var doc = PdfProofDocument.Load(stream);

        Assert.Equal(3, doc.Metadata.PageCount);
    }

    [Fact]
    public void Load_reads_from_the_current_position_and_leaves_the_stream_open()
    {
        var pdf = TestPdfs.Create(title: "Offset");
        using var stream = new MemoryStream();
        stream.Write([0x42, 0x42, 0x42], 0, 3);
        stream.Write(pdf, 0, pdf.Length);
        stream.Position = 3;

        var doc = PdfProofDocument.Load(stream);

        Assert.Equal("Offset", doc.Metadata.Title);
        Assert.True(stream.CanRead, "Load must not dispose the caller's stream.");
    }

    [Fact]
    public void Load_reads_a_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pdfproof-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, TestPdfs.Create(title: "From disk"));
        try
        {
            Assert.Equal("From disk", PdfProofDocument.Load(path).Metadata.Title);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Bytes_that_are_not_a_pdf_throw_PdfProofException()
    {
        var notAPdf = "<html><body>Not a PDF</body></html>"u8.ToArray();

        var ex = Assert.Throws<PdfProofException>(() => PdfProofDocument.Load(notAPdf));

        Assert.StartsWith("The input could not be read as a PDF", ex.Message);
        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public void Empty_input_throws_PdfProofException()
    {
        Assert.Throws<PdfProofException>(() => PdfProofDocument.Load(Array.Empty<byte>()));
    }

    [Fact]
    public void A_missing_file_throws_FileNotFoundException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.pdf");

        var ex = Assert.Throws<FileNotFoundException>(() => PdfProofDocument.Load(path));

        Assert.Equal(path, ex.FileName);
    }

    [Fact]
    public void Null_input_throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PdfProofDocument.Load((byte[])null!));
        Assert.Throws<ArgumentNullException>(() => PdfProofDocument.Load((Stream)null!));
        Assert.Throws<ArgumentNullException>(() => PdfProofDocument.Load((string)null!));
    }

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }
}
```

- [ ] **Step 5: Run the tests to verify they fail**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: build FAILS with `CS0103`/`CS0246`: `PdfProofDocument` and `PdfProofException` do not exist.

- [ ] **Step 6: Implement `PdfProofException`, `PdfMetadata` and `PdfProofDocument`**

`src/PdfProof/PdfProofException.cs`:
```csharp
namespace PdfProof;

/// <summary>
/// Thrown when a PDF can't be loaded: the input isn't a PDF, it is corrupt, or it is encrypted and no valid password
/// was given. The inner exception, when present, is the underlying parser's error.
/// </summary>
public sealed class PdfProofException : Exception
{
    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong, in terms of the PDF.</param>
    public PdfProofException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the underlying error.</summary>
    /// <param name="message">What went wrong, in terms of the PDF.</param>
    /// <param name="innerException">The underlying error, usually from the PDF parser.</param>
    public PdfProofException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
```

`src/PdfProof/PdfMetadata.cs`:
```csharp
namespace PdfProof;

/// <summary>
/// Document-level metadata: the PDF version, the page count and the standard entries of the info dictionary.
/// Entries the document doesn't have are <see langword="null"/>.
/// </summary>
public sealed class PdfMetadata
{
    internal PdfMetadata(
        string pdfVersion,
        int pageCount,
        string? title,
        string? author,
        string? subject,
        string? keywords,
        string? creator,
        string? producer,
        DateTimeOffset? creationDate,
        DateTimeOffset? modifiedDate)
    {
        PdfVersion = pdfVersion;
        PageCount = pageCount;
        Title = title;
        Author = author;
        Subject = subject;
        Keywords = keywords;
        Creator = creator;
        Producer = producer;
        CreationDate = creationDate;
        ModifiedDate = modifiedDate;
    }

    /// <summary>The PDF version from the file header, always formatted with a dot, e.g. <c>1.7</c>.</summary>
    public string PdfVersion { get; }

    /// <summary>The number of pages.</summary>
    public int PageCount { get; }

    /// <summary>The <c>/Title</c> entry.</summary>
    public string? Title { get; }

    /// <summary>The <c>/Author</c> entry.</summary>
    public string? Author { get; }

    /// <summary>The <c>/Subject</c> entry.</summary>
    public string? Subject { get; }

    /// <summary>The <c>/Keywords</c> entry.</summary>
    public string? Keywords { get; }

    /// <summary>The <c>/Creator</c> entry: the application that created the original document.</summary>
    public string? Creator { get; }

    /// <summary>The <c>/Producer</c> entry: the application that wrote the PDF.</summary>
    public string? Producer { get; }

    /// <summary>The <c>/CreationDate</c> entry, when present and valid.</summary>
    public DateTimeOffset? CreationDate { get; }

    /// <summary>The <c>/ModDate</c> entry, when present and valid.</summary>
    public DateTimeOffset? ModifiedDate { get; }
}
```

`src/PdfProof/PdfProofDocument.cs`:
```csharp
using System.Globalization;
using UglyToad.PdfPig;

namespace PdfProof;

/// <summary>
/// A loaded PDF. This is the only type you load; the exporters, the CLI and the test assertions all work on it.
/// It is immutable and safe to share between threads, and it holds no open file or parser.
/// </summary>
public sealed class PdfProofDocument
{
    private PdfProofDocument(PdfMetadata metadata)
    {
        Metadata = metadata;
    }

    /// <summary>The PDF version, page count and info dictionary.</summary>
    public PdfMetadata Metadata { get; }

    /// <summary>Loads a PDF from its bytes.</summary>
    /// <param name="pdf">The complete PDF file.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pdf"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfProofException">The bytes can't be read as a PDF.</exception>
    public static PdfProofDocument Load(byte[] pdf)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        return Open(pdf);
    }

    /// <summary>
    /// Loads a PDF from a stream, reading from its current position to the end. The stream may be non-seekable,
    /// and it is left open.
    /// </summary>
    /// <param name="pdf">A stream positioned at the start of the PDF.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pdf"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfProofException">The content can't be read as a PDF.</exception>
    public static PdfProofDocument Load(Stream pdf)
    {
        ArgumentNullException.ThrowIfNull(pdf);

        // Copying makes the behavior the same for every kind of stream: read from here to the end, never seek,
        // never dispose the caller's stream.
        using var buffer = new MemoryStream();
        pdf.CopyTo(buffer);
        return Open(buffer.ToArray());
    }

    /// <summary>Loads a PDF file.</summary>
    /// <param name="path">The path to the PDF file.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="FileNotFoundException">The file doesn't exist.</exception>
    /// <exception cref="PdfProofException">The file can't be read as a PDF.</exception>
    public static PdfProofDocument Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The PDF file was not found.", path);
        }

        return Open(File.ReadAllBytes(path));
    }

    private static PdfProofDocument Open(byte[] pdf)
    {
        try
        {
            using var document = PdfDocument.Open(pdf);
            return new PdfProofDocument(ReadMetadata(document));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new PdfProofException($"The input could not be read as a PDF: {ex.Message}", ex);
        }
    }

    private static PdfMetadata ReadMetadata(PdfDocument document)
    {
        var info = document.Information;
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
            info.GetModifiedDateTimeOffset());
    }
}
```

- [ ] **Step 7: Record the public API**

Run: `dotnet build src/PdfProof/PdfProof.csproj`
Expected: FAILS with `RS0016` (symbol not part of the declared public API) for each new public member.

Replace the contents of `src/PdfProof/PublicAPI.Unshipped.txt` with exactly:
```
#nullable enable
PdfProof.PdfMetadata
PdfProof.PdfMetadata.Author.get -> string?
PdfProof.PdfMetadata.CreationDate.get -> System.DateTimeOffset?
PdfProof.PdfMetadata.Creator.get -> string?
PdfProof.PdfMetadata.Keywords.get -> string?
PdfProof.PdfMetadata.ModifiedDate.get -> System.DateTimeOffset?
PdfProof.PdfMetadata.PageCount.get -> int
PdfProof.PdfMetadata.PdfVersion.get -> string!
PdfProof.PdfMetadata.Producer.get -> string?
PdfProof.PdfMetadata.Subject.get -> string?
PdfProof.PdfMetadata.Title.get -> string?
PdfProof.PdfProofDocument
PdfProof.PdfProofDocument.Metadata.get -> PdfProof.PdfMetadata!
PdfProof.PdfProofException
PdfProof.PdfProofException.PdfProofException(string! message) -> void
PdfProof.PdfProofException.PdfProofException(string! message, System.Exception? innerException) -> void
static PdfProof.PdfProofDocument.Load(byte[]! pdf) -> PdfProof.PdfProofDocument!
static PdfProof.PdfProofDocument.Load(string! path) -> PdfProof.PdfProofDocument!
static PdfProof.PdfProofDocument.Load(System.IO.Stream! pdf) -> PdfProof.PdfProofDocument!
```

If the build still reports `RS0016`/`RS0017` because the analyzer expects a slightly different spelling, let the analyzer write the lines:
`dotnet format analyzers src/PdfProof/PdfProof.csproj --diagnostics RS0016 RS0017 --severity info`.
Then check that the file lists exactly the members above and nothing else.

Run: `dotnet build src/PdfProof/PdfProof.csproj`
Expected: Build succeeded, 0 warnings, for `netstandard2.0`, `net8.0` and `net10.0`.

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: 10 tests pass on each target framework (net48 and net10.0 on Windows; net10.0 elsewhere).

If `Pdf_version_uses_a_dot_in_every_culture` fails because `document.Version` is not numeric in this PdfPig version, look up its type with `dnx dotnet-inspect -y -- member PdfDocument --package PdfPig@0.1.16 -m Version`. Format it with `CultureInfo.InvariantCulture` either way.

- [ ] **Step 9: Normalize line endings and commit**

```bash
git add --renormalize .
git add .gitattributes .editorconfig global.json Directory.Build.props Directory.Packages.props PdfProof.slnx src tests
git commit -m "Add PdfProofDocument.Load with document metadata

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `PdfProof.Testing` with `PdfAssertionException`, and proof that xUnit v3 reports it as an assertion failure (OQ-1)

**Files:**
- Create: `src/PdfProof.Testing/PdfProof.Testing.csproj`, `src/PdfProof.Testing/IAssertionException.cs`, `src/PdfProof.Testing/PdfAssertionException.cs`, `src/PdfProof.Testing/PublicAPI.Shipped.txt`, `src/PdfProof.Testing/PublicAPI.Unshipped.txt`
- Create: `tests/PdfProof.Testing.Tests/PdfProof.Testing.Tests.csproj`, `tests/PdfProof.Testing.Tests/AssertionFailureClassificationTests.cs`
- Modify: `PdfProof.slnx`

**Interfaces:**
- Consumes: the `PdfProof` project (reference only; nothing from it is used yet).
- Produces:
  - `PdfProof.Testing.IAssertionException`, an empty marker interface.
  - `PdfProof.Testing.PdfAssertionException : Exception, IAssertionException` with constructor `(string message)`. Every later `PdfAssert` failure throws this type.

- [ ] **Step 1: Create the projects**

`src/PdfProof.Testing/PdfProof.Testing.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>netstandard2.0;net8.0;net10.0</TargetFrameworks>
    <Description>Assertions on the meaning of a PDF (headings, tables, links, sequences, invariants) that work in any .NET test framework.</Description>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <IsAotCompatible Condition="$([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'net8.0'))">true</IsAotCompatible>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Polyfill" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" PrivateAssets="all" />
    <ProjectReference Include="../PdfProof/PdfProof.csproj" />
  </ItemGroup>

  <ItemGroup>
    <AdditionalFiles Include="PublicAPI.Shipped.txt" />
    <AdditionalFiles Include="PublicAPI.Unshipped.txt" />
  </ItemGroup>

</Project>
```

`src/PdfProof.Testing/PublicAPI.Shipped.txt` and `src/PdfProof.Testing/PublicAPI.Unshipped.txt`, each containing exactly:
```
#nullable enable
```

`tests/PdfProof.Testing.Tests/PdfProof.Testing.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="Polyfill" PrivateAssets="all" />
    <ProjectReference Include="../../src/PdfProof.Testing/PdfProof.Testing.csproj" />
  </ItemGroup>

</Project>
```

Replace `PdfProof.slnx` with:
```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/PdfProof/PdfProof.csproj" />
    <Project Path="src/PdfProof.Testing/PdfProof.Testing.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/PdfProof.Tests/PdfProof.Tests.csproj" />
    <Project Path="tests/PdfProof.Testing.Tests/PdfProof.Testing.Tests.csproj" />
  </Folder>
</Solution>
```

- [ ] **Step 2: Write the failing tests**

xUnit v3 decides whether a failed test is an *assertion failure* or an *error* in `Xunit.Sdk.ExceptionUtility.ExtractMetadata`. The return value's `Cause` is a `Xunit.Sdk.FailureCause` (`Assertion`, `Exception`, `Other`, `Timeout`, `Unknown`). The control test proves the check can tell the two apart.

`tests/PdfProof.Testing.Tests/AssertionFailureClassificationTests.cs`:
```csharp
using Xunit.Sdk;

namespace PdfProof.Testing.Tests;

// Spec §8.7 / OQ-1: PdfProof.Testing must not depend on xunit, yet its failures must show up in xUnit v3 as
// assertion failures, not errors. xUnit v3 recognizes any exception implementing an interface named
// IAssertionException, whatever its namespace.
public sealed class AssertionFailureClassificationTests
{
    [Fact]
    public void Xunit_reports_PdfAssertionException_as_an_assertion_failure()
    {
        var metadata = ExceptionUtility.ExtractMetadata(new PdfAssertionException("Expected a heading \"Terms\"."));

        Assert.Equal(FailureCause.Assertion, metadata.Cause);
    }

    [Fact]
    public void Xunit_reports_an_ordinary_exception_as_an_error()
    {
        var metadata = ExceptionUtility.ExtractMetadata(new InvalidOperationException("Boom."));

        Assert.Equal(FailureCause.Exception, metadata.Cause);
    }

    [Fact]
    public void The_message_is_kept_as_written()
    {
        var ex = new PdfAssertionException("Expected a heading \"Terms\" on page 2.");

        Assert.Equal("Expected a heading \"Terms\" on page 2.", ex.Message);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test --project tests/PdfProof.Testing.Tests/PdfProof.Testing.Tests.csproj`
Expected: build FAILS with `CS0246`: `PdfAssertionException` could not be found.

- [ ] **Step 4: Implement the interface and the exception**

`src/PdfProof.Testing/IAssertionException.cs`:
```csharp
namespace PdfProof.Testing;

/// <summary>
/// Marks exceptions that represent a failed assertion rather than an error. xUnit v3 reports any exception that
/// implements an interface with this name as an assertion failure, so PdfProof.Testing doesn't need to reference
/// xunit. Other frameworks fail the test on any exception.
/// </summary>
public interface IAssertionException
{
}
```

`src/PdfProof.Testing/PdfAssertionException.cs`:
```csharp
namespace PdfProof.Testing;

/// <summary>
/// Thrown when a PDF assertion fails. The message states the expectation, what was found instead, and an excerpt
/// of the document.
/// </summary>
public sealed class PdfAssertionException : Exception, IAssertionException
{
    /// <summary>Creates the exception with a complete failure message.</summary>
    /// <param name="message">The failure message, shown as is by the test runner.</param>
    public PdfAssertionException(string message)
        : base(message)
    {
    }
}
```

Replace the contents of `src/PdfProof.Testing/PublicAPI.Unshipped.txt` with exactly:
```
#nullable enable
PdfProof.Testing.IAssertionException
PdfProof.Testing.PdfAssertionException
PdfProof.Testing.PdfAssertionException.PdfAssertionException(string! message) -> void
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --project tests/PdfProof.Testing.Tests/PdfProof.Testing.Tests.csproj`
Expected: 3 tests pass on each target framework.

**Decision point:** if `Xunit_reports_PdfAssertionException_as_an_assertion_failure` fails and the control test passes, xUnit v3 doesn't match the interface by name. **Stop and report this to the human partner.** Don't work around it. The spec's fallback (§8.7) is a separate `PdfProof.Testing.Xunit` package, which is a design change for them to approve.

- [ ] **Step 6: Commit**

```bash
git add PdfProof.slnx src/PdfProof.Testing tests/PdfProof.Testing.Tests
git commit -m "Add PdfAssertionException, reported by xUnit v3 as an assertion failure

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Cake build with Build and Test targets

**Files:**
- Create: `build.ps1`, `build.sh`
- Create: `build/Build.slnx`, `build/Directory.Build.props`, `build/Build/Build.csproj`, `build/Build/Program.cs`, `build/Build/BuildParameters.cs`, `build/Build/Tasks/BuildTasks.cs`, `build/Build/Tasks/TestTasks.cs`, `build/Build/Tasks/DefaultTasks.cs`
- Modify: `Directory.Packages.props`

**Interfaces:**
- Consumes: `PdfProof.slnx` (Tasks 1–2).
- Produces:
  - Cake targets `Build`, `Test` and `Default`, run by `./build.ps1 [--target=<name>] [--configuration=<c>]`.
  - `BuildParameters(string Configuration, DirectoryPath Artifacts)` with `DirectoryPath Packages` = `artifacts/packages`. Task 4 adds `Pack`.

- [ ] **Step 1: Add the Cake package versions**

In `Directory.Packages.props`, add inside the `<ItemGroup>`:
```xml
    <!-- The build under build/. Cake.Common matches Cake.Sdk in global.json. -->
    <PackageVersion Include="Cake.Common" Version="6.3.0" />
    <PackageVersion Include="Cake.BuildSystems.Module" Version="9.0.0" />
```

- [ ] **Step 2: Create the build project**

`build/Build.slnx`:
```xml
<Solution>
  <Project Path="Build/Build.csproj" />
</Solution>
```

`build/Directory.Build.props`:
```xml
<Project>
  <!-- The repository's settings (warnings as errors, nullable, latest C#), then what only the build projects need. -->
  <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
</Project>
```

`build/Build/Build.csproj`:
```xml
<Project Sdk="Cake.Sdk">

  <PropertyGroup>
    <!-- Every path in the build is relative to the repository root, wherever the bootstrap is run from. -->
    <RunWorkingDirectory>$(MSBuildProjectDirectory)/../..</RunWorkingDirectory>
  </PropertyGroup>

  <ItemGroup>
    <!-- Groups log output per task on GitHub Actions. -->
    <PackageReference Include="Cake.BuildSystems.Module" />
  </ItemGroup>

</Project>
```

`build/Build/BuildParameters.cs`:
```csharp
// The build's inputs, read once in Setup from the command line (--name=value).
public sealed record BuildParameters(string Configuration, DirectoryPath Artifacts)
{
    public DirectoryPath Packages => Artifacts.Combine("packages");

    public static BuildParameters Create(ICakeContext context) => new(
        context.Argument("configuration", "Release"),
        context.MakeAbsolute(context.Directory("artifacts")));
}
```

`build/Build/Program.cs`:
```csharp
// Builds, tests and packs PdfProof exactly as CI does; CI runs this build, so the two can't drift apart.
// Arguments: --target=<task> (Default), --configuration=<c> (Release).
var target = Argument("target", "Default");

Setup(context => BuildParameters.Create(context));

RegisterBuildTasks();
RegisterTestTasks();
RegisterDefaultTask();

RunTarget(target);
```

`build/Build/Tasks/BuildTasks.cs`:
```csharp
public static partial class Program
{
    static void RegisterBuildTasks()
    {
        Task("Build")
            .Does<BuildParameters>(parameters =>
                DotNetBuild("PdfProof.slnx", new DotNetBuildSettings { Configuration = parameters.Configuration }));
    }
}
```

`build/Build/Tasks/TestTasks.cs`:
```csharp
public static partial class Program
{
    // Every test project in the solution; on Windows that includes the net48 runs.
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
                }));
    }
}
```

`build/Build/Tasks/DefaultTasks.cs`:
```csharp
public static partial class Program
{
    // What CI runs on every platform, and what a local ./build.ps1 or ./build.sh runs.
    static void RegisterDefaultTask()
    {
        Task("Default")
            .IsDependentOn("Test");
    }
}
```

- [ ] **Step 3: Create the bootstraps**

`build.ps1`:
```powershell
#!/usr/bin/env pwsh
# Runs the build (build/Build, Cake.Sdk); arguments pass through, e.g. --target=Test or --configuration=Debug.
# build.sh does the same without PowerShell.
$ErrorActionPreference = 'Stop'
dotnet run --project "$PSScriptRoot/build/Build/Build.csproj" -- @args
exit $LASTEXITCODE
```

`build.sh`:
```bash
#!/usr/bin/env bash
# Runs the build (build/Build, Cake.Sdk); arguments pass through, e.g. --target=Test or --configuration=Debug.
# build.ps1 does the same in PowerShell.
set -euo pipefail
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec dotnet run --project "$script_dir/build/Build/Build.csproj" -- "$@"
```

Make them executable in git:
```bash
git add build.ps1 build.sh
git update-index --chmod=+x build.ps1 build.sh
```

- [ ] **Step 4: Run the build and verify it**

Run: `./build.ps1`
Expected: Cake prints a task summary with `Build` and `Test` succeeded. The 13 tests from Tasks 1–2 pass (on Windows, for both net48 and net10.0). Exit code 0.

Run: `./build.ps1 --target=Build --configuration=Debug`
Expected: only `Build` runs, in Debug; exit code 0.

Run it from another directory (Review Focus 5):
```bash
cd .. && ./PdfProof/build.sh --target=Build; echo "exit $?"; cd PdfProof
```
Expected: `Build` succeeds and prints `exit 0`, because `RunWorkingDirectory` puts the build at the repository root.

Check that a failing test fails the build. Temporarily change `Assert.Equal(2, doc.Metadata.PageCount);` in `PdfProofDocumentLoadTests.cs` to `Assert.Equal(3, …)`, then run `./build.ps1 --target=Test`.
Expected: the `Test` task fails and the exit code is non-zero. **Revert the change** and run `./build.ps1` again; it should pass.

- [ ] **Step 5: Commit**

```bash
git add Directory.Packages.props build build.ps1 build.sh
git commit -m "Add Cake.Sdk build with Build and Test targets

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Packaging: MinVer, icon, README, symbols, and a Pack target that checks every package

**Files:**
- Create: `Directory.Build.targets`, `src/PdfProof/README.md`, `src/PdfProof.Testing/README.md`
- Create: `build/Build.Cake.Extensions/Build.Cake.Extensions.csproj`, `build/Build.Cake.Extensions/Packaging/PackageInspector.cs`
- Create: `build/Build.Cake.Extensions.Tests/Build.Cake.Extensions.Tests.csproj`, `build/Build.Cake.Extensions.Tests/Packaging/PackageInspectorTests.cs`
- Create: `build/Build/Tasks/PackTasks.cs`
- Modify: `Directory.Packages.props`, `build/Build.slnx`, `build/Build/Build.csproj`, `build/Build/Program.cs`, `build/Build/Tasks/BuildTasks.cs`, `build/Build/Tasks/DefaultTasks.cs`

**Interfaces:**
- Consumes: `BuildParameters.Packages` and the `Build` target (Task 3).
- Produces:
  - `Build.Cake.Extensions.Packaging.PackageInspector.FindProblems(string nupkgPath) -> IReadOnlyList<string>`.
  - Cake targets `BuildTests` (the build's own tests) and `Pack`. `Default` becomes `Test → Pack`.

- [ ] **Step 1: Add package versions**

Before pinning MinVer, check the latest stable version: `dotnet package search MinVer --exact-match`. Use that version instead of `8.0.0` if it's newer.

In `Directory.Packages.props`, add inside the `<ItemGroup>`:
```xml
    <!-- Version from the latest git tag (v-prefixed); untagged builds get 0.0.0-alpha.0.<height>. -->
    <PackageVersion Include="MinVer" Version="8.0.0" />
```

- [ ] **Step 2: Write the failing tests for the package inspector**

`build/Build.Cake.Extensions/Build.Cake.Extensions.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <InternalsVisibleTo Include="Build.Cake.Extensions.Tests" />
  </ItemGroup>

</Project>
```

`build/Build.Cake.Extensions.Tests/Build.Cake.Extensions.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <IsTestProject>true</IsTestProject>
    <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
    <TestingPlatformShowTestsFailure>true</TestingPlatformShowTestsFailure>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3" />
    <ProjectReference Include="../Build.Cake.Extensions/Build.Cake.Extensions.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

</Project>
```

Replace `build/Build.slnx` with:
```xml
<Solution>
  <Project Path="Build/Build.csproj" />
  <Project Path="Build.Cake.Extensions/Build.Cake.Extensions.csproj" />
  <Project Path="Build.Cake.Extensions.Tests/Build.Cake.Extensions.Tests.csproj" />
</Solution>
```

`build/Build.Cake.Extensions.Tests/Packaging/PackageInspectorTests.cs`:
```csharp
using System.IO.Compression;
using System.Text;
using Build.Cake.Extensions.Packaging;

namespace Build.Cake.Extensions.Tests.Packaging;

public sealed class PackageInspectorTests : IDisposable
{
    private const string GoodNuspec = """
        <?xml version="1.0" encoding="utf-8"?>
        <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
          <metadata>
            <id>PdfProof</id>
            <version>0.1.0-alpha.1</version>
            <license type="expression">MIT</license>
            <icon>icon.png</icon>
            <readme>README.md</readme>
            <repository type="git" url="https://github.com/mgnslndh/PdfProof" commit="0123456789abcdef" />
          </metadata>
        </package>
        """;

    private readonly string _folder = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), $"pdfproof-pack-{Guid.NewGuid():N}")).FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void A_complete_package_has_no_problems()
    {
        var nupkg = WritePackage(GoodNuspec, "icon.png", "README.md");

        Assert.Empty(PackageInspector.FindProblems(nupkg));
    }

    [Fact]
    public void A_package_without_its_icon_or_readme_is_reported()
    {
        var nupkg = WritePackage(GoodNuspec);

        Assert.Equal(["icon.png is missing", "README.md is missing"], PackageInspector.FindProblems(nupkg));
    }

    [Fact]
    public void A_package_without_a_symbols_package_is_reported()
    {
        var nupkg = WritePackage(GoodNuspec, "icon.png", "README.md");
        File.Delete(Path.ChangeExtension(nupkg, ".snupkg"));

        Assert.Equal(["PdfProof.0.1.0-alpha.1.snupkg is missing"], PackageInspector.FindProblems(nupkg));
    }

    [Fact]
    public void A_nuspec_without_license_or_repository_commit_is_reported()
    {
        var nuspec = GoodNuspec
            .Replace("""<license type="expression">MIT</license>""", "")
            .Replace(""" commit="0123456789abcdef" """, " ");
        var nupkg = WritePackage(nuspec, "icon.png", "README.md");

        Assert.Equal(
            ["the license expression is not MIT", "the repository commit is missing (SourceLink)"],
            PackageInspector.FindProblems(nupkg));
    }

    private string WritePackage(string nuspec, params string[] files)
    {
        var nupkg = Path.Combine(_folder, "PdfProof.0.1.0-alpha.1.nupkg");
        using (var zip = ZipFile.Open(nupkg, ZipArchiveMode.Create))
        {
            WriteEntry(zip, "PdfProof.nuspec", nuspec);
            foreach (var file in files)
            {
                WriteEntry(zip, file, "content");
            }
        }

        File.WriteAllText(Path.ChangeExtension(nupkg, ".snupkg"), "symbols");
        return nupkg;
    }

    private static void WriteEntry(ZipArchive zip, string name, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open(), Encoding.UTF8);
        writer.Write(content);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test --project build/Build.Cake.Extensions.Tests/Build.Cake.Extensions.Tests.csproj`
Expected: build FAILS with `CS0246`/`CS0103`: `PackageInspector` does not exist.

- [ ] **Step 4: Implement the inspector**

`build/Build.Cake.Extensions/Packaging/PackageInspector.cs`:
```csharp
using System.IO.Compression;
using System.Xml.Linq;

namespace Build.Cake.Extensions.Packaging;

// Checks a .nupkg for what makes a package look finished on nuget.org: icon, README, MIT license, SourceLink
// commit, and a .snupkg next to it. Returns the problems found, in a fixed order; empty means the package is fine.
public static class PackageInspector
{
    public static IReadOnlyList<string> FindProblems(string nupkgPath)
    {
        var problems = new List<string>();
        using var zip = ZipFile.OpenRead(nupkgPath);
        var entries = zip.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!entries.Contains("icon.png"))
        {
            problems.Add("icon.png is missing");
        }

        if (!entries.Contains("README.md"))
        {
            problems.Add("README.md is missing");
        }

        var nuspecEntry = zip.Entries.SingleOrDefault(entry =>
            !entry.FullName.Contains('/') && entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        if (nuspecEntry is null)
        {
            problems.Add("the .nuspec is missing");
        }
        else
        {
            using var stream = nuspecEntry.Open();
            var metadata = XDocument.Load(stream).Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "metadata");
            var license = metadata?.Elements().FirstOrDefault(e => e.Name.LocalName == "license");
            if (license?.Value != "MIT")
            {
                problems.Add("the license expression is not MIT");
            }

            var repository = metadata?.Elements().FirstOrDefault(e => e.Name.LocalName == "repository");
            if (string.IsNullOrEmpty(repository?.Attribute("commit")?.Value))
            {
                problems.Add("the repository commit is missing (SourceLink)");
            }
        }

        var symbols = Path.ChangeExtension(nupkgPath, ".snupkg");
        if (!File.Exists(symbols))
        {
            problems.Add($"{Path.GetFileName(symbols)} is missing");
        }

        return problems;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --project build/Build.Cake.Extensions.Tests/Build.Cake.Extensions.Tests.csproj`
Expected: 4 tests pass.

- [ ] **Step 6: Add the package settings and the package READMEs**

`Directory.Build.targets`:
```xml
<Project>
  <!-- Every package carries the PdfProof icon (assets/README.md), its own README, symbols and a MinVer version.
       In targets rather than props because projects set IsPackable in their own body, after the props. -->
  <PropertyGroup Condition="'$(IsPackable)' != 'false'">
    <PackageIcon>icon.png</PackageIcon>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <MinVerTagPrefix>v</MinVerTagPrefix>
  </PropertyGroup>

  <ItemGroup Condition="'$(IsPackable)' != 'false'">
    <None Include="$(MSBuildThisFileDirectory)assets/branding/brand/png/pdfproof-icon-128.png" Pack="true" PackagePath="icon.png" Visible="false" />
    <None Include="$(MSBuildProjectDirectory)/README.md" Pack="true" PackagePath="README.md" />
    <PackageReference Include="MinVer" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

`src/PdfProof/README.md`:
````markdown
# PdfProof

Reads a PDF into layout, structure and semantic models, and exports deterministic Markdown and JSON for tests and
snapshots. Built on [PdfPig](https://github.com/UglyToad/PdfPig).

> Pre-release: the API changes between versions until 1.0.

```csharp
var doc = PdfProofDocument.Load(pdfBytes);
Console.WriteLine($"{doc.Metadata.Title}: {doc.Metadata.PageCount} pages");
```

Documentation: https://github.com/mgnslndh/PdfProof
````

`src/PdfProof.Testing/README.md`:
````markdown
# PdfProof.Testing

Assertions on the meaning of a PDF (headings, tables, links, sequences and invariants) that work in xUnit, TUnit,
NUnit and MSTest. Failures throw `PdfAssertionException`, which xUnit v3 reports as an assertion failure.

> Pre-release: the assertion API arrives in the next versions; until 1.0 the API changes between versions.

Documentation: https://github.com/mgnslndh/PdfProof
````

- [ ] **Step 7: Add the `BuildTests` and `Pack` targets**

Replace `build/Build/Build.csproj` with:
```xml
<Project Sdk="Cake.Sdk">

  <PropertyGroup>
    <!-- Every path in the build is relative to the repository root, wherever the bootstrap is run from. -->
    <RunWorkingDirectory>$(MSBuildProjectDirectory)/../..</RunWorkingDirectory>
  </PropertyGroup>

  <ItemGroup>
    <!-- Groups log output per task on GitHub Actions. -->
    <PackageReference Include="Cake.BuildSystems.Module" />
    <ProjectReference Include="../Build.Cake.Extensions/Build.Cake.Extensions.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Build.Cake.Extensions.Packaging" />
  </ItemGroup>

</Project>
```

Replace `build/Build/Tasks/BuildTasks.cs` with:
```csharp
public static partial class Program
{
    static void RegisterBuildTasks()
    {
        // The build's own tests first: they are fast, and a broken build helper would make every later result suspect.
        Task("BuildTests")
            .Does<BuildParameters>(parameters =>
                DotNetTest("build/Build.Cake.Extensions.Tests", new DotNetTestSettings
                {
                    PathType = DotNetTestPathType.Project,
                    Configuration = parameters.Configuration,
                }));

        Task("Build")
            .IsDependentOn("BuildTests")
            .Does<BuildParameters>(parameters =>
                DotNetBuild("PdfProof.slnx", new DotNetBuildSettings { Configuration = parameters.Configuration }));
    }
}
```

`build/Build/Tasks/PackTasks.cs`:
```csharp
public static partial class Program
{
    // Packs every packable project into artifacts/packages and fails when a package is incomplete.
    static void RegisterPackTasks()
    {
        Task("Pack")
            .IsDependentOn("Build")
            .Does<BuildParameters>(parameters =>
            {
                CleanDirectory(parameters.Packages);
                DotNetPack("PdfProof.slnx", new DotNetPackSettings
                {
                    Configuration = parameters.Configuration,
                    NoBuild = true,
                    OutputDirectory = parameters.Packages,
                });

                var packages = GetFiles($"{parameters.Packages.FullPath}/*.nupkg");
                if (packages.Count == 0)
                {
                    throw new CakeException("Pack produced no packages.");
                }

                var problems = packages
                    .SelectMany(package => PackageInspector.FindProblems(package.FullPath)
                        .Select(problem => $"{package.GetFilename()}: {problem}"))
                    .ToList();
                if (problems.Count > 0)
                {
                    throw new CakeException("Incomplete packages:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
                }

                foreach (var package in packages)
                {
                    Information("Packed {0}", package.GetFilename());
                }
            });
    }
}
```

Replace `build/Build/Tasks/DefaultTasks.cs` with:
```csharp
public static partial class Program
{
    // What CI runs on every platform, and what a local ./build.ps1 or ./build.sh runs.
    static void RegisterDefaultTask()
    {
        Task("Default")
            .IsDependentOn("Test")
            .IsDependentOn("Pack");
    }
}
```

In `build/Build/Program.cs`, register the pack tasks and update the header comment:
```csharp
// Builds, tests and packs PdfProof exactly as CI does; CI runs this build, so the two can't drift apart.
// Arguments: --target=<task> (Default = BuildTests, Build, Test, Pack), --configuration=<c> (Release).
var target = Argument("target", "Default");

Setup(context => BuildParameters.Create(context));

RegisterBuildTasks();
RegisterTestTasks();
RegisterPackTasks();
RegisterDefaultTask();

RunTarget(target);
```

- [ ] **Step 8: Run the full build and verify the packages**

Run: `./build.ps1`
Expected:
- `BuildTests`, `Build`, `Test` and `Pack` succeed.
- The log prints `Packed PdfProof.0.0.0-alpha.0.<n>.nupkg` and `Packed PdfProof.Testing.0.0.0-alpha.0.<n>.nupkg`.
- `artifacts/packages/` contains those two `.nupkg` files and their two `.snupkg` files.

Check that the inspector really guards the build. Temporarily rename `src/PdfProof/README.md` to `README.txt` and run `./build.ps1 --target=Pack`.
Expected: pack fails with a missing-file error, or `Incomplete packages: PdfProof…nupkg: README.md is missing`. **Rename it back** and run `./build.ps1` again; it should pass.

- [ ] **Step 9: Commit**

```bash
git add Directory.Build.targets Directory.Packages.props src/PdfProof/README.md src/PdfProof.Testing/README.md build
git commit -m "Add packaging (MinVer, icon, README, symbols) and a Pack target that checks packages

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: CI workflow and the repository README

**Files:**
- Create: `.github/workflows/ci.yml`
- Modify: `README.md`

**Interfaces:**
- Consumes: `build.ps1`, `build.sh` and the `Default` target (Tasks 3–4).
- Produces: a `CI` workflow with jobs `build (windows-latest)` and `build (ubuntu-latest)`, and a `packages` artifact uploaded from Windows. Plan 4 adds a fixtures job before these; Plan 7 adds `release.yml`.

- [ ] **Step 1: Write the workflow**

The action SHAs are the ones Typstium's CI pins.

`.github/workflows/ci.yml`:
```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:

permissions:
  contents: read

jobs:
  build:
    name: build (${{ matrix.os }})
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
      # The same steps as a local run: build tests, build, tests (net48 + net10.0 on Windows), pack and package checks.
      # Each platform starts the build with its own bootstrap, so CI exercises both.
      - if: runner.os == 'Windows'
        run: ./build.ps1
        shell: pwsh
      - if: runner.os != 'Windows'
        run: ./build.sh
        shell: bash
      - if: runner.os == 'Windows'
        uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
        with:
          name: packages
          path: artifacts/packages/*
          if-no-files-found: error
          retention-days: 7
```

- [ ] **Step 2: Write the repository README**

Replace `README.md` (currently just `# PdfProof`) with:
````markdown
<h1 align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/branding/brand/pdfproof-logo-light-text.svg">
    <source media="(prefers-color-scheme: light)" srcset="assets/branding/brand/pdfproof-logo-dark-text.svg">
    <img alt="PdfProof" src="assets/branding/brand/pdfproof-logo-dark-text.svg" width="400">
  </picture>
</h1>

Unit tests for the PDFs you generate. PdfProof reads a PDF with [PdfPig](https://github.com/UglyToad/PdfPig) and gives
you its meaning (headings, paragraphs, tables, lists, links, pages) to assert on, plus deterministic Markdown and JSON
for snapshot tests. Tests written against one PDF producer keep working when you switch to another.

> In development, not on NuGet yet. Design: [docs/superpowers/specs/2026-10-11-pdfproof-v1-design.md](docs/superpowers/specs/2026-10-11-pdfproof-v1-design.md).

## Building

```bash
./build.ps1        # or ./build.sh: build, test (net48 + net10.0 on Windows), pack
./build.ps1 --target=Test
```

Requires the .NET SDK in `global.json`. CI runs the same build on Windows and Ubuntu.

## License

[MIT](LICENSE)
````

- [ ] **Step 3: Check the workflow file**

The workflow can't run until it's pushed, so check what can be checked locally:

Run: `./build.ps1`
Expected: passes (unchanged from Task 4).

If `actionlint` is available, run `actionlint .github/workflows/ci.yml`; expect no output. Otherwise, re-read the YAML and check the indentation and the four pinned `uses:` lines against Step 1.

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/ci.yml README.md
git commit -m "Add CI workflow and repository README

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: `AGENTS.md` and `CLAUDE.md` for agents working on this repository

Written last, so every command it names exists and has been run.

**Files:**
- Create: `AGENTS.md`, `CLAUDE.md`

**Interfaces:**
- Consumes: the commands and layout from Tasks 1–5.
- Produces: the contributor guidance that Plans 2–7 keep current.

- [ ] **Step 1: Write `AGENTS.md`**

`AGENTS.md`:
````markdown
# AGENTS.md

Guidance for coding agents (Codex, Copilot, Claude Code) working on this repository. Claude Code reads it through
`CLAUDE.md`.

## What this is

PdfProof is a .NET library family for testing generated PDFs. It reads a PDF with PdfPig into layout, structure and
semantic models, exports deterministic Markdown and JSON, and offers assertions on meaning (`PdfAssert`).
- Design: `docs/superpowers/specs/2026-10-11-pdfproof-v1-design.md`. It is the source of truth; read the relevant
  section before changing behavior.
- Implementation plans: `docs/superpowers/plans/`.

## Commands

```bash
./build.ps1                      # or ./build.sh: build tests, build, test, pack (what CI runs)
./build.ps1 --target=Test        # build and test only
dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj   # one test project
```

On Windows, tests run on net48 and net10.0; elsewhere on net10.0 only.

## Layout

| Path | What |
|---|---|
| `src/PdfProof` | Core: `PdfProofDocument`, models, export (netstandard2.0; net8.0; net10.0) |
| `src/PdfProof.Testing` | Assertions; `PdfAssertionException` |
| `tests/` | xUnit v3 on Microsoft.Testing.Platform |
| `build/` | Cake.Sdk build (`Build`), reusable helpers (`Build.Cake.Extensions`) and their tests |
| `assets/branding/` | Logo and icon; see `assets/README.md` |

## Rules

- **No real company or customer names anywhere**: code, tests, samples, docs, commit messages. Use neutral sample
  data ("Consulting, senior", `example.com`).
- **TDD:** write the failing test first, see it fail, then implement.
- **Public API:** every public API change is recorded in that project's `PublicAPI.Unshipped.txt`; the build fails
  otherwise. Every public member has XML docs.
- **net48 is a real target.** Libraries build for netstandard2.0; use Polyfill for newer APIs and run the tests on
  net48 on Windows.
- **Deterministic output:** invariant culture, `\n` line endings, no unordered iteration, no timestamps.
- **Output changes are breaking for users.** A change to approved Markdown/JSON output needs re-approval and an
  "Output changes" entry in the changelog.
- **Corpus:** generated PDFs are never committed. External PDFs are listed with URL, SHA-256 and license.
- **Dependencies:** versions are pinned in `Directory.Packages.props`; upgrade deliberately.
- **Keep this file current:** a change that adds commands, projects or rules updates `AGENTS.md` too.
````

`CLAUDE.md`:
```markdown
@AGENTS.md
```

- [ ] **Step 2: Verify every command in `AGENTS.md`**

Run: `./build.ps1 --target=Test`
Expected: passes.

Run: `dotnet test --project tests/PdfProof.Tests/PdfProof.Tests.csproj`
Expected: 10 tests pass on each target framework.

Search the repository for real company names before committing. `git grep -n -i "<name>"` must return nothing for the
names the human partner has listed as off-limits. Ask them for the list if you don't have it; don't guess.

- [ ] **Step 3: Commit**

```bash
git add AGENTS.md CLAUDE.md
git commit -m "Add AGENTS.md and CLAUDE.md for agents working on the repository

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 4: Hand over for the first CI run**

Pushing publishes the work, so **don't push without asking.** Tell the human partner that the branch is ready to push, and that the first CI run is what proves Linux (net10.0 only) and the GitHub-hosted Windows runner (net48 + net10.0).

---

## Self-review notes

- **Spec coverage for this plan's scope:**

  | Spec item | Task |
  |---|---|
  | D3 target frameworks | 1, 2 |
  | D4 `PdfProofDocument` (immutable, not `IDisposable`) | 1 |
  | §5.1 metadata subset; custom info and the other models come in Plans 2–3 | 1 |
  | §5.4 errors: corrupt input; encrypted documents come in Plan 2 with `PdfProofOptions.Password` | 1 |
  | D12 / §8.7 / OQ-1 | 2 |
  | D17 / §11.1 Cake build | 3, 4 |
  | D18 MinVer | 4 |
  | §11.3 packaging (deterministic, SourceLink, snupkg, README, icon, PublicApiAnalyzers) | 1, 2, 4 |
  | §11.2 CI, first stage | 5 |
  | §12 test stack (xUnit v3 + MTP, net48 + net10.0) | 1, 2 |
  | D21 / §16.4 `AGENTS.md` + `CLAUDE.md` | 6 |

- **Deferred on purpose:**
  - `PdfProofOptions`, logging and profiles go to Plan 2, which first needs them.
  - The fixtures job and `PDFPROOF_REQUIRE_FIXTURES` go to Plan 4.
  - `release.yml` and reserving the NuGet prefix go to Plan 7.
  - The agent skill, `pdfproof skill …` and `CheckSkill` (§16.1–16.3) go to Plan 7, together with the CLI.
