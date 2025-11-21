using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using mostlylucid.llmslidetranslator.Demo.Hubs;
using mostlylucid.llmslidetranslator.Models;
using mostlylucid.llmslidetranslator.Services;

namespace mostlylucid.llmslidetranslator.Demo.Services;

/// <summary>
///     Service for streaming translations with real-time updates via SignalR
/// </summary>
public class StreamingTranslationService(
    ILogger<StreamingTranslationService> logger,
    IHubContext<TranslationHub> hubContext,
    IServiceScopeFactory scopeFactory)
{
    private readonly IHubContext<TranslationHub> _hubContext = hubContext;
    private readonly ILogger<StreamingTranslationService> _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    /// <summary>
    ///     Stream translation of a document with real-time updates
    /// </summary>
    public async IAsyncEnumerable<TranslationUpdate> StreamTranslationAsync(
        string markdown,
        string documentId,
        string sourceLanguage,
        string targetLanguage,
        TranslationMethod method)
    {
        var stopwatch = Stopwatch.StartNew();
        var groupName = $"document_{documentId}";

        _logger.LogInformation("Starting streaming translation for document {DocumentId}", documentId);

        // Send start notification
        await _hubContext.Clients.Group(groupName).SendAsync("TranslationStarted", new
        {
            DocumentId = documentId,
            SourceLanguage = sourceLanguage,
            TargetLanguage = targetLanguage,
            Method = method.ToString()
        });

        yield return new TranslationUpdate
        {
            DocumentId = documentId,
            Status = "Started",
            Message = "Translation started"
        };

        using var scope = _scopeFactory.CreateScope();
        var translator = scope.ServiceProvider.GetRequiredService<ILlmSlideTranslator>();
        var chunker = scope.ServiceProvider.GetRequiredService<IMarkdownChunker>();
        var embeddingGenerator = scope.ServiceProvider.GetRequiredService<IEmbeddingGenerator>();
        var vectorStore = scope.ServiceProvider.GetRequiredService<IVectorStore>();

        // Use iterator pattern to avoid yield in try-catch
        TranslationUpdate? errorUpdate = null;
        IAsyncEnumerable<TranslationUpdate>? translationUpdates = null;

        try
        {
            translationUpdates = StreamTranslationCoreAsync(
                markdown, documentId, sourceLanguage, targetLanguage,
                stopwatch, groupName, translator, chunker, embeddingGenerator, vectorStore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing streaming translation for document {DocumentId}", documentId);

            await _hubContext.Clients.Group(groupName).SendAsync("TranslationError", new
            {
                DocumentId = documentId,
                Error = ex.Message
            });

            errorUpdate = new TranslationUpdate
            {
                DocumentId = documentId,
                Status = "Error",
                Message = ex.Message
            };
        }

        // Yield error outside catch block if one occurred
        if (errorUpdate != null)
        {
            yield return errorUpdate;
            yield break;
        }

        // Iterate through results if no error
        if (translationUpdates == null) yield break;
        await foreach (var update in translationUpdates)
            yield return update;
    }

    /// <summary>
    ///     Core translation streaming logic (separated to avoid yield in try-catch)
    /// </summary>
    private async IAsyncEnumerable<TranslationUpdate> StreamTranslationCoreAsync(
        string markdown,
        string documentId,
        string sourceLanguage,
        string targetLanguage,
        Stopwatch stopwatch,
        string groupName,
        ILlmSlideTranslator translator,
        IMarkdownChunker chunker,
        IEmbeddingGenerator embeddingGenerator,
        IVectorStore vectorStore)
    {
        // Step 1: Chunk
        yield return new TranslationUpdate
        {
            DocumentId = documentId,
            Status = "Chunking",
            Message = "Breaking document into blocks..."
        };

        var blocks = await chunker.ChunkAsync(markdown, documentId, sourceLanguage, targetLanguage);

        await _hubContext.Clients.Group(groupName).SendAsync("ChunkingComplete", new
        {
            DocumentId = documentId,
            BlockCount = blocks.Count
        });

        yield return new TranslationUpdate
        {
            DocumentId = documentId,
            Status = "Chunked",
            Message = $"Created {blocks.Count} blocks",
            Progress = new { TotalBlocks = blocks.Count, CurrentBlock = 0 }
        };

        // Step 2: Generate embeddings
        yield return new TranslationUpdate
        {
            DocumentId = documentId,
            Status = "Embedding",
            Message = "Generating embeddings..."
        };

        blocks = await embeddingGenerator.GenerateEmbeddingsAsync(blocks);

        await _hubContext.Clients.Group(groupName).SendAsync("EmbeddingComplete", new
        {
            DocumentId = documentId
        });

        // Step 3: Store
        await vectorStore.StoreAsync(blocks, documentId);

        // Step 4: Translate block by block
        TranslationBlock? previousBlock = null;
        var translatedBlocks = new List<TranslationBlock>();

        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];

            yield return new TranslationUpdate
            {
                DocumentId = documentId,
                Status = "Translating",
                Message = $"Translating block {i + 1} of {blocks.Count}",
                Progress = new
                {
                    TotalBlocks = blocks.Count,
                    CurrentBlock = i + 1,
                    PercentComplete = (float)(i + 1) / blocks.Count * 100
                }
            };

            await _hubContext.Clients.Group(groupName).SendAsync("BlockTranslationStarted", new
            {
                DocumentId = documentId,
                BlockIndex = i,
                block.BlockId
            });

            var translatedBlock = await translator.TranslateBlockAsync(block, previousBlock);
            translatedBlocks.Add(translatedBlock);

            await _hubContext.Clients.Group(groupName).SendAsync("BlockTranslationComplete", new
            {
                DocumentId = documentId,
                BlockIndex = i,
                block.BlockId,
                OriginalText = block.Text,
                translatedBlock.TranslatedText
            });

            yield return new TranslationUpdate
            {
                DocumentId = documentId,
                Status = "BlockComplete",
                Message = $"Block {i + 1} translated",
                Progress = new
                {
                    TotalBlocks = blocks.Count,
                    CurrentBlock = i + 1,
                    PercentComplete = (float)(i + 1) / blocks.Count * 100
                },
                Data = new
                {
                    BlockIndex = i,
                    OriginalText = block.Text,
                    translatedBlock.TranslatedText
                }
            };

            previousBlock = translatedBlock;
        }

        // Update vector store with translations
        await vectorStore.StoreAsync(translatedBlocks, documentId);

        stopwatch.Stop();

        // Send completion notification
        await _hubContext.Clients.Group(groupName).SendAsync("TranslationComplete", new
        {
            DocumentId = documentId,
            Duration = stopwatch.Elapsed.TotalSeconds,
            BlockCount = translatedBlocks.Count
        });

        yield return new TranslationUpdate
        {
            DocumentId = documentId,
            Status = "Complete",
            Message = $"Translation completed in {stopwatch.Elapsed.TotalSeconds:F2}s",
            Progress = new
            {
                TotalBlocks = blocks.Count,
                CurrentBlock = blocks.Count,
                PercentComplete = 100
            }
        };
    }
}

/// <summary>
///     Real-time translation update
/// </summary>
public class TranslationUpdate
{
    public required string DocumentId { get; set; }
    public required string Status { get; set; }
    public required string Message { get; set; }
    public object? Progress { get; set; }
    public object? Data { get; set; }
}