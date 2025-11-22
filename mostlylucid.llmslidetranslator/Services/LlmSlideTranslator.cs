using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using mostlylucid.llmslidetranslator.Models;

namespace mostlylucid.llmslidetranslator.Services;

/// <summary>
///     Main translation service orchestrating RAG-assisted LLM translation
/// </summary>
public class LlmSlideTranslator(
    ILogger<LlmSlideTranslator> logger,
    IMarkdownChunker chunker,
    IEmbeddingGenerator embeddingGenerator,
    IVectorStore vectorStore,
    IOllamaClient ollamaClient,
    INmtClient nmtClient,
    IOptions<LlmSlideTranslatorConfig> config) : ILlmSlideTranslator
{
    private readonly LlmSlideTranslatorConfig config = config.Value;

    public async Task<TranslationResult> TranslateAsync(
        string markdown,
        string documentId,
        string sourceLanguage,
        string targetLanguage,
        TranslationMethod method = TranslationMethod.RagLlm,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation(
            "Starting translation of document {DocumentId} from {Source} to {Target} using {Method}",
            documentId, sourceLanguage, targetLanguage, method);

        var result = new TranslationResult
        {
            DocumentId = documentId,
            SourceLanguage = sourceLanguage,
            TargetLanguage = targetLanguage,
            Method = method
        };

        try
        {
            // Step 1: Chunk the markdown
            var blocks = await chunker.ChunkAsync(
                markdown,
                documentId,
                sourceLanguage,
                targetLanguage,
                cancellationToken);

            logger.LogInformation("Chunked document into {Count} blocks", blocks.Count);

            // Step 2: Generate embeddings
            blocks = await embeddingGenerator.GenerateEmbeddingsAsync(blocks, cancellationToken);
            logger.LogInformation("Generated embeddings for all blocks");

            // Step 3: Store blocks with embeddings
            await vectorStore.StoreAsync(blocks, documentId, cancellationToken);

            // Step 4: Translate based on method
            switch (method)
            {
                case TranslationMethod.NmtOnly:
                    blocks = await TranslateWithNmtOnlyAsync(blocks, cancellationToken);
                    break;

                case TranslationMethod.LlmOnly:
                    blocks = await TranslateWithLlmOnlyAsync(blocks, cancellationToken);
                    break;

                case TranslationMethod.NmtPlusLlm:
                    blocks = await TranslateWithNmtPlusLlmAsync(blocks, cancellationToken);
                    break;

                case TranslationMethod.RagLlm:
                default:
                    blocks = await TranslateWithRagLlmAsync(blocks, cancellationToken);
                    break;
            }

            // Step 5: Update stored blocks with translations
            await vectorStore.StoreAsync(blocks, documentId, cancellationToken);

            result.Blocks = blocks;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error translating document {DocumentId}", documentId);
            result.Errors.Add(ex.Message);
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;

        logger.LogInformation(
            "Translation completed in {Duration:F2}s for document {DocumentId}",
            result.Duration.TotalSeconds, documentId);

        return result;
    }

    public async Task<TranslationBlock> TranslateBlockAsync(
        TranslationBlock block,
        TranslationBlock? previousBlock = null,
        CancellationToken cancellationToken = default)
    {
        if (!block.ShouldTranslate)
        {
            block.TranslatedText = block.Text;
            return block;
        }

        // Get RAG context
        var similarBlocks = new List<TranslationBlock>();
        if (block.Embedding != null)
        {
            var searchResults = await vectorStore.SearchAsync(
                block.Embedding,
                block.DocumentId,
                config.Rag.TopK,
                config.Rag.MinSimilarity,
                cancellationToken);

            similarBlocks = searchResults
                .Where(r => r.Block.Index < block.Index) // Only use earlier blocks
                .Select(r => r.Block)
                .ToList();
        }

        // Build translation context
        var context = new TranslationContext
        {
            CurrentBlock = block,
            PreviousBlock = config.Rag.UseSlidingWindow ? previousBlock : null,
            SimilarBlocks = similarBlocks.Take(config.Rag.MaxContextBlocks).ToList()
        };

        // Translate with LLM
        var translatedText = await ollamaClient.TranslateWithContextAsync(context, cancellationToken);
        block.TranslatedText = translatedText;

        return block;
    }

    public async Task<TranslationProgress> GetProgressAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        var blocks = await vectorStore.GetDocumentBlocksAsync(documentId, cancellationToken);

        var translatedCount = blocks.Count(b => !string.IsNullOrEmpty(b.TranslatedText));

        return new TranslationProgress
        {
            DocumentId = documentId,
            TotalBlocks = blocks.Count,
            TranslatedBlocks = translatedCount
        };
    }

    private async Task<List<TranslationBlock>> TranslateWithNmtOnlyAsync(
        List<TranslationBlock> blocks,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Translating with NMT only");
        return await nmtClient.TranslateBatchAsync(blocks, cancellationToken);
    }

    private async Task<List<TranslationBlock>> TranslateWithLlmOnlyAsync(
        List<TranslationBlock> blocks,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Translating with LLM only (no RAG)");

        TranslationBlock? previousBlock = null;

        foreach (var block in blocks.OrderBy(b => b.Index))
        {
            if (!block.ShouldTranslate)
            {
                block.TranslatedText = block.Text;
                previousBlock = block;
                continue;
            }

            var context = new TranslationContext
            {
                CurrentBlock = block,
                PreviousBlock = previousBlock
            };

            block.TranslatedText = await ollamaClient.TranslateWithContextAsync(context, cancellationToken);
            previousBlock = block;
        }

        return blocks;
    }

    private async Task<List<TranslationBlock>> TranslateWithNmtPlusLlmAsync(
        List<TranslationBlock> blocks,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Translating with NMT + LLM post-editing");

        // First pass: NMT baseline
        blocks = await nmtClient.TranslateBatchAsync(blocks, cancellationToken);

        // Second pass: LLM post-editing with RAG
        return await TranslateWithRagLlmAsync(blocks, cancellationToken);
    }

    private async Task<List<TranslationBlock>> TranslateWithRagLlmAsync(
        List<TranslationBlock> blocks,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Translating with RAG-enhanced LLM");

        TranslationBlock? previousBlock = null;

        foreach (var block in blocks.OrderBy(b => b.Index))
        {
            await TranslateBlockAsync(block, previousBlock, cancellationToken);
            previousBlock = block;
        }

        return blocks;
    }
}