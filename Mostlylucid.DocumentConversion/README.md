# Mostlylucid.DocumentConversion

> **Note**: These packages are provided as-is. I'll get them working good enough to release but I can't commit to support. However they are Unlicense so have at it!

Document format conversion library for .NET. Converts between Word documents (.docx), Markdown, PDF, HTML, and plain text formats. Extracts text and images from documents.

## Features

- **Word to Markdown**: Convert .docx files to clean Markdown
- **Word to PDF**: Generate PDFs from Word documents
- **Markdown to Word**: Create .docx files from Markdown content
- **Markdown to PDF**: Generate PDFs from Markdown
- **Text Extraction**: Extract plain text from any supported format
- **Image Extraction**: Extract embedded images from Word documents
- **Document Elements**: Parse documents into structured elements (paragraphs, headings, lists, tables, images)
- **Format Detection**: Automatically detect document format from file extension
- **Easy Integration**: Simple dependency injection setup with .NET

## Installation

```bash
dotnet add package Mostlylucid.DocumentConversion
```

## Quick Start

### 1. Register Services

```csharp
using Mostlylucid.DocumentConversion.Extensions;

// In Program.cs
builder.Services.AddDocumentConversion();

// Or for singleton registration (better for high-throughput)
builder.Services.AddDocumentConversionSingleton();
```

### 2. Use the Service

```csharp
using Mostlylucid.DocumentConversion.Interfaces;
using Mostlylucid.DocumentConversion.Models;

public class DocumentController : ControllerBase
{
    private readonly IDocumentConversionService _conversionService;

    public DocumentController(IDocumentConversionService conversionService)
    {
        _conversionService = conversionService;
    }

    [HttpPost("word-to-markdown")]
    public async Task<IActionResult> ConvertWordToMarkdown(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        var markdown = await _conversionService.WordToMarkdownAsync(stream);
        return Ok(markdown);
    }

    [HttpPost("word-to-pdf")]
    public async Task<IActionResult> ConvertWordToPdf(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        var pdfBytes = await _conversionService.WordToPdfAsync(stream);
        return File(pdfBytes, "application/pdf", "converted.pdf");
    }
}
```

## Supported Formats

### Reading (Input)
| Format | Extension | Support |
|--------|-----------|---------|
| Word | .docx | Full |
| Markdown | .md, .markdown | Full |
| Plain Text | .txt | Full |
| Legacy Word | .doc | Not Supported |
| PDF | .pdf | Not Supported (output only) |

### Writing (Output)
| Format | Extension | Support |
|--------|-----------|---------|
| Word | .docx | Full |
| Markdown | .md | Full |
| PDF | .pdf | Full |
| HTML | .html, .htm | Full |
| Plain Text | .txt | Full |

## API Reference

### IDocumentConversionService

#### Read Documents

```csharp
// Read from file path
Task<Document> ReadDocumentAsync(string filePath, CancellationToken ct = default);

// Read from stream
Task<Document> ReadDocumentAsync(Stream stream, string fileName, CancellationToken ct = default);

// Read from bytes
Task<Document> ReadDocumentAsync(byte[] data, string fileName, CancellationToken ct = default);
```

#### Convert Documents

```csharp
// Convert a parsed document to target format
Task<ConversionResult> ConvertAsync(
    Document document,
    DocFormat targetFormat,
    ConversionOptions? options = null,
    CancellationToken ct = default);

// Convert file to file
Task<ConversionResult> ConvertFileAsync(
    string inputPath,
    string outputPath,
    ConversionOptions? options = null,
    CancellationToken ct = default);
```

#### Quick Conversion Methods

```csharp
// Word to Markdown
Task<string> WordToMarkdownAsync(Stream wordStream, ConversionOptions? options = null, CancellationToken ct = default);

// Word to PDF
Task<byte[]> WordToPdfAsync(Stream wordStream, ConversionOptions? options = null, CancellationToken ct = default);

// Markdown to Word
Task<byte[]> MarkdownToWordAsync(string markdown, ConversionOptions? options = null, CancellationToken ct = default);

// Markdown to PDF
Task<byte[]> MarkdownToPdfAsync(string markdown, ConversionOptions? options = null, CancellationToken ct = default);
```

#### Extract Content

```csharp
// Extract all document elements
Task<List<DocumentElement>> ExtractElementsAsync(Stream stream, string fileName, CancellationToken ct = default);

// Extract plain text
Task<string> ExtractTextAsync(Stream stream, string fileName, CancellationToken ct = default);

// Extract images from Word documents
Task<List<ImageElement>> ExtractImagesAsync(Stream stream, string fileName, CancellationToken ct = default);
```

#### Utility Methods

```csharp
// Detect format from filename
DocFormat DetectFormat(string fileName);

// Check if format can be read
bool CanRead(DocFormat format);

// Check if format can be written
bool CanWrite(DocFormat format);
```

## Document Model

### Document

```csharp
public class Document
{
    public string? FileName { get; set; }
    public DocFormat SourceFormat { get; set; }
    public List<DocumentElement> Elements { get; set; }
    public List<ImageElement> Images { get; set; }
    public Dictionary<string, string> Metadata { get; set; }

    public string GetPlainText();  // Get all text content
}
```

### Document Elements

The document model includes various element types:

- `ParagraphElement` - Text paragraphs
- `HeadingElement` - Headings (levels 1-6)
- `ListElement` - Bulleted or numbered lists
- `TableElement` - Tables with rows and cells
- `ImageElement` - Embedded images with data
- `CodeBlockElement` - Code blocks with language info

### ConversionResult

```csharp
public class ConversionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public byte[]? OutputBytes { get; set; }    // For binary formats (PDF, Word)
    public string? OutputText { get; set; }      // For text formats (Markdown, HTML)
    public DocFormat OutputFormat { get; set; }
    public string? SuggestedFileName { get; set; }
}
```

## Usage Examples

### Convert File to File

```csharp
// Convert Word document to PDF
var result = await _conversionService.ConvertFileAsync(
    "input.docx",
    "output.pdf");

if (result.Success)
{
    Console.WriteLine($"Converted to: {result.SuggestedFileName}");
}
else
{
    Console.WriteLine($"Error: {result.ErrorMessage}");
}
```

### Read and Analyze Document Structure

```csharp
var document = await _conversionService.ReadDocumentAsync("report.docx");

// Iterate through elements
foreach (var element in document.Elements)
{
    switch (element)
    {
        case HeadingElement heading:
            Console.WriteLine($"H{heading.Level}: {heading.Text}");
            break;
        case ParagraphElement para:
            Console.WriteLine($"Paragraph: {para.Text}");
            break;
        case TableElement table:
            Console.WriteLine($"Table: {table.Rows.Count} rows");
            break;
        case ImageElement image:
            Console.WriteLine($"Image: {image.AltText ?? "No alt text"}");
            break;
    }
}
```

### Extract Images from Word Document

```csharp
using var stream = File.OpenRead("document.docx");
var images = await _conversionService.ExtractImagesAsync(stream, "document.docx");

foreach (var image in images)
{
    var fileName = $"image_{image.Order}.{image.Extension}";
    await File.WriteAllBytesAsync(fileName, image.Data);
}
```

### Markdown to PDF with Options

```csharp
var markdown = @"
# My Document

This is a **bold** statement.

## Features
- Feature 1
- Feature 2
- Feature 3
";

var pdfBytes = await _conversionService.MarkdownToPdfAsync(markdown);
await File.WriteAllBytesAsync("output.pdf", pdfBytes);
```

## Dependencies

This package uses:

- **DocumentFormat.OpenXml** - Word document processing
- **Markdig** - Markdown parsing and rendering
- **QuestPDF** - PDF generation

## Requirements

- **.NET 9.0** or later
- No external dependencies required

## Performance Considerations

- **Scoped vs Singleton**: Use `AddDocumentConversionSingleton()` for high-throughput scenarios
- **Stream Position**: Remember to reset stream position if reusing streams
- **Large Documents**: For very large documents, consider streaming approaches
- **PDF Generation**: QuestPDF is efficient but complex documents may require more memory

## Limitations

- **Legacy .doc files**: Not supported. Convert to .docx first
- **PDF reading**: PDF is output-only (no PDF parsing)
- **Complex Word formatting**: Some advanced Word features may not convert perfectly
- **Image extraction**: Only supported for Word documents

## License

Unlicense - Public Domain

## Contributing

Contributions welcome! Please see the main repository:
https://github.com/scottgal/mostlylucid.nugetpackages

## Support

For issues, questions, or contributions:
https://github.com/scottgal/mostlylucid.nugetpackages/issues
