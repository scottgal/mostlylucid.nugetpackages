using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.Extensions.Logging;
using mostlylucid.llmslidetranslator.Models;

namespace mostlylucid.llmslidetranslator.Services;

/// <summary>
///     Chunks markdown documents into translatable blocks
/// </summary>
public class MarkdownChunker(ILogger<MarkdownChunker> logger) : IMarkdownChunker
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public Task<List<TranslationBlock>> ChunkAsync(
        string markdown,
        string documentId,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Chunking markdown document {DocumentId}", documentId);

        var document = Markdown.Parse(markdown, _pipeline);
        var blocks = new List<TranslationBlock>();
        var index = 0;

        foreach (var block in document)
        {
            var translationBlock = CreateTranslationBlock(
                block,
                documentId,
                sourceLanguage,
                targetLanguage,
                index);

            if (translationBlock != null)
            {
                blocks.Add(translationBlock);
                index++;
            }
        }

        logger.LogInformation("Created {Count} translation blocks from document {DocumentId}",
            blocks.Count, documentId);

        return Task.FromResult(blocks);
    }

    private TranslationBlock? CreateTranslationBlock(
        Block block,
        string documentId,
        string sourceLanguage,
        string targetLanguage,
        int index)
    {
        var text = ExtractText(block);
        if (string.IsNullOrWhiteSpace(text)) return null;

        var blockType = DetermineBlockType(block);
        var shouldTranslate = ShouldTranslateBlock(block, blockType);

        return new TranslationBlock
        {
            BlockId = $"{documentId}_{index}",
            Index = index,
            DocumentId = documentId,
            Text = text.Trim(),
            SourceLanguage = sourceLanguage,
            TargetLanguage = targetLanguage,
            BlockType = blockType,
            ShouldTranslate = shouldTranslate
        };
    }

    private string ExtractText(Block block)
    {
        return block switch
        {
            HeadingBlock heading => ExtractInlineText(heading.Inline),
            ParagraphBlock paragraph => ExtractInlineText(paragraph.Inline),
            LeafBlock leafBlock => leafBlock.Lines.ToString(),
            ContainerBlock containerBlock => string.Join("\n",
                containerBlock.SelectMany(b => ExtractBlockText(b))),
            _ => string.Empty
        };
    }

    private IEnumerable<string> ExtractBlockText(Block block)
    {
        var text = ExtractText(block);
        if (!string.IsNullOrWhiteSpace(text))
            yield return text;
    }

    private string ExtractInlineText(ContainerInline? inline)
    {
        if (inline == null) return string.Empty;

        var texts = new List<string>();
        foreach (var item in inline)
        {
            var text = item switch
            {
                LiteralInline literal => literal.Content.ToString(),
                CodeInline code => $"`{code.Content}`",
                LinkInline link => ExtractInlineText(link),
                EmphasisInline emphasis => ExtractInlineText(emphasis),
                _ => string.Empty
            };
            if (!string.IsNullOrEmpty(text))
                texts.Add(text);
        }
        return string.Join("", texts);
    }

    private string DetermineBlockType(Block block)
    {
        return block switch
        {
            HeadingBlock => "heading",
            FencedCodeBlock => "code", // More specific type must come before CodeBlock
            CodeBlock => "code",
            QuoteBlock => "quote",
            ListBlock => "list",
            ParagraphBlock => "paragraph",
            _ => "other"
        };
    }

    private bool ShouldTranslateBlock(Block block, string blockType)
    {
        // Don't translate code blocks
        if (blockType == "code") return false;

        // Don't translate blocks that are primarily URLs or code
        if (block is ParagraphBlock paragraph)
        {
            var text = ExtractText(block);

            // Check if it's mostly a URL
            if (text.StartsWith(@"/") ||text.StartsWith("http://") || text.StartsWith("https://")) return false;

            // Check if it contains inline code
            var hasInlineCode = paragraph.Inline?.Any(inline => inline is CodeInline) ?? false;
            if (hasInlineCode && text.Length < 100)
                // Short blocks with code snippets might be better left untranslated
                logger.LogDebug("Skipping translation of short code-heavy block");
        }

        return true;
    }
}