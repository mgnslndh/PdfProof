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
