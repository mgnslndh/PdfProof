namespace PdfProof;

/// <summary>
/// Document-level metadata: the PDF version, the page count and the standard entries of the info dictionary.
/// Entries the document doesn't have are <see langword="null"/>.
/// </summary>
public sealed class PdfMetadata
{
    internal PdfMetadata(
        string pdfVersion,
        int pageCount,
        string? title,
        string? author,
        string? subject,
        string? keywords,
        string? creator,
        string? producer,
        DateTimeOffset? creationDate,
        DateTimeOffset? modifiedDate)
    {
        PdfVersion = pdfVersion;
        PageCount = pageCount;
        Title = title;
        Author = author;
        Subject = subject;
        Keywords = keywords;
        Creator = creator;
        Producer = producer;
        CreationDate = creationDate;
        ModifiedDate = modifiedDate;
    }

    /// <summary>The PDF version from the file header, always formatted with a dot, e.g. <c>1.7</c>.</summary>
    public string PdfVersion { get; }

    /// <summary>The number of pages.</summary>
    public int PageCount { get; }

    /// <summary>The <c>/Title</c> entry.</summary>
    public string? Title { get; }

    /// <summary>The <c>/Author</c> entry.</summary>
    public string? Author { get; }

    /// <summary>The <c>/Subject</c> entry.</summary>
    public string? Subject { get; }

    /// <summary>The <c>/Keywords</c> entry.</summary>
    public string? Keywords { get; }

    /// <summary>The <c>/Creator</c> entry: the application that created the original document.</summary>
    public string? Creator { get; }

    /// <summary>The <c>/Producer</c> entry: the application that wrote the PDF.</summary>
    public string? Producer { get; }

    /// <summary>The <c>/CreationDate</c> entry, when present and valid.</summary>
    public DateTimeOffset? CreationDate { get; }

    /// <summary>The <c>/ModDate</c> entry, when present and valid.</summary>
    public DateTimeOffset? ModifiedDate { get; }
}
