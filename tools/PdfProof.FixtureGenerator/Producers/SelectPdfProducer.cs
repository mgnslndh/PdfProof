using System.Drawing;
using System.Text.RegularExpressions;

namespace PdfProof.FixtureGenerator;

// SelectPdf Community (HTML to PDF, Windows only, at most 5 pages). The footer is a SelectPdf text section, and the
// title is set from <title> the way an application would set it.
internal sealed class SelectPdfProducer : IProducer
{
    public string Name => "selectpdf";

    public bool BestEffort => false;

    public string Version => "Select.HtmlToPdf.NetCore " + Versions.Of(typeof(SelectPdf.HtmlToPdf));

    public Task<string?> CheckAsync() => Task.FromResult(OperatingSystem.IsWindows() ? null : "SelectPdf runs on Windows only");

    public Task<byte[]> RenderAsync(CorpusDocument document)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("SelectPdf runs on Windows only.");
        }

        var html = File.ReadAllText(document.HtmlPath);
        var converter = new SelectPdf.HtmlToPdf();
        converter.Options.DisplayFooter = true;
        converter.Footer.Height = 30;
        converter.Footer.Add(new SelectPdf.PdfTextSection(0, 10, "Page {page_number} of {total_pages}", new Font("Arial", 8))
        {
            HorizontalAlign = SelectPdf.PdfTextHorizontalAlign.Center,
        });
        var pdf = converter.ConvertHtmlString(html, new Uri(document.Directory + Path.DirectorySeparatorChar).AbsoluteUri);
        try
        {
            pdf.DocumentInformation.Title = Title(html);
            return Task.FromResult(pdf.Save());
        }
        finally
        {
            pdf.Close();
        }
    }

    private static string Title(string html)
    {
        var match = Regex.Match(html, "<title>(.*?)</title>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }
}
