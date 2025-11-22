using Microsoft.Extensions.Logging;
using Mostlylucid.DocumentConversion.Interfaces;
using Mostlylucid.DocumentConversion.Models;

namespace Mostlylucid.DocumentConversion.Services;

/// <summary>
/// Main document conversion service that orchestrates all format conversions
/// </summary>
public class DocumentConversionService : IDocumentConversionService
{
    private readonly ILogger<DocumentConversionService> _logger;
    private readonly IWordDocumentService _wordService;
    private readonly IMarkdownConversionService _markdownService;
    private readonly IPdfConversionService _pdfService;

    private static readonly Dictionary<string, DocFormat> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".docx", DocFormat.Word },
        { ".doc", DocFormat.WordLegacy },
        { ".md", DocFormat.Markdown },
        { ".markdown", DocFormat.Markdown },
        { ".pdf", DocFormat.Pdf },
        { ".html", DocFormat.Html },
        { ".htm", DocFormat.Html },
        { ".txt", DocFormat.PlainText }
    };

    private static readonly HashSet<DocFormat> ReadableFormats =
    [
        DocFormat.Word,
        DocFormat.Markdown,
        DocFormat.PlainText
    ];

    private static readonly HashSet<DocFormat> WritableFormats =
    [
        DocFormat.Word,
        DocFormat.Markdown,
        DocFormat.Pdf,
        DocFormat.Html,
        DocFormat.PlainText
    ];

    public DocumentConversionService(
        ILogger<DocumentConversionService> logger,
        IWordDocumentService wordService,
        IMarkdownConversionService markdownService,
        IPdfConversionService pdfService)
    {
        _logger = logger;
        _wordService = wordService;
        _markdownService = markdownService;
        _pdfService = pdfService;
    }

    public async Task<Document> ReadDocumentAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var format = DetectFormat(filePath);
        await using var stream = File.OpenRead(filePath);
        return await ReadDocumentAsync(stream, Path.GetFileName(filePath), cancellationToken);
    }

    public async Task<Document> ReadDocumentAsync(byte[] data, string fileName, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(data);
        return await ReadDocumentAsync(stream, fileName, cancellationToken);
    }

    public async Task<Document> ReadDocumentAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        var format = DetectFormat(fileName);

        _logger.LogInformation("Reading document {FileName} as {Format}", fileName, format);

        return format switch
        {
            DocFormat.Word => await _wordService.ReadAsync(stream, fileName, cancellationToken),
            DocFormat.Markdown => await ReadMarkdownFromStreamAsync(stream, fileName, cancellationToken),
            DocFormat.PlainText => await ReadPlainTextFromStreamAsync(stream, fileName, cancellationToken),
            DocFormat.WordLegacy => throw new NotSupportedException("Legacy .doc format is not supported. Please convert to .docx first."),
            DocFormat.Pdf => throw new NotSupportedException("PDF reading is not supported. PDF is an output-only format."),
            _ => throw new NotSupportedException($"Format {format} is not supported for reading.")
        };
    }

    public async Task<ConversionResult> ConvertAsync(Document document, DocFormat targetFormat, ConversionOptions? options = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Converting document from {Source} to {Target}", document.SourceFormat, targetFormat);

        try
        {
            return targetFormat switch
            {
                DocFormat.Word => await ConvertToWordAsync(document, options, cancellationToken),
                DocFormat.Markdown => await ConvertToMarkdownAsync(document, options, cancellationToken),
                DocFormat.Pdf => await ConvertToPdfAsync(document, options, cancellationToken),
                DocFormat.Html => await ConvertToHtmlAsync(document, options, cancellationToken),
                DocFormat.PlainText => ConvertToPlainText(document),
                _ => ConversionResult.Failure($"Target format {targetFormat} is not supported.")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert document to {Format}", targetFormat);
            return ConversionResult.Failure($"Conversion failed: {ex.Message}");
        }
    }

    public async Task<ConversionResult> ConvertFileAsync(string inputPath, string outputPath, ConversionOptions? options = null, CancellationToken cancellationToken = default)
    {
        var document = await ReadDocumentAsync(inputPath, cancellationToken);
        var targetFormat = DetectFormat(outputPath);

        var result = await ConvertAsync(document, targetFormat, options, cancellationToken);

        if (result.Success)
        {
            if (result.OutputBytes != null)
            {
                await File.WriteAllBytesAsync(outputPath, result.OutputBytes, cancellationToken);
            }
            else if (result.OutputText != null)
            {
                await File.WriteAllTextAsync(outputPath, result.OutputText, cancellationToken);
            }

            result.SuggestedFileName = Path.GetFileName(outputPath);
            _logger.LogInformation("Successfully converted {Input} to {Output}", inputPath, outputPath);
        }

        return result;
    }

    public async Task<string> WordToMarkdownAsync(Stream wordStream, ConversionOptions? options = null, CancellationToken cancellationToken = default)
    {
        var document = await _wordService.ReadAsync(wordStream, null, cancellationToken);
        return await _markdownService.ToMarkdownAsync(document, options, cancellationToken);
    }

    public async Task<byte[]> WordToPdfAsync(Stream wordStream, ConversionOptions? options = null, CancellationToken cancellationToken = default)
    {
        var document = await _wordService.ReadAsync(wordStream, null, cancellationToken);
        return await _pdfService.ToPdfAsync(document, options, cancellationToken);
    }

    public async Task<byte[]> MarkdownToWordAsync(string markdown, ConversionOptions? options = null, CancellationToken cancellationToken = default)
    {
        var document = await _markdownService.ParseAsync(markdown, null, cancellationToken);
        return await _wordService.CreateAsync(document, options, cancellationToken);
    }

    public async Task<byte[]> MarkdownToPdfAsync(string markdown, ConversionOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _pdfService.MarkdownToPdfAsync(markdown, options, cancellationToken);
    }

    public async Task<List<DocumentElement>> ExtractElementsAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        var document = await ReadDocumentAsync(stream, fileName, cancellationToken);
        return document.Elements;
    }

    public async Task<string> ExtractTextAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        var format = DetectFormat(fileName);

        if (format == DocFormat.Word)
        {
            return await _wordService.ExtractTextAsync(stream, cancellationToken);
        }

        var document = await ReadDocumentAsync(stream, fileName, cancellationToken);
        return document.GetPlainText();
    }

    public async Task<List<ImageElement>> ExtractImagesAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        var format = DetectFormat(fileName);

        if (format == DocFormat.Word)
        {
            return await _wordService.ExtractImagesAsync(stream, cancellationToken);
        }

        var document = await ReadDocumentAsync(stream, fileName, cancellationToken);
        return document.Images;
    }

    public DocFormat DetectFormat(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return ExtensionMap.TryGetValue(extension, out var format) ? format : DocFormat.Unknown;
    }

    public bool CanRead(DocFormat format) => ReadableFormats.Contains(format);

    public bool CanWrite(DocFormat format) => WritableFormats.Contains(format);

    private async Task<Document> ReadMarkdownFromStreamAsync(Stream stream, string? fileName, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(cancellationToken);
        return await _markdownService.ParseAsync(content, fileName, cancellationToken);
    }

    private async Task<Document> ReadPlainTextFromStreamAsync(Stream stream, string? fileName, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(cancellationToken);

        return new Document
        {
            FileName = fileName,
            SourceFormat = DocFormat.PlainText,
            Elements = content.Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries)
                .Select((text, index) => new ParagraphElement
                {
                    Order = index,
                    Text = text.Trim()
                } as DocumentElement)
                .ToList()
        };
    }

    private async Task<ConversionResult> ConvertToWordAsync(Document document, ConversionOptions? options, CancellationToken cancellationToken)
    {
        var bytes = await _wordService.CreateAsync(document, options, cancellationToken);
        var fileName = GetOutputFileName(document.FileName, ".docx");

        return ConversionResult.SuccessBytes(bytes, DocFormat.Word, fileName);
    }

    private async Task<ConversionResult> ConvertToMarkdownAsync(Document document, ConversionOptions? options, CancellationToken cancellationToken)
    {
        var markdown = await _markdownService.ToMarkdownAsync(document, options, cancellationToken);
        var fileName = GetOutputFileName(document.FileName, ".md");

        return ConversionResult.SuccessText(markdown, DocFormat.Markdown, fileName);
    }

    private async Task<ConversionResult> ConvertToPdfAsync(Document document, ConversionOptions? options, CancellationToken cancellationToken)
    {
        var bytes = await _pdfService.ToPdfAsync(document, options, cancellationToken);
        var fileName = GetOutputFileName(document.FileName, ".pdf");

        return ConversionResult.SuccessBytes(bytes, DocFormat.Pdf, fileName);
    }

    private async Task<ConversionResult> ConvertToHtmlAsync(Document document, ConversionOptions? options, CancellationToken cancellationToken)
    {
        var html = await _markdownService.ToHtmlAsync(document, options, cancellationToken);
        var fileName = GetOutputFileName(document.FileName, ".html");

        return ConversionResult.SuccessText(html, DocFormat.Html, fileName);
    }

    private ConversionResult ConvertToPlainText(Document document)
    {
        var text = document.GetPlainText();
        var fileName = GetOutputFileName(document.FileName, ".txt");

        return ConversionResult.SuccessText(text, DocFormat.PlainText, fileName);
    }

    private string GetOutputFileName(string? sourceFileName, string newExtension)
    {
        if (string.IsNullOrEmpty(sourceFileName))
        {
            return $"document{newExtension}";
        }

        return Path.ChangeExtension(sourceFileName, newExtension);
    }
}
