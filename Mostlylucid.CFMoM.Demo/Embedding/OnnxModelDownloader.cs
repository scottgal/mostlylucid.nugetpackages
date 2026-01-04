namespace Mostlylucid.CFMoM.Demo.Embedding;

/// <summary>
///     Downloads and caches ONNX models from HuggingFace.
/// </summary>
public class OnnxModelDownloader
{
    private readonly HttpClient _httpClient;
    private readonly string _modelDirectory;

    public OnnxModelDownloader(string? modelDirectory = null)
    {
        _modelDirectory = modelDirectory ?? GetDefaultModelDirectory();
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "CFMoM-Demo/1.0");
    }

    /// <summary>
    ///     Ensure embedding model is downloaded and return local paths.
    /// </summary>
    public async Task<EmbeddingModelPaths> EnsureEmbeddingModelAsync(
        EmbeddingModelInfo model,
        CancellationToken ct = default)
    {
        var modelDir = Path.Combine(_modelDirectory, "embeddings", SanitizeName(model.Name));
        Directory.CreateDirectory(modelDir);

        var modelPath = Path.Combine(modelDir, "model.onnx");
        var tokenizerPath = Path.Combine(modelDir, "tokenizer.json");
        var vocabPath = Path.Combine(modelDir, "vocab.txt");

        var tasks = new List<Task>();

        if (!File.Exists(modelPath))
            tasks.Add(DownloadFileAsync(model.GetModelUrl(), modelPath, $"model ({model.SizeBytes / 1_000_000}MB)", ct));

        if (!File.Exists(tokenizerPath))
            tasks.Add(DownloadFileAsync(model.GetTokenizerUrl(), tokenizerPath, "tokenizer", ct));

        if (!File.Exists(vocabPath))
            tasks.Add(DownloadFileAsync(model.GetVocabUrl(), vocabPath, "vocab", ct));

        if (tasks.Count > 0)
        {
            Console.Error.WriteLine($"[ONNX] Downloading {model.Name} (~{model.SizeBytes / 1_000_000}MB)...");
            Console.Error.WriteLine($"[ONNX] Cache directory: {modelDir}");

            await Task.WhenAll(tasks);

            Console.Error.WriteLine("[ONNX] Model downloaded successfully!");
        }

        return new EmbeddingModelPaths(modelPath, tokenizerPath, vocabPath);
    }

    private async Task DownloadFileAsync(
        string url,
        string localPath,
        string description,
        CancellationToken ct)
    {
        var tempPath = localPath + ".tmp";

        try
        {
            Console.Error.WriteLine($"[ONNX] Downloading {description}...");

            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            // Write to temp file
            {
                await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                await contentStream.CopyToAsync(fileStream, ct);
            }

            // Retry file move with backoff (Windows file handle release can be delayed)
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    File.Move(tempPath, localPath, overwrite: true);
                    return;
                }
                catch (IOException) when (attempt < 2)
                {
                    await Task.Delay(100 * (attempt + 1), ct);
                }
            }
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); }
                catch { /* ignore cleanup errors */ }
            }
            throw;
        }
    }

    private static string SanitizeName(string name)
    {
        return name.Replace("/", "_").Replace("\\", "_").Replace(":", "_");
    }

    private static string GetDefaultModelDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".cfmom", "models");
    }
}
