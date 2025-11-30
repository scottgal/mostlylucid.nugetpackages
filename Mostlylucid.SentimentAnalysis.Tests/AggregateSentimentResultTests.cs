using Mostlylucid.SentimentAnalysis.Models;

namespace Mostlylucid.SentimentAnalysis.Tests;

public class AggregateSentimentResultTests
{
    [Fact]
    public void AggregateSentimentResult_ChunkCount_ReturnsCorrectValue()
    {
        var chunkResults = new List<SentimentResult>
        {
            CreateResult(SentimentLabel.Positive),
            CreateResult(SentimentLabel.Negative),
            CreateResult(SentimentLabel.Neutral)
        };

        var result = new AggregateSentimentResult
        {
            Source = "test",
            OverallSentiment = SentimentLabel.Neutral,
            AverageConfidence = 0.8f,
            ChunkResults = chunkResults,
            SentimentDistribution = new Dictionary<SentimentLabel, int>
            {
                { SentimentLabel.Positive, 1 },
                { SentimentLabel.Negative, 1 },
                { SentimentLabel.Neutral, 1 }
            },
            WeightedScore = 0.0f
        };

        Assert.Equal(3, result.ChunkCount);
    }

    [Theory]
    [InlineData(SentimentLabel.Positive, true, false)]
    [InlineData(SentimentLabel.VeryPositive, true, false)]
    [InlineData(SentimentLabel.Negative, false, true)]
    [InlineData(SentimentLabel.VeryNegative, false, true)]
    [InlineData(SentimentLabel.Neutral, false, false)]
    public void AggregateSentimentResult_Flags_AreCorrect(
        SentimentLabel sentiment, bool isPositive, bool isNegative)
    {
        var result = new AggregateSentimentResult
        {
            Source = "test",
            OverallSentiment = sentiment,
            AverageConfidence = 0.8f,
            ChunkResults = [],
            SentimentDistribution = new Dictionary<SentimentLabel, int>(),
            WeightedScore = 0.0f
        };

        Assert.Equal(isPositive, result.IsPositive);
        Assert.Equal(isNegative, result.IsNegative);
    }

    [Fact]
    public void AggregateSentimentResult_ToString_IncludesAllInfo()
    {
        var result = new AggregateSentimentResult
        {
            Source = "test",
            OverallSentiment = SentimentLabel.Positive,
            AverageConfidence = 0.75f,
            ChunkResults = [CreateResult(SentimentLabel.Positive), CreateResult(SentimentLabel.Positive)],
            SentimentDistribution = new Dictionary<SentimentLabel, int>
            {
                { SentimentLabel.Positive, 2 }
            },
            WeightedScore = 0.5f
        };

        var str = result.ToString();
        Assert.Contains("Positive", str);
        Assert.Contains("2 chunks", str);
    }

    private static SentimentResult CreateResult(SentimentLabel sentiment)
    {
        return new SentimentResult
        {
            Text = "test",
            Sentiment = sentiment,
            Confidence = 0.8f,
            Scores = new Dictionary<SentimentLabel, float>
            {
                { sentiment, 0.8f }
            }
        };
    }
}
