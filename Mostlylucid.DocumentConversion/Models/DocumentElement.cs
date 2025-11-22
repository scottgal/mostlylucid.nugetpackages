namespace Mostlylucid.DocumentConversion.Models;

/// <summary>
/// Base class for all document elements
/// </summary>
public abstract class DocumentElement
{
    /// <summary>
    /// Unique identifier for the element
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Type of the document element
    /// </summary>
    public abstract DocumentElementType ElementType { get; }

    /// <summary>
    /// Order/position of the element in the document
    /// </summary>
    public int Order { get; set; }
}

/// <summary>
/// Types of document elements that can be extracted
/// </summary>
public enum DocumentElementType
{
    Paragraph,
    Heading,
    Table,
    Image,
    List,
    ListItem,
    CodeBlock,
    BlockQuote,
    HorizontalRule,
    Hyperlink,
    Run,
    Unknown
}

/// <summary>
/// Represents a paragraph element
/// </summary>
public class ParagraphElement : DocumentElement
{
    public override DocumentElementType ElementType => DocumentElementType.Paragraph;

    /// <summary>
    /// Text content of the paragraph
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Child runs with formatting
    /// </summary>
    public List<TextRun> Runs { get; set; } = [];

    /// <summary>
    /// Paragraph style name (e.g., "Normal", "Heading 1")
    /// </summary>
    public string? StyleName { get; set; }

    /// <summary>
    /// Paragraph alignment
    /// </summary>
    public TextAlignment Alignment { get; set; } = TextAlignment.Left;
}

/// <summary>
/// Represents a heading element
/// </summary>
public class HeadingElement : DocumentElement
{
    public override DocumentElementType ElementType => DocumentElementType.Heading;

    /// <summary>
    /// Heading level (1-6)
    /// </summary>
    public int Level { get; set; } = 1;

    /// <summary>
    /// Heading text content
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Child runs with formatting
    /// </summary>
    public List<TextRun> Runs { get; set; } = [];
}

/// <summary>
/// Represents formatted text within a paragraph or heading
/// </summary>
public class TextRun
{
    /// <summary>
    /// The text content
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Is the text bold
    /// </summary>
    public bool IsBold { get; set; }

    /// <summary>
    /// Is the text italic
    /// </summary>
    public bool IsItalic { get; set; }

    /// <summary>
    /// Is the text underlined
    /// </summary>
    public bool IsUnderline { get; set; }

    /// <summary>
    /// Is the text strikethrough
    /// </summary>
    public bool IsStrikethrough { get; set; }

    /// <summary>
    /// Is the text superscript
    /// </summary>
    public bool IsSuperscript { get; set; }

    /// <summary>
    /// Is the text subscript
    /// </summary>
    public bool IsSubscript { get; set; }

    /// <summary>
    /// Is the text code/monospace
    /// </summary>
    public bool IsCode { get; set; }

    /// <summary>
    /// Font name if specified
    /// </summary>
    public string? FontName { get; set; }

    /// <summary>
    /// Font size in points if specified
    /// </summary>
    public double? FontSize { get; set; }

    /// <summary>
    /// Text color in hex format (e.g., "#FF0000")
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Highlight/background color
    /// </summary>
    public string? HighlightColor { get; set; }

    /// <summary>
    /// Hyperlink URL if this run is a link
    /// </summary>
    public string? HyperlinkUrl { get; set; }
}

/// <summary>
/// Text alignment options
/// </summary>
public enum TextAlignment
{
    Left,
    Center,
    Right,
    Justify
}

/// <summary>
/// Represents a table element
/// </summary>
public class TableElement : DocumentElement
{
    public override DocumentElementType ElementType => DocumentElementType.Table;

    /// <summary>
    /// Table rows
    /// </summary>
    public List<TableRow> Rows { get; set; } = [];

    /// <summary>
    /// Whether the first row is a header
    /// </summary>
    public bool HasHeaderRow { get; set; }

    /// <summary>
    /// Table width in percentage or twips
    /// </summary>
    public double? Width { get; set; }
}

/// <summary>
/// Represents a table row
/// </summary>
public class TableRow
{
    /// <summary>
    /// Cells in the row
    /// </summary>
    public List<TableCell> Cells { get; set; } = [];

    /// <summary>
    /// Whether this is a header row
    /// </summary>
    public bool IsHeader { get; set; }
}

/// <summary>
/// Represents a table cell
/// </summary>
public class TableCell
{
    /// <summary>
    /// Cell content as document elements
    /// </summary>
    public List<DocumentElement> Content { get; set; } = [];

    /// <summary>
    /// Plain text content
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Number of columns this cell spans
    /// </summary>
    public int ColumnSpan { get; set; } = 1;

    /// <summary>
    /// Number of rows this cell spans
    /// </summary>
    public int RowSpan { get; set; } = 1;

    /// <summary>
    /// Cell background color
    /// </summary>
    public string? BackgroundColor { get; set; }
}

/// <summary>
/// Represents an image element
/// </summary>
public class ImageElement : DocumentElement
{
    public override DocumentElementType ElementType => DocumentElementType.Image;

    /// <summary>
    /// Image data as bytes
    /// </summary>
    public byte[]? Data { get; set; }

    /// <summary>
    /// Image content type (e.g., "image/png", "image/jpeg")
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Image file name
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Alt text for the image
    /// </summary>
    public string? AltText { get; set; }

    /// <summary>
    /// Image width in pixels
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Image height in pixels
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Base64 encoded data URI
    /// </summary>
    public string? DataUri => Data != null && ContentType != null
        ? $"data:{ContentType};base64,{Convert.ToBase64String(Data)}"
        : null;
}

/// <summary>
/// Represents a list element
/// </summary>
public class ListElement : DocumentElement
{
    public override DocumentElementType ElementType => DocumentElementType.List;

    /// <summary>
    /// List items
    /// </summary>
    public List<ListItemElement> Items { get; set; } = [];

    /// <summary>
    /// Type of list
    /// </summary>
    public ListType ListType { get; set; } = ListType.Bullet;

    /// <summary>
    /// Starting number for ordered lists
    /// </summary>
    public int StartNumber { get; set; } = 1;
}

/// <summary>
/// Represents a list item
/// </summary>
public class ListItemElement : DocumentElement
{
    public override DocumentElementType ElementType => DocumentElementType.ListItem;

    /// <summary>
    /// Item text content
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Child runs with formatting
    /// </summary>
    public List<TextRun> Runs { get; set; } = [];

    /// <summary>
    /// Nesting level (0-based)
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Nested child list (for nested lists)
    /// </summary>
    public ListElement? NestedList { get; set; }
}

/// <summary>
/// Type of list
/// </summary>
public enum ListType
{
    Bullet,
    Numbered,
    LetterLower,
    LetterUpper,
    RomanLower,
    RomanUpper
}

/// <summary>
/// Represents a code block element
/// </summary>
public class CodeBlockElement : DocumentElement
{
    public override DocumentElementType ElementType => DocumentElementType.CodeBlock;

    /// <summary>
    /// Code content
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Programming language (for syntax highlighting)
    /// </summary>
    public string? Language { get; set; }
}

/// <summary>
/// Represents a block quote element
/// </summary>
public class BlockQuoteElement : DocumentElement
{
    public override DocumentElementType ElementType => DocumentElementType.BlockQuote;

    /// <summary>
    /// Quote content as document elements
    /// </summary>
    public List<DocumentElement> Content { get; set; } = [];

    /// <summary>
    /// Plain text content
    /// </summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Represents a horizontal rule/divider
/// </summary>
public class HorizontalRuleElement : DocumentElement
{
    public override DocumentElementType ElementType => DocumentElementType.HorizontalRule;
}

/// <summary>
/// Represents a hyperlink element
/// </summary>
public class HyperlinkElement : DocumentElement
{
    public override DocumentElementType ElementType => DocumentElementType.Hyperlink;

    /// <summary>
    /// Link URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Link text
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Link title/tooltip
    /// </summary>
    public string? Title { get; set; }
}
