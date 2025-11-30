using System.Diagnostics;
using System.Reflection;

namespace Mostlylucid.SentimentAnalysis.Telemetry;

/// <summary>
/// OpenTelemetry ActivitySource for sentiment analysis operations.
/// </summary>
public static class SentimentTelemetry
{
    public const string ActivitySourceName = "Mostlylucid.SentimentAnalysis";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, GetVersion());

    private static string GetVersion()
    {
        return Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "1.0.0";
    }

    /// <summary>
    /// Starts an activity for analyzing text sentiment.
    /// </summary>
    public static Activity? StartAnalyzeActivity(int textLength)
    {
        var activity = ActivitySource.StartActivity("SentimentAnalysis.Analyze");
        activity?.SetTag("sentiment.operation", "analyze");
        activity?.SetTag("sentiment.text_length", textLength);
        return activity;
    }

    /// <summary>
    /// Starts an activity for analyzing file sentiment.
    /// </summary>
    public static Activity? StartAnalyzeFileActivity(string filePath)
    {
        var activity = ActivitySource.StartActivity("SentimentAnalysis.AnalyzeFile");
        activity?.SetTag("sentiment.operation", "analyze_file");
        activity?.SetTag("sentiment.file_path", filePath);
        return activity;
    }

    /// <summary>
    /// Starts an activity for analyzing long text with chunking.
    /// </summary>
    public static Activity? StartAnalyzeLongTextActivity(int textLength)
    {
        var activity = ActivitySource.StartActivity("SentimentAnalysis.AnalyzeLongText");
        activity?.SetTag("sentiment.operation", "analyze_long_text");
        activity?.SetTag("sentiment.text_length", textLength);
        return activity;
    }

    /// <summary>
    /// Starts an activity for downloading the sentiment model.
    /// </summary>
    public static Activity? StartModelDownloadActivity()
    {
        var activity = ActivitySource.StartActivity("SentimentAnalysis.ModelDownload");
        activity?.SetTag("sentiment.operation", "model_download");
        return activity;
    }
}
