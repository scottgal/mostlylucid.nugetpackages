using Mostlylucid.SentimentAnalysis.Models;
using Xunit;

namespace Mostlylucid.SentimentAnalysis.Tests;

public class SentimentLabelTests
{
    [Fact]
    public void SentimentLabel_HasCorrectValues()
    {
        Assert.Equal(1, (int)SentimentLabel.VeryNegative);
        Assert.Equal(2, (int)SentimentLabel.Negative);
        Assert.Equal(3, (int)SentimentLabel.Neutral);
        Assert.Equal(4, (int)SentimentLabel.Positive);
        Assert.Equal(5, (int)SentimentLabel.VeryPositive);
    }

    [Fact]
    public void SentimentLabel_HasFiveValues()
    {
        var values = Enum.GetValues<SentimentLabel>();
        Assert.Equal(5, values.Length);
    }

    [Theory]
    [InlineData(SentimentLabel.VeryNegative, "VeryNegative")]
    [InlineData(SentimentLabel.Negative, "Negative")]
    [InlineData(SentimentLabel.Neutral, "Neutral")]
    [InlineData(SentimentLabel.Positive, "Positive")]
    [InlineData(SentimentLabel.VeryPositive, "VeryPositive")]
    public void SentimentLabel_ToString_ReturnsCorrectName(SentimentLabel label, string expected)
    {
        Assert.Equal(expected, label.ToString());
    }

    [Theory]
    [InlineData("VeryNegative", SentimentLabel.VeryNegative)]
    [InlineData("Negative", SentimentLabel.Negative)]
    [InlineData("Neutral", SentimentLabel.Neutral)]
    [InlineData("Positive", SentimentLabel.Positive)]
    [InlineData("VeryPositive", SentimentLabel.VeryPositive)]
    public void SentimentLabel_Parse_ReturnsCorrectValue(string name, SentimentLabel expected)
    {
        var result = Enum.Parse<SentimentLabel>(name);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SentimentLabel_OrderIsLogical()
    {
        // Verify that sentiment values increase from negative to positive
        Assert.True(SentimentLabel.VeryNegative < SentimentLabel.Negative);
        Assert.True(SentimentLabel.Negative < SentimentLabel.Neutral);
        Assert.True(SentimentLabel.Neutral < SentimentLabel.Positive);
        Assert.True(SentimentLabel.Positive < SentimentLabel.VeryPositive);
    }
}
