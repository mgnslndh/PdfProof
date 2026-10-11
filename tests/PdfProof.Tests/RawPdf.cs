using System.Globalization;
using System.Text;

namespace PdfProof.Tests;

// Writes small PDFs from raw PDF syntax, for tests that need exact control: positions, clipping, rotation,
// annotations, outlines, info entries and (from Plan 3) structure trees. Object 1 is the catalog, 2 the page tree.
// Every page is A4 (595 x 842 pt) and gets the fonts /F1 Helvetica, /F2 Helvetica-Bold, /F3 Helvetica-Oblique
// (WinAnsiEncoding, so U+00AD soft hyphen and U+00A0 no-break space can be written) and a 2x2 gray image /Im1.
// Text is written as ISO-8859-1. Numbers are formatted with the invariant culture.
internal sealed class RawPdf
{
    private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");

    private readonly List<string> _objects = [];
    private readonly List<int> _pages = [];
    private readonly Dictionary<int, int> _contents = [];
    private readonly Dictionary<int, List<string>> _annotations = [];
    private readonly int _resources;

    public RawPdf()
    {
        Reserve(); // 1: catalog, written by Build
        Reserve(); // 2: page tree, written by Build
        var regular = Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        var bold = Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
        var oblique = Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Oblique /Encoding /WinAnsiEncoding >>");
        var image = AddStream(
            "/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceGray /BitsPerComponent 8",
            "\u0000\u00FF\u00FF\u0000");
        _resources = Add($"<< /Font << /F1 {regular} 0 R /F2 {bold} 0 R /F3 {oblique} 0 R >> /XObject << /Im1 {image} 0 R >> >>");
    }

    /// Extra entries for the catalog, e.g. "/Outlines 9 0 R".
    public string CatalogEntries { get; set; } = "";

    /// The trailer's /Info dictionary, e.g. "<< /Title (Invoice) >>"; null leaves it out.
    public string? Info { get; set; }

    public int Reserve() => Add("");

    public int Add(string body)
    {
        _objects.Add(body);
        return _objects.Count;
    }

    public void Set(int number, string body) => _objects[number - 1] = body;

    public int AddStream(string entries, string data) =>
        Add($"<< {entries} /Length {Latin1.GetByteCount(data)} >>\nstream\n{data}\nendstream");

    /// Adds a page with the given content stream and returns its object number (for /Dest arrays).
    public int AddPage(string content)
    {
        var page = Reserve();
        _contents[page] = AddStream("", content);
        _pages.Add(page);
        return page;
    }

    /// Adds an annotation dictionary, e.g. "<< /Type /Annot /Subtype /Link /Rect [...] /A << ... >> >>", to a page.
    public void Annotate(int page, string annotation)
    {
        if (!_annotations.TryGetValue(page, out var list))
        {
            _annotations[page] = list = [];
        }

        list.Add(annotation);
    }

    public byte[] Build()
    {
        foreach (var page in _pages)
        {
            var annots = _annotations.TryGetValue(page, out var list) ? $" /Annots [{string.Join(" ", list)}]" : "";
            Set(page, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources {_resources} 0 R /Contents {_contents[page]} 0 R{annots} >>");
        }

        Set(1, $"<< /Type /Catalog /Pages 2 0 R {CatalogEntries} >>");
        Set(2, $"<< /Type /Pages /Kids [{string.Join(" ", _pages.Select(p => $"{p} 0 R"))}] /Count {_pages.Count} >>");
        var objects = new List<string>(_objects);
        var infoEntry = "";
        if (Info is not null)
        {
            objects.Add(Info);
            infoEntry = $" /Info {objects.Count} 0 R";
        }

        using var output = new MemoryStream();
        void Write(string text)
        {
            var bytes = Latin1.GetBytes(text);
            output.Write(bytes, 0, bytes.Length);
        }

        Write("%PDF-1.7\n");
        var offsets = new List<long>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(output.Position);
            Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = output.Position;
        Write($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            Write(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        }

        Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R{infoEntry} >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }

    /// A text object showing one string at (x, y).
    public static string Text(double x, double y, string text, int size = 12, string font = "F1") =>
        FormattableString.Invariant($"BT /{font} {size} Tf {x} {y} Td ({Escape(text)}) Tj ET\n");

    private static string Escape(string text) =>
        text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
