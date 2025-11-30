using Mostlylucid.SentimentAnalysis.Telemetry;

namespace Mostlylucid.SentimentAnalysis.Tests;

public class TelemetryTests
{
    [Fact]
    public void ActivitySourceName_IsCorrect()
    {
        Assert.Equal("Mostlylucid.SentimentAnalysis", SentimentTelemetry.ActivitySourceName);
    }

    [Fact]
    public void ActivitySource_IsNotNull()
    {
        Assert.NotNull(SentimentTelemetry.ActivitySource);
    }

    [Fact]
    public void ActivitySource_HasCorrectName()
    {
        Assert.Equal(SentimentTelemetry.ActivitySourceName, SentimentTelemetry.ActivitySource.Name);
    }

    [Fact]
    public void StartAnalyzeActivity_ReturnsActivity()
    {
        // Activity may be null if no listeners, but method should not throw
        var activity = SentimentTelemetry.StartAnalyzeActivity(100);

        // If activity is created, verify tags
        if (activity != null)
        {
            Assert.Equal("analyze", activity.GetTagItem("sentiment.operation"));
            Assert.Equal(100, activity.GetTagItem("sentiment.text_length"));
            activity.Dispose();
        }
    }

    [Fact]
    public void StartAnalyzeFileActivity_ReturnsActivity()
    {
        var activity = SentimentTelemetry.StartAnalyzeFileActivity("/test/path.txt");

        if (activity != null)
        {
            Assert.Equal("analyze_file", activity.GetTagItem("sentiment.operation"));
            Assert.Equal("/test/path.txt", activity.GetTagItem("sentiment.file_path"));
            activity.Dispose();
        }
    }

    [Fact]
    public void StartAnalyzeLongTextActivity_ReturnsActivity()
    {
        var activity = SentimentTelemetry.StartAnalyzeLongTextActivity(5000);

        if (activity != null)
        {
            Assert.Equal("analyze_long_text", activity.GetTagItem("sentiment.operation"));
            Assert.Equal(5000, activity.GetTagItem("sentiment.text_length"));
            activity.Dispose();
        }
    }

    [Fact]
    public void StartModelDownloadActivity_ReturnsActivity()
    {
        var activity = SentimentTelemetry.StartModelDownloadActivity();

        if (activity != null)
        {
            Assert.Equal("model_download", activity.GetTagItem("sentiment.operation"));
            activity.Dispose();
        }
    }
}
