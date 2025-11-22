using Mostlylucid.LlmLogSummarizer.Extensions;
using Mostlylucid.LlmLogSummarizer.Models;
using Mostlylucid.LlmLogSummarizer.Services;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog to write JSON logs that we can analyze
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File(
        new CompactJsonFormatter(),
        "logs/app-.json",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

builder.Host.UseSerilog();

// Add the log summarizer
builder.Services.AddLlmLogSummarizer(options =>
{
    // Run every hour for demo (normally you'd run daily)
    options.SummarizationInterval = TimeSpan.FromHours(1);
    options.LookbackPeriod = TimeSpan.FromHours(24);
    options.RunOnStartup = false; // Don't run immediately - we'll trigger manually
    options.EnableDiagnosticLogging = true;

    // Configure log sources
    options.Sources.SerilogFiles.Add(new SerilogSourceConfig
    {
        Name = "Application Logs",
        Path = "logs/*.json"
    });

    // Configure Ollama (ensure Ollama is running locally)
    options.Ollama.Endpoint = "http://localhost:11434";
    options.Ollama.Model = "llama3.2:3b"; // Use a small, fast model

    // Configure outputs
    options.Output.Markdown = new MarkdownOutputConfig
    {
        Enabled = true,
        OutputDirectory = "./logs/summaries"
    };

    // Optionally configure Slack (set your webhook URL)
    // options.Output.Slack = new SlackOutputConfig
    // {
    //     Enabled = true,
    //     WebhookUrl = "https://hooks.slack.com/services/YOUR/WEBHOOK/URL"
    // };
});

// Add services
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Demo endpoint to generate some log entries
app.MapGet("/", () =>
{
    Log.Information("Home page accessed");
    return "Log Summarizer Demo - Generate some errors with /error or /simulate";
});

// Endpoint to generate a single error
app.MapGet("/error", () =>
{
    try
    {
        throw new InvalidOperationException("This is a test error for the log summarizer demo");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred in the /error endpoint");
        return Results.Problem("Intentional error generated");
    }
});

// Endpoint to simulate various types of errors
app.MapGet("/simulate", (int? count) =>
{
    var errorCount = count ?? 20;
    var random = new Random();
    var errors = new[]
    {
        ("NullReferenceException", "Object reference not set to an instance of an object"),
        ("ArgumentException", "Value cannot be null. Parameter name: userId"),
        ("InvalidOperationException", "Sequence contains no elements"),
        ("TimeoutException", "The operation has timed out"),
        ("HttpRequestException", "An error occurred while sending the request"),
        ("DatabaseException", "Cannot open database requested by the login. The login failed"),
        ("FileNotFoundException", "Could not find file 'config.json'"),
        ("UnauthorizedAccessException", "Access to the path is denied"),
    };

    for (var i = 0; i < errorCount; i++)
    {
        var (exType, message) = errors[random.Next(errors.Length)];
        var severity = random.Next(100);

        if (severity < 5)
        {
            Log.Fatal(new Exception($"{exType}: {message}"),
                "Critical failure in processing request {RequestId}",
                Guid.NewGuid().ToString("N")[..8]);
        }
        else if (severity < 30)
        {
            Log.Error(new Exception($"{exType}: {message}"),
                "Error processing request {RequestId} for user {UserId}",
                Guid.NewGuid().ToString("N")[..8],
                random.Next(1000, 9999));
        }
        else if (severity < 60)
        {
            Log.Warning("Potential issue detected: {Issue} for request {RequestId}",
                message, Guid.NewGuid().ToString("N")[..8]);
        }
        else
        {
            Log.Information("Processed request {RequestId} successfully",
                Guid.NewGuid().ToString("N")[..8]);
        }
    }

    return Results.Ok(new
    {
        message = $"Generated {errorCount} simulated log entries",
        checkLogsAt = "./logs/app-*.json"
    });
});

// Manual trigger for summarization
app.MapPost("/summarize", async (ILogSummarizationOrchestrator orchestrator, CancellationToken ct) =>
{
    Log.Information("Manual summarization triggered");

    try
    {
        var report = await orchestrator.RunSummarizationAsync(ct);

        return Results.Ok(new
        {
            health = report.OverallHealth.ToString(),
            totalLogs = report.TotalLogsAnalyzed,
            errors = report.ErrorCount,
            warnings = report.WarningCount,
            uniquePatterns = report.AllClusters.Count,
            newErrorTypes = report.NewErrorTypes.Count,
            executiveSummary = report.ExecutiveSummary,
            keyInsights = report.KeyInsights,
            topErrors = report.TopErrorPatterns.Take(5).Select(c => new
            {
                title = c.Title,
                count = c.Count,
                severity = c.Severity.ToString(),
                summary = c.LlmSummary
            }),
            processingTime = $"{report.ProcessingStats.TotalDuration.TotalSeconds:F2}s"
        });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Summarization failed");
        return Results.Problem("Summarization failed: " + ex.Message);
    }
});

// Check Ollama availability
app.MapGet("/health/ollama", async (ILogSummarizer summarizer, CancellationToken ct) =>
{
    var available = await summarizer.IsAvailableAsync(ct);
    return Results.Ok(new
    {
        ollama = available ? "available" : "unavailable",
        note = available ? "LLM summarization enabled" : "Start Ollama with: ollama serve"
    });
});

// View latest summary report
app.MapGet("/reports/latest", () =>
{
    var summaryDir = "./logs/summaries";
    if (!Directory.Exists(summaryDir))
        return Results.NotFound("No summaries generated yet. POST to /summarize first.");

    var latestFile = Directory.GetFiles(summaryDir, "*.md")
        .Select(f => new FileInfo(f))
        .OrderByDescending(f => f.LastWriteTime)
        .FirstOrDefault();

    if (latestFile == null)
        return Results.NotFound("No summary files found");

    var content = File.ReadAllText(latestFile.FullName);
    return Results.Content(content, "text/markdown");
});

Log.Information("Log Summarizer Demo starting...");
Log.Information("Endpoints:");
Log.Information("  GET  /           - Home page");
Log.Information("  GET  /simulate   - Generate sample log entries");
Log.Information("  GET  /error      - Generate a single error");
Log.Information("  POST /summarize  - Manually trigger summarization");
Log.Information("  GET  /health/ollama - Check Ollama availability");
Log.Information("  GET  /reports/latest - View latest summary report");

app.Run();
