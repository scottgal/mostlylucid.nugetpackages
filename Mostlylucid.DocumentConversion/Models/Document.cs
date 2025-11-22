namespace Mostlylucid.DocumentConversion.Models;

/// <summary>
/// Represents a complete document with metadata and elements
/// </summary>
public class Document
{
    /// <summary>
    /// Document title
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Document author
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Document subject/description
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Document keywords/tags
    /// </summary>
    public List<string> Keywords { get; set; } = [];

    /// <summary>
    /// Document creation date
    /// </summary>
    public DateTime? CreatedDate { get; set; }

    /// <summary>
    /// Document last modified date
    /// </summary>
    public DateTime? ModifiedDate { get; set; }

    /// <summary>
    /// Original file name
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Source format of the document
    /// </summary>
    public DocFormat SourceFormat { get; set; }

    /// <summary>
    /// All document elements in order
    /// </summary>
    public List<DocumentElement> Elements { get; set; } = [];

    /// <summary>
    /// Embedded images extracted from the document
    /// </summary>
    public List<ImageElement> Images { get; set; } = [];

    /// <summary>
    /// Custom properties/metadata
    /// </summary>
    public Dictionary<string, string> CustomProperties { get; set; } = new();

    /// <summary>
    /// Get plain text representation of the document
    /// </summary>
    public string GetPlainText()
    {
        var lines = new List<string>();

        foreach (var element in Elements)
        {
            var text = element switch
            {
                HeadingElement h => h.Text,
                ParagraphElement p => p.Text,
                ListElement l => string.Join("\n", l.Items.Select(i => $"• {i.Text}")),
                CodeBlockElement c => c.Code,
                BlockQuoteElement b => $"> {b.Text}",
                TableElement t => GetTableText(t),
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(text))
            {
                lines.Add(text);
            }
        }

        return string.Join("\n\n", lines);
    }

    private static string GetTableText(TableElement table)
    {
        var rows = table.Rows.Select(r =>
            string.Join(" | ", r.Cells.Select(c => c.Text)));
        return string.Join("\n", rows);
    }

    /// <summary>
    /// Get all headings from the document
    /// </summary>
    public IEnumerable<HeadingElement> GetHeadings()
    {
        return Elements.OfType<HeadingElement>();
    }

    /// <summary>
    /// Get all paragraphs from the document
    /// </summary>
    public IEnumerable<ParagraphElement> GetParagraphs()
    {
        return Elements.OfType<ParagraphElement>();
    }

    /// <summary>
    /// Get all tables from the document
    /// </summary>
    public IEnumerable<TableElement> GetTables()
    {
        return Elements.OfType<TableElement>();
    }

    /// <summary>
    /// Get all lists from the document
    /// </summary>
    public IEnumerable<ListElement> GetLists()
    {
        return Elements.OfType<ListElement>();
    }

    /// <summary>
    /// Get all code blocks from the document
    /// </summary>
    public IEnumerable<CodeBlockElement> GetCodeBlocks()
    {
        return Elements.OfType<CodeBlockElement>();
    }

    /// <summary>
    /// Get word count of the document
    /// </summary>
    public int GetWordCount()
    {
        var text = GetPlainText();
        return string.IsNullOrWhiteSpace(text)
            ? 0
            : text.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
    }
}

/// <summary>
/// Supported document formats
/// </summary>
public enum DocFormat
{
    Unknown,
    Word,       // .docx
    WordLegacy, // .doc (not supported for full processing)
    Markdown,   // .md
    Pdf,        // .pdf (output only)
    Html,       // .html
    PlainText   // .txt
}

/// <summary>
/// Result of a document conversion operation
/// </summary>
public class ConversionResult
{
    /// <summary>
    /// Whether the conversion was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if conversion failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Converted content as bytes (for binary formats like PDF)
    /// </summary>
    public byte[]? OutputBytes { get; set; }

    /// <summary>
    /// Converted content as string (for text formats like Markdown)
    /// </summary>
    public string? OutputText { get; set; }

    /// <summary>
    /// Output format
    /// </summary>
    public DocFormat OutputFormat { get; set; }

    /// <summary>
    /// Suggested file name for the output
    /// </summary>
    public string? SuggestedFileName { get; set; }

    /// <summary>
    /// Content type/MIME type for the output
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Warnings encountered during conversion
    /// </summary>
    public List<string> Warnings { get; set; } = [];

    /// <summary>
    /// Create a successful result with text output
    /// </summary>
    public static ConversionResult SuccessText(string text, DocFormat format, string? fileName = null)
    {
        return new ConversionResult
        {
            Success = true,
            OutputText = text,
            OutputFormat = format,
            SuggestedFileName = fileName,
            ContentType = GetContentType(format)
        };
    }

    /// <summary>
    /// Create a successful result with binary output
    /// </summary>
    public static ConversionResult SuccessBytes(byte[] bytes, DocFormat format, string? fileName = null)
    {
        return new ConversionResult
        {
            Success = true,
            OutputBytes = bytes,
            OutputFormat = format,
            SuggestedFileName = fileName,
            ContentType = GetContentType(format)
        };
    }

    /// <summary>
    /// Create a failed result
    /// </summary>
    public static ConversionResult Failure(string errorMessage)
    {
        return new ConversionResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }

    private static string GetContentType(DocFormat format) => format switch
    {
        DocFormat.Word => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        DocFormat.Pdf => "application/pdf",
        DocFormat.Markdown => "text/markdown",
        DocFormat.Html => "text/html",
        DocFormat.PlainText => "text/plain",
        _ => "application/octet-stream"
    };
}

/// <summary>
/// Options for document conversion
/// </summary>
public class ConversionOptions
{
    /// <summary>
    /// Include images in the output
    /// </summary>
    public bool IncludeImages { get; set; } = true;

    /// <summary>
    /// Include tables in the output
    /// </summary>
    public bool IncludeTables { get; set; } = true;

    /// <summary>
    /// Include document metadata in the output
    /// </summary>
    public bool IncludeMetadata { get; set; } = true;

    /// <summary>
    /// Preserve formatting (bold, italic, etc.)
    /// </summary>
    public bool PreserveFormatting { get; set; } = true;

    /// <summary>
    /// For PDF output: page size
    /// </summary>
    public PdfPageSize PageSize { get; set; } = PdfPageSize.A4;

    /// <summary>
    /// For PDF output: page orientation
    /// </summary>
    public PdfOrientation Orientation { get; set; } = PdfOrientation.Portrait;

    /// <summary>
    /// For Markdown output: use GitHub Flavored Markdown
    /// </summary>
    public bool UseGitHubFlavoredMarkdown { get; set; } = true;

    /// <summary>
    /// For images: embed as base64 data URIs
    /// </summary>
    public bool EmbedImagesAsDataUri { get; set; } = true;

    /// <summary>
    /// For images: output directory (if not embedding)
    /// </summary>
    public string? ImageOutputDirectory { get; set; }

    /// <summary>
    /// Custom CSS for HTML output
    /// </summary>
    public string? CustomCss { get; set; }
}

/// <summary>
/// PDF page sizes
/// </summary>
public enum PdfPageSize
{
    A4,
    A3,
    A5,
    Letter,
    Legal
}

/// <summary>
/// PDF page orientations
/// </summary>
public enum PdfOrientation
{
    Portrait,
    Landscape
}
