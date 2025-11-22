using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using Mostlylucid.DocumentConversion.Interfaces;
using Mostlylucid.DocumentConversion.Models;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using Document = Mostlylucid.DocumentConversion.Models.Document;
using TextAlignment = Mostlylucid.DocumentConversion.Models.TextAlignment;
using ModelTableRow = Mostlylucid.DocumentConversion.Models.TableRow;
using ModelTableCell = Mostlylucid.DocumentConversion.Models.TableCell;
using OoxTableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;
using OoxTableCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;

namespace Mostlylucid.DocumentConversion.Services;

/// <summary>
/// Service for reading and writing Word documents using Open XML SDK
/// </summary>
public class WordDocumentService : IWordDocumentService
{
    private readonly ILogger<WordDocumentService> _logger;

    public WordDocumentService(ILogger<WordDocumentService> logger)
    {
        _logger = logger;
    }

    public async Task<Document> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        return await ReadAsync(stream, Path.GetFileName(filePath), cancellationToken);
    }

    public async Task<Document> ReadAsync(byte[] data, string? fileName = null, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(data);
        return await ReadAsync(stream, fileName, cancellationToken);
    }

    public Task<Document> ReadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var document = new Document
            {
                FileName = fileName,
                SourceFormat = DocFormat.Word
            };

            using var wordDoc = WordprocessingDocument.Open(stream, false);
            var mainPart = wordDoc.MainDocumentPart;
            if (mainPart == null)
            {
                _logger.LogWarning("Word document has no main document part");
                return document;
            }

            // Extract metadata
            ExtractMetadata(wordDoc, document);

            // Extract body elements
            var body = mainPart.Document?.Body;
            if (body != null)
            {
                int order = 0;
                foreach (var element in body.Elements())
                {
                    var extracted = ExtractElement(element, mainPart, ref order);
                    if (extracted != null)
                    {
                        document.Elements.Add(extracted);
                    }
                }
            }

            // Extract images
            document.Images.AddRange(ExtractAllImages(mainPart));

            return document;
        }, cancellationToken);
    }

    public async Task<byte[]> CreateAsync(Document document, ConversionOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            using var stream = new MemoryStream();
            using (var wordDoc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
            {
                var mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
                var body = mainPart.Document.AppendChild(new Body());

                // Add metadata
                AddMetadata(wordDoc, document);

                // Add styles
                AddStyles(mainPart);

                // Convert elements to Word
                foreach (var element in document.Elements)
                {
                    AddElement(body, mainPart, element, options);
                }

                mainPart.Document.Save();
            }

            return stream.ToArray();
        }, cancellationToken);
    }

    public async Task WriteAsync(Document document, string filePath, ConversionOptions? options = null, CancellationToken cancellationToken = default)
    {
        var bytes = await CreateAsync(document, options, cancellationToken);
        await File.WriteAllBytesAsync(filePath, bytes, cancellationToken);
    }

    public Task<string> ExtractTextAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            using var wordDoc = WordprocessingDocument.Open(stream, false);
            var body = wordDoc.MainDocumentPart?.Document?.Body;
            if (body == null) return string.Empty;

            return string.Join("\n", body.Descendants<Text>().Select(t => t.Text));
        }, cancellationToken);
    }

    public Task<List<ImageElement>> ExtractImagesAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            using var wordDoc = WordprocessingDocument.Open(stream, false);
            var mainPart = wordDoc.MainDocumentPart;
            if (mainPart == null) return [];

            return ExtractAllImages(mainPart);
        }, cancellationToken);
    }

    private void ExtractMetadata(WordprocessingDocument wordDoc, Document document)
    {
        var coreProps = wordDoc.PackageProperties;
        document.Title = coreProps.Title;
        document.Author = coreProps.Creator;
        document.Subject = coreProps.Subject;
        document.CreatedDate = coreProps.Created;
        document.ModifiedDate = coreProps.Modified;

        if (!string.IsNullOrEmpty(coreProps.Keywords))
        {
            document.Keywords = coreProps.Keywords.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim()).ToList();
        }
    }

    private DocumentElement? ExtractElement(OpenXmlElement element, MainDocumentPart mainPart, ref int order)
    {
        return element switch
        {
            Paragraph p => ExtractParagraph(p, mainPart, ref order),
            Table t => ExtractTable(t, mainPart, ref order),
            _ => null
        };
    }

    private DocumentElement? ExtractParagraph(Paragraph paragraph, MainDocumentPart mainPart, ref int order)
    {
        var styleName = GetStyleName(paragraph, mainPart);
        var headingLevel = GetHeadingLevel(styleName);

        var runs = ExtractRuns(paragraph, mainPart);
        var text = string.Join("", runs.Select(r => r.Text));

        if (string.IsNullOrWhiteSpace(text) && !HasImage(paragraph))
        {
            return null;
        }

        // Check for images
        var imageElement = ExtractImageFromParagraph(paragraph, mainPart, ref order);
        if (imageElement != null)
        {
            return imageElement;
        }

        if (headingLevel > 0)
        {
            return new HeadingElement
            {
                Order = order++,
                Level = headingLevel,
                Text = text,
                Runs = runs
            };
        }

        return new ParagraphElement
        {
            Order = order++,
            Text = text,
            Runs = runs,
            StyleName = styleName,
            Alignment = GetAlignment(paragraph)
        };
    }

    private List<TextRun> ExtractRuns(Paragraph paragraph, MainDocumentPart mainPart)
    {
        var textRuns = new List<TextRun>();

        foreach (var run in paragraph.Elements<Run>())
        {
            var text = string.Join("", run.Elements<Text>().Select(t => t.Text));
            if (string.IsNullOrEmpty(text)) continue;

            var props = run.RunProperties;

            textRuns.Add(new TextRun
            {
                Text = text,
                IsBold = props?.Bold != null || props?.BoldComplexScript != null,
                IsItalic = props?.Italic != null || props?.ItalicComplexScript != null,
                IsUnderline = props?.Underline != null && props.Underline.Val?.Value != UnderlineValues.None,
                IsStrikethrough = props?.Strike != null || props?.DoubleStrike != null,
                FontName = props?.RunFonts?.Ascii?.Value,
                FontSize = props?.FontSize?.Val != null ? double.Parse(props.FontSize.Val.Value) / 2 : null,
                Color = props?.Color?.Val?.Value,
                HighlightColor = props?.Highlight?.Val?.ToString()
            });
        }

        // Check for hyperlinks
        foreach (var hyperlink in paragraph.Elements<Hyperlink>())
        {
            var relationshipId = hyperlink.Id?.Value;
            string? url = null;

            if (relationshipId != null)
            {
                var relationship = mainPart.HyperlinkRelationships
                    .FirstOrDefault(r => r.Id == relationshipId);
                url = relationship?.Uri?.ToString();
            }

            foreach (var run in hyperlink.Elements<Run>())
            {
                var text = string.Join("", run.Elements<Text>().Select(t => t.Text));
                if (string.IsNullOrEmpty(text)) continue;

                textRuns.Add(new TextRun
                {
                    Text = text,
                    HyperlinkUrl = url,
                    IsUnderline = true,
                    Color = "#0000FF" // Default link color
                });
            }
        }

        return textRuns;
    }

    private TableElement ExtractTable(Table table, MainDocumentPart mainPart, ref int order)
    {
        var tableElement = new TableElement
        {
            Order = order++
        };

        bool isFirstRow = true;
        foreach (var row in table.Elements<OoxTableRow>())
        {
            var tableRow = new ModelTableRow
            {
                IsHeader = isFirstRow
            };

            foreach (var cell in row.Elements<OoxTableCell>())
            {
                var cellText = string.Join(" ", cell.Descendants<Text>().Select(t => t.Text));
                tableRow.Cells.Add(new ModelTableCell
                {
                    Text = cellText
                });
            }

            tableElement.Rows.Add(tableRow);
            if (isFirstRow)
            {
                tableElement.HasHeaderRow = true;
                isFirstRow = false;
            }
        }

        return tableElement;
    }

    private ImageElement? ExtractImageFromParagraph(Paragraph paragraph, MainDocumentPart mainPart, ref int order)
    {
        var drawing = paragraph.Descendants<Drawing>().FirstOrDefault();
        if (drawing == null) return null;

        var blip = drawing.Descendants<A.Blip>().FirstOrDefault();
        if (blip?.Embed?.Value == null) return null;

        var imagePart = mainPart.GetPartById(blip.Embed.Value) as ImagePart;
        if (imagePart == null) return null;

        using var imageStream = imagePart.GetStream();
        using var memoryStream = new MemoryStream();
        imageStream.CopyTo(memoryStream);

        var extent = drawing.Descendants<DW.Extent>().FirstOrDefault();

        return new ImageElement
        {
            Order = order++,
            Data = memoryStream.ToArray(),
            ContentType = imagePart.ContentType,
            Width = extent?.Cx != null ? (int)(extent.Cx.Value / 9525) : null, // EMUs to pixels
            Height = extent?.Cy != null ? (int)(extent.Cy.Value / 9525) : null
        };
    }

    private List<ImageElement> ExtractAllImages(MainDocumentPart mainPart)
    {
        var images = new List<ImageElement>();
        int imageIndex = 0;

        foreach (var imagePart in mainPart.ImageParts)
        {
            using var imageStream = imagePart.GetStream();
            using var memoryStream = new MemoryStream();
            imageStream.CopyTo(memoryStream);

            var extension = GetImageExtension(imagePart.ContentType);
            images.Add(new ImageElement
            {
                Data = memoryStream.ToArray(),
                ContentType = imagePart.ContentType,
                FileName = $"image{++imageIndex}{extension}"
            });
        }

        return images;
    }

    private bool HasImage(Paragraph paragraph)
    {
        return paragraph.Descendants<Drawing>().Any();
    }

    private string? GetStyleName(Paragraph paragraph, MainDocumentPart mainPart)
    {
        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        if (styleId == null) return null;

        var styles = mainPart.StyleDefinitionsPart?.Styles;
        var style = styles?.Elements<Style>().FirstOrDefault(s => s.StyleId?.Value == styleId);
        return style?.StyleName?.Val?.Value ?? styleId;
    }

    private int GetHeadingLevel(string? styleName)
    {
        if (string.IsNullOrEmpty(styleName)) return 0;

        // Check for "Heading 1", "Heading 2", etc.
        if (styleName.StartsWith("Heading ", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(styleName.AsSpan(8), out var level) && level is >= 1 and <= 6)
            {
                return level;
            }
        }

        // Check for style IDs like "Heading1", "Heading2"
        if (styleName.StartsWith("Heading", StringComparison.OrdinalIgnoreCase) && styleName.Length == 8)
        {
            if (int.TryParse(styleName.AsSpan(7), out var level) && level is >= 1 and <= 6)
            {
                return level;
            }
        }

        return 0;
    }

    private TextAlignment GetAlignment(Paragraph paragraph)
    {
        var justification = paragraph.ParagraphProperties?.Justification?.Val?.Value;
        if (justification == JustificationValues.Center) return TextAlignment.Center;
        if (justification == JustificationValues.Right) return TextAlignment.Right;
        if (justification == JustificationValues.Both) return TextAlignment.Justify;
        return TextAlignment.Left;
    }

    private string GetImageExtension(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/gif" => ".gif",
            "image/bmp" => ".bmp",
            "image/tiff" => ".tiff",
            "image/svg+xml" => ".svg",
            _ => ".bin"
        };
    }

    private void AddMetadata(WordprocessingDocument wordDoc, Document document)
    {
        wordDoc.PackageProperties.Title = document.Title;
        wordDoc.PackageProperties.Creator = document.Author;
        wordDoc.PackageProperties.Subject = document.Subject;
        wordDoc.PackageProperties.Created = document.CreatedDate ?? DateTime.Now;
        wordDoc.PackageProperties.Modified = DateTime.Now;

        if (document.Keywords.Count != 0)
        {
            wordDoc.PackageProperties.Keywords = string.Join(", ", document.Keywords);
        }
    }

    private void AddStyles(MainDocumentPart mainPart)
    {
        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        var styles = new Styles();

        // Add heading styles
        for (int i = 1; i <= 6; i++)
        {
            var style = new Style
            {
                Type = StyleValues.Paragraph,
                StyleId = $"Heading{i}",
                StyleName = new StyleName { Val = $"Heading {i}" }
            };

            var pPr = new StyleParagraphProperties();
            var rPr = new StyleRunProperties
            {
                Bold = new Bold(),
                FontSize = new FontSize { Val = ((32 - (i * 4)) * 2).ToString() }
            };

            style.Append(pPr);
            style.Append(rPr);
            styles.Append(style);
        }

        stylesPart.Styles = styles;
    }

    private void AddElement(Body body, MainDocumentPart mainPart, DocumentElement element, ConversionOptions? options)
    {
        switch (element)
        {
            case HeadingElement heading:
                AddHeading(body, heading);
                break;
            case ParagraphElement paragraph:
                AddParagraph(body, paragraph, options);
                break;
            case TableElement table:
                AddTable(body, table);
                break;
            case ListElement list:
                AddList(body, list);
                break;
            case ImageElement image when options?.IncludeImages != false:
                AddImage(body, mainPart, image);
                break;
            case CodeBlockElement codeBlock:
                AddCodeBlock(body, codeBlock);
                break;
            case BlockQuoteElement blockQuote:
                AddBlockQuote(body, blockQuote);
                break;
            case HorizontalRuleElement:
                AddHorizontalRule(body);
                break;
        }
    }

    private void AddHeading(Body body, HeadingElement heading)
    {
        var paragraph = new Paragraph();
        var props = new ParagraphProperties
        {
            ParagraphStyleId = new ParagraphStyleId { Val = $"Heading{heading.Level}" }
        };
        paragraph.Append(props);

        if (heading.Runs.Count != 0)
        {
            foreach (var textRun in heading.Runs)
            {
                paragraph.Append(CreateRun(textRun));
            }
        }
        else
        {
            var run = new Run(new Text(heading.Text));
            paragraph.Append(run);
        }

        body.Append(paragraph);
    }

    private void AddParagraph(Body body, ParagraphElement para, ConversionOptions? options)
    {
        var paragraph = new Paragraph();
        var props = new ParagraphProperties();

        if (para.Alignment != TextAlignment.Left)
        {
            props.Justification = new Justification
            {
                Val = para.Alignment switch
                {
                    TextAlignment.Center => JustificationValues.Center,
                    TextAlignment.Right => JustificationValues.Right,
                    TextAlignment.Justify => JustificationValues.Both,
                    _ => JustificationValues.Left
                }
            };
        }

        paragraph.Append(props);

        if (para.Runs.Count != 0 && options?.PreserveFormatting != false)
        {
            foreach (var textRun in para.Runs)
            {
                paragraph.Append(CreateRun(textRun));
            }
        }
        else
        {
            var run = new Run(new Text(para.Text));
            paragraph.Append(run);
        }

        body.Append(paragraph);
    }

    private Run CreateRun(TextRun textRun)
    {
        var run = new Run();
        var props = new RunProperties();

        if (textRun.IsBold) props.Bold = new Bold();
        if (textRun.IsItalic) props.Italic = new Italic();
        if (textRun.IsUnderline) props.Underline = new Underline { Val = UnderlineValues.Single };
        if (textRun.IsStrikethrough) props.Strike = new Strike();
        if (!string.IsNullOrEmpty(textRun.Color)) props.Color = new Color { Val = textRun.Color.TrimStart('#') };
        if (!string.IsNullOrEmpty(textRun.FontName)) props.RunFonts = new RunFonts { Ascii = textRun.FontName };
        if (textRun.FontSize.HasValue) props.FontSize = new FontSize { Val = ((int)(textRun.FontSize.Value * 2)).ToString() };

        run.Append(props);
        run.Append(new Text(textRun.Text) { Space = SpaceProcessingModeValues.Preserve });

        return run;
    }

    private void AddTable(Body body, TableElement tableElement)
    {
        var table = new Table();

        var tableProps = new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Size = 4 },
                new LeftBorder { Val = BorderValues.Single, Size = 4 },
                new RightBorder { Val = BorderValues.Single, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }
            )
        );
        table.Append(tableProps);

        foreach (var rowElement in tableElement.Rows)
        {
            var row = new DocumentFormat.OpenXml.Wordprocessing.TableRow();

            foreach (var cellElement in rowElement.Cells)
            {
                var cell = new DocumentFormat.OpenXml.Wordprocessing.TableCell();
                var cellParagraph = new Paragraph(new Run(new Text(cellElement.Text)));

                if (rowElement.IsHeader)
                {
                    var runProps = cellParagraph.Descendants<Run>().First().PrependChild(new RunProperties());
                    runProps.Bold = new Bold();
                }

                cell.Append(cellParagraph);
                row.Append(cell);
            }

            table.Append(row);
        }

        body.Append(table);
        body.Append(new Paragraph()); // Add spacing after table
    }

    private void AddList(Body body, ListElement listElement)
    {
        foreach (var item in listElement.Items)
        {
            var paragraph = new Paragraph();
            var prefix = listElement.ListType == ListType.Bullet ? "• " : $"{listElement.StartNumber + listElement.Items.IndexOf(item)}. ";
            var run = new Run(new Text(prefix + item.Text));
            paragraph.Append(run);
            body.Append(paragraph);
        }
    }

    private void AddImage(Body body, MainDocumentPart mainPart, ImageElement imageElement)
    {
        if (imageElement.Data == null) return;

        var imagePart = mainPart.AddImagePart(GetImagePartType(imageElement.ContentType));
        using var stream = new MemoryStream(imageElement.Data);
        imagePart.FeedData(stream);

        var relationshipId = mainPart.GetIdOfPart(imagePart);
        var width = (imageElement.Width ?? 400) * 9525L; // Pixels to EMUs
        var height = (imageElement.Height ?? 300) * 9525L;

        var element = new Drawing(
            new DW.Inline(
                new DW.Extent { Cx = width, Cy = height },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties { Id = 1U, Name = imageElement.FileName ?? "Image" },
                new DW.NonVisualGraphicFrameDrawingProperties(
                    new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = 0U, Name = imageElement.FileName ?? "Image" },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = relationshipId },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0L, Y = 0L },
                                    new A.Extents { Cx = width, Cy = height }),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }))
                    ) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })
            ) { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U }
        );

        var paragraph = new Paragraph(new Run(element));
        body.Append(paragraph);
    }

    private PartTypeInfo GetImagePartType(string? contentType)
    {
        return contentType?.ToLowerInvariant() switch
        {
            "image/png" => ImagePartType.Png,
            "image/jpeg" or "image/jpg" => ImagePartType.Jpeg,
            "image/gif" => ImagePartType.Gif,
            "image/bmp" => ImagePartType.Bmp,
            "image/tiff" => ImagePartType.Tiff,
            _ => ImagePartType.Png
        };
    }

    private void AddCodeBlock(Body body, CodeBlockElement codeBlock)
    {
        // Add code block as a paragraph with monospace font
        var paragraph = new Paragraph();
        var props = new ParagraphProperties
        {
            Shading = new Shading { Val = ShadingPatternValues.Clear, Fill = "F5F5F5" }
        };
        paragraph.Append(props);

        var run = new Run();
        var runProps = new RunProperties
        {
            RunFonts = new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" },
            FontSize = new FontSize { Val = "20" }
        };
        run.Append(runProps);
        run.Append(new Text(codeBlock.Code) { Space = SpaceProcessingModeValues.Preserve });

        paragraph.Append(run);
        body.Append(paragraph);
    }

    private void AddBlockQuote(Body body, BlockQuoteElement blockQuote)
    {
        var paragraph = new Paragraph();
        var props = new ParagraphProperties
        {
            Indentation = new Indentation { Left = "720" }, // 0.5 inch
            ParagraphBorders = new ParagraphBorders
            {
                LeftBorder = new LeftBorder { Val = BorderValues.Single, Size = 12, Color = "CCCCCC" }
            }
        };
        paragraph.Append(props);

        var run = new Run();
        var runProps = new RunProperties { Italic = new Italic() };
        run.Append(runProps);
        run.Append(new Text(blockQuote.Text) { Space = SpaceProcessingModeValues.Preserve });

        paragraph.Append(run);
        body.Append(paragraph);
    }

    private void AddHorizontalRule(Body body)
    {
        var paragraph = new Paragraph();
        var props = new ParagraphProperties
        {
            ParagraphBorders = new ParagraphBorders
            {
                BottomBorder = new BottomBorder { Val = BorderValues.Single, Size = 6, Space = 1 }
            }
        };
        paragraph.Append(props);
        body.Append(paragraph);
    }
}
