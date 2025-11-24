# Building an AI-Powered Log Summarizer with Local LLMs

If you're running any kind of production system, you know the pain: logs pile up, exceptions scroll by, and somewhere in
that sea of text are the patterns that matter. Traditional monitoring tools help, but they often require expensive
subscriptions, send your data to third parties, and still leave you drowning in alerts.

What if you could have an AI assistant that reads your logs every night, clusters similar errors, identifies trends, and
delivers a human-readable summary to your inbox or Slack channel? And what if it ran entirely locally, for free?

That's exactly what `Mostlylucid.LlmLogSummarizer` does.

## The Problem

Modern applications generate enormous amounts of log data. Even a modest web application might produce:

- Thousands of INFO messages per hour
- Hundreds of warnings about deprecations, slow queries, or edge cases
- Dozens of errors that need attention
- Occasional critical failures that need immediate action

The challenge isn't collecting this data - Serilog, Application Insights, and similar tools handle that well. The
challenge is making sense of it all.

Traditional approaches have problems:

- **Alert fatigue**: Too many notifications train you to ignore them
- **Pattern blindness**: Similar errors with different IDs look like separate issues
- **Context loss**: Stack traces don't explain *why* something matters
- **Trend invisibility**: Gradual increases go unnoticed until they become crises

## The Solution: AI-Powered Log Analysis

Large Language Models are remarkably good at:

- Summarizing technical content
- Identifying patterns
- Explaining complex errors in plain language
- Suggesting investigation steps

And with local models like Llama 3.2 running through Ollama, you can do all of this:

- **Privately**: Your logs never leave your infrastructure
- **For free**: No API costs or subscription fees
- **Quickly**: Small models run in seconds, not minutes

## Architecture Overview

The `LlmLogSummarizer` package consists of several key components:

```
┌─────────────────────────────────────────────────────────┐
│                   Background Service                     │
│              (runs daily/hourly/on-demand)               │
└─────────────────┬───────────────────────────────────────┘
                  │
    ┌─────────────▼─────────────┐
    │    Log Source Aggregator   │
    │  ┌─────┐ ┌─────┐ ┌─────┐  │
    │  │Seri-│ │Text │ │App  │  │
    │  │log  │ │Logs │ │Ins. │  │
    │  └─────┘ └─────┘ └─────┘  │
    └─────────────┬─────────────┘
                  │
    ┌─────────────▼─────────────┐
    │    Exception Clusterer     │
    │  (fingerprinting + fuzzy   │
    │   matching + trends)       │
    └─────────────┬─────────────┘
                  │
    ┌─────────────▼─────────────┐
    │    LLM Summarizer          │
    │  (Ollama local inference)  │
    └─────────────┬─────────────┘
                  │
    ┌─────────────▼─────────────┐
    │    Output Providers        │
    │  ┌────┐ ┌─────┐ ┌──────┐  │
    │  │ MD │ │Email│ │Slack │  │
    │  └────┘ └─────┘ └──────┘  │
    └───────────────────────────┘
```

## Key Features

### 1. Smart Exception Clustering

Not all "NullReferenceException" errors are the same. The clusterer uses multiple signals to group related errors:

```csharp
public string GetClusteringFingerprint()
{
    var parts = new List<string>();

    if (!string.IsNullOrEmpty(ExceptionType))
        parts.Add(ExceptionType);

    if (!string.IsNullOrEmpty(SourceContext))
        parts.Add(SourceContext);

    // Normalize variable parts (GUIDs, numbers, quoted strings)
    var normalizedMessage = NormalizeMessage(Message);
    if (!string.IsNullOrEmpty(normalizedMessage))
        parts.Add(normalizedMessage);

    // First stack frame for specificity
    if (!string.IsNullOrEmpty(StackTrace))
    {
        var firstStackLine = StackTrace.Split('\n').FirstOrDefault()?.Trim();
        if (!string.IsNullOrEmpty(firstStackLine))
            parts.Add(firstStackLine);
    }

    return string.Join("|", parts);
}
```

This means "User 12345 not found" and "User 67890 not found" get clustered together, while "User not found" in different
services stay separate.

### 2. Trend Analysis

By comparing current clusters with historical data, we can identify:

- **New error types**: First appearance in this period
- **Trending up**: Increasing frequency (potential emerging issue)
- **Trending down**: Decreasing frequency (maybe you fixed something!)

```csharp
if (historical == null)
{
    current.IsNew = true;
    current.TrendPercent = 100; // Brand new pattern
}
else
{
    current.PreviousPeriodCount = historical.Count;
    current.TrendPercent = ((double)(current.Count - historical.Count) / historical.Count) * 100;
}
```

### 3. LLM-Powered Summaries

Each cluster gets an AI-generated summary and suggested actions:

```csharp
var prompt = $"""
You are a software engineer analyzing application logs.
Summarize this error pattern concisely (2-3 sentences).

Exception Type: {cluster.ExceptionType}
Occurrences: {cluster.Count}
Error Message: {cluster.RepresentativeMessage}
Stack Trace (first 5 lines): {stackPreview}

Provide a brief technical summary of what this error means and its potential impact.
""";
```

The LLM turns cryptic stack traces into actionable insights like:

> "This NullReferenceException occurs when the user cache expires and GetProfile attempts to access the cached object
> without null checking. With 89 occurrences in 24 hours and increasing frequency, this is likely impacting user
> experience. The pattern suggests the cache invalidation logic may not be properly handling edge cases."

### 4. Multiple Output Channels

Results can go to:

- **Markdown files**: Perfect for archiving and searching
- **Email**: HTML and text versions with configurable recipients
- **Slack**: Rich block-formatted messages
- **Webhooks**: JSON payload for custom integrations

## Getting Started

### 1. Install Ollama

```bash
curl -fsSL https://ollama.com/install.sh | sh
ollama pull llama3.2:3b
ollama serve
```

### 2. Configure Your Application

```csharp
// Configure Serilog to write JSON logs
Log.Logger = new LoggerConfiguration()
    .WriteTo.File(
        new CompactJsonFormatter(),
        "logs/app-.json",
        rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add the log summarizer
builder.Services.AddLlmLogSummarizer(options =>
{
    options.DailyRunTime = TimeSpan.FromHours(2); // Run at 2 AM
    options.Sources.SerilogFiles.Add(new SerilogSourceConfig
    {
        Name = "Application",
        Path = "logs/*.json"
    });
    options.Output.Markdown = new MarkdownOutputConfig
    {
        Enabled = true,
        OutputDirectory = "./logs/summaries"
    };
});
```

### 3. Generate Some Logs and Summarize

```csharp
// Manually trigger (or let the background service run)
app.MapPost("/summarize", async (ILogSummarizationOrchestrator orchestrator) =>
{
    var report = await orchestrator.RunSummarizationAsync();
    return Results.Ok(new
    {
        health = report.OverallHealth,
        errors = report.ErrorCount,
        summary = report.ExecutiveSummary
    });
});
```

## Sample Output

Here's what a typical summary looks like:

```markdown
# Log Summary Report

**Period:** 2024-01-14 02:00 to 2024-01-15 02:00

## Overall Status: :yellow_circle: Degraded

## Executive Summary
The application experienced 247 errors across 12 unique patterns.
A new database connection timeout pattern emerged affecting the UserService.
The NullReferenceException in ProfileController continues trending upward (+35%).
Priority should be given to investigating the database connectivity issues.

## Key Insights
- New timeout errors correlate with peak traffic hours (9-11 AM)
- 78% of errors originate from three source contexts
- Error rate increased 23% compared to previous 24-hour period

## Top Error Patterns

### :orange_circle: SqlException: Connection timeout expired
**Occurrences:** 89 | **Severity:** High
**Summary:** Database connection pool exhaustion occurring during peak load...
**Suggested Actions:**
- Increase connection pool size in connection string
- Add retry logic with exponential backoff
- Consider read replica for read-heavy operations
```

## Performance Considerations

The summarizer is designed for efficiency:

- **Streaming log reads**: Files are read line-by-line, not loaded entirely
- **Configurable limits**: Set max entries per run to control memory
- **Parallel sources**: Multiple log sources can be read concurrently
- **LLM batching**: Only top clusters get AI summarization (configurable)
- **Incremental trends**: Historical data is kept in memory between runs

A typical run with 50,000 log entries:

- Collection: ~2-5 seconds
- Clustering: ~1-2 seconds
- LLM summarization (10 clusters): ~30-60 seconds
- Total: Under 2 minutes

## Extensibility

The package is designed to be extensible:

### Custom Log Sources

Implement `ILogSource` to add new sources:

```csharp
public class ElasticsearchLogSource : ILogSource
{
    public async IAsyncEnumerable<LogEntry> GetEntriesAsync(
        DateTimeOffset from, DateTimeOffset to, int maxEntries,
        CancellationToken ct = default)
    {
        // Query Elasticsearch and yield LogEntry objects
    }
}
```

### Custom Output Providers

Implement `IOutputProvider` to add new outputs:

```csharp
public class PagerDutyOutputProvider : IOutputProvider
{
    public async Task OutputAsync(SummaryReport report, CancellationToken ct)
    {
        if (report.OverallHealth == HealthStatus.Critical)
        {
            // Create PagerDuty incident
        }
    }
}
```

## Why Local LLMs?

You might wonder why not use OpenAI or Claude for this. There are good reasons:

1. **Privacy**: Production logs often contain sensitive data - user IDs, internal service names, maybe even PII. Keeping
   everything local means no data leaves your infrastructure.

2. **Cost**: If you're running this daily across multiple services, API costs add up. Local inference is free after the
   initial model download.

3. **Latency**: Local models respond in seconds. API calls can take longer, especially under load.

4. **Reliability**: No external dependencies means the summarizer works even when your internet is down (which might be
   when you need it most!).

5. **Customization**: You can fine-tune local models on your specific log patterns and terminology.

## Conclusion

`Mostlylucid.LlmLogSummarizer` brings AI-powered log analysis to .NET applications with minimal setup. By running
locally, it provides the benefits of LLM analysis without the privacy concerns or costs of cloud APIs.

The key insight is that you don't need cutting-edge models for this task. Smaller, faster models like Llama 3.2 3B are
perfectly capable of summarizing errors, identifying patterns, and generating actionable insights. What matters is
having the right pipeline to collect, cluster, and present the data.

Give it a try - your future self debugging a production incident at 2 AM will thank you.

---

## Installation

```bash
dotnet add package Mostlylucid.LlmLogSummarizer
```

## Links

- [GitHub Repository](https://github.com/scottgal/mostlylucid.nugetpackages)
- [Ollama](https://ollama.com)
- [Serilog Compact JSON Formatter](https://github.com/serilog/serilog-formatting-compact)
