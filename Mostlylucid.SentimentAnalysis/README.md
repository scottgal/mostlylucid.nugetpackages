# Mostlylucid.SentimentAnalysis

A lightweight, CPU-only sentiment analysis library using ONNX Runtime with a small, efficient multilingual model. Perfect for local sentiment analysis without cloud dependencies.

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.sentimentanalysis.svg)](https://www.nuget.org/packages/mostlylucid.sentimentanalysis)

## Features

- **CPU-only** - No GPU required, runs efficiently on any machine
- **Small Model** - Uses a quantized DistilBERT model (~60MB)
- **Multilingual Support** - Works with 104+ languages
- **Auto-download** - Model is automatically downloaded on first use
- **File Support** - Analyze text files with automatic chunking
- **Stream Support** - Process streams for memory-efficient analysis
- **Batch Processing** - Analyze multiple texts efficiently
- **OpenTelemetry** - Built-in observability and tracing support
- **Thread-safe** - Safe for concurrent use in web applications

## Installation

```bash
dotnet add package mostlylucid.sentimentanalysis
```

## Quick Start

### Basic Usage

```csharp
using Mostlylucid.SentimentAnalysis.Extensions;
using Mostlylucid.SentimentAnalysis.Services;

// Add to services
services.AddSentimentAnalysis();

// Inject and use
public class MyService
{
    private readonly ISentimentAnalysisService _sentiment;

    public MyService(ISentimentAnalysisService sentiment)
    {
        _sentiment = sentiment;
    }

    public async Task AnalyzeText()
    {
        // Analyze a single string
        var result = await _sentiment.AnalyzeAsync("I love this product!");
        Console.WriteLine($"Sentiment: {result.Sentiment} ({result.Confidence:P1})");

        // Get simple sentiment label
        var sentiment = await _sentiment.GetSentimentAsync("This is terrible.");
        Console.WriteLine($"Sentiment: {sentiment}");

        // Get normalized score (-1 to +1)
        var score = await _sentiment.GetSentimentScoreAsync("Pretty good overall");
        Console.WriteLine($"Score: {score:F2}");
    }
}
```

### ASP.NET Core Integration

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSentimentAnalysis(options =>
{
    options.ModelPath = "./models/sentiment";
});

var app = builder.Build();

// Minimal API example
app.MapPost("/analyze", async (ISentimentAnalysisService sentiment, string text) =>
{
    var result = await sentiment.AnalyzeAsync(text);
    return Results.Ok(new
    {
        result.Sentiment,
        result.Confidence,
        result.NormalizedScore,
        IsPositive = result.IsPositive,
        IsNegative = result.IsNegative
    });
});

app.Run();
```

### File Analysis

```csharp
// Analyze a text file (automatically chunks long content)
var fileResult = await _sentiment.AnalyzeFileAsync("review.txt");

Console.WriteLine($"Overall: {fileResult.OverallSentiment}");
Console.WriteLine($"Chunks analyzed: {fileResult.ChunkCount}");
Console.WriteLine($"Weighted score: {fileResult.WeightedScore:F2}");
Console.WriteLine($"Average confidence: {fileResult.AverageConfidence:P1}");

// View sentiment distribution across chunks
foreach (var (label, count) in fileResult.SentimentDistribution)
{
    Console.WriteLine($"  {label}: {count} chunks");
}
```

### Stream Analysis

```csharp
// Analyze from a stream (useful for uploaded files)
using var stream = new MemoryStream(Encoding.UTF8.GetBytes(longText));
var streamResult = await _sentiment.AnalyzeStreamAsync(stream, "User Upload");

Console.WriteLine($"Overall: {streamResult.OverallSentiment}");
```

### Batch Analysis

```csharp
var reviews = new[]
{
    "Great experience! Highly recommend.",
    "Disappointed with the service quality.",
    "It was okay, nothing special.",
    "Absolutely fantastic product!",
    "Terrible. Would not buy again."
};

var results = await _sentiment.AnalyzeBatchAsync(reviews);

foreach (var result in results)
{
    var emoji = result.IsPositive ? "+" : result.IsNegative ? "-" : "~";
    Console.WriteLine($"[{emoji}] {result.Sentiment}: {result.Text}");
}
```

### Multilingual Support

The model supports 104 languages including:

```csharp
// English
var en = await _sentiment.AnalyzeAsync("I love this!");

// Spanish
var es = await _sentiment.AnalyzeAsync("Me encanta esto!");

// French
var fr = await _sentiment.AnalyzeAsync("J'adore ce produit!");

// German
var de = await _sentiment.AnalyzeAsync("Ich liebe dieses Produkt!");

// Chinese
var zh = await _sentiment.AnalyzeAsync("我喜欢这个产品!");

// Japanese
var ja = await _sentiment.AnalyzeAsync("この製品が大好きです!");
```

## Configuration

### All Options

```csharp
services.AddSentimentAnalysis(options =>
{
    // Path where the ONNX model will be stored
    options.ModelPath = "./models/sentiment";

    // Model filename
    options.ModelFileName = "sentiment_model.onnx";

    // Custom model URL (if using a different model)
    options.ModelUrl = "https://huggingface.co/.../model.onnx";

    // Enable detailed diagnostic logging
    options.EnableDiagnosticLogging = true;

    // Number of inference threads (0 = use all cores)
    options.InferenceThreads = 4;

    // Maximum tokens per chunk for long text analysis
    options.MaxChunkLength = 450;

    // Overlap between chunks (helps maintain context)
    options.ChunkOverlap = 50;

    // Disable auto-download (model must already exist)
    options.AutoDownloadModel = false;

    // Model download timeout in seconds
    options.DownloadTimeoutSeconds = 300;
});
```

### Shorthand Configuration

```csharp
// Just specify model path
services.AddSentimentAnalysis("./my-models/sentiment");
```

## API Reference

### ISentimentAnalysisService

| Method | Description |
|--------|-------------|
| `AnalyzeAsync(string text)` | Analyze sentiment of a single text string |
| `AnalyzeBatchAsync(IEnumerable<string> texts)` | Analyze multiple texts |
| `AnalyzeFileAsync(string filePath)` | Analyze a text file with automatic chunking |
| `AnalyzeStreamAsync(Stream stream, string sourceName)` | Analyze text from a stream |
| `AnalyzeLongTextAsync(string text, string sourceName)` | Analyze long text with chunking |
| `GetSentimentAsync(string text)` | Get just the sentiment label |
| `GetSentimentScoreAsync(string text)` | Get normalized score (-1 to +1) |
| `IsReady` | Check if the service is initialized |

### SentimentResult

| Property | Type | Description |
|----------|------|-------------|
| `Text` | `string` | The analyzed text (truncated if long) |
| `Sentiment` | `SentimentLabel` | The predicted sentiment |
| `Confidence` | `float` | Confidence score (0.0 to 1.0) |
| `Scores` | `Dictionary<SentimentLabel, float>` | Scores for each sentiment class |
| `IsPositive` | `bool` | True if Positive or VeryPositive |
| `IsNegative` | `bool` | True if Negative or VeryNegative |
| `IsNeutral` | `bool` | True if Neutral |
| `NormalizedScore` | `float` | Score from -1 (very negative) to +1 (very positive) |

### AggregateSentimentResult

| Property | Type | Description |
|----------|------|-------------|
| `Source` | `string` | Source identifier (file path, etc.) |
| `OverallSentiment` | `SentimentLabel` | Aggregated sentiment |
| `AverageConfidence` | `float` | Average confidence across chunks |
| `ChunkResults` | `IReadOnlyList<SentimentResult>` | Individual chunk results |
| `ChunkCount` | `int` | Number of chunks analyzed |
| `SentimentDistribution` | `Dictionary<SentimentLabel, int>` | Distribution across chunks |
| `WeightedScore` | `float` | Confidence-weighted average score |

## Sentiment Labels

The service returns sentiment on a 5-point scale:

| Label | Value | Score Range | Description |
|-------|-------|-------------|-------------|
| VeryNegative | 1 | -1.0 to -0.5 | Strongly negative sentiment |
| Negative | 2 | -0.5 to -0.1 | Negative sentiment |
| Neutral | 3 | -0.1 to 0.1 | Neutral sentiment |
| Positive | 4 | 0.1 to 0.5 | Positive sentiment |
| VeryPositive | 5 | 0.5 to 1.0 | Strongly positive sentiment |

## Model Information

This library uses a quantized version of [distilbert-base-multilingual-cased-sentiments-student](https://huggingface.co/lxyuan/distilbert-base-multilingual-cased-sentiments-student):

- **Size**: ~60MB quantized ONNX model
- **Base**: DistilBERT (66M parameters, 6 layers)
- **Training**: Fine-tuned on multilingual sentiment data
- **Languages**: 104 languages supported
- **Inference**: Optimized for CPU with ONNX Runtime

### Model Output Classes

The underlying model outputs 3 classes (negative, neutral, positive) which are mapped to our 5-point scale based on confidence scores.

## OpenTelemetry Integration

The library includes built-in OpenTelemetry tracing:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("Mostlylucid.SentimentAnalysis");
        tracing.AddConsoleExporter(); // or your preferred exporter
    });
```

### Activity Tags

Activities include these tags:
- `sentiment.operation` - Operation type (analyze, analyze_file, etc.)
- `sentiment.text_length` - Length of input text
- `sentiment.result` - Resulting sentiment label
- `sentiment.confidence` - Confidence score
- `sentiment.file_path` - File path (for file analysis)

## Performance Notes

- **First Run**: Model download takes 30-60 seconds depending on connection
- **Initialization**: Model loading takes 2-5 seconds
- **Inference**: ~50-100ms per text on modern CPUs
- **Memory**: ~200-300MB runtime memory usage
- **Thread Safety**: Service is thread-safe for concurrent requests

### Optimization Tips

1. **Reuse the service**: Register as Singleton (default) to avoid reloading the model
2. **Batch when possible**: Use `AnalyzeBatchAsync` for multiple texts
3. **Tune thread count**: Set `InferenceThreads` based on your CPU cores
4. **Pre-download model**: Set `AutoDownloadModel = false` in production and deploy with the model

## Troubleshooting

### Model Download Fails

```csharp
// Increase timeout
options.DownloadTimeoutSeconds = 600;

// Or download manually and specify path
options.AutoDownloadModel = false;
options.ModelPath = "/path/to/existing/model";
```

### Service Not Ready

```csharp
if (!sentiment.IsReady)
{
    // Check logs for initialization errors
    // Common causes:
    // - Model file not found
    // - Insufficient memory
    // - ONNX Runtime not compatible with CPU
}
```

### Out of Memory with Large Files

```csharp
// Reduce chunk size
options.MaxChunkLength = 256;

// Or use streaming
await using var stream = File.OpenRead("large-file.txt");
var result = await sentiment.AnalyzeStreamAsync(stream);
```

### Slow Performance

```csharp
// Optimize thread usage
options.InferenceThreads = Environment.ProcessorCount;

// Use batch processing
var results = await sentiment.AnalyzeBatchAsync(manyTexts);
```

## Examples

See the `Mostlylucid.SentimentAnalysis.Demo` project for complete working examples including:

- Single text analysis
- Batch processing
- Long text/file analysis
- Multilingual examples
- Configuration options

## License

Unlicense - Free and unencumbered software released into the public domain.

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

## Related Packages

- [Mostlylucid.LlmAltText](https://www.nuget.org/packages/mostlylucid.llmalttext) - AI-powered image alt text generation
- [Mostlylucid.LLMContentModeration](https://www.nuget.org/packages/mostlylucid.llmcontentmoderation) - Content moderation with LLMs
