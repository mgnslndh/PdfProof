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
