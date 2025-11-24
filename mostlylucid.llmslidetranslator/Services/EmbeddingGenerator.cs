using System.Net.Http.Json;
using LLama;
using LLama.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using mostlylucid.llmslidetranslator.Models;

namespace mostlylucid.llmslidetranslator.Services;

/// <summary>
///     Generates embeddings using LlamaSharp (local) or Ollama (API)
/// </summary>
public class EmbeddingGenerator(
    ILogger<EmbeddingGenerator> logger,
    IOptions<LlmSlideTranslatorConfig> options,
    IHttpClientFactory? httpClientFactory = null) : IEmbeddingGenerator, IDisposable
{
    private readonly LlmSlideTranslatorConfig config = options.Value;

    private readonly HttpClient httpClient = ConfigureHttpClient(
        httpClientFactory?.CreateClient("EmbeddingGenerator") ?? new HttpClient(),
        options.Value);

    private readonly SemaphoreSlim initLock = new(1, 1);
    private LLamaEmbedder? _embedder;
    private bool _initialized;
    private bool _useOllama;

    public void Dispose()
    {
        _embedder?.Dispose();
        initLock.Dispose();
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        logger.LogDebug("Generating embedding for text of length {Length}", text.Length);

        try
        {
            if (_useOllama) return await GenerateOllamaEmbeddingAsync(text, cancellationToken);

            if (_embedder == null) throw new InvalidOperationException("Embedder not initialized");
            var embeddings = await Task.Run(() => _embedder.GetEmbeddings(text), cancellationToken);
            return embeddings.FirstOrDefault() ?? Array.Empty<float>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating embedding");
            throw;
        }
    }

    public async Task<List<TranslationBlock>> GenerateEmbeddingsAsync(
        List<TranslationBlock> blocks,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        logger.LogInformation("Generating embeddings for {Count} blocks using {Provider}",
            blocks.Count, _useOllama ? "Ollama" : "Local LlamaSharp");

        var tasks = blocks.Select(async block =>
        {
            if (block.ShouldTranslate) block.Embedding = await GenerateEmbeddingAsync(block.Text, cancellationToken);
            return block;
        });

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    public float CalculateSimilarity(float[] embedding1, float[] embedding2)
    {
        if (embedding1.Length != embedding2.Length)
            throw new ArgumentException("Embeddings must have the same dimension");

        // Calculate cosine similarity
        var dotProduct = 0.0f;
        var magnitude1 = 0.0f;
        var magnitude2 = 0.0f;

        for (var i = 0; i < embedding1.Length; i++)
        {
            dotProduct += embedding1[i] * embedding2[i];
            magnitude1 += embedding1[i] * embedding1[i];
            magnitude2 += embedding2[i] * embedding2[i];
        }

        magnitude1 = MathF.Sqrt(magnitude1);
        magnitude2 = MathF.Sqrt(magnitude2);

        if (magnitude1 == 0 || magnitude2 == 0) return 0;

        return dotProduct / (magnitude1 * magnitude2);
    }

    private static HttpClient ConfigureHttpClient(HttpClient client, LlmSlideTranslatorConfig config)
    {
        client.BaseAddress = new Uri(config.Ollama.Endpoint);
        client.Timeout = TimeSpan.FromSeconds(config.Ollama.TimeoutSeconds);
        return client;
    }

    private async Task<float[]> GenerateOllamaEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            model = config.Embedding.OllamaModel,
            prompt = text
        };

        var response = await httpClient.PostAsJsonAsync("/api/embeddings", requestBody, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(cancellationToken);

        if (result?.Embedding == null || result.Embedding.Length == 0)
            throw new InvalidOperationException("Ollama returned empty embedding");

        return result.Embedding;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized)
            return;

        await initLock.WaitAsync();
        try
        {
            if (_initialized)
                return;

            var provider = config.Embedding.Provider?.ToLowerInvariant() ?? "ollama";
            _useOllama = provider == "ollama";

            if (_useOllama)
            {
                logger.LogInformation("Using Ollama for embeddings with model {Model}",
                    config.Embedding.OllamaModel);

                // Verify Ollama is available and model exists
                var response = await httpClient.GetAsync("/api/tags");
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException(
                        $"Ollama is not available at {config.Ollama.Endpoint}");

                logger.LogInformation("Ollama embedding provider initialized successfully");
            }
            else
            {
                logger.LogInformation("Initializing local embedding model from {ModelPath}",
                    config.Embedding.ModelPath);

                if (!File.Exists(config.Embedding.ModelPath))
                    throw new FileNotFoundException(
                        $"Embedding model not found at {config.Embedding.ModelPath}. " +
                        "Please download a GGUF embedding model (e.g., nomic-embed-text) " +
                        "or set Embedding.Provider to 'Ollama' to use Ollama embeddings.");

                var parameters = new ModelParams(config.Embedding.ModelPath)
                {
                    ContextSize = (uint)config.Embedding.ContextSize,
                    Embeddings = true,
                    GpuLayerCount = 0 // Use CPU for embeddings
                };

                var weights = LLamaWeights.LoadFromFile(parameters);
                _embedder = new LLamaEmbedder(weights, parameters);

                logger.LogInformation("Local embedding model initialized successfully");
            }

            _initialized = true;
        }
        finally
        {
            initLock.Release();
        }
    }

    private class OllamaEmbeddingResponse
    {
        public float[]? Embedding { get; set; }
    }
}