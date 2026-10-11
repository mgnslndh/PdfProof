using PdfProof.Layout;
using UglyToad.PdfPig;

namespace PdfProof;

/// <summary>
/// A loaded PDF. This is the only type you load; the exporters, the CLI and the test assertions all work on it.
/// It is immutable and safe to share between threads, and it holds no open file or parser.
/// </summary>
public sealed class PdfProofDocument
{
    private PdfProofDocument(PdfMetadata metadata, LayoutModel layout, PdfDiagnostics diagnostics)
    {
        Metadata = metadata;
        Layout = layout;
        Diagnostics = diagnostics;
    }

    /// <summary>The PDF version, page count and info dictionary.</summary>
    public PdfMetadata Metadata { get; }

    /// <summary>The geometry: pages, with text grouped into blocks, lines and spans.</summary>
    public LayoutModel Layout { get; }

    /// <summary>The profile used and every decision and warning recorded while loading.</summary>
    public PdfDiagnostics Diagnostics { get; }

    /// <summary>Loads a PDF from its bytes, with the profile detected from the producer.</summary>
    /// <param name="pdf">The complete PDF file.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pdf"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfProofException">The bytes can't be read as a PDF.</exception>
    public static PdfProofDocument Load(byte[] pdf)
    {
        Throw.IfNull(pdf);
        return Open(pdf, null);
    }

    /// <summary>Loads a PDF from its bytes with the given options.</summary>
    /// <param name="pdf">The complete PDF file.</param>
    /// <param name="options">The options to use, exactly as given.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pdf"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfProofException">The bytes can't be read as a PDF.</exception>
    public static PdfProofDocument Load(byte[] pdf, PdfProofOptions options)
    {
        Throw.IfNull(pdf);
        Throw.IfNull(options);
        return Open(pdf, options);
    }

    /// <summary>
    /// Loads a PDF from a stream, reading from its current position to the end, with the profile detected from the
    /// producer. The stream may be non-seekable, and it is left open.
    /// </summary>
    /// <param name="pdf">A stream positioned at the start of the PDF.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pdf"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfProofException">The content can't be read as a PDF.</exception>
    public static PdfProofDocument Load(Stream pdf)
    {
        Throw.IfNull(pdf);
        return Open(ReadToEnd(pdf), null);
    }

    /// <summary>
    /// Loads a PDF from a stream with the given options, reading from its current position to the end. The stream
    /// may be non-seekable, and it is left open.
    /// </summary>
    /// <param name="pdf">A stream positioned at the start of the PDF.</param>
    /// <param name="options">The options to use, exactly as given.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pdf"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfProofException">The content can't be read as a PDF.</exception>
    public static PdfProofDocument Load(Stream pdf, PdfProofOptions options)
    {
        Throw.IfNull(pdf);
        Throw.IfNull(options);
        return Open(ReadToEnd(pdf), options);
    }

    /// <summary>Loads a PDF file, with the profile detected from the producer.</summary>
    /// <param name="path">The path to the PDF file.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or whitespace, or names a directory.</exception>
    /// <exception cref="FileNotFoundException">The file doesn't exist.</exception>
    /// <exception cref="PdfProofException">The file can't be read as a PDF.</exception>
    public static PdfProofDocument Load(string path)
    {
        Throw.IfNull(path);
        return Open(ReadFile(path), null);
    }

    /// <summary>Loads a PDF file with the given options.</summary>
    /// <param name="path">The path to the PDF file.</param>
    /// <param name="options">The options to use, exactly as given.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or whitespace, or names a directory.</exception>
    /// <exception cref="FileNotFoundException">The file doesn't exist.</exception>
    /// <exception cref="PdfProofException">The file can't be read as a PDF.</exception>
    public static PdfProofDocument Load(string path, PdfProofOptions options)
    {
        Throw.IfNull(path);
        Throw.IfNull(options);
        return Open(ReadFile(path), options);
    }

    // Copying makes the behavior the same for every kind of stream: read from here to the end, never seek, never
    // dispose the caller's stream.
    private static byte[] ReadToEnd(Stream pdf)
    {
        using var buffer = new MemoryStream();
        pdf.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static byte[] ReadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Throw.ArgumentException(nameof(path), "The path is empty.");
        }

        if (Directory.Exists(path))
        {
            Throw.ArgumentException(nameof(path), $"The path is a directory, not a PDF file. Path: {path}");
        }

        if (!File.Exists(path))
        {
            Throw.FileNotFoundException("The PDF file was not found.", path);
        }

        return File.ReadAllBytes(path);
    }

    // options is null when the caller gave none: the profile is then detected from the producer.
    private static PdfProofDocument Open(byte[] pdf, PdfProofOptions? options)
    {
        // Clipping is a parsing option, so it is read from the options before the profile is known. Every profile
        // uses the same layout options today; when they differ, detection must happen before this point.
        var applyClipping = (options?.Layout ?? LayoutOptions.Default).ApplyClipping;
        using var document = PdfPigCall.Read(() => PdfDocument.Open(pdf, new ParsingOptions { ClipPaths = applyClipping }));
        var metadata = PdfPigCall.Read(() => MetadataReader.Read(document));

        var detected = options is null;
        var effective = options ?? PdfProofOptions.ForProfile(ProducerProfiles.Detect(metadata.Producer, metadata.Creator));
        var diagnostics = new DiagnosticsCollector(effective.Logger);
        diagnostics.Add(
            PdfDiagnosticCodes.Profile,
            null,
            detected ? $"{effective.Profile} (detected from producer \"{metadata.Producer}\")" : $"{effective.Profile} (from options)");

        var layout = LayoutBuilder.Build(document, effective.Layout, diagnostics);
        return new PdfProofDocument(metadata, layout, diagnostics.ToDiagnostics(effective.Profile, detected));
    }
}
