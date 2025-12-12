using Serilog;
using Serilog.Events;
using Yarp.ReverseProxy.Transforms;
using Mostlylucid.BotDetection.Extensions;
using Mostlylucid.BotDetection.Models;
using Mostlylucid.BotDetection.Middleware;
using System.Diagnostics;

// Parse command-line arguments
var cmdArgs = Environment.GetCommandLineArgs();
var upstream = GetArg(cmdArgs, "--upstream") ?? Environment.GetEnvironmentVariable("UPSTREAM") ?? "http://localhost:8080";
var port = GetArg(cmdArgs, "--port") ?? Environment.GetEnvironmentVariable("PORT") ?? "5000";
var mode = GetArg(cmdArgs, "--mode") ?? Environment.GetEnvironmentVariable("MODE") ?? "demo";

// Configure Serilog (console only, structured logging)
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Yarp", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        restrictedToMinimumLevel: LogEventLevel.Debug)
    .CreateLogger();

try
{
    Log.Information("╔══════════════════════════════════════════════════════════╗");
    Log.Information("║   Mostlylucid Bot Detection Console Gateway            ║");
    Log.Information("╚══════════════════════════════════════════════════════════╝");
    Log.Information("");
    Log.Information("Mode:     {Mode}", mode.ToUpper());
    Log.Information("Upstream: {Upstream}", upstream);
    Log.Information("Port:     {Port}", port);
    Log.Information("");

    var builder = WebApplication.CreateSlimBuilder();

    // Use Serilog
    builder.Host.UseSerilog();

    // Load configuration from appsettings.json (with mode override)
    builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    builder.Configuration.AddJsonFile($"appsettings.{mode}.json", optional: true, reloadOnChange: true);

    // Add YARP
    var yarpBuilder = builder.Services.AddReverseProxy()
        .LoadFromMemory(
            new[]
            {
                new Yarp.ReverseProxy.Configuration.RouteConfig
                {
                    RouteId = "catch-all",
                    Match = new Yarp.ReverseProxy.Configuration.RouteMatch
                    {
                        Path = "{**catch-all}"
                    },
                    ClusterId = "upstream"
                }
            },
            new[]
            {
                new Yarp.ReverseProxy.Configuration.ClusterConfig
                {
                    ClusterId = "upstream",
                    Destinations = new Dictionary<string, Yarp.ReverseProxy.Configuration.DestinationConfig>
                    {
                        ["default"] = new() { Address = upstream }
                    }
                }
            });

    // Add Bot Detection (configured via appsettings.json)
    builder.Services.AddBotDetection(builder.Configuration);

    // Add YARP transforms for bot detection headers and CSP fixes
    yarpBuilder.AddTransforms(builderContext =>
    {
        builderContext.AddRequestTransform(async transformContext =>
        {
            var httpContext = transformContext.HttpContext;
            var stopwatch = Stopwatch.StartNew();

            // Get detection result from HttpContext.Items (set by BotDetectionMiddleware)
            if (httpContext.Items.TryGetValue(BotDetectionMiddleware.BotDetectionResultKey, out var resultObj) &&
                resultObj is BotDetectionResult detection)
            {
                stopwatch.Stop();

                // Log full detection info (mode-dependent verbosity)
                if (mode.Equals("demo", StringComparison.OrdinalIgnoreCase))
                {
                    LogDetectionDemo(httpContext, detection, stopwatch.Elapsed);
                }
                else
                {
                    LogDetectionProduction(httpContext, detection, stopwatch.Elapsed);
                }

                // Forward detection headers to upstream
                transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-Bot-Detection", detection.IsBot.ToString());
                transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-Bot-Probability", detection.ConfidenceScore.ToString("F2"));
                transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-Bot-DetectionTime", stopwatch.ElapsedMilliseconds.ToString());

                if (!string.IsNullOrEmpty(detection.BotName))
                {
                    transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-Bot-Name", detection.BotName);
                }

                if (detection.BotType.HasValue)
                {
                    transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-Bot-Type", detection.BotType.Value.ToString());
                }
            }
        });

        // Add response transform to remove restrictive CSP and fix CORS
        builderContext.AddResponseTransform(async transformContext =>
        {
            // Remove or replace restrictive Content-Security-Policy headers
            if (transformContext.ProxyResponse?.Headers.Contains("Content-Security-Policy") == true)
            {
                transformContext.ProxyResponse.Headers.Remove("Content-Security-Policy");
            }
            if (transformContext.ProxyResponse?.Headers.Contains("Content-Security-Policy-Report-Only") == true)
            {
                transformContext.ProxyResponse.Headers.Remove("Content-Security-Policy-Report-Only");
            }

            // Remove X-Frame-Options to allow embedding
            if (transformContext.ProxyResponse?.Headers.Contains("X-Frame-Options") == true)
            {
                transformContext.ProxyResponse.Headers.Remove("X-Frame-Options");
            }

            await Task.CompletedTask;
        });
    });

    var app = builder.Build();

    // Use Bot Detection middleware
    app.UseBotDetection();

    // Map YARP
    app.MapReverseProxy();

    // Health check endpoint
    app.MapGet("/health", () => Results.Ok(new { status = "healthy", mode, upstream, port }));

    // Configure Kestrel to listen on specified port
    app.Urls.Add($"http://*:{port}");

    Log.Information("✓ Gateway ready on http://localhost:{Port}", port);
    Log.Information("✓ Proxying to {Upstream}", upstream);
    Log.Information("✓ Health check: http://localhost:{Port}/health", port);
    Log.Information("");

    await app.RunAsync();
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

// Demo mode: Full verbose logging with all signals
static void LogDetectionDemo(
    Microsoft.AspNetCore.Http.HttpContext context,
    BotDetectionResult detection,
    TimeSpan elapsed)
{
    var ua = context.Request.Headers.UserAgent.ToString();
    var uaDisplay = ua.Length > 60 ? ua.Substring(0, 57) + "..." : ua;

    Log.Information("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    Log.Information("🔍 Bot Detection Result");
    Log.Information("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    Log.Information("  Request:     {Method} {Path}", context.Request.Method, context.Request.Path);
    Log.Information("  IP:          {IP}", context.Connection.RemoteIpAddress);
    Log.Information("  User-Agent:  {UA}", uaDisplay);
    Log.Information("");
    Log.Information("  IsBot:       {IsBot}", detection.IsBot ? "✗ YES" : "✓ NO");
    Log.Information("  Confidence:  {Confidence:F2}", detection.ConfidenceScore);
    Log.Information("  Bot Type:    {BotType}", detection.BotType?.ToString() ?? "(none)");
    Log.Information("  Bot Name:    {BotName}", detection.BotName ?? "(none)");
    Log.Information("  Time:        {Time:F2}ms", elapsed.TotalMilliseconds);
    Log.Information("");

    if (detection.Reasons != null && detection.Reasons.Count > 0)
    {
        Log.Information("  Detection Reasons: {Count}", detection.Reasons.Count);
        Log.Information("  ┌─────────────────────────────────────────────────────");
        foreach (var reason in detection.Reasons.OrderByDescending(r => r.ConfidenceImpact))
        {
            Log.Information("  │ {Category,-25} {Impact,6:F2} - {Detail}",
                reason.Category,
                reason.ConfidenceImpact,
                reason.Detail.Length > 40 ? reason.Detail.Substring(0, 37) + "..." : reason.Detail);
        }
        Log.Information("  └─────────────────────────────────────────────────────");
    }

    // Show additional HttpContext.Items if available
    if (context.Items.TryGetValue(BotDetectionMiddleware.BotCategoryKey, out var category))
    {
        Log.Information("  Primary Category: {Category}", category);
    }

    if (context.Items.TryGetValue(BotDetectionMiddleware.PolicyNameKey, out var policy))
    {
        Log.Information("  Policy Used:      {Policy}", policy);
    }

    Log.Information("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    Log.Information("");
}

// Production mode: Concise logging
static void LogDetectionProduction(
    Microsoft.AspNetCore.Http.HttpContext context,
    BotDetectionResult detection,
    TimeSpan elapsed)
{
    var result = detection.IsBot ? "BOT" : "HUMAN";
    var symbol = detection.IsBot ? "✗" : "✓";
    var botType = detection.BotType?.ToString() ?? "-";
    var botName = detection.BotName ?? "-";

    Log.Information(
        "{Symbol} {Result,-6} {Confidence,4:F2} {BotType,-15} {Time,5:F0}ms {Method,-4} {Path} [{IP}] {BotName}",
        symbol,
        result,
        detection.ConfidenceScore,
        botType,
        elapsed.TotalMilliseconds,
        context.Request.Method,
        context.Request.Path,
        context.Connection.RemoteIpAddress,
        botName);
}

// Helper to get command-line argument
static string? GetArg(string[] args, string name)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }
    return null;
}
