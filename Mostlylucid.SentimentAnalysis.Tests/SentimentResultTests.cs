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
        var result = CreateResult(sentiment);

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
        var result = CreateResult(sentiment);
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

    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.5f)]
    [InlineData(1.0f)]
    public void SentimentResult_Confidence_InValidRange(float confidence)
    {
        var result = new SentimentResult
        {
            Text = "test",
            Sentiment = SentimentLabel.Neutral,
            Confidence = confidence,
            Scores = new Dictionary<SentimentLabel, float>()
        };

        Assert.Equal(confidence, result.Confidence);
        Assert.InRange(result.Confidence, 0.0f, 1.0f);
    }

    [Fact]
    public void SentimentResult_Text_IsPreserved()
    {
        var originalText = "This is a test sentence.";
        var result = new SentimentResult
        {
            Text = originalText,
            Sentiment = SentimentLabel.Neutral,
            Confidence = 0.8f,
            Scores = new Dictionary<SentimentLabel, float>()
        };

        Assert.Equal(originalText, result.Text);
    }

    [Fact]
    public void SentimentResult_Scores_ContainsMultipleLabels()
    {
        var scores = new Dictionary<SentimentLabel, float>
        {
            { SentimentLabel.VeryNegative, 0.05f },
            { SentimentLabel.Negative, 0.1f },
            { SentimentLabel.Neutral, 0.15f },
            { SentimentLabel.Positive, 0.4f },
            { SentimentLabel.VeryPositive, 0.3f }
        };

        var result = new SentimentResult
        {
            Text = "test",
            Sentiment = SentimentLabel.Positive,
            Confidence = 0.4f,
            Scores = scores
        };

        Assert.Equal(5, result.Scores.Count);
        Assert.Equal(0.4f, result.Scores[SentimentLabel.Positive]);
    }

    [Fact]
    public void SentimentResult_MutualExclusivity_OfFlags()
    {
        foreach (var sentiment in Enum.GetValues<SentimentLabel>())
        {
            var result = CreateResult(sentiment);

            // Only one of these should be true at a time
            var trueCount = new[] { result.IsPositive, result.IsNegative, result.IsNeutral }
                .Count(x => x);

            Assert.Equal(1, trueCount);
        }
    }

    [Theory]
    [InlineData(SentimentLabel.VeryPositive)]
    [InlineData(SentimentLabel.Positive)]
    public void SentimentResult_IsPositive_TrueForPositiveSentiments(SentimentLabel sentiment)
    {
        var result = CreateResult(sentiment);
        Assert.True(result.IsPositive);
        Assert.False(result.IsNegative);
        Assert.False(result.IsNeutral);
    }

    [Theory]
    [InlineData(SentimentLabel.Negative)]
    [InlineData(SentimentLabel.VeryNegative)]
    public void SentimentResult_IsNegative_TrueForNegativeSentiments(SentimentLabel sentiment)
    {
        var result = CreateResult(sentiment);
        Assert.True(result.IsNegative);
        Assert.False(result.IsPositive);
        Assert.False(result.IsNeutral);
    }

    [Fact]
    public void SentimentResult_IsNeutral_TrueOnlyForNeutral()
    {
        var result = CreateResult(SentimentLabel.Neutral);
        Assert.True(result.IsNeutral);
        Assert.False(result.IsPositive);
        Assert.False(result.IsNegative);
    }

    [Fact]
    public void SentimentResult_NormalizedScore_RangeIsCorrect()
    {
        foreach (var sentiment in Enum.GetValues<SentimentLabel>())
        {
            var result = CreateResult(sentiment);
            Assert.InRange(result.NormalizedScore, -1.0f, 1.0f);
        }
    }

    private static SentimentResult CreateResult(SentimentLabel sentiment, float confidence = 0.9f)
    {
        return new SentimentResult
        {
            Text = "test",
            Sentiment = sentiment,
            Confidence = confidence,
            Scores = new Dictionary<SentimentLabel, float>
            {
                { sentiment, confidence }
            }
        };
    }
}
