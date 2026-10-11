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
