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
