using Microsoft.Extensions.Logging;
using Mostlylucid.DocumentConversion.Interfaces;
using Mostlylucid.DocumentConversion.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Document = Mostlylucid.DocumentConversion.Models.Document;

namespace Mostlylucid.DocumentConversion.Services;

/// <summary>
/// Service for generating PDF documents using QuestPDF
/// </summary>
public class PdfConversionService : IPdfConversionService
{
    private readonly ILogger<PdfConversionService> _logger;
    private readonly IMarkdownConversionService _markdownService;

    // Font configuration
    private const string DefaultFont = "Arial";
    private const string MonospaceFont = "Courier New";
    private const float BaseFontSize = 11f;
    private const float LineHeight = 1.4f;

    public PdfConversionService(
        ILogger<PdfConversionService> logger,
        IMarkdownConversionService markdownService)
    {
        _logger = logger;
        _markdownService = markdownService;

        // Configure QuestPDF license (Community license is free)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> ToPdfAsync(Document document, ConversionOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var pdfDocument = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    ConfigurePage(page, options);

                    page.Header().Element(header => RenderHeader(header, document));
                    page.Content().Element(content => RenderContent(content, document, options));
                    page.Footer().Element(footer => RenderFooter(footer));
                });
            });

            return pdfDocument.GeneratePdf();
        }, cancellationToken);
    }

    public async Task ToPdfFileAsync(Document document, string filePath, ConversionOptions? options = null, CancellationToken cancellationToken = default)
    {
        var bytes = await ToPdfAsync(document, options, cancellationToken);
        await File.WriteAllBytesAsync(filePath, bytes, cancellationToken);
    }

    public async Task<byte[]> MarkdownToPdfAsync(string markdown, ConversionOptions? options = null, CancellationToken cancellationToken = default)
    {
        var document = await _markdownService.ParseAsync(markdown, null, cancellationToken);
        return await ToPdfAsync(document, options, cancellationToken);
    }

    public async Task<byte[]> HtmlToPdfAsync(string html, ConversionOptions? options = null, CancellationToken cancellationToken = default)
    {
        // For HTML, we'll create a simple document with the HTML as text
        // Note: Full HTML rendering would require a more complex solution
        _logger.LogWarning("HTML to PDF conversion provides basic text extraction only. For full HTML rendering, consider using a browser-based solution.");

        var document = new Document
        {
            SourceFormat = DocFormat.Html,
            Elements =
            [
                new ParagraphElement
                {
                    Text = StripHtmlTags(html),
                    Order = 0
                }
            ]
        };

        return await ToPdfAsync(document, options, cancellationToken);
    }

    private void ConfigurePage(PageDescriptor page, ConversionOptions? options)
    {
        var pageSize = options?.PageSize ?? PdfPageSize.A4;
        var orientation = options?.Orientation ?? PdfOrientation.Portrait;

        var size = pageSize switch
        {
            PdfPageSize.A3 => PageSizes.A3,
            PdfPageSize.A5 => PageSizes.A5,
            PdfPageSize.Letter => PageSizes.Letter,
            PdfPageSize.Legal => PageSizes.Legal,
            _ => PageSizes.A4
        };

        if (orientation == PdfOrientation.Landscape)
        {
            size = size.Landscape();
        }

        page.Size(size);
        page.Margin(50);
        page.DefaultTextStyle(style => style
            .FontFamily(DefaultFont)
            .FontSize(BaseFontSize)
            .LineHeight(LineHeight));
    }

    private void RenderHeader(IContainer container, Document document)
    {
        if (string.IsNullOrEmpty(document.Title)) return;

        container
            .PaddingBottom(10)
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten1)
            .Text(document.Title)
            .FontSize(10)
            .FontColor(Colors.Grey.Medium);
    }

    private void RenderFooter(IContainer container)
    {
        container
            .AlignCenter()
            .Text(text =>
            {
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
    }

    private void RenderContent(IContainer container, Document document, ConversionOptions? options)
    {
        container.Column(column =>
        {
            column.Spacing(10);

            foreach (var element in document.Elements)
            {
                column.Item().Element(item => RenderElement(item, element, options));
            }
        });
    }

    private void RenderElement(IContainer container, DocumentElement element, ConversionOptions? options)
    {
        switch (element)
        {
            case HeadingElement heading:
                RenderHeading(container, heading);
                break;
            case ParagraphElement paragraph:
                RenderParagraph(container, paragraph, options);
                break;
            case CodeBlockElement codeBlock:
                RenderCodeBlock(container, codeBlock);
                break;
            case BlockQuoteElement blockQuote:
                RenderBlockQuote(container, blockQuote);
                break;
            case ListElement list:
                RenderList(container, list);
                break;
            case TableElement table when options?.IncludeTables != false:
                RenderTable(container, table);
                break;
            case ImageElement image when options?.IncludeImages != false:
                RenderImage(container, image);
                break;
            case HorizontalRuleElement:
                RenderHorizontalRule(container);
                break;
        }
    }

    private void RenderHeading(IContainer container, HeadingElement heading)
    {
        var fontSize = heading.Level switch
        {
            1 => 24f,
            2 => 20f,
            3 => 16f,
            4 => 14f,
            5 => 12f,
            _ => 11f
        };

        container.Text(text =>
        {
            if (heading.Runs.Count > 0)
            {
                RenderTextRuns(text, heading.Runs, fontSize, true);
            }
            else
            {
                text.Span(heading.Text)
                    .FontSize(fontSize)
                    .Bold();
            }
        });
    }

    private void RenderParagraph(IContainer container, ParagraphElement paragraph, ConversionOptions? options)
    {
        container.Text(text =>
        {
            if (paragraph.Runs.Count > 0 && options?.PreserveFormatting != false)
            {
                RenderTextRuns(text, paragraph.Runs, BaseFontSize, false);
            }
            else
            {
                text.Span(paragraph.Text);
            }
        });
    }

    private void RenderTextRuns(TextDescriptor text, List<TextRun> runs, float baseFontSize, bool isHeading)
    {
        foreach (var run in runs)
        {
            var span = text.Span(run.Text);

            span.FontSize(run.FontSize.HasValue ? (float)run.FontSize.Value : baseFontSize);

            if (run.IsBold || isHeading) span.Bold();
            if (run.IsItalic) span.Italic();
            if (run.IsUnderline) span.Underline();
            if (run.IsStrikethrough) span.Strikethrough();

            if (run.IsCode)
            {
                span.FontFamily(MonospaceFont)
                    .BackgroundColor(Colors.Grey.Lighten3);
            }

            if (!string.IsNullOrEmpty(run.Color) && TryParseColor(run.Color, out var color))
            {
                span.FontColor(color);
            }

            if (!string.IsNullOrEmpty(run.HyperlinkUrl))
            {
                span.FontColor(Colors.Blue.Medium)
                    .Underline();
            }

            if (!string.IsNullOrEmpty(run.FontName))
            {
                span.FontFamily(run.FontName);
            }
        }
    }

    private void RenderCodeBlock(IContainer container, CodeBlockElement codeBlock)
    {
        container
            .Background(Colors.Grey.Lighten4)
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(10)
            .Text(text =>
            {
                text.Span(codeBlock.Code)
                    .FontFamily(MonospaceFont)
                    .FontSize(10);
            });
    }

    private void RenderBlockQuote(IContainer container, BlockQuoteElement blockQuote)
    {
        container
            .BorderLeft(3)
            .BorderColor(Colors.Grey.Lighten1)
            .PaddingLeft(15)
            .Text(text =>
            {
                text.Span(blockQuote.Text)
                    .Italic()
                    .FontColor(Colors.Grey.Darken1);
            });
    }

    private void RenderList(IContainer container, ListElement list, int indentLevel = 0)
    {
        container.Column(column =>
        {
            column.Spacing(5);

            int itemNumber = list.StartNumber;
            foreach (var item in list.Items)
            {
                column.Item().Row(row =>
                {
                    // Indent
                    if (indentLevel > 0)
                    {
                        row.ConstantItem(indentLevel * 20);
                    }

                    // Bullet or number
                    var prefix = list.ListType switch
                    {
                        ListType.Bullet => "•",
                        ListType.Numbered => $"{itemNumber++}.",
                        ListType.LetterLower => $"{(char)('a' + itemNumber++ - 1)}.",
                        ListType.LetterUpper => $"{(char)('A' + itemNumber++ - 1)}.",
                        _ => "•"
                    };

                    row.ConstantItem(25).Text(prefix);
                    row.RelativeItem().Text(text =>
                    {
                        if (item.Runs.Count > 0)
                        {
                            RenderTextRuns(text, item.Runs, BaseFontSize, false);
                        }
                        else
                        {
                            text.Span(item.Text);
                        }
                    });
                });

                // Nested list
                if (item.NestedList != null)
                {
                    column.Item().Element(nested =>
                        RenderList(nested, item.NestedList, indentLevel + 1));
                }
            }
        });
    }

    private void RenderTable(IContainer container, TableElement tableElement)
    {
        if (tableElement.Rows.Count == 0) return;

        var columnCount = tableElement.Rows.Max(r => r.Cells.Count);
        if (columnCount == 0) return;

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                for (int i = 0; i < columnCount; i++)
                {
                    columns.RelativeColumn();
                }
            });

            foreach (var row in tableElement.Rows)
            {
                foreach (var cell in row.Cells)
                {
                    var cellElement = table.Cell()
                        .Border(1)
                        .BorderColor(Colors.Grey.Lighten1)
                        .Padding(5);

                    if (row.IsHeader)
                    {
                        cellElement
                            .Background(Colors.Grey.Lighten3)
                            .Text(cell.Text)
                            .Bold();
                    }
                    else
                    {
                        cellElement.Text(cell.Text);
                    }
                }

                // Pad with empty cells if necessary
                for (int i = row.Cells.Count; i < columnCount; i++)
                {
                    table.Cell()
                        .Border(1)
                        .BorderColor(Colors.Grey.Lighten1)
                        .Padding(5)
                        .Text(string.Empty);
                }
            }
        });
    }

    private void RenderImage(IContainer container, ImageElement image)
    {
        if (image.Data == null || image.Data.Length == 0) return;

        try
        {
            container
                .AlignCenter()
                .MaxWidth(400)
                .Image(image.Data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to render image in PDF: {FileName}", image.FileName);
            container.Text($"[Image: {image.FileName ?? "unnamed"}]")
                .Italic()
                .FontColor(Colors.Grey.Medium);
        }
    }

    private void RenderHorizontalRule(IContainer container)
    {
        container
            .PaddingVertical(10)
            .LineHorizontal(1)
            .LineColor(Colors.Grey.Lighten1);
    }

    private bool TryParseColor(string colorString, out string color)
    {
        color = Colors.Black;

        if (string.IsNullOrEmpty(colorString)) return false;

        // Handle hex colors
        if (colorString.StartsWith('#'))
        {
            color = colorString;
            return true;
        }

        // Handle named colors (basic mapping)
        color = colorString.ToLowerInvariant() switch
        {
            "red" => Colors.Red.Medium,
            "blue" => Colors.Blue.Medium,
            "green" => Colors.Green.Medium,
            "yellow" => Colors.Yellow.Medium,
            "orange" => Colors.Orange.Medium,
            "purple" => Colors.Purple.Medium,
            "black" => Colors.Black,
            "white" => Colors.White,
            "grey" or "gray" => Colors.Grey.Medium,
            _ => Colors.Black
        };

        return true;
    }

    private string StripHtmlTags(string html)
    {
        // Basic HTML tag stripping
        var result = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", " ");
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\s+", " ");
        return System.Net.WebUtility.HtmlDecode(result).Trim();
    }
}
