using Mostlylucid.SentimentAnalysis.Models;

namespace Mostlylucid.SentimentAnalysis.Services;

/// <summary>
/// Service for analyzing sentiment of text using ONNX models.
/// </summary>
public interface ISentimentAnalysisService
{
    /// <summary>
    /// Indicates whether the service is ready to process requests.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Analyzes the sentiment of a single text string.
    /// </summary>
    /// <param name="text">The text to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sentiment analysis result.</returns>
    Task<SentimentResult> AnalyzeAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes the sentiment of multiple text strings.
    /// </summary>
    /// <param name="texts">The texts to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Sentiment results for each input text.</returns>
    Task<IReadOnlyList<SentimentResult>> AnalyzeBatchAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes the sentiment of text from a file.
    /// Long files are automatically chunked and results aggregated.
    /// </summary>
    /// <param name="filePath">Path to the file to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Aggregated sentiment results from the file.</returns>
    Task<AggregateSentimentResult> AnalyzeFileAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes the sentiment of text from a stream.
    /// Long content is automatically chunked and results aggregated.
    /// </summary>
    /// <param name="stream">The stream containing text to analyze.</param>
    /// <param name="sourceName">Optional name to identify the source.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Aggregated sentiment results from the stream.</returns>
    Task<AggregateSentimentResult> AnalyzeStreamAsync(
        Stream stream,
        string sourceName = "stream",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes long text by chunking and aggregating results.
    /// </summary>
    /// <param name="text">The text to analyze.</param>
    /// <param name="sourceName">Optional name to identify the source.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Aggregated sentiment results.</returns>
    Task<AggregateSentimentResult> AnalyzeLongTextAsync(
        string text,
        string sourceName = "text",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a simple sentiment classification (positive, negative, or neutral).
    /// </summary>
    /// <param name="text">The text to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sentiment label.</returns>
    Task<SentimentLabel> GetSentimentAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a normalized sentiment score from -1 (very negative) to +1 (very positive).
    /// </summary>
    /// <param name="text">The text to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A normalized score between -1 and +1.</returns>
    Task<float> GetSentimentScoreAsync(string text, CancellationToken cancellationToken = default);
}
