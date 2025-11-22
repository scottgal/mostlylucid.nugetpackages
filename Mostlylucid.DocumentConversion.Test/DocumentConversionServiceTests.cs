using Microsoft.Extensions.Logging;
using Mostlylucid.DocumentConversion.Interfaces;
using Mostlylucid.DocumentConversion.Models;
using Mostlylucid.DocumentConversion.Services;

namespace Mostlylucid.DocumentConversion.Test;

public class DocumentConversionServiceTests
{
    private readonly IDocumentConversionService _service;

    public DocumentConversionServiceTests()
    {
        var wordLogger = new Mock<ILogger<WordDocumentService>>();
        var mdLogger = new Mock<ILogger<MarkdownConversionService>>();
        var pdfLogger = new Mock<ILogger<PdfConversionService>>();
        var serviceLogger = new Mock<ILogger<DocumentConversionService>>();

        var wordService = new WordDocumentService(wordLogger.Object);
        var mdService = new MarkdownConversionService(mdLogger.Object);
        var pdfService = new PdfConversionService(pdfLogger.Object, mdService);

        _service = new DocumentConversionService(
            serviceLogger.Object,
            wordService,
            mdService,
            pdfService);
    }

    [Theory]
    [InlineData("document.docx", DocFormat.Word)]
    [InlineData("document.DOCX", DocFormat.Word)]
    [InlineData("readme.md", DocFormat.Markdown)]
    [InlineData("README.MD", DocFormat.Markdown)]
    [InlineData("notes.txt", DocFormat.PlainText)]
    [InlineData("report.pdf", DocFormat.Pdf)]
    [InlineData("page.html", DocFormat.Html)]
    [InlineData("page.htm", DocFormat.Html)]
    [InlineData("unknown.xyz", DocFormat.Unknown)]
    public void DetectFormat_VariousExtensions_DetectsCorrectly(string fileName, DocFormat expected)
    {
        // Act
        var format = _service.DetectFormat(fileName);

        // Assert
        Assert.Equal(expected, format);
    }

    [Theory]
    [InlineData(DocFormat.Word, true)]
    [InlineData(DocFormat.Markdown, true)]
    [InlineData(DocFormat.PlainText, true)]
    [InlineData(DocFormat.Pdf, false)]
    [InlineData(DocFormat.Html, false)]
    [InlineData(DocFormat.Unknown, false)]
    public void CanRead_VariousFormats_ReturnsCorrectly(DocFormat format, bool expected)
    {
        // Act
        var canRead = _service.CanRead(format);

        // Assert
        Assert.Equal(expected, canRead);
    }

    [Theory]
    [InlineData(DocFormat.Word, true)]
    [InlineData(DocFormat.Markdown, true)]
    [InlineData(DocFormat.Pdf, true)]
    [InlineData(DocFormat.Html, true)]
    [InlineData(DocFormat.PlainText, true)]
    [InlineData(DocFormat.Unknown, false)]
    public void CanWrite_VariousFormats_ReturnsCorrectly(DocFormat format, bool expected)
    {
        // Act
        var canWrite = _service.CanWrite(format);

        // Assert
        Assert.Equal(expected, canWrite);
    }

    [Fact]
    public async Task MarkdownToWordAsync_ValidMarkdown_ReturnsDocxBytes()
    {
        // Arrange
        var markdown = "# Test Document\n\nThis is a test.";

        // Act
        var bytes = await _service.MarkdownToWordAsync(markdown);

        // Assert
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        // DOCX files start with PK (ZIP signature)
        Assert.Equal(0x50, bytes[0]); // 'P'
        Assert.Equal(0x4B, bytes[1]); // 'K'
    }

    [Fact]
    public async Task MarkdownToPdfAsync_ValidMarkdown_ReturnsPdfBytes()
    {
        // Arrange
        var markdown = "# Test Document\n\nThis is a test.";

        // Act
        var bytes = await _service.MarkdownToPdfAsync(markdown);

        // Assert
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        // PDF files start with %PDF
        Assert.Equal(0x25, bytes[0]); // '%'
        Assert.Equal(0x50, bytes[1]); // 'P'
        Assert.Equal(0x44, bytes[2]); // 'D'
        Assert.Equal(0x46, bytes[3]); // 'F'
    }

    [Fact]
    public async Task ReadDocumentAsync_MarkdownBytes_ParsesCorrectly()
    {
        // Arrange
        var markdown = "# Hello World\n\nTest content.";
        var bytes = System.Text.Encoding.UTF8.GetBytes(markdown);

        // Act
        var document = await _service.ReadDocumentAsync(bytes, "test.md");

        // Assert
        Assert.NotNull(document);
        Assert.Equal(DocFormat.Markdown, document.SourceFormat);
        Assert.Equal(2, document.Elements.Count);
    }

    [Fact]
    public async Task ConvertAsync_DocumentToMarkdown_ReturnsMarkdownResult()
    {
        // Arrange
        var document = new Document
        {
            Title = "Test",
            SourceFormat = DocFormat.Word,
            Elements =
            [
                new HeadingElement { Level = 1, Text = "Title", Order = 0 },
                new ParagraphElement { Text = "Content", Order = 1 }
            ]
        };

        // Act
        var result = await _service.ConvertAsync(document, DocFormat.Markdown);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.OutputText);
        Assert.Contains("# Title", result.OutputText);
        Assert.Contains("Content", result.OutputText);
        Assert.Equal(DocFormat.Markdown, result.OutputFormat);
        Assert.Equal("text/markdown", result.ContentType);
    }

    [Fact]
    public async Task ConvertAsync_DocumentToPdf_ReturnsPdfBytes()
    {
        // Arrange
        var document = new Document
        {
            Title = "Test",
            Elements =
            [
                new HeadingElement { Level = 1, Text = "Title", Order = 0 },
                new ParagraphElement { Text = "Content", Order = 1 }
            ]
        };

        // Act
        var result = await _service.ConvertAsync(document, DocFormat.Pdf);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.OutputBytes);
        Assert.Equal(DocFormat.Pdf, result.OutputFormat);
        Assert.Equal("application/pdf", result.ContentType);
    }

    [Fact]
    public async Task ConvertAsync_DocumentToPlainText_ReturnsText()
    {
        // Arrange
        var document = new Document
        {
            Elements =
            [
                new HeadingElement { Level = 1, Text = "Title", Order = 0 },
                new ParagraphElement { Text = "Paragraph content.", Order = 1 }
            ]
        };

        // Act
        var result = await _service.ConvertAsync(document, DocFormat.PlainText);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.OutputText);
        Assert.Contains("Title", result.OutputText);
        Assert.Contains("Paragraph content", result.OutputText);
    }

    [Fact]
    public async Task ConvertAsync_DocumentToHtml_ReturnsHtml()
    {
        // Arrange
        var document = new Document
        {
            Elements =
            [
                new HeadingElement { Level = 1, Text = "Title", Order = 0 },
                new ParagraphElement { Text = "Paragraph", Order = 1 }
            ]
        };

        // Act
        var result = await _service.ConvertAsync(document, DocFormat.Html);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.OutputText);
        Assert.Contains("<h1>", result.OutputText);
        Assert.Contains("<p>", result.OutputText);
    }

    [Fact]
    public async Task ConvertAsync_UnsupportedFormat_ReturnsFailure()
    {
        // Arrange
        var document = new Document();

        // Act
        var result = await _service.ConvertAsync(document, DocFormat.Unknown);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextAsync_MarkdownFile_ReturnsPlainText()
    {
        // Arrange
        var markdown = "# Title\n\n**Bold** text here.";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markdown));

        // Act
        var text = await _service.ExtractTextAsync(stream, "test.md");

        // Assert
        Assert.Contains("Title", text);
        Assert.Contains("Bold", text);
    }

    [Fact]
    public async Task ExtractElementsAsync_MarkdownFile_ReturnsElements()
    {
        // Arrange
        var markdown = "# Heading\n\nParagraph\n\n- Item 1\n- Item 2";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markdown));

        // Act
        var elements = await _service.ExtractElementsAsync(stream, "test.md");

        // Assert
        Assert.True(elements.Count >= 3);
        Assert.Contains(elements, e => e is HeadingElement);
        Assert.Contains(elements, e => e is ParagraphElement);
        Assert.Contains(elements, e => e is ListElement);
    }

    [Fact]
    public async Task MarkdownToPdfAsync_WithOptions_AppliesOptions()
    {
        // Arrange
        var markdown = "# Test";
        var options = new ConversionOptions
        {
            PageSize = PdfPageSize.Letter,
            Orientation = PdfOrientation.Landscape
        };

        // Act
        var bytes = await _service.MarkdownToPdfAsync(markdown, options);

        // Assert
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public async Task MarkdownToWordAsync_WithOptions_PreservesFormatting()
    {
        // Arrange
        var markdown = "**Bold** and *italic* text.";
        var options = new ConversionOptions { PreserveFormatting = true };

        // Act
        var bytes = await _service.MarkdownToWordAsync(markdown, options);

        // Assert
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }
}
