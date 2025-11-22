# Mostlylucid.LlmLogSummarizer

AI-powered log summarization using local LLMs. Clusters similar exceptions, identifies error patterns, and generates human-readable digests from Serilog, text files, and Application Insights logs.

## Features

- **Background Service**: Runs nightly/periodic summarization automatically
- **Multiple Log Sources**: Serilog JSON (compact format), plain text logs, Azure Application Insights
- **Exception Clustering**: Groups similar errors using fingerprinting and Levenshtein distance
- **LLM Summarization**: Uses local Ollama for privacy-preserving AI analysis
- **Multiple Outputs**: Markdown files, Email, Slack webhooks, custom webhooks
- **Trend Analysis**: Identifies new error types and increasing/decreasing trends

## Quick Start

```bash
# Install the package
dotnet add package Mostlylucid.LlmLogSummarizer
```

### Minimal Setup

```csharp
// Program.cs
builder.Services.AddLlmLogSummarizer(
    serilogPath: "logs/*.json",
    outputDirectory: "./logs/summaries");
```

### Full Configuration

```csharp
builder.Services.AddLlmLogSummarizer(options =>
{
    // Scheduling
    options.SummarizationInterval = TimeSpan.FromHours(24);
    options.DailyRunTime = TimeSpan.FromHours(2); // Run at 2 AM
    options.LookbackPeriod = TimeSpan.FromHours(24);

    // Log Sources
    options.Sources.SerilogFiles.Add(new SerilogSourceConfig
    {
        Name = "Application",
        Path = "logs/*.json"
    });

    // Ollama LLM
    options.Ollama.Endpoint = "http://localhost:11434";
    options.Ollama.Model = "llama3.2:3b";

    // Outputs
    options.Output.Markdown = new MarkdownOutputConfig
    {
        Enabled = true,
        OutputDirectory = "./logs/summaries"
    };

    options.Output.Slack = new SlackOutputConfig
    {
        Enabled = true,
        WebhookUrl = "https://hooks.slack.com/..."
    };
});
```

### From appsettings.json

```csharp
builder.Services.AddLlmLogSummarizer(builder.Configuration);
```

```json
{
  "LlmLogSummarizer": {
    "Enabled": true,
    "SummarizationInterval": "24:00:00",
    "DailyRunTime": "02:00:00",
    "LookbackPeriod": "24:00:00",
    "Ollama": {
      "Endpoint": "http://localhost:11434",
      "Model": "llama3.2:3b"
    },
    "Sources": {
      "SerilogFiles": [
        {
          "Name": "Application",
          "Path": "logs/*.json"
        }
      ]
    },
    "Output": {
      "Markdown": {
        "Enabled": true,
        "OutputDirectory": "./logs/summaries"
      }
    }
  }
}
```

## Prerequisites

### Ollama (Required for AI Summarization)

```bash
# Install Ollama
curl -fsSL https://ollama.com/install.sh | sh

# Pull a small, fast model
ollama pull llama3.2:3b

# Start the server
ollama serve
```

The summarizer works without Ollama too - it will just skip AI-powered summaries and use rule-based health assessment instead.

## Log Sources

### Serilog JSON (Compact Format)

Configure Serilog with compact JSON formatting:

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.File(
        new CompactJsonFormatter(),
        "logs/app-.json",
        rollingInterval: RollingInterval.Day)
    .CreateLogger();
```

### Plain Text Logs

Supports common text log formats with customizable regex parsing:

```json
{
  "TextFiles": [{
    "Name": "Legacy App",
    "Path": "logs/*.log",
    "ParsePattern": "^(?<timestamp>\\d{4}-\\d{2}-\\d{2}\\s+\\d{2}:\\d{2}:\\d{2})\\s*\\[(?<level>\\w+)\\]\\s*(?<message>.*)$"
  }]
}
```

### Azure Application Insights

```json
{
  "AppInsights": {
    "Name": "Production",
    "ApplicationId": "your-app-id",
    "ApiKey": "your-api-key"
  }
}
```

## Output Providers

### Markdown

Generates detailed markdown reports with:
- Executive summary
- Health status badge
- Top error patterns with stack traces
- New error types
- Trending errors
- Key insights and recommended actions

### Email

```json
{
  "Email": {
    "Enabled": true,
    "SmtpHost": "smtp.example.com",
    "SmtpPort": 587,
    "UseSsl": true,
    "Username": "alerts@example.com",
    "Password": "your-password",
    "FromAddress": "alerts@example.com",
    "ToAddresses": ["team@example.com"],
    "OnlyOnErrors": true
  }
}
```

### Slack

```json
{
  "Slack": {
    "Enabled": true,
    "WebhookUrl": "https://hooks.slack.com/services/...",
    "Channel": "#alerts",
    "OnlyOnErrors": false
  }
}
```

### Generic Webhook

```json
{
  "Webhook": {
    "Enabled": true,
    "Url": "https://your-api.com/log-summary",
    "Method": "POST",
    "Headers": {
      "Authorization": "Bearer your-token"
    }
  }
}
```

## Manual Triggering

You can also trigger summarization manually:

```csharp
app.MapPost("/summarize", async (ILogSummarizationOrchestrator orchestrator, CancellationToken ct) =>
{
    var report = await orchestrator.RunSummarizationAsync(ct);
    return Results.Ok(new
    {
        health = report.OverallHealth.ToString(),
        errors = report.ErrorCount,
        patterns = report.AllClusters.Count
    });
});
```

## How It Works

1. **Collection**: Reads logs from configured sources within the lookback period
2. **Clustering**: Groups similar errors using:
   - Exception type matching
   - Source context
   - Normalized message fingerprinting
   - Levenshtein distance for fuzzy matching
3. **Trend Analysis**: Compares with previous period to identify:
   - New error types
   - Increasing error rates
   - Decreasing error rates (improvements!)
4. **LLM Summarization**: Uses local Ollama to:
   - Summarize each error pattern
   - Generate suggested fixes
   - Create executive summary
   - Extract key insights
   - Assess overall health
5. **Output**: Sends results to configured providers

## Sample Output

```markdown
# Log Summary Report

**Generated:** 2024-01-15 02:00:00 UTC
**Period:** 2024-01-14 02:00 to 2024-01-15 02:00

## Overall Status: :yellow_circle: Degraded

## Executive Summary
The application experienced elevated error rates with 247 errors across 12 unique patterns.
A new database connection timeout issue emerged affecting 15% of requests.
The NullReferenceException in UserService continues to trend upward (+35%).

## Quick Stats
| Metric | Count |
|--------|-------|
| Total Logs Analyzed | 45,231 |
| Errors | 247 |
| Warnings | 892 |
| Unique Error Patterns | 12 |
| New Error Types | 2 |

## Top Error Patterns

### :orange_circle: NullReferenceException: Object reference not set...
**Occurrences:** 89 | **Severity:** High
**Summary:** Null reference occurring in UserService.GetProfile when user cache expires...
```

## License

This project is licensed under the Unlicense - see the LICENSE file for details.
