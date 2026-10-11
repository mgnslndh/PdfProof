namespace PdfProof;

/// <summary>The codes of <see cref="PdfDiagnostic"/> entries.</summary>
public static class PdfDiagnosticCodes
{
    /// <summary>Which profile was used, and whether it was detected or given.</summary>
    public const string Profile = "Profile";

    /// <summary>What the layout step produced for a page.</summary>
    public const string Layout = "Layout";

    /// <summary>No page has extractable text; the PDF is probably a scan. Logged as a warning.</summary>
    public const string NoTextLayer = "NoTextLayer";

    /// <summary>The bookmarks (outline) could not be read and are left out; the pages are unaffected. Logged as a warning.</summary>
    public const string BookmarksUnreadable = "BookmarksUnreadable";
}
