using Mostlylucid.SentimentAnalysis.Models;

namespace Mostlylucid.SentimentAnalysis.Tests;

public class SentimentResultTests
{
    [Theory]
    [InlineData(SentimentLabel.VeryPositive, true, false, false)]
    [InlineData(SentimentLabel.Positive, true, false, false)]
    [InlineData(SentimentLabel.Neutral, false, false, true)]
    [InlineData(SentimentLabel.Negative, false, true, false)]
    [InlineData(SentimentLabel.VeryNegative, false, true, false)]
    public void SentimentResult_Flags_AreCorrect(
        SentimentLabel sentiment, bool isPositive, bool isNegative, bool isNeutral)
    {
        var result = new SentimentResult
        {
            Text = "test",
            Sentiment = sentiment,
            Confidence = 0.9f,
            Scores = new Dictionary<SentimentLabel, float>
            {
                { sentiment, 0.9f }
            }
        };

        Assert.Equal(isPositive, result.IsPositive);
        Assert.Equal(isNegative, result.IsNegative);
        Assert.Equal(isNeutral, result.IsNeutral);
    }

    [Theory]
    [InlineData(SentimentLabel.VeryPositive, 1.0f)]
    [InlineData(SentimentLabel.Positive, 0.5f)]
    [InlineData(SentimentLabel.Neutral, 0.0f)]
    [InlineData(SentimentLabel.Negative, -0.5f)]
    [InlineData(SentimentLabel.VeryNegative, -1.0f)]
    public void SentimentResult_NormalizedScore_IsCorrect(SentimentLabel sentiment, float expectedScore)
    {
        var result = new SentimentResult
        {
            Text = "test",
            Sentiment = sentiment,
            Confidence = 0.9f,
            Scores = new Dictionary<SentimentLabel, float>
            {
                { sentiment, 0.9f }
            }
        };

        Assert.Equal(expectedScore, result.NormalizedScore);
    }

    [Fact]
    public void SentimentResult_ToString_FormatsCorrectly()
    {
        var result = new SentimentResult
        {
            Text = "test",
            Sentiment = SentimentLabel.Positive,
            Confidence = 0.85f,
            Scores = new Dictionary<SentimentLabel, float>
            {
                { SentimentLabel.Positive, 0.85f }
            }
        };

        var str = result.ToString();
        Assert.Contains("Positive", str);
        Assert.Contains("85", str); // Should contain the percentage
    }
}
