using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Mostlylucid.CFMoM.Demo.Embedding;

/// <summary>
///     ONNX-based embedding service with automatic model downloading from HuggingFace.
///     Uses all-MiniLM-L6-v2 model producing 384-dimensional embeddings.
/// </summary>
public sealed class OnnxEmbeddingService : IEmbeddingService, IDisposable
{
    private readonly EmbeddingModelInfo _modelInfo;
    private readonly int _maxSequenceLength;
    private readonly OnnxModelDownloader _downloader;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private InferenceSession? _session;
    private HuggingFaceTokenizer? _tokenizer;
    private bool _initialized;

    /// <summary>
    ///     Embedding dimension for this model (384 for MiniLM).
    /// </summary>
    public int Dimensions => _modelInfo.EmbeddingDimension;

    public OnnxEmbeddingService(
        OnnxEmbeddingModel model = OnnxEmbeddingModel.AllMiniLmL6V2,
        bool quantized = true,
        int? maxSequenceLength = null)
    {
        _modelInfo = OnnxModelRegistry.GetEmbeddingModel(model, quantized);
        _maxSequenceLength = maxSequenceLength ?? _modelInfo.MaxSequenceLength;
        _downloader = new OnnxModelDownloader();
    }

    /// <summary>
    ///     Initialize the model (downloads from HuggingFace if needed).
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized) return;

            var paths = await _downloader.EnsureEmbeddingModelAsync(_modelInfo, ct);

            var options = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                IntraOpNumThreads = Environment.ProcessorCount
            };

            _session = new InferenceSession(paths.ModelPath, options);

            Console.Error.WriteLine($"[ONNX] Model loaded: {_modelInfo.Name} ({_modelInfo.EmbeddingDimension}d)");

            // Prefer tokenizer.json (universal format) with vocab.txt fallback
            _tokenizer = File.Exists(paths.TokenizerPath)
                ? HuggingFaceTokenizer.FromFile(paths.TokenizerPath)
                : HuggingFaceTokenizer.FromVocabFile(paths.VocabPath);

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    ///     Generate embedding for text (synchronous, blocks on first call for download).
    /// </summary>
    public float[] Embed(string text)
    {
        // Ensure initialized synchronously (blocking)
        if (!_initialized)
        {
            InitializeAsync().GetAwaiter().GetResult();
        }

        return EmbedInternal(text);
    }

    /// <summary>
    ///     Generate embedding for text (async).
    /// </summary>
    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        await InitializeAsync(ct);
        return EmbedInternal(text);
    }

    /// <summary>
    ///     Generate embeddings for multiple texts.
    /// </summary>
    public float[][] EmbedBatch(IEnumerable<string> texts)
    {
        return texts.Select(Embed).ToArray();
    }

    /// <summary>
    ///     Compute cosine similarity between two embeddings.
    /// </summary>
    public float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0f;

        float dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denom = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        return denom > 0 ? dot / denom : 0f;
    }

    private float[] EmbedInternal(string text)
    {
        if (_session == null || _tokenizer == null)
            throw new InvalidOperationException("Model not initialized");

        // Prepend instruction if model requires it (e.g., BGE models)
        if (_modelInfo.RequiresInstruction && !string.IsNullOrEmpty(_modelInfo.QueryInstruction))
            text = _modelInfo.QueryInstruction + text;

        // Tokenize using HuggingFace tokenizer
        var (inputIds, attentionMask, tokenTypeIds) = _tokenizer.Encode(text, _maxSequenceLength);

        // Create tensors
        var inputIdsTensor = new DenseTensor<long>(inputIds, [1, inputIds.Length]);
        var attentionMaskTensor = new DenseTensor<long>(attentionMask, [1, attentionMask.Length]);
        var tokenTypeIdsTensor = new DenseTensor<long>(tokenTypeIds, [1, tokenTypeIds.Length]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor)
        };

        // Run inference
        using var results = _session.Run(inputs);

        // Get last_hidden_state output
        var output = results.First(r => r.Name == "last_hidden_state" || r.Name == "output_0");
        var outputTensor = output.AsTensor<float>();

        // Mean pooling with attention mask
        return MeanPool(outputTensor, attentionMask, _modelInfo.EmbeddingDimension);
    }

    private static float[] MeanPool(Tensor<float> hiddenStates, long[] attentionMask, int hiddenSize)
    {
        var result = new float[hiddenSize];
        var dims = hiddenStates.Dimensions.ToArray();
        var seqLen = dims[1];

        float maskSum = attentionMask.Sum();
        if (maskSum == 0) maskSum = 1; // Avoid division by zero

        for (int h = 0; h < hiddenSize; h++)
        {
            float sum = 0;
            for (int s = 0; s < seqLen; s++)
            {
                if (attentionMask[s] == 1)
                    sum += hiddenStates[0, s, h];
            }
            result[h] = sum / maskSum;
        }

        // L2 normalize
        float norm = MathF.Sqrt(result.Sum(x => x * x));
        if (norm > 0)
        {
            for (int i = 0; i < result.Length; i++)
                result[i] /= norm;
        }

        return result;
    }

    public void Dispose()
    {
        _session?.Dispose();
        _initLock.Dispose();
    }
}
