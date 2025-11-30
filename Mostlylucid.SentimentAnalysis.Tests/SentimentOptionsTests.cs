using Mostlylucid.SentimentAnalysis.Models;
using Xunit;

namespace Mostlylucid.SentimentAnalysis.Tests;

public class SentimentOptionsTests
{
    [Fact]
    public void SentimentOptions_HasCorrectDefaults()
    {
        var options = new SentimentOptions();

        Assert.Equal("./models/sentiment", options.ModelPath);
        Assert.Equal("sentiment_model.onnx", options.ModelFileName);
        Assert.Equal(450, options.MaxChunkLength);
        Assert.Equal(50, options.ChunkOverlap);
        Assert.False(options.EnableDiagnosticLogging);
        Assert.Equal(0, options.InferenceThreads);
        Assert.Equal(300, options.DownloadTimeoutSeconds);
        Assert.True(options.AutoDownloadModel);
    }

    [Fact]
    public void SentimentOptions_ModelLabels_HasThreeLabels()
    {
        var options = new SentimentOptions();

        Assert.Equal(3, options.ModelLabels.Length);
        Assert.Contains("negative", options.ModelLabels);
        Assert.Contains("neutral", options.ModelLabels);
        Assert.Contains("positive", options.ModelLabels);
    }

    [Fact]
    public void SentimentOptions_ModelUrl_IsValid()
    {
        var options = new SentimentOptions();

        Assert.NotEmpty(options.ModelUrl);
        Assert.True(Uri.TryCreate(options.ModelUrl, UriKind.Absolute, out var uri));
        Assert.Equal("https", uri.Scheme);
    }
}
