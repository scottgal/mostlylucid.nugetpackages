# Mostlylucid.SentimentAnalysis

A lightweight, CPU-only sentiment analysis library using ONNX Runtime with a small, efficient multilingual model.

## Features

- **CPU-only** - No GPU required, runs efficiently on any machine
- **Small Model** - Uses a quantized DistilBERT model (~60MB)
- **Multilingual Support** - Works with multiple languages
- **Auto-download** - Model is automatically downloaded on first use
- **File Support** - Analyze text files with automatic chunking
- **OpenTelemetry** - Built-in observability support

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

        // Get simple sentiment
        var sentiment = await _sentiment.GetSentimentAsync("This is terrible.");
        Console.WriteLine($"Sentiment: {sentiment}");

        // Get normalized score (-1 to +1)
        var score = await _sentiment.GetSentimentScoreAsync("Pretty good overall");
        Console.WriteLine($"Score: {score:F2}");
    }
}
```

### File Analysis

```csharp
// Analyze a text file (automatically chunks long content)
var fileResult = await _sentiment.AnalyzeFileAsync("review.txt");
Console.WriteLine($"Overall: {fileResult.OverallSentiment}");
Console.WriteLine($"Chunks analyzed: {fileResult.ChunkCount}");
Console.WriteLine($"Weighted score: {fileResult.WeightedScore:F2}");
```

### Batch Analysis

```csharp
var texts = new[] {
    "Great experience!",
    "Disappointed with the service",
    "It was okay"
};

var results = await _sentiment.AnalyzeBatchAsync(texts);
foreach (var result in results)
{
    Console.WriteLine($"{result.Sentiment}: {result.Text}");
}
```

## Configuration

```csharp
services.AddSentimentAnalysis(options =>
{
    // Custom model storage path
    options.ModelPath = "./models/sentiment";

    // Enable detailed logging
    options.EnableDiagnosticLogging = true;

    // Limit inference threads (0 = use all cores)
    options.InferenceThreads = 4;

    // Chunk settings for long text
    options.MaxChunkLength = 450;
    options.ChunkOverlap = 50;

    // Disable auto-download (model must exist)
    options.AutoDownloadModel = false;
});
```

## Sentiment Labels

The service returns sentiment on a 5-point scale:

| Label | Score Range | Description |
|-------|-------------|-------------|
| VeryNegative | -1.0 to -0.5 | Strongly negative sentiment |
| Negative | -0.5 to -0.1 | Negative sentiment |
| Neutral | -0.1 to 0.1 | Neutral sentiment |
| Positive | 0.1 to 0.5 | Positive sentiment |
| VeryPositive | 0.5 to 1.0 | Strongly positive sentiment |

## Model Information

This library uses a quantized version of [distilbert-base-multilingual-cased-sentiments-student](https://huggingface.co/lxyuan/distilbert-base-multilingual-cased-sentiments-student), which is:

- ~60MB quantized ONNX model
- Fine-tuned on multilingual sentiment data
- Supports 104 languages
- Optimized for CPU inference

## OpenTelemetry

The library includes built-in OpenTelemetry support:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("Mostlylucid.SentimentAnalysis");
    });
```

## License

Unlicense - Free and unencumbered software released into the public domain.
