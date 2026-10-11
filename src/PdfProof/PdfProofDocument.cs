using System.Globalization;
using PdfProof.Layout;
using PdfProof.Structure;
using UglyToad.PdfPig;

namespace PdfProof;

/// <summary>
/// A loaded PDF. This is the only type you load; the exporters, the CLI and the test assertions all work on it.
/// It is immutable and safe to share between threads, and it holds no open file or parser.
/// </summary>
public sealed class PdfProofDocument
{
    private PdfProofDocument(PdfMetadata metadata, LayoutModel layout, StructureModel structure, PdfDiagnostics diagnostics)
    {
        Metadata = metadata;
        Layout = layout;
        Structure = structure;
        Diagnostics = diagnostics;
    }

    /// <summary>The PDF version, page count and info dictionary.</summary>
    public PdfMetadata Metadata { get; }

    /// <summary>The geometry: pages, with text grouped into blocks, lines and spans.</summary>
    public LayoutModel Layout { get; }

    /// <summary>The Tagged PDF structure tree; empty, with <see cref="StructureModel.IsTagged"/> false, when the PDF isn't tagged.</summary>
    public StructureModel Structure { get; }

    /// <summary>The profile used and every decision and warning recorded while loading.</summary>
    public PdfDiagnostics Diagnostics { get; }

    /// <summary>Loads a PDF from its bytes, with the profile detected from the producer.</summary>
    /// <param name="pdf">The complete PDF file.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pdf"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfProofException">The bytes can't be read as a PDF.</exception>
    public static PdfProofDocument Load(byte[] pdf)
    {
        Throw.IfNull(pdf);
        return Open(pdf, PdfProofOptions.Default);
    }

    /// <summary>Loads a PDF from its bytes with the given options.</summary>
    /// <param name="pdf">The complete PDF file.</param>
    /// <param name="options">How to load the PDF. Without a profile in them, the profile is detected from the producer.</param>
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
        return Open(ReadToEnd(pdf), PdfProofOptions.Default);
    }

    /// <summary>
    /// Loads a PDF from a stream with the given options, reading from its current position to the end. The stream
    /// may be non-seekable, and it is left open.
    /// </summary>
    /// <param name="pdf">A stream positioned at the start of the PDF.</param>
    /// <param name="options">How to load the PDF. Without a profile in them, the profile is detected from the producer.</param>
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
        return Open(ReadFile(path), PdfProofOptions.Default);
    }

    /// <summary>Loads a PDF file with the given options.</summary>
    /// <param name="path">The path to the PDF file.</param>
    /// <param name="options">How to load the PDF. Without a profile in them, the profile is detected from the producer.</param>
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

    private static PdfProofDocument Open(byte[] pdf, PdfProofOptions options)
    {
        // Clipping is a parsing option, so it is read before the profile is detected. Every preset uses the same
        // layout options today; when they differ, detection must read the info dictionary before the pages (Plan 5).
        var applyClipping = (options.Profile ?? PdfProfile.Default).Layout.ApplyClipping;
        using var document = PdfPigCall.Read(() => PdfDocument.Open(pdf, new ParsingOptions { ClipPaths = applyClipping }));
        // PdfPig returns the raw values; mapping them to PdfMetadata happens outside PdfPigCall.
        var info = PdfPigCall.Read(() => (document.Version, document.NumberOfPages, document.Information));
        var metadata = MetadataReader.Read(info.Version.ToString("0.0", CultureInfo.InvariantCulture), info.NumberOfPages, info.Information);

        var (profile, reason) = options.Profile is { } given
            ? (given, Chosen(given))
            : ProducerProfiles.Detect(metadata.Producer, metadata.Creator);
        var detected = options.Profile is null;
        var diagnostics = new DiagnosticsCollector(options.Logger);
        diagnostics.Add(PdfDiagnosticCodes.Profile, null, $"{profile.Name} ({reason})");

        var raw = ReadStructure(document, diagnostics);
        var tagged = raw.Elements.Count > 0;
        var layoutResult = LayoutBuilder.Build(document, profile.Layout, diagnostics, collectMarkedText: tagged);
        var layout = layoutResult.Model;
        if (layout.Pages.Count > 0 && layout.Pages.All(page => page.Blocks.Count == 0))
        {
            diagnostics.Add(PdfDiagnosticCodes.NoTextLayer, null, "No page has extractable text; the PDF may be a scan. OCR is not supported.");
        }

        var structure = tagged ? StructureBuilder.Build(raw, layoutResult, profile.Layout) : StructureModel.Untagged;
        if (structure.IsTagged)
        {
            diagnostics.Add(PdfDiagnosticCodes.Structure, null, $"{structure.Root.Descendants().Count()} elements");
        }

        if (raw.SkippedStreamContent)
        {
            diagnostics.Add(
                PdfDiagnosticCodes.StructureIncomplete,
                null,
                "Marked content inside form XObjects or annotations (/Stm) is not supported and is left out; the text of page marked content with the same MCID may include it.");
        }

        if (raw.Truncated)
        {
            diagnostics.Add(PdfDiagnosticCodes.StructureIncomplete, null, "Elements nested deeper than 256 levels are left out.");
        }

        if (raw.HasStructTreeRoot || raw.Marked)
        {
            if (raw.Marked && !structure.IsTagged)
            {
                diagnostics.Add(PdfDiagnosticCodes.MarkInfoMismatch, null, "/MarkInfo sets /Marked true, but the document has no structure tree with elements.");
            }
            else if (!raw.Marked && structure.IsTagged)
            {
                diagnostics.Add(PdfDiagnosticCodes.MarkInfoMismatch, null, "The document has a structure tree, but /MarkInfo doesn't set /Marked true.");
            }
        }

        return new PdfProofDocument(metadata, layout, structure, diagnostics.ToDiagnostics(profile, detected));
    }

    // How a profile given in the options relates to the presets, for the profile diagnostic.
    private static string Chosen(PdfProfile profile) =>
        PdfProfile.Presets.Contains(profile) ? "from options"
        : PdfProfile.Presets.Any(preset => preset.Name == profile.Name) ? "from options, adjusted"
        : "from options, custom";

    // The structure tree is secondary to page content: a tree the parser rejects is reported and left out, and the
    // document still loads.
    private static RawStructure ReadStructure(PdfDocument document, DiagnosticsCollector diagnostics)
    {
        try
        {
            return PdfPigCall.Read(() => StructureTreeReader.Read(document));
        }
        catch (PdfProofException ex)
        {
            diagnostics.Add(
                PdfDiagnosticCodes.StructureUnreadable,
                null,
                "The structure tree could not be read and is left out: " + (ex.InnerException ?? ex).Message);
            return RawStructure.None;
        }
    }
}
