using System.Text;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Renderers.Normalize;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.Extensions.Logging;
using Mostlylucid.DocumentConversion.Interfaces;
using Mostlylucid.DocumentConversion.Models;
using MarkdigTableRow = Markdig.Extensions.Tables.TableRow;
using MarkdigTableCell = Markdig.Extensions.Tables.TableCell;
using ModelTableRow = Mostlylucid.DocumentConversion.Models.TableRow;
using ModelTableCell = Mostlylucid.DocumentConversion.Models.TableCell;

namespace Mostlylucid.DocumentConversion.Services;

/// <summary>
/// Service for converting to and from Markdown format using Markdig
/// </summary>
public class MarkdownConversionService : IMarkdownConversionService
{
    private readonly ILogger<MarkdownConversionService> _logger;
    private readonly MarkdownPipeline _pipeline;
    private readonly MarkdownPipeline _plainTextPipeline;

    public MarkdownConversionService(ILogger<MarkdownConversionService> logger)
    {
        _logger = logger;
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UsePipeTables()
            .UseAutoLinks()
            .UseEmphasisExtras()
            .UseTaskLists()
            .Build();

        _plainTextPipeline = new MarkdownPipelineBuilder()
            .Build();
    }

    public async Task<Document> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        return await ParseAsync(content, Path.GetFileName(filePath), cancellationToken);
    }

    public Task<Document> ParseAsync(string markdown, string? fileName = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var document = new Document
            {
                FileName = fileName,
                SourceFormat = DocFormat.Markdown
            };

            var markdownDoc = Markdown.Parse(markdown, _pipeline);

            int order = 0;
            foreach (var block in markdownDoc)
            {
                var element = ConvertBlock(block, ref order);
                if (element != null)
                {
                    document.Elements.Add(element);
                }
            }

            // Extract title from first heading
            var firstHeading = document.Elements.OfType<HeadingElement>().FirstOrDefault();
            if (firstHeading != null)
            {
                document.Title = firstHeading.Text;
            }

            return document;
        }, cancellationToken);
    }

    public Task<string> ToMarkdownAsync(Document document, ConversionOptions? options = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var sb = new StringBuilder();
            var useGfm = options?.UseGitHubFlavoredMarkdown ?? true;

            foreach (var element in document.Elements)
            {
                var markdown = ConvertElementToMarkdown(element, options, useGfm);
                if (!string.IsNullOrEmpty(markdown))
                {
                    sb.AppendLine(markdown);
                    sb.AppendLine();
                }
            }

            return sb.ToString().TrimEnd();
        }, cancellationToken);
    }

    public Task<string> ToHtmlAsync(string markdown, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Markdown.ToHtml(markdown, _pipeline));
    }

    public async Task<string> ToHtmlAsync(Document document, ConversionOptions? options = null, CancellationToken cancellationToken = default)
    {
        var markdown = await ToMarkdownAsync(document, options, cancellationToken);
        var html = Markdown.ToHtml(markdown, _pipeline);

        // Wrap in HTML document if requested
        if (options?.CustomCss != null)
        {
            html = WrapInHtmlDocument(html, document.Title, options.CustomCss);
        }

        return html;
    }

    public Task<string> ToPlainTextAsync(string markdown, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var doc = Markdown.Parse(markdown, _plainTextPipeline);
            return ExtractPlainText(doc);
        }, cancellationToken);
    }

    private DocumentElement? ConvertBlock(Block block, ref int order)
    {
        return block switch
        {
            HeadingBlock heading => ConvertHeading(heading, ref order),
            ParagraphBlock paragraph => ConvertParagraph(paragraph, ref order),
            FencedCodeBlock codeBlock => ConvertFencedCodeBlock(codeBlock, ref order),
            CodeBlock codeBlock => ConvertCodeBlock(codeBlock, ref order),
            QuoteBlock quote => ConvertQuoteBlock(quote, ref order),
            ListBlock list => ConvertListBlock(list, ref order),
            ThematicBreakBlock => new HorizontalRuleElement { Order = order++ },
            Markdig.Extensions.Tables.Table table => ConvertTable(table, ref order),
            _ => null
        };
    }

    private HeadingElement ConvertHeading(HeadingBlock heading, ref int order)
    {
        var text = GetInlineText(heading.Inline);
        var runs = GetInlineRuns(heading.Inline);

        return new HeadingElement
        {
            Order = order++,
            Level = heading.Level,
            Text = text,
            Runs = runs
        };
    }

    private ParagraphElement ConvertParagraph(ParagraphBlock paragraph, ref int order)
    {
        var text = GetInlineText(paragraph.Inline);
        var runs = GetInlineRuns(paragraph.Inline);

        return new ParagraphElement
        {
            Order = order++,
            Text = text,
            Runs = runs
        };
    }

    private CodeBlockElement ConvertFencedCodeBlock(FencedCodeBlock codeBlock, ref int order)
    {
        var code = string.Join("\n", codeBlock.Lines.Lines.Select(l => l.Slice.ToString()));

        return new CodeBlockElement
        {
            Order = order++,
            Code = code.TrimEnd(),
            Language = codeBlock.Info
        };
    }

    private CodeBlockElement ConvertCodeBlock(CodeBlock codeBlock, ref int order)
    {
        var code = string.Join("\n", codeBlock.Lines.Lines.Select(l => l.Slice.ToString()));

        return new CodeBlockElement
        {
            Order = order++,
            Code = code.TrimEnd()
        };
    }

    private BlockQuoteElement ConvertQuoteBlock(QuoteBlock quote, ref int order)
    {
        var content = new List<DocumentElement>();
        int childOrder = 0;

        foreach (var block in quote)
        {
            var element = ConvertBlock(block, ref childOrder);
            if (element != null)
            {
                content.Add(element);
            }
        }

        var text = string.Join("\n", content
            .Select(e => e switch
            {
                ParagraphElement p => p.Text,
                HeadingElement h => h.Text,
                _ => ""
            })
            .Where(t => !string.IsNullOrEmpty(t)));

        return new BlockQuoteElement
        {
            Order = order++,
            Content = content,
            Text = text
        };
    }

    private ListElement ConvertListBlock(ListBlock list, ref int order)
    {
        var listElement = new ListElement
        {
            Order = order++,
            ListType = list.IsOrdered ? ListType.Numbered : ListType.Bullet,
            StartNumber = list.OrderedStart != null ? int.Parse(list.OrderedStart) : 1
        };

        int itemOrder = 0;
        foreach (var item in list.OfType<ListItemBlock>())
        {
            var itemText = new StringBuilder();
            var runs = new List<TextRun>();

            foreach (var block in item)
            {
                if (block is ParagraphBlock paragraph)
                {
                    itemText.Append(GetInlineText(paragraph.Inline));
                    runs.AddRange(GetInlineRuns(paragraph.Inline));
                }
            }

            // Check for nested lists
            ListElement? nestedList = null;
            foreach (var block in item)
            {
                if (block is ListBlock nested)
                {
                    int nestedOrder = 0;
                    nestedList = ConvertListBlock(nested, ref nestedOrder);
                }
            }

            listElement.Items.Add(new ListItemElement
            {
                Order = itemOrder++,
                Text = itemText.ToString(),
                Runs = runs,
                NestedList = nestedList
            });
        }

        return listElement;
    }

    private TableElement ConvertTable(Markdig.Extensions.Tables.Table table, ref int order)
    {
        var tableElement = new TableElement
        {
            Order = order++
        };

        bool isFirstRow = true;
        foreach (var row in table.OfType<MarkdigTableRow>())
        {
            var tableRow = new ModelTableRow
            {
                IsHeader = row.IsHeader || isFirstRow
            };

            foreach (var cell in row.OfType<MarkdigTableCell>())
            {
                var cellText = new StringBuilder();
                foreach (var block in cell)
                {
                    if (block is ParagraphBlock paragraph)
                    {
                        cellText.Append(GetInlineText(paragraph.Inline));
                    }
                }

                tableRow.Cells.Add(new ModelTableCell
                {
                    Text = cellText.ToString(),
                    ColumnSpan = cell.ColumnSpan,
                    RowSpan = cell.RowSpan
                });
            }

            tableElement.Rows.Add(tableRow);
            if (isFirstRow && tableRow.IsHeader)
            {
                tableElement.HasHeaderRow = true;
            }
            isFirstRow = false;
        }

        return tableElement;
    }

    private string GetInlineText(ContainerInline? inline)
    {
        if (inline == null) return string.Empty;

        var sb = new StringBuilder();
        foreach (var child in inline)
        {
            sb.Append(GetInlineItemText(child));
        }
        return sb.ToString();
    }

    private string GetInlineItemText(Inline inline)
    {
        return inline switch
        {
            LiteralInline literal => literal.Content.ToString(),
            EmphasisInline emphasis => GetContainerInlineText(emphasis),
            CodeInline code => code.Content,
            LinkInline link => GetContainerInlineText(link),
            AutolinkInline autolink => autolink.Url,
            LineBreakInline => "\n",
            HtmlInline html => html.Tag,
            _ => string.Empty
        };
    }

    private string GetContainerInlineText(ContainerInline container)
    {
        var sb = new StringBuilder();
        foreach (var child in container)
        {
            sb.Append(GetInlineItemText(child));
        }
        return sb.ToString();
    }

    private List<TextRun> GetInlineRuns(ContainerInline? inline)
    {
        var runs = new List<TextRun>();
        if (inline == null) return runs;

        foreach (var child in inline)
        {
            runs.AddRange(GetInlineItemRuns(child, false, false, false));
        }

        return runs;
    }

    private List<TextRun> GetInlineItemRuns(Inline inline, bool isBold, bool isItalic, bool isCode)
    {
        var runs = new List<TextRun>();

        switch (inline)
        {
            case LiteralInline literal:
                runs.Add(new TextRun
                {
                    Text = literal.Content.ToString(),
                    IsBold = isBold,
                    IsItalic = isItalic,
                    IsCode = isCode
                });
                break;

            case EmphasisInline emphasis:
                var emphasisBold = isBold || (emphasis.DelimiterCount == 2 && emphasis.DelimiterChar is '*' or '_');
                var emphasisItalic = isItalic || (emphasis.DelimiterCount == 1 && emphasis.DelimiterChar is '*' or '_');
                foreach (var child in emphasis)
                {
                    runs.AddRange(GetInlineItemRuns(child, emphasisBold, emphasisItalic, isCode));
                }
                break;

            case CodeInline code:
                runs.Add(new TextRun
                {
                    Text = code.Content,
                    IsCode = true
                });
                break;

            case LinkInline link:
                var linkText = GetContainerInlineText(link);
                runs.Add(new TextRun
                {
                    Text = linkText,
                    HyperlinkUrl = link.Url,
                    IsUnderline = true
                });
                break;

            case AutolinkInline autolink:
                runs.Add(new TextRun
                {
                    Text = autolink.Url,
                    HyperlinkUrl = autolink.Url,
                    IsUnderline = true
                });
                break;
        }

        return runs;
    }

    private string ConvertElementToMarkdown(DocumentElement element, ConversionOptions? options, bool useGfm)
    {
        return element switch
        {
            HeadingElement h => $"{new string('#', h.Level)} {FormatRuns(h.Runs, h.Text)}",
            ParagraphElement p => FormatRuns(p.Runs, p.Text),
            CodeBlockElement c => FormatCodeBlock(c, useGfm),
            BlockQuoteElement b => FormatBlockQuote(b),
            ListElement l => FormatList(l, options),
            TableElement t => FormatTable(t, useGfm),
            ImageElement i => FormatImage(i, options),
            HorizontalRuleElement => "---",
            HyperlinkElement h => $"[{h.Text}]({h.Url})",
            _ => string.Empty
        };
    }

    private string FormatRuns(List<TextRun> runs, string fallbackText)
    {
        if (runs.Count == 0) return fallbackText;

        var sb = new StringBuilder();
        foreach (var run in runs)
        {
            var text = run.Text;

            if (run.IsCode)
            {
                text = $"`{text}`";
            }
            else
            {
                if (run.IsBold && run.IsItalic)
                {
                    text = $"***{text}***";
                }
                else if (run.IsBold)
                {
                    text = $"**{text}**";
                }
                else if (run.IsItalic)
                {
                    text = $"*{text}*";
                }

                if (run.IsStrikethrough)
                {
                    text = $"~~{text}~~";
                }
            }

            if (!string.IsNullOrEmpty(run.HyperlinkUrl))
            {
                text = $"[{text}]({run.HyperlinkUrl})";
            }

            sb.Append(text);
        }

        return sb.ToString();
    }

    private string FormatCodeBlock(CodeBlockElement codeBlock, bool useGfm)
    {
        if (useGfm)
        {
            var lang = codeBlock.Language ?? string.Empty;
            return $"```{lang}\n{codeBlock.Code}\n```";
        }
        else
        {
            // Indent code block
            var lines = codeBlock.Code.Split('\n');
            return string.Join("\n", lines.Select(l => $"    {l}"));
        }
    }

    private string FormatBlockQuote(BlockQuoteElement blockQuote)
    {
        var lines = blockQuote.Text.Split('\n');
        return string.Join("\n", lines.Select(l => $"> {l}"));
    }

    private string FormatList(ListElement list, ConversionOptions? options, int indentLevel = 0)
    {
        var sb = new StringBuilder();
        var indent = new string(' ', indentLevel * 2);
        int itemNumber = list.StartNumber;

        foreach (var item in list.Items)
        {
            var prefix = list.ListType == ListType.Bullet ? "-" : $"{itemNumber++}.";
            sb.AppendLine($"{indent}{prefix} {FormatRuns(item.Runs, item.Text)}");

            if (item.NestedList != null)
            {
                sb.Append(FormatList(item.NestedList, options, indentLevel + 1));
            }
        }

        return sb.ToString().TrimEnd();
    }

    private string FormatTable(TableElement table, bool useGfm)
    {
        if (!useGfm || table.Rows.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        var firstRow = table.Rows.First();
        var columnCount = firstRow.Cells.Count;

        // Header row
        sb.AppendLine($"| {string.Join(" | ", firstRow.Cells.Select(c => c.Text))} |");

        // Separator row
        sb.AppendLine($"| {string.Join(" | ", Enumerable.Repeat("---", columnCount))} |");

        // Data rows
        foreach (var row in table.Rows.Skip(table.HasHeaderRow ? 1 : 0))
        {
            var cells = row.Cells.Select(c => c.Text).ToList();
            // Pad if necessary
            while (cells.Count < columnCount) cells.Add(string.Empty);
            sb.AppendLine($"| {string.Join(" | ", cells)} |");
        }

        return sb.ToString().TrimEnd();
    }

    private string FormatImage(ImageElement image, ConversionOptions? options)
    {
        var alt = image.AltText ?? "image";

        if (options?.EmbedImagesAsDataUri == true && image.DataUri != null)
        {
            return $"![{alt}]({image.DataUri})";
        }
        else if (!string.IsNullOrEmpty(image.FileName))
        {
            return $"![{alt}]({image.FileName})";
        }

        return string.Empty;
    }

    private string ExtractPlainText(MarkdownDocument document)
    {
        var sb = new StringBuilder();

        foreach (var block in document)
        {
            ExtractBlockText(block, sb);
            sb.AppendLine();
        }

        return sb.ToString().Trim();
    }

    private void ExtractBlockText(Block block, StringBuilder sb)
    {
        switch (block)
        {
            case LeafBlock leaf when leaf.Inline != null:
                sb.Append(GetInlineText(leaf.Inline));
                break;
            case ContainerBlock container:
                foreach (var child in container)
                {
                    ExtractBlockText(child, sb);
                }
                break;
        }
    }

    private string WrapInHtmlDocument(string bodyHtml, string? title, string css)
    {
        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>{title ?? "Document"}</title>
    <style>
{css}
    </style>
</head>
<body>
{bodyHtml}
</body>
</html>";
    }
}
