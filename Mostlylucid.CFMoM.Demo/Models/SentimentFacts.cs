namespace Mostlylucid.CFMoM.Demo.Models;

/// <summary>
///     Facts schema for sentiment analysis signals.
/// </summary>
public sealed record SentimentFacts
{
    /// <summary>
    ///     Schema ID for this facts type.
    /// </summary>
    public const string SchemaId = "sentiment.v1";

    /// <summary>
    ///     The detected sentiment: "positive", "negative", or "neutral".
    /// </summary>
    public required string Sentiment { get; init; }

    /// <summary>
    ///     Intensity of the sentiment (0-1).
    /// </summary>
    public required double Intensity { get; init; }

    /// <summary>
    ///     Emotion categories detected.
    /// </summary>
    public string[] Emotions { get; init; } = [];

    /// <summary>
    ///     Words that contributed to sentiment detection.
    /// </summary>
    public string[] IndicatorWords { get; init; } = [];
}
