namespace PdfProof;

/// <summary>The codes of <see cref="PdfDiagnostic"/> entries.</summary>
public static class PdfDiagnosticCodes
{
    /// <summary>Which profile was used, and whether it was detected or given.</summary>
    public const string Profile = "Profile";

    /// <summary>The PDF is encrypted, and how it was opened.</summary>
    public const string Encrypted = "Encrypted";

    /// <summary>What the layout step produced for a page.</summary>
    public const string Layout = "Layout";

    /// <summary>No page has extractable text; the PDF is probably a scan. Logged as a warning.</summary>
    public const string NoTextLayer = "NoTextLayer";

    /// <summary>The bookmarks (outline) could not be read and are left out; the pages are unaffected. Logged as a warning.</summary>
    public const string BookmarksUnreadable = "BookmarksUnreadable";

    /// <summary>What the structure tree holds, for tagged documents.</summary>
    public const string Structure = "Structure";

    /// <summary>The structure tree could not be read and is left out; pages and layout are unaffected. Logged as a warning.</summary>
    public const string StructureUnreadable = "StructureUnreadable";

    /// <summary><c>/MarkInfo /Marked</c> disagrees with whether the document has a structure tree.</summary>
    public const string MarkInfoMismatch = "MarkInfoMismatch";

    /// <summary>Part of the structure tree couldn't be read faithfully and is left out. Logged as a warning.</summary>
    public const string StructureIncomplete = "StructureIncomplete";

    /// <summary>How many form field widgets were read, and how many were left out for having no page.</summary>
    public const string FormFields = "FormFields";

    /// <summary>The form fields, or some of them, could not be read and are left out; pages are unaffected. Logged as a warning.</summary>
    public const string FormFieldsUnreadable = "FormFieldsUnreadable";

    /// <summary>What the semantic model holds: its source and how many blocks and artifacts.</summary>
    public const string Semantics = "Semantics";
}
