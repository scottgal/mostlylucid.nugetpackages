using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.ArchiveOrg;
using Mostlylucid.ArchiveOrg.Config;
using Mostlylucid.ArchiveOrg.Services;
using Serilog;

// Determine config base path - prefer exe directory over current directory
var exeDirectory = AppContext.BaseDirectory;
var configPath = Path.Combine(exeDirectory, "appsettings.json");

// Fall back to current directory if not found in exe directory
if (!File.Exists(configPath))
{
    configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
    if (!File.Exists(configPath))
    {
        Console.WriteLine("ERROR: appsettings.json not found!");
        Console.WriteLine($"  Checked: {Path.Combine(exeDirectory, "appsettings.json")}");
        Console.WriteLine($"  Checked: {Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json")}");
        return 1;
    }
}

var basePath = Path.GetDirectoryName(configPath)!;
Console.WriteLine($"Loading config from: {basePath}");

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(basePath)
    .AddJsonFile("appsettings.json", false, false)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

// Set up cancellation token for graceful shutdown
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    Log.Warning("Cancellation requested (Ctrl+C). Shutting down gracefully...");
    e.Cancel = true; // Prevent immediate termination
    cts.Cancel();
};

// Also handle SIGTERM for container environments
AppDomain.CurrentDomain.ProcessExit += (_, _) =>
{
    Log.Information("Process exit requested. Cleaning up...");
    cts.Cancel();
};

try
{
    Log.Information("Archive.org Downloader starting...");
    Log.Information("Press Ctrl+C to cancel gracefully");

    // Build host
    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureServices((context, services) => { services.AddArchiveOrgServices(configuration, basePath); })
        .Build();

    // Parse command - default to "full" if no command specified
    var command = args.Length > 0 ? args[0].ToLower() : "full";

    switch (command)
    {
        case "download":
            await RunDownloadAsync(host.Services, cts.Token);
            break;

        case "convert":
            await RunConvertAsync(host.Services, cts.Token);
            break;

        case "full":
            await RunFullPipelineAsync(host.Services, cts.Token);
            break;

        case "help":
            PrintHelp();
            break;

        default:
            Log.Warning("Unknown command: {Command}. Running full pipeline.", command);
            await RunFullPipelineAsync(host.Services, cts.Token);
            break;
    }

    Log.Information("Archive.org Downloader completed");
}
catch (OperationCanceledException)
{
    Log.Warning("Operation was cancelled by user");
    return 0; // Graceful cancellation is not an error
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

return 0;

static async Task RunDownloadAsync(IServiceProvider services, CancellationToken cancellationToken = default)
{
    var downloader = services.GetRequiredService<IArchiveDownloader>();
    var options = services.GetRequiredService<IOptions<ArchiveOrgOptions>>().Value;
    var logger = services.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Starting download from Archive.org");
    logger.LogInformation("Target URL: {Url}", options.TargetUrl);
    logger.LogInformation("Output Directory: {Dir}", options.OutputDirectory);

    if (options.EndDate.HasValue)
        logger.LogInformation("End Date: {Date:yyyy-MM-dd}", options.EndDate);
    if (options.StartDate.HasValue)
        logger.LogInformation("Start Date: {Date:yyyy-MM-dd}", options.StartDate);
    else
        logger.LogInformation("Start Date: (none - fetching ALL available archives)");

    logger.LogInformation("Rate Limit: {Ms}ms between requests", options.RateLimitMs);
    logger.LogInformation("Request Timeout: {Seconds}s, Max Retries: {Retries}",
        options.RequestTimeoutSeconds, options.MaxRetries);

    var progress = new Progress<DownloadProgress>(p =>
    {
        if (p.ProcessedRecords % 10 == 0 || p.ProcessedRecords == p.TotalRecords)
            logger.LogInformation(
                "Download Progress: {Processed}/{Total} ({Percent:F1}%) - Success: {Success}, Failed: {Failed}",
                p.ProcessedRecords, p.TotalRecords, p.PercentComplete,
                p.SuccessfulDownloads, p.FailedDownloads);
    });

    var results = await downloader.DownloadAllAsync(progress, cancellationToken);

    logger.LogInformation("Download complete. Total: {Total}, Success: {Success}, Failed: {Failed}",
        results.Count,
        results.Count(r => r.Success),
        results.Count(r => !r.Success));
}

static async Task RunConvertAsync(IServiceProvider services, CancellationToken cancellationToken = default)
{
    var converter = services.GetRequiredService<IHtmlToMarkdownConverter>();
    var options = services.GetRequiredService<IOptions<MarkdownConversionOptions>>().Value;
    var logger = services.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Starting HTML to Markdown conversion");
    logger.LogInformation("Input Directory: {Dir}", options.InputDirectory);
    logger.LogInformation("Output Directory: {Dir}", options.OutputDirectory);

    var progress = new Progress<ConversionProgress>(p =>
    {
        logger.LogInformation(
            "Conversion Progress: {Processed}/{Total} ({Percent:F1}%) - Success: {Success}, Failed: {Failed}",
            p.ProcessedFiles, p.TotalFiles, p.PercentComplete,
            p.SuccessfulConversions, p.FailedConversions);
    });

    var articles = await converter.ConvertAllAsync(progress, cancellationToken);

    logger.LogInformation("Conversion complete. Total articles: {Count}", articles.Count);

    foreach (var article in articles)
        logger.LogInformation("  - {Title} [{Categories}] -> {Path}",
            article.Title,
            string.Join(", ", article.Categories),
            article.OutputFilePath);
}

static async Task RunFullPipelineAsync(IServiceProvider services, CancellationToken cancellationToken = default)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    var downloader = services.GetRequiredService<IArchiveDownloader>();
    var converter = services.GetRequiredService<IHtmlToMarkdownConverter>();
    var downloadOptions = services.GetRequiredService<IOptions<ArchiveOrgOptions>>().Value;
    var convertOptions = services.GetRequiredService<IOptions<MarkdownConversionOptions>>().Value;

    logger.LogInformation("Running full pipeline: Download + Convert (parallel)");
    logger.LogInformation("Target URL: {Url}", downloadOptions.TargetUrl);
    logger.LogInformation("Output Directory: {Dir}", downloadOptions.OutputDirectory);
    logger.LogInformation("Request Timeout: {Seconds}s, Max Retries: {Retries}",
        downloadOptions.RequestTimeoutSeconds, downloadOptions.MaxRetries);

    // Use a channel to pass downloaded files to the converter
    var channel = Channel.CreateBounded<string>(
        new BoundedChannelOptions(10)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

    // Stats tracking
    var downloadStats = new PipelineStats();
    var convertStats = new PipelineStats();

    // Start the converter task (consumer)
    var converterTask = Task.Run(async () =>
    {
        try
        {
            await foreach (var filePath in channel.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    logger.LogDebug("Converting: {File}", Path.GetFileName(filePath));
                    var articles = await converter.ConvertFileAsync(filePath, cancellationToken);
                    convertStats.Successful += articles.Count;

                    if (articles.Count > 1)
                        logger.LogInformation("Converted {Count} posts from: {File}",
                            articles.Count, Path.GetFileName(filePath));
                    else if (articles.Count == 1)
                        logger.LogInformation("Converted: {File} -> {Output}",
                            Path.GetFileName(filePath), articles[0].OutputFilePath);
                    else
                        logger.LogDebug("Skipped (no articles): {File}", Path.GetFileName(filePath));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to convert: {File}", filePath);
                    convertStats.Failed++;
                }

                convertStats.Processed++;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
    }, cancellationToken);

    // Run the downloader (producer)
    try
    {
        // Ensure output directories exist (paths are already absolute from service registration)
        Directory.CreateDirectory(downloadOptions.OutputDirectory);
        Directory.CreateDirectory(convertOptions.OutputDirectory);

        logger.LogInformation("Download output: {Dir}", downloadOptions.OutputDirectory);
        logger.LogInformation("Markdown output: {Dir}", convertOptions.OutputDirectory);

        // First, queue any existing HTML files that haven't been converted yet
        var existingHtmlFiles = Directory.GetFiles(downloadOptions.OutputDirectory, "*.html");
        if (existingHtmlFiles.Length > 0)
        {
            logger.LogInformation("Found {Count} existing HTML files, queueing unconverted ones...",
                existingHtmlFiles.Length);
            var queuedCount = 0;
            foreach (var htmlFile in existingHtmlFiles)
            {
                // Queue for conversion - the converter will skip if already done
                await channel.Writer.WriteAsync(htmlFile, cancellationToken);
                queuedCount++;
            }

            logger.LogInformation("Queued {Count} existing files for conversion", queuedCount);
        }

        var cdxClient = services.GetRequiredService<ICdxApiClient>();
        logger.LogInformation("Fetching CDX records...");

        var records = await cdxClient.GetCdxRecordsAsync(
            downloadOptions.TargetUrl,
            downloadOptions.StartDate,
            downloadOptions.EndDate,
            cancellationToken);

        if (records.Count == 0)
        {
            logger.LogWarning("No records found");
            channel.Writer.Complete();
            return;
        }

        logger.LogInformation("Found {Count} records to process", records.Count);
        downloadStats.Total = records.Count;

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await downloader.DownloadRecordAsync(record, cancellationToken);
            downloadStats.Processed++;

            if (result.Success)
            {
                downloadStats.Successful++;

                // Send to converter if we have a file path
                if (!string.IsNullOrEmpty(result.FilePath) && File.Exists(result.FilePath))
                    // Always queue - the converter will skip if already converted or should be filtered
                    await channel.Writer.WriteAsync(result.FilePath, cancellationToken);
                else
                    logger.LogWarning("Download succeeded but no file path: {Url}", record.OriginalUrl);
            }
            else
            {
                downloadStats.Failed++;
            }

            // Log progress every 10 items
            if (downloadStats.Processed % 10 == 0 || downloadStats.Processed == downloadStats.Total)
                logger.LogInformation(
                    "Pipeline: Download {DProc}/{DTotal} (OK:{DOk} Fail:{DFail}) | Convert {CProc} (OK:{COk} Fail:{CFail})",
                    downloadStats.Processed, downloadStats.Total, downloadStats.Successful, downloadStats.Failed,
                    convertStats.Processed, convertStats.Successful, convertStats.Failed);
        }
    }
    finally
    {
        // Signal that no more items will be written
        channel.Writer.Complete();
    }

    // Wait for converter to finish processing remaining items
    logger.LogInformation("Downloads complete, waiting for conversions to finish...");
    await converterTask;

    logger.LogInformation("Pipeline complete. Downloaded: {DSuccess}/{DTotal}, Converted: {CSuccess} articles",
        downloadStats.Successful, downloadStats.Total, convertStats.Successful);
}

static void PrintHelp()
{
    Console.WriteLine("""
                      Archive.org Downloader & Markdown Converter
                      ==========================================

                      Commands:
                        download    Download archived pages from Archive.org
                        convert     Convert downloaded HTML to Markdown
                        full        Run full pipeline (download + convert)
                        help        Show this help message

                      Configuration (appsettings.json):

                        {
                          "ArchiveOrg": {
                            "TargetUrl": "https://example.com",
                            "EndDate": "2024-01-01",
                            "StartDate": null,              // null = ALL pages (greedy mode)
                            "OutputDirectory": "./archive-output",
                            "RateLimitMs": 5000,            // 5 seconds between requests
                            "RequestTimeoutSeconds": 180,   // 3 minute timeout per request
                            "MaxRetries": 3,                // Retry failed downloads 3 times
                            "RetryDelayMs": 5000,           // 5 seconds between retries
                            "UniqueUrlsOnly": true,
                            "IncludePatterns": [],
                            "ExcludePatterns": [".*\\.js$", ".*\\.css$"]
                          },
                          "MarkdownConversion": {
                            "InputDirectory": "./archive-output",
                            "OutputDirectory": "./markdown-output",
                            "ContentSelector": "article",   // CSS selector for main content
                            "GenerateTags": true,
                            "ExtractDates": true
                          },
                          "Ollama": {
                            "BaseUrl": "http://localhost:11434",
                            "Model": "llama3.2",
                            "Enabled": true,
                            "MaxTags": 5
                          }
                        }

                      Examples:
                        dotnet run -- download
                        dotnet run -- convert
                        dotnet run -- full

                        # Override config via command line:
                        dotnet run -- download --ArchiveOrg:TargetUrl=https://myblog.com --ArchiveOrg:EndDate=2023-12-31

                      Notes:
                        - Press Ctrl+C to cancel gracefully (progress is saved)
                        - Already downloaded files are automatically skipped on resume
                        - Failed downloads are retried up to MaxRetries times
                        - Designed for long-running operations (hours)
                      """);
}

// Make Program class accessible for generic logger
public partial class Program;

internal class PipelineStats
{
    public int Failed;
    public int Processed;
    public int Successful;
    public int Total;
}