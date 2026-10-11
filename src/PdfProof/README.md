# PdfProof

Reads a PDF into layout, structure and semantic models, and exports deterministic Markdown and JSON for tests and
snapshots. Built on [PdfPig](https://github.com/UglyToad/PdfPig).

> Pre-release: the API changes between versions until 1.0.

```csharp
var doc = PdfProofDocument.Load(pdfBytes);
Console.WriteLine($"{doc.Metadata.Title}: {doc.Metadata.PageCount} pages");
```

Documentation: https://github.com/mgnslndh/PdfProof
