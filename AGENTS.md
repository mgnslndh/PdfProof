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
# record new public API (run after adding public members; then rebuild and review the appended lines)
dotnet build src/PdfProof/PdfProof.csproj 2>&1 | sed -n "s/.*error RS0016: Symbol '\([^']*\)' is not part of the declared public API.*/\1/p" | sort -u >> src/PdfProof/PublicAPI.Unshipped.txt
```

On Windows, tests run on net48 and net10.0; elsewhere on net10.0 only.

## Layout

| Path | What |
|---|---|
| `src/PdfProof` | Core: `PdfProofDocument`, options and profiles, diagnostics; `Layout/` (layout model and strategies); `Structure/` (Tagged PDF structure tree) (netstandard2.0; net8.0; net10.0) |
| `src/PdfProof.Testing` | Assertions; `PdfAssertionException` |
| `src/Shared` | Source shared into every `src/` project (`Throw`) |
| `tests/` | xUnit v3 on Microsoft.Testing.Platform |
| `build/` | Cake.Sdk build (`Build`), reusable helpers (`Build.Cake.Extensions`) and their tests |
| `assets/branding/` | Logo and icon; see `assets/README.md` |

## Rules

- **No real company or customer names anywhere**: code, tests, samples, docs, commit messages. Use neutral sample
  data ("Consulting, senior", `example.com`).
- **TDD:** write the failing test first, see it fail, then implement.
- **Throwing:** guard clauses and throws use `Throw` (`src/Shared/Throw/`, namespace `PdfProof.Shared`, implicit in every `src/` project): `Throw.IfNull(x)`, `Throw.ArgumentException(…)`. BCL helpers go in `Throw.cs` (kept close to its dotnet/extensions upstream so it can be shared), PdfProof helpers in `Throw.PdfProof.cs`.
- **Test PDFs:** build them in code. Use `tests/PdfProof.Tests/RawPdf.cs` (raw PDF syntax: exact positions, clipping, rotation, annotations, outlines, marked content and structure trees: `RawPdf.Marked`, `RawPdf.Tag`) or PdfPig's `PdfDocumentBuilder`. Don't commit generated PDF files.
- **Calls into PdfPig** go through `PdfPigCall.Read`, so parser errors become `PdfProofException` and PdfProof bugs don't.
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
