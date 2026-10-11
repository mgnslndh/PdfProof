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
