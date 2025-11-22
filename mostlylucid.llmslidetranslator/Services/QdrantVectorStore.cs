using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using mostlylucid.llmslidetranslator.Models;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace mostlylucid.llmslidetranslator.Services;

/// <summary>
///     Qdrant-based vector store for embeddings
/// </summary>
public class QdrantVectorStore(
    ILogger<QdrantVectorStore> logger,
    IEmbeddingGenerator embeddingGenerator,
    IOptions<LlmSlideTranslatorConfig> options) : IVectorStore
{
    private readonly LlmSlideTranslatorConfig config = options.Value;
    private readonly QdrantClient client = new(options.Value.Qdrant.Endpoint, apiKey: options.Value.Qdrant.ApiKey);
    private readonly SemaphoreSlim initLock = new(1, 1);
    private bool _collectionInitialized;

    public async Task StoreAsync(
        List<TranslationBlock> blocks,
        string documentId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCollectionExistsAsync(cancellationToken);

        logger.LogInformation("Storing {Count} blocks for document {DocumentId} in Qdrant",
            blocks.Count, documentId);

        var collectionName = config.Qdrant.CollectionName;
        var points = new List<PointStruct>();

        foreach (var block in blocks)
        {
            if (block.Embedding == null)
            {
                logger.LogWarning("Block {BlockId} has no embedding, skipping", block.BlockId);
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
            await client.UpsertAsync(collectionName, points, cancellationToken: cancellationToken);
            logger.LogInformation("Stored {Count} points in Qdrant", points.Count);
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

        logger.LogDebug("Searching for top {TopK} similar blocks in document {DocumentId}",
            topK, documentId);

        var collectionName = config.Qdrant.CollectionName;

        var searchResult = await client.SearchAsync(
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

        logger.LogDebug("Found {Count} similar blocks above threshold {MinSimilarity}",
            results.Count, minSimilarity);

        return results;
    }

    public async Task<List<TranslationBlock>> GetDocumentBlocksAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCollectionExistsAsync(cancellationToken);

        var collectionName = config.Qdrant.CollectionName;

        logger.LogDebug("Retrieving all blocks for document {DocumentId} from Qdrant", documentId);

        var scrollResult = await client.ScrollAsync(
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

        logger.LogDebug("Retrieved {Count} blocks for document {DocumentId}", blocks.Count, documentId);

        return blocks;
    }

    public async Task ClearDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        await EnsureCollectionExistsAsync(cancellationToken);

        var collectionName = config.Qdrant.CollectionName;

        await client.DeleteAsync(
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

        logger.LogInformation("Cleared document {DocumentId} from Qdrant", documentId);
    }

    private async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        if (_collectionInitialized)
            return;

        await initLock.WaitAsync(cancellationToken);
        try
        {
            if (_collectionInitialized)
                return;

            var collectionName = config.Qdrant.CollectionName;

            logger.LogInformation("Checking if Qdrant collection {CollectionName} exists", collectionName);

            var collections = await client.ListCollectionsAsync(cancellationToken);
            // ListCollectionsAsync returns IReadOnlyList<string> in current Qdrant client version
            var exists = collections.Any(c => c == collectionName);

            if (!exists)
            {
                logger.LogInformation("Creating Qdrant collection {CollectionName}", collectionName);

                await client.CreateCollectionAsync(
                    collectionName,
                    new VectorParams
                    {
                        Size = (ulong)config.Embedding.Dimension,
                        Distance = Distance.Cosine
                    },
                    cancellationToken: cancellationToken);

                logger.LogInformation("Collection {CollectionName} created successfully", collectionName);
            }

            _collectionInitialized = true;
        }
        finally
        {
            initLock.Release();
        }
    }
}