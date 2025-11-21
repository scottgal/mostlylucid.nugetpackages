using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using mostlylucid.llmslidetranslator.Models;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace mostlylucid.llmslidetranslator.Services;

/// <summary>
///     Qdrant-based vector store for embeddings
/// </summary>
public class QdrantVectorStore : IVectorStore
{
    private readonly QdrantClient _client;
    private readonly LlmSlideTranslatorConfig _config;
    private readonly IEmbeddingGenerator _embeddingGenerator;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly ILogger<QdrantVectorStore> _logger;
    private bool _collectionInitialized;

    public QdrantVectorStore(
        ILogger<QdrantVectorStore> logger,
        IEmbeddingGenerator embeddingGenerator,
        IOptions<LlmSlideTranslatorConfig> config)
    {
        _logger = logger;
        _embeddingGenerator = embeddingGenerator;
        _config = config.Value;

        var endpoint = _config.Qdrant.Endpoint;
        _client = new QdrantClient(endpoint, apiKey: _config.Qdrant.ApiKey);
    }

    public async Task StoreAsync(
        List<TranslationBlock> blocks,
        string documentId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCollectionExistsAsync(cancellationToken);

        _logger.LogInformation("Storing {Count} blocks for document {DocumentId} in Qdrant",
            blocks.Count, documentId);

        var collectionName = _config.Qdrant.CollectionName;
        var points = new List<PointStruct>();

        foreach (var block in blocks)
        {
            if (block.Embedding == null)
            {
                _logger.LogWarning("Block {BlockId} has no embedding, skipping", block.BlockId);
                continue;
            }

            var point = new PointStruct
            {
                Id = new PointId { Uuid = block.BlockId },
                Vectors = block.Embedding,
                Payload =
                {
                    ["documentId"] = documentId,
                    ["blockId"] = block.BlockId,
                    ["index"] = block.Index,
                    ["text"] = block.Text,
                    ["translatedText"] = block.TranslatedText ?? "",
                    ["sourceLanguage"] = block.SourceLanguage,
                    ["targetLanguage"] = block.TargetLanguage,
                    ["blockType"] = block.BlockType,
                    ["shouldTranslate"] = block.ShouldTranslate
                }
            };

            points.Add(point);
        }

        if (points.Count > 0)
        {
            await _client.UpsertAsync(collectionName, points, cancellationToken: cancellationToken);
            _logger.LogInformation("Stored {Count} points in Qdrant", points.Count);
        }
    }

    public async Task<List<(TranslationBlock Block, float Similarity)>> SearchAsync(
        float[] queryEmbedding,
        string documentId,
        int topK,
        float minSimilarity = 0.0f,
        CancellationToken cancellationToken = default)
    {
        await EnsureCollectionExistsAsync(cancellationToken);

        _logger.LogDebug("Searching for top {TopK} similar blocks in document {DocumentId}",
            topK, documentId);

        var collectionName = _config.Qdrant.CollectionName;

        var searchResult = await _client.SearchAsync(
            collectionName,
            queryEmbedding,
            limit: (ulong)topK,
            filter: new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "documentId",
                            Match = new Match { Keyword = documentId }
                        }
                    }
                }
            },
            scoreThreshold: minSimilarity,
            cancellationToken: cancellationToken);

        var results = searchResult.Select(r =>
        {
            var block = new TranslationBlock
            {
                BlockId = r.Payload["blockId"].StringValue,
                Index = (int)r.Payload["index"].IntegerValue,
                DocumentId = r.Payload["documentId"].StringValue,
                Text = r.Payload["text"].StringValue,
                TranslatedText = r.Payload["translatedText"].StringValue,
                SourceLanguage = r.Payload["sourceLanguage"].StringValue,
                TargetLanguage = r.Payload["targetLanguage"].StringValue,
                BlockType = r.Payload["blockType"].StringValue,
                ShouldTranslate = r.Payload["shouldTranslate"].BoolValue,
                Embedding = r.Vectors.Vector.Data.ToArray()
            };

            return (block, r.Score);
        }).ToList();

        _logger.LogDebug("Found {Count} similar blocks above threshold {MinSimilarity}",
            results.Count, minSimilarity);

        return results;
    }

    public async Task<List<TranslationBlock>> GetDocumentBlocksAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCollectionExistsAsync(cancellationToken);

        var collectionName = _config.Qdrant.CollectionName;

        _logger.LogDebug("Retrieving all blocks for document {DocumentId} from Qdrant", documentId);

        var scrollResult = await _client.ScrollAsync(
            collectionName,
            filter: new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "documentId",
                            Match = new Match { Keyword = documentId }
                        }
                    }
                }
            },
            limit: 1000,
            payloadSelector: true,
            vectorsSelector: true,
            cancellationToken: cancellationToken);

        var blocks = scrollResult.Result.Select(r => new TranslationBlock
        {
            BlockId = r.Payload["blockId"].StringValue,
            Index = (int)r.Payload["index"].IntegerValue,
            DocumentId = r.Payload["documentId"].StringValue,
            Text = r.Payload["text"].StringValue,
            TranslatedText = r.Payload["translatedText"].StringValue,
            SourceLanguage = r.Payload["sourceLanguage"].StringValue,
            TargetLanguage = r.Payload["targetLanguage"].StringValue,
            BlockType = r.Payload["blockType"].StringValue,
            ShouldTranslate = r.Payload["shouldTranslate"].BoolValue,
            Embedding = r.Vectors.Vector.Data.ToArray()
        }).OrderBy(b => b.Index).ToList();

        _logger.LogDebug("Retrieved {Count} blocks for document {DocumentId}", blocks.Count, documentId);

        return blocks;
    }

    public async Task ClearDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        await EnsureCollectionExistsAsync(cancellationToken);

        var collectionName = _config.Qdrant.CollectionName;

        await _client.DeleteAsync(
            collectionName,
            new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "documentId",
                            Match = new Match { Keyword = documentId }
                        }
                    }
                }
            },
            cancellationToken: cancellationToken);

        _logger.LogInformation("Cleared document {DocumentId} from Qdrant", documentId);
    }

    private async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        if (_collectionInitialized)
            return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_collectionInitialized)
                return;

            var collectionName = _config.Qdrant.CollectionName;

            _logger.LogInformation("Checking if Qdrant collection {CollectionName} exists", collectionName);

            var collections = await _client.ListCollectionsAsync(cancellationToken);
            // ListCollectionsAsync returns IReadOnlyList<string> in current Qdrant client version
            var exists = collections.Any(c => c == collectionName);

            if (!exists)
            {
                _logger.LogInformation("Creating Qdrant collection {CollectionName}", collectionName);

                await _client.CreateCollectionAsync(
                    collectionName,
                    new VectorParams
                    {
                        Size = (ulong)_config.Embedding.Dimension,
                        Distance = Distance.Cosine
                    },
                    cancellationToken: cancellationToken);

                _logger.LogInformation("Collection {CollectionName} created successfully", collectionName);
            }

            _collectionInitialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }
}