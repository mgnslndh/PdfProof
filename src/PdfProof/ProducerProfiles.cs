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
