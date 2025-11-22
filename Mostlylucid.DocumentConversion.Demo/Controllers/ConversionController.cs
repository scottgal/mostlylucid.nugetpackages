using Microsoft.AspNetCore.Mvc;
using Mostlylucid.DocumentConversion.Interfaces;
using Mostlylucid.DocumentConversion.Models;

namespace Mostlylucid.DocumentConversion.Demo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConversionController : ControllerBase
{
    private readonly IDocumentConversionService _conversionService;
    private readonly ILogger<ConversionController> _logger;

    public ConversionController(
        IDocumentConversionService conversionService,
        ILogger<ConversionController> logger)
    {
        _conversionService = conversionService;
        _logger = logger;
    }

    /// <summary>
    /// Convert a Word document to Markdown
    /// </summary>
    [HttpPost("word-to-markdown")]
    public async Task<IActionResult> WordToMarkdown(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        if (!file.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only .docx files are supported");

        try
        {
            await using var stream = file.OpenReadStream();
            var markdown = await _conversionService.WordToMarkdownAsync(stream);

            return Ok(new { markdown, fileName = Path.ChangeExtension(file.FileName, ".md") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert Word to Markdown");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Convert a Word document to PDF
    /// </summary>
    [HttpPost("word-to-pdf")]
    public async Task<IActionResult> WordToPdf(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        if (!file.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only .docx files are supported");

        try
        {
            await using var stream = file.OpenReadStream();
            var pdfBytes = await _conversionService.WordToPdfAsync(stream);

            return File(pdfBytes, "application/pdf", Path.ChangeExtension(file.FileName, ".pdf"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert Word to PDF");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Convert Markdown to Word document
    /// </summary>
    [HttpPost("markdown-to-word")]
    public async Task<IActionResult> MarkdownToWord([FromBody] MarkdownInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Markdown))
            return BadRequest("Markdown content is required");

        try
        {
            var wordBytes = await _conversionService.MarkdownToWordAsync(input.Markdown);
            var fileName = string.IsNullOrEmpty(input.FileName)
                ? "document.docx"
                : Path.ChangeExtension(input.FileName, ".docx");

            return File(wordBytes,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert Markdown to Word");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Convert Markdown to PDF
    /// </summary>
    [HttpPost("markdown-to-pdf")]
    public async Task<IActionResult> MarkdownToPdf([FromBody] MarkdownInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Markdown))
            return BadRequest("Markdown content is required");

        try
        {
            var options = new ConversionOptions
            {
                PageSize = input.PageSize ?? PdfPageSize.A4,
                Orientation = input.Orientation ?? PdfOrientation.Portrait
            };

            var pdfBytes = await _conversionService.MarkdownToPdfAsync(input.Markdown, options);
            var fileName = string.IsNullOrEmpty(input.FileName)
                ? "document.pdf"
                : Path.ChangeExtension(input.FileName, ".pdf");

            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert Markdown to PDF");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Extract elements from a document
    /// </summary>
    [HttpPost("extract-elements")]
    public async Task<IActionResult> ExtractElements(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        try
        {
            await using var stream = file.OpenReadStream();
            var elements = await _conversionService.ExtractElementsAsync(stream, file.FileName);

            return Ok(new
            {
                fileName = file.FileName,
                elementCount = elements.Count,
                elements = elements.Select(e => new
                {
                    type = e.ElementType.ToString(),
                    order = e.Order,
                    content = GetElementContent(e)
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract elements");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Extract text from a document
    /// </summary>
    [HttpPost("extract-text")]
    public async Task<IActionResult> ExtractText(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        try
        {
            await using var stream = file.OpenReadStream();
            var text = await _conversionService.ExtractTextAsync(stream, file.FileName);

            return Ok(new { text, wordCount = CountWords(text) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Extract images from a Word document
    /// </summary>
    [HttpPost("extract-images")]
    public async Task<IActionResult> ExtractImages(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        try
        {
            await using var stream = file.OpenReadStream();
            var images = await _conversionService.ExtractImagesAsync(stream, file.FileName);

            return Ok(new
            {
                imageCount = images.Count,
                images = images.Select((img, i) => new
                {
                    index = i,
                    fileName = img.FileName ?? $"image{i + 1}",
                    contentType = img.ContentType,
                    width = img.Width,
                    height = img.Height,
                    dataUri = img.DataUri
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract images");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get supported formats
    /// </summary>
    [HttpGet("formats")]
    public IActionResult GetFormats()
    {
        return Ok(new
        {
            readable = new[] { "docx", "md", "txt" },
            writable = new[] { "docx", "md", "pdf", "html", "txt" },
            conversions = new[]
            {
                new { from = "docx", to = new[] { "md", "pdf", "html", "txt" } },
                new { from = "md", to = new[] { "docx", "pdf", "html", "txt" } },
                new { from = "txt", to = new[] { "docx", "md", "pdf", "html" } }
            }
        });
    }

    private static string GetElementContent(DocumentElement element)
    {
        return element switch
        {
            HeadingElement h => $"[H{h.Level}] {h.Text}",
            ParagraphElement p => p.Text.Length > 100 ? p.Text[..100] + "..." : p.Text,
            TableElement t => $"[Table: {t.Rows.Count} rows]",
            ListElement l => $"[List: {l.Items.Count} items]",
            CodeBlockElement c => $"[Code: {c.Language ?? "unknown"}] {c.Code.Length} chars",
            ImageElement i => $"[Image: {i.Width}x{i.Height}]",
            _ => element.ElementType.ToString()
        };
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return text.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
    }
}

public class MarkdownInput
{
    public string Markdown { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public PdfPageSize? PageSize { get; set; }
    public PdfOrientation? Orientation { get; set; }
}
