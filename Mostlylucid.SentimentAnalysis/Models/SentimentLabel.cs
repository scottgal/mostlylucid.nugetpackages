namespace Mostlylucid.SentimentAnalysis.Models;

/// <summary>
/// Represents the sentiment classification of analyzed text.
/// </summary>
public enum SentimentLabel
{
    /// <summary>
    /// Strongly negative sentiment (1 star equivalent).
    /// </summary>
    VeryNegative = 1,

    /// <summary>
    /// Negative sentiment (2 star equivalent).
    /// </summary>
    Negative = 2,

    /// <summary>
    /// Neutral sentiment (3 star equivalent).
    /// </summary>
    Neutral = 3,

    /// <summary>
    /// Positive sentiment (4 star equivalent).
    /// </summary>
    Positive = 4,

    /// <summary>
    /// Strongly positive sentiment (5 star equivalent).
    /// </summary>
    VeryPositive = 5
}
