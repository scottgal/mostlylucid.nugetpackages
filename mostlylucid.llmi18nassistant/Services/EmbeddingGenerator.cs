using System.Net.Http.Json;
using LLama;
using LLama.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmI18nAssistant.Models;

namespace Mostlylucid.LlmI18nAssistant.Services;

/// <summary>
///     Generates text embeddings using LlamaSharp or Ollama
/// </summary>
public class EmbeddingGenerator : IEmbeddingGenerator, IDisposable
{
    private readonly LlmI18nAssistantConfig _config;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly ILogger<EmbeddingGenerator> _logger;
    private LLamaEmbedder? _embedder;
    private bool _initialized;
    private bool _useOllama;

    public EmbeddingGenerator(
        ILogger<EmbeddingGenerator> logger,
        HttpClient httpClient,
        IOptions<LlmI18nAssistantConfig> config)
    {
        _logger = logger;
        _httpClient = httpClient;
        _config = config.Value;
    }

    public void Dispose()
    {
        _embedder?.Dispose();
        _initLock.Dispose();
    }

    /// <inheritdoc />
    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (_useOllama)
            return await GenerateOllamaEmbeddingAsync(text, cancellationToken);

        return await GenerateLocalEmbeddingAsync(text, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<float[]>> GenerateBatchEmbeddingsAsync(
        List<string> texts,
        CancellationToken cancellationToken = default)
    {
        var results = new List<float[]>();

        foreach (var text in texts)
        {
            var embedding = await GenerateEmbeddingAsync(text, cancellationToken);
            results.Add(embedding);
        }

        return results;
    }

    /// <inheritdoc />
    public float CalculateSimilarity(float[] embedding1, float[] embedding2)
    {
        if (embedding1.Length != embedding2.Length)
            return 0;

        float dotProduct = 0;
        float norm1 = 0;
        float norm2 = 0;

        for (var i = 0; i < embedding1.Length; i++)
        {
            dotProduct += embedding1[i] * embedding2[i];
            norm1 += embedding1[i] * embedding1[i];
            norm2 += embedding2[i] * embedding2[i];
        }

        var denominator = MathF.Sqrt(norm1) * MathF.Sqrt(norm2);
        return denominator == 0 ? 0 : dotProduct / denominator;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
            return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
                return;

            if (_config.Embedding.Provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
            {
                _useOllama = true;
                _httpClient.BaseAddress = new Uri(_config.Ollama.Endpoint);
                _logger.LogInformation("Using Ollama for embeddings with model: {Model}",
                    _config.Embedding.OllamaModel);
            }
            else
            {
                _useOllama = false;
                await InitializeLocalEmbedderAsync();
            }

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private Task InitializeLocalEmbedderAsync()
    {
        if (!File.Exists(_config.Embedding.ModelPath))
            throw new FileNotFoundException(
                $"Embedding model file not found: {_config.Embedding.ModelPath}");

        var parameters = new ModelParams(_config.Embedding.ModelPath)
        {
            ContextSize = (uint)_config.Embedding.ContextSize
            // Note: EmbeddingMode property was removed in newer LlamaSharp versions
        };

        var weights = LLamaWeights.LoadFromFile(parameters);
        _embedder = new LLamaEmbedder(weights, parameters);

        _logger.LogInformation("Loaded local embedding model: {ModelPath}",
            _config.Embedding.ModelPath);

        return Task.CompletedTask;
    }

    private async Task<float[]> GenerateOllamaEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        var request = new
        {
            model = _config.Embedding.OllamaModel,
            prompt = text
        };

        var response = await _httpClient.PostAsJsonAsync("/api/embeddings", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(
            cancellationToken);

        if (result?.Embedding == null || result.Embedding.Length == 0)
            throw new InvalidOperationException("Empty embedding response from Ollama");

        return result.Embedding;
    }

    private async Task<float[]> GenerateLocalEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        if (_embedder == null)
            throw new InvalidOperationException("Local embedder not initialized");

        var embeddings = await _embedder.GetEmbeddings(text);

        // LlamaSharp returns embeddings per token, we need to average them
        if (embeddings.Count == 0)
            return new float[_config.Embedding.Dimension];

        var result = new float[embeddings[0].Length];

        foreach (var embedding in embeddings)
            for (var i = 0; i < embedding.Length; i++)
                result[i] += embedding[i];

        for (var i = 0; i < result.Length; i++)
            result[i] /= embeddings.Count;

        return result;
    }

    private class OllamaEmbeddingResponse
    {
        public float[]? Embedding { get; set; }
    }
}