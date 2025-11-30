namespace Mostlylucid.SentimentAnalysis.Models;

/// <summary>
/// Represents the result of sentiment analysis on a piece of text.
/// </summary>
public class SentimentResult
{
    /// <summary>
    /// The original text that was analyzed.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// The predicted sentiment label.
    /// </summary>
    public required SentimentLabel Sentiment { get; init; }

    /// <summary>
    /// The confidence score for the prediction (0.0 to 1.0).
    /// </summary>
    public required float Confidence { get; init; }

    /// <summary>
    /// Individual scores for each sentiment class.
    /// Keys are sentiment labels, values are probability scores.
    /// </summary>
    public required Dictionary<SentimentLabel, float> Scores { get; init; }

    /// <summary>
    /// Whether the overall sentiment is considered positive (Positive or VeryPositive).
    /// </summary>
    public bool IsPositive => Sentiment is SentimentLabel.Positive or SentimentLabel.VeryPositive;

    /// <summary>
    /// Whether the overall sentiment is considered negative (Negative or VeryNegative).
    /// </summary>
    public bool IsNegative => Sentiment is SentimentLabel.Negative or SentimentLabel.VeryNegative;

    /// <summary>
    /// Whether the sentiment is neutral.
    /// </summary>
    public bool IsNeutral => Sentiment == SentimentLabel.Neutral;

    /// <summary>
    /// Gets a normalized score from -1 (very negative) to +1 (very positive).
    /// </summary>
    public float NormalizedScore => ((int)Sentiment - 3) / 2.0f;

    public override string ToString()
    {
        return $"{Sentiment} ({Confidence:P1})";
    }
}

/// <summary>
/// Represents aggregated sentiment analysis results for multiple text chunks (e.g., from a file).
/// </summary>
public class AggregateSentimentResult
{
    /// <summary>
    /// The source of the text (e.g., file path or "direct input").
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// The overall predicted sentiment based on aggregated chunk results.
    /// </summary>
    public required SentimentLabel OverallSentiment { get; init; }

    /// <summary>
    /// The average confidence across all chunks.
    /// </summary>
    public required float AverageConfidence { get; init; }

    /// <summary>
    /// Individual results for each analyzed chunk.
    /// </summary>
    public required IReadOnlyList<SentimentResult> ChunkResults { get; init; }

    /// <summary>
    /// Total number of chunks analyzed.
    /// </summary>
    public int ChunkCount => ChunkResults.Count;

    /// <summary>
    /// Distribution of sentiments across chunks.
    /// </summary>
    public required Dictionary<SentimentLabel, int> SentimentDistribution { get; init; }

    /// <summary>
    /// The weighted average score across all chunks (-1 to +1 scale).
    /// </summary>
    public required float WeightedScore { get; init; }

    /// <summary>
    /// Whether the overall sentiment is positive.
    /// </summary>
    public bool IsPositive => OverallSentiment is SentimentLabel.Positive or SentimentLabel.VeryPositive;

    /// <summary>
    /// Whether the overall sentiment is negative.
    /// </summary>
    public bool IsNegative => OverallSentiment is SentimentLabel.Negative or SentimentLabel.VeryNegative;

    public override string ToString()
    {
        return $"Overall: {OverallSentiment} (avg confidence: {AverageConfidence:P1}, {ChunkCount} chunks)";
    }
}
