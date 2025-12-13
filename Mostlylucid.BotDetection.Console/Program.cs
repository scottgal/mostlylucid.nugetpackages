using Serilog;
using Serilog.Events;
using Yarp.ReverseProxy.Transforms;
using Mostlylucid.BotDetection.Extensions;
using Mostlylucid.BotDetection.Models;
using Mostlylucid.BotDetection.Middleware;
using Mostlylucid.BotDetection.Events;
using Microsoft.AspNetCore.HttpOverrides;
using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

// Initialize SQLite bundle BEFORE anything else
SQLitePCL.Batteries.Init();

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

    // Configure web root path explicitly for static files
    var webRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
    Log.Information("Configuring web root path: {WebRootPath}", webRootPath);
    Log.Information("Web root exists: {Exists}", Directory.Exists(webRootPath));

    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        WebRootPath = webRootPath
    });

    // Use Serilog
    builder.Host.UseSerilog();

    // Configure forwarded headers to extract real client IP from Cloudflare/proxies
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        // Trust all proxies (Cloudflare, reverse proxies, etc.)
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();

        // Limit to first proxy for security
        options.ForwardLimit = 1;
    });

    // Load configuration from appsettings.json (with mode override)
    builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    builder.Configuration.AddJsonFile($"appsettings.{mode}.json", optional: true, reloadOnChange: true);

    // Read signature logging configuration early (needed by YARP transforms)
    var sigLoggingConfig = new SignatureLoggingConfig
    {
        Enabled = builder.Configuration.GetValue<bool>("SignatureLogging:Enabled", true),
        MinConfidence = builder.Configuration.GetValue<double>("SignatureLogging:MinConfidence", 0.7),
        PrettyPrintJsonLd = builder.Configuration.GetValue<bool>("SignatureLogging:PrettyPrintJsonLd", false),
        SignatureHashKey = builder.Configuration.GetValue<string>("SignatureLogging:SignatureHashKey") ?? "DEFAULT_INSECURE_KEY_CHANGE_ME",
        LogRawPii = builder.Configuration.GetValue<bool>("SignatureLogging:LogRawPii", false)  // DEFAULT: zero-PII
    };

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
    builder.Services.AddBotDetection();

    // Add YARP transforms for bot detection headers and CSP fixes
    yarpBuilder.AddTransforms(builderContext =>
    {
        builderContext.AddRequestTransform(async transformContext =>
        {
            try
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
                        LogDetectionDemo(httpContext, detection, stopwatch.Elapsed, sigLoggingConfig);
                    }
                    else
                    {
                        // Production: Only log bot detections and blocks
                        var wasBlocked = httpContext.Items.TryGetValue("BotDetectionAction", out var actionObj) &&
                                        actionObj?.ToString()?.Contains("Block", StringComparison.OrdinalIgnoreCase) == true;

                        if (detection.IsBot || wasBlocked)
                        {
                            LogDetectionProduction(httpContext, detection, stopwatch.Elapsed, sigLoggingConfig);
                        }
                    }

                    // Log signature in JSON-LD format if enabled and meets confidence threshold
                    if (sigLoggingConfig.Enabled && detection.IsBot && detection.ConfidenceScore >= sigLoggingConfig.MinConfidence)
                    {
                        LogSignatureJsonLd(httpContext, detection, sigLoggingConfig);
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
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in request transform - continuing request");
            }
        });

        // Add response transform to remove restrictive CSP and fix CORS, plus add client-side callback URL
        builderContext.AddResponseTransform(async transformContext =>
        {
            try
            {
                var httpContext = transformContext.HttpContext;

                // Add bot detection callback URL header for client-side tag
                var callbackUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/api/bot-detection/client-result";
                httpContext.Response.Headers.TryAdd("X-Bot-Detection-Callback-Url", callbackUrl);

                // Pass through bot detection headers to client as well (so JavaScript can see them)
                if (httpContext.Items.TryGetValue(BotDetectionMiddleware.BotDetectionResultKey, out var detectionObj) &&
                    detectionObj is BotDetectionResult detection)
                {
                    httpContext.Response.Headers.TryAdd("X-Bot-Detection", detection.IsBot.ToString());
                    httpContext.Response.Headers.TryAdd("X-Bot-Probability", detection.ConfidenceScore.ToString("F2"));

                    if (!string.IsNullOrEmpty(detection.BotName))
                    {
                        httpContext.Response.Headers.TryAdd("X-Bot-Name", detection.BotName);
                    }
                }

                // Remove Content-Security-Policy from both proxy response AND final response headers
                if (transformContext.ProxyResponse?.Headers.Contains("Content-Security-Policy") == true)
                {
                    transformContext.ProxyResponse.Headers.Remove("Content-Security-Policy");
                }
                httpContext.Response.Headers.Remove("Content-Security-Policy");

                // Remove Content-Security-Policy-Report-Only
                if (transformContext.ProxyResponse?.Headers.Contains("Content-Security-Policy-Report-Only") == true)
                {
                    transformContext.ProxyResponse.Headers.Remove("Content-Security-Policy-Report-Only");
                }
                httpContext.Response.Headers.Remove("Content-Security-Policy-Report-Only");

                // Remove X-Frame-Options to allow embedding
                if (transformContext.ProxyResponse?.Headers.Contains("X-Frame-Options") == true)
                {
                    transformContext.ProxyResponse.Headers.Remove("X-Frame-Options");
                }
                httpContext.Response.Headers.Remove("X-Frame-Options");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in response transform - continuing response");
            }
        });
    });

    var app = builder.Build();

    // Load signatures from JSON-L files on startup
    await LoadSignaturesFromJsonL(app.Services, Log.Logger);

    // Use Forwarded Headers middleware FIRST to extract real client IP
    app.UseForwardedHeaders();

    // Serve static files (for test pages)
    app.UseStaticFiles();

    // Use Bot Detection middleware
    app.UseBotDetection();

    // Health check endpoint (AOT-compatible) - mapped BEFORE YARP to avoid being proxied
    app.MapGet("/health", () => Results.Text($"{{\"status\":\"healthy\",\"mode\":\"{mode}\",\"upstream\":\"{upstream}\",\"port\":\"{port}\"}}", "application/json"));

    // Learning endpoint - Active in demo and learning modes (demo is default)
    // MUST be mapped BEFORE YARP to avoid being proxied
    if (mode.Equals("demo", StringComparison.OrdinalIgnoreCase) || mode.Equals("learning", StringComparison.OrdinalIgnoreCase))
    {
        Log.Information("Signature learning endpoint enabled - /stylobot-learning/ active (mode: {Mode})", mode);

        // Supports status code simulation via path markers: /404/, /403/, /500/, etc.
        // Example: /stylobot-learning/404/admin.php -> returns 404
        // Example: /stylobot-learning/products -> returns 200
        app.MapMethods("/stylobot-learning/{**path}", new[] { "GET", "POST", "HEAD", "PUT", "DELETE", "PATCH" }, (HttpContext context) =>
    {
        // Use actual request path instead of route values to avoid double prefix
        var requestPath = context.Request.Path.Value ?? "/";
        // Remove /stylobot-learning prefix and normalize
        var path = requestPath.StartsWith("/stylobot-learning/", StringComparison.OrdinalIgnoreCase)
            ? requestPath.Substring("/stylobot-learning/".Length).Trim('/')
            : requestPath.StartsWith("/stylobot-learning", StringComparison.OrdinalIgnoreCase)
                ? requestPath.Substring("/stylobot-learning".Length).Trim('/')
                : "";

        var method = context.Request.Method;
        var userAgent = context.Request.Headers.UserAgent.ToString();

        // Determine status code from path markers
        int statusCode = 200;
        string statusReason = "OK";

        if (path.Contains("/404/") || path.EndsWith(".php") || path.Contains("admin") || path.Contains("wp-"))
        {
            statusCode = 404;
            statusReason = "Not Found";
        }
        else if (path.Contains("/403/") || path.Contains("forbidden"))
        {
            statusCode = 403;
            statusReason = "Forbidden";
        }
        else if (path.Contains("/500/") || path.Contains("error"))
        {
            statusCode = 500;
            statusReason = "Internal Server Error";
        }

        // Build normalized URL path (avoid double slashes)
        var urlPath = string.IsNullOrEmpty(path) ? "/stylobot-learning" : $"/stylobot-learning/{path}";

        Log.Information("[LEARNING-MODE] Request handled internally: {Method} {UrlPath} UA={UserAgent} -> {StatusCode}",
            method, urlPath, userAgent.Length > 50 ? userAgent.Substring(0, 47) + "..." : userAgent, statusCode);

        // Return appropriate response based on status code
        var responseJson = statusCode == 404
            ? $$"""
{
  "@context": "https://schema.org",
  "@type": "WebPage",
  "name": "404 Not Found",
  "description": "The requested resource was not found.",
  "url": "{{urlPath}}",
  "metadata": {
    "statusCode": 404,
    "statusText": "Not Found",
    "learningMode": true
  }
}
"""
            : $$"""
{
  "@context": "https://schema.org",
  "@type": "WebPage",
  "name": "Stylobot Learning Mode",
  "url": "{{urlPath}}",
  "description": "This is a synthetic response for bot detection training. No real website was contacted.",
  "provider": {
    "@type": "Organization",
    "name": "Stylobot Bot Detection",
    "url": "https://stylobot.net"
  },
  "mainEntity": {
    "@type": "Dataset",
    "name": "Training Data",
    "description": "Request processed for bot detection learning",
    "temporalCoverage": "{{DateTime.UtcNow:O}}",
    "distribution": {
      "@type": "DataDownload",
      "contentUrl": "{{urlPath}}",
      "encodingFormat": "application/json"
    }
  },
  "metadata": {
    "requestMethod": "{{method}}",
    "requestPath": "{{urlPath}}",
    "statusCode": {{statusCode}},
    "statusText": "{{statusReason}}",
    "detectionApplied": true,
    "learningMode": true
  }
}
""";

        context.Response.StatusCode = statusCode;
        return Results.Content(responseJson, "application/json");
        });
    }

    // Client-side detection callback endpoint (AOT-compatible)
    app.MapPost("/api/bot-detection/client-result", async (HttpContext context, ILearningEventBus? eventBus) =>
    {
        try
        {
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();

            Log.Information("[CLIENT-SIDE-CALLBACK] Received client-side detection result");

            // Parse JSON (AOT-compatible using JsonDocument)
            using var jsonDoc = JsonDocument.Parse(body);
            var root = jsonDoc.RootElement;

            // Extract server detection results (echoed back from client)
            var serverDetection = root.TryGetProperty("serverDetection", out var serverDet) ? serverDet : (JsonElement?)null;
            var serverIsBot = serverDetection?.TryGetProperty("isBot", out var isBotProp) == true && isBotProp.GetString() == "True";
            var serverProbability = serverDetection?.TryGetProperty("probability", out var probProp) == true ? double.Parse(probProp.GetString() ?? "0") : 0.0;

            // Extract client-side checks
            var clientChecks = root.TryGetProperty("clientChecks", out var checks) ? checks : (JsonElement?)null;
            if (clientChecks.HasValue)
            {
                var hasCanvas = clientChecks.Value.TryGetProperty("hasCanvas", out var canvas) && canvas.GetBoolean();
                var hasWebGL = clientChecks.Value.TryGetProperty("hasWebGL", out var webgl) && webgl.GetBoolean();
                var hasAudioContext = clientChecks.Value.TryGetProperty("hasAudioContext", out var audio) && audio.GetBoolean();
                var pluginCount = clientChecks.Value.TryGetProperty("pluginCount", out var plugins) ? plugins.GetInt32() : 0;
                var hardwareConcurrency = clientChecks.Value.TryGetProperty("hardwareConcurrency", out var hardware) ? hardware.GetInt32() : 0;

                // Calculate client-side "bot score" based on checks
                var clientBotScore = CalculateClientBotScore(hasCanvas, hasWebGL, hasAudioContext, pluginCount, hardwareConcurrency);

                Log.Information("[CLIENT-SIDE-VALIDATION] Server: IsBot={ServerIsBot} (prob={ServerProb:F2}), Client: Score={ClientScore:F2}",
                    serverIsBot, serverProbability, clientBotScore);

                // Detect mismatches (server says bot, but client looks human - or vice versa)
                var mismatch = (serverIsBot && clientBotScore < 0.3) || (!serverIsBot && clientBotScore > 0.7);
                if (mismatch)
                {
                    Log.Warning("[CLIENT-SIDE-MISMATCH] Server detection ({ServerIsBot}) conflicts with client score ({ClientScore:F2})",
                        serverIsBot, clientBotScore);
                }

                // Publish learning event for pattern improvement
                if (eventBus != null)
                {
                    var learningEvent = new LearningEvent
                    {
                        Type = LearningEventType.ClientSideValidation,
                        Source = "ClientSideCallback",
                        Timestamp = DateTimeOffset.UtcNow,
                        Label = serverIsBot,  // Server's verdict
                        Confidence = clientBotScore,  // Client-side bot score
                        Metadata = new Dictionary<string, object>
                        {
                            ["ipAddress"] = context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                            ["userAgent"] = root.TryGetProperty("userAgent", out var ua) ? ua.GetString() ?? "" : "",
                            ["serverIsBot"] = serverIsBot,
                            ["serverProbability"] = serverProbability,
                            ["clientBotScore"] = clientBotScore,
                            ["hasCanvas"] = hasCanvas,
                            ["hasWebGL"] = hasWebGL,
                            ["hasAudioContext"] = hasAudioContext,
                            ["pluginCount"] = pluginCount,
                            ["hardwareConcurrency"] = hardwareConcurrency,
                            ["mismatch"] = mismatch
                        }
                    };

                    if (eventBus.TryPublish(learningEvent))
                    {
                        Log.Debug("[CLIENT-SIDE-CALLBACK] Published learning event for client-side validation");
                    }
                    else
                    {
                        Log.Warning("[CLIENT-SIDE-CALLBACK] Failed to publish learning event (channel full?)");
                    }
                }
            }

            return Results.Text("{\"status\":\"accepted\",\"message\":\"Client-side detection result processed\"}", "application/json");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to process client-side detection callback");
            return Results.Text("{\"status\":\"error\",\"message\":\"Invalid request\"}", "application/json", statusCode: 400);
        }
    });

    // Map YARP reverse proxy (catch-all, should be LAST)
    app.MapReverseProxy();

    // Configure Kestrel to listen on specified port
    app.Urls.Add($"http://*:{port}");

    Log.Information("✓ Gateway ready on http://localhost:{Port}", port);
    Log.Information("✓ Proxying to {Upstream}", upstream);
    Log.Information("✓ Health check: http://localhost:{Port}/health", port);
    Log.Information("");
    Log.Information("Starting application host... (press Ctrl+C to stop)");

    try
    {
        await app.RunAsync();
        Log.Warning("Application host stopped normally (this should only happen on shutdown)");
    }
    catch (OperationCanceledException)
    {
        Log.Information("Application shutdown requested (Ctrl+C or SIGTERM)");
    }
    catch (Exception innerEx)
    {
        Log.Fatal(innerEx, "Application host crashed with unhandled exception");
        throw;
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup or configuration failed");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

return 0;

// Calculate client-side bot score based on browser fingerprinting checks
static double CalculateClientBotScore(bool hasCanvas, bool hasWebGL, bool hasAudioContext, int pluginCount, int hardwareConcurrency)
{
    var score = 0.0;

    // Headless browsers typically fail these checks
    if (!hasCanvas) score += 0.30;  // Major red flag
    if (!hasWebGL) score += 0.25;   // Very suspicious
    if (!hasAudioContext) score += 0.15;  // Somewhat suspicious

    // Real browsers typically have 1-5 plugins (though modern browsers have few)
    if (pluginCount == 0) score += 0.10;  // Suspicious but not definitive

    // Headless browsers often report 0 or suspiciously high values
    if (hardwareConcurrency == 0) score += 0.10;
    else if (hardwareConcurrency > 32) score += 0.05;  // Unusual but possible

    // If all checks pass, give strong confidence it's a real browser
    if (hasCanvas && hasWebGL && hasAudioContext && hardwareConcurrency > 0 && hardwareConcurrency <= 32)
        score = Math.Max(0, score - 0.20);  // Bonus for passing all checks

    return Math.Clamp(score, 0.0, 1.0);
}

// Demo mode: Full verbose logging with all signals
static void LogDetectionDemo(
    Microsoft.AspNetCore.Http.HttpContext context,
    BotDetectionResult detection,
    TimeSpan elapsed,
    SignatureLoggingConfig config)
{
    var ua = context.Request.Headers.UserAgent.ToString();
    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    // Compute HMAC hashes for zero-PII logging (default)
    var keyBytes = Encoding.UTF8.GetBytes(config.SignatureHashKey);
    var ipHash = ComputeHmacHash(keyBytes, ip);
    var uaHash = ComputeHmacHash(keyBytes, ua);

    // Format display strings based on LogRawPii setting
    string ipDisplay, uaDisplay;
    if (config.LogRawPii)
    {
        // Demo mode with explicit PII override: show raw + hash
        ipDisplay = $"{ip} (hash: {ipHash})";
        var uaTruncated = ua.Length > 40 ? ua.Substring(0, 37) + "..." : ua;
        uaDisplay = $"{uaTruncated} (hash: {uaHash})";
    }
    else
    {
        // DEFAULT: Zero-PII mode (hash only)
        ipDisplay = ipHash;
        uaDisplay = uaHash;
    }

    // Check if request was blocked (from context items)
    var actionTaken = context.Items.TryGetValue("BotDetectionAction", out var actionObj)
        ? actionObj?.ToString()
        : null;
    var wasBlocked = actionTaken != null && actionTaken.Contains("Block", StringComparison.OrdinalIgnoreCase);

    if (wasBlocked)
    {
        // BIG RED BLOCKED BANNER for demo mode
        Log.Error("╔════════════════════════════════════════════════════════════╗");
        Log.Error("║                   🚫 REQUEST BLOCKED 🚫                    ║");
        Log.Error("╚════════════════════════════════════════════════════════════╝");
    }
    else
    {
        Log.Information("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Log.Information("🔍 Bot Detection Result");
        Log.Information("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    }

    Log.Information("  Request:     {Method} {Path}", context.Request.Method, context.Request.Path);
    Log.Information("  IP:          {IP}", ipDisplay);
    Log.Information("  User-Agent:  {UA}", uaDisplay);
    Log.Information("");
    Log.Information("  IsBot:       {IsBot}", detection.IsBot ? "✗ YES" : "✓ NO");
    Log.Information("  Confidence:  {Confidence:F2}", detection.ConfidenceScore);
    Log.Information("  Bot Type:    {BotType}", detection.BotType?.ToString() ?? "(none)");
    Log.Information("  Bot Name:    {BotName}", detection.BotName ?? "(none)");
    Log.Information("  Time:        {Time:F2}ms", elapsed.TotalMilliseconds);
    if (wasBlocked)
    {
        Log.Error("  ⚠️ ACTION:     {Action}", actionTaken);
    }
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

    if (wasBlocked)
    {
        Log.Error("╚════════════════════════════════════════════════════════════╝");
    }
    else
    {
        Log.Information("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    }
    Log.Information("");
}

// Production mode: Concise logging (ALWAYS zero-PII)
static void LogDetectionProduction(
    Microsoft.AspNetCore.Http.HttpContext context,
    BotDetectionResult detection,
    TimeSpan elapsed,
    SignatureLoggingConfig config)
{
    var result = detection.IsBot ? "BOT" : "HUMAN";
    var symbol = detection.IsBot ? "✗" : "✓";
    var botType = detection.BotType?.ToString() ?? "-";
    var botName = detection.BotName ?? "-";

    // ALWAYS use HMAC hash in production (zero-PII)
    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var keyBytes = Encoding.UTF8.GetBytes(config.SignatureHashKey);
    var ipHash = ComputeHmacHash(keyBytes, ip);

    Log.Information(
        "{Symbol} {Result,-6} {Confidence,4:F2} {BotType,-15} {Time,5:F0}ms {Method,-4} {Path} [{IpHash}] {BotName}",
        symbol,
        result,
        detection.ConfidenceScore,
        botType,
        elapsed.TotalMilliseconds,
        context.Request.Method,
        context.Request.Path,
        ipHash,
        botName);
}

// Log bot signature in JSON-LD format (schema.org SecurityAction) with ZERO-PII multi-factor signatures
static void LogSignatureJsonLd(
    Microsoft.AspNetCore.Http.HttpContext context,
    BotDetectionResult detection,
    SignatureLoggingConfig config)
{
    // Compute privacy-safe multi-factor signature hashes
    var userAgent = context.Request.Headers.UserAgent.ToString();
    var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var referer = context.Request.Headers.Referer.ToString();
    var xForwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();

    var multiFactorSig = ComputeMultiFactorSignature(
        config.SignatureHashKey,
        userAgent,
        clientIp,
        context.Request.Path.ToString(),
        referer);

    // Write to date-based JSONL file (manual JSON for AOT compatibility)
    try
    {
        var dateStr = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var filename = $"signatures-{dateStr}.jsonl";

        // Build JSON manually for AOT compatibility
        var reasonsJson = detection.Reasons != null
            ? string.Join(",", detection.Reasons.Select(r =>
                BuildJsonObject(new Dictionary<string, object>
                {
                    ["category"] = r.Category,
                    ["detail"] = r.Detail,
                    ["impact"] = r.ConfidenceImpact
                }, indent: config.PrettyPrintJsonLd ? 8 : 0)))
            : "";

        var indent = config.PrettyPrintJsonLd;
        var json = BuildJsonObject(new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "SecurityAction",
            ["agent"] = new Dictionary<string, object>
            {
                ["@type"] = "SoftwareApplication",
                ["name"] = "Mostlylucid.BotDetection.Console",
                ["version"] = "1.0.0"
            },
            ["actionStatus"] = "CompletedActionStatus",
            ["result"] = new Dictionary<string, object>
            {
                ["@type"] = "ThreatDetection",
                ["detectedAt"] = DateTime.UtcNow.ToString("O"),
                ["threatType"] = detection.BotType?.ToString() ?? "Unknown",
                ["threatName"] = detection.BotName ?? "Unidentified",
                ["confidenceScore"] = detection.ConfidenceScore,
                ["riskLevel"] = detection.ConfidenceScore switch
                {
                    >= 0.9 => "VeryHigh",
                    >= 0.7 => "High",
                    >= 0.5 => "Medium",
                    >= 0.3 => "Low",
                    _ => "VeryLow"
                },
                ["multiFactorSignature"] = new Dictionary<string, object>
                {
                    ["primary"] = multiFactorSig.Primary,
                    ["ip"] = multiFactorSig.IpHash,
                    ["ua"] = multiFactorSig.UaHash,
                    ["path"] = multiFactorSig.PathHash,
                    ["referer"] = multiFactorSig.RefererHash
                },
                ["requestContext"] = new Dictionary<string, object>
                {
                    ["path"] = context.Request.Path.ToString(),
                    ["method"] = context.Request.Method,
                    ["protocol"] = context.Request.Protocol,
                    ["hasReferer"] = !string.IsNullOrEmpty(referer),
                    ["hasXForwardedFor"] = !string.IsNullOrEmpty(xForwardedFor)
                },
                ["reasons"] = $"[{reasonsJson}]"
            }
        }, indent: indent ? 0 : 0);

        // Log to console (structured)
        Log.Information("[JSON-LD-SIGNATURE] Primary signature: {PrimarySignature}, Confidence: {Confidence:F2}, Type: {ThreatType}",
            multiFactorSig.Primary,
            detection.ConfidenceScore,
            detection.BotType?.ToString() ?? "Unknown");

        File.AppendAllText(filename, json + Environment.NewLine);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Failed to write signature to file");
    }
}

static string EscapeJson(string text)
{
    if (string.IsNullOrEmpty(text)) return text;
    return text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
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

// Compute multi-factor signature using HMAC-SHA256 for zero-PII logging
static MultiFactorSignature ComputeMultiFactorSignature(
    string secretKey,
    string userAgent,
    string clientIp,
    string path,
    string referer)
{
    var keyBytes = Encoding.UTF8.GetBytes(secretKey);

    return new MultiFactorSignature
    {
        Primary = ComputeHmacHash(keyBytes, $"{userAgent}|{clientIp}|{path}"),
        UaHash = ComputeHmacHash(keyBytes, userAgent),
        IpHash = ComputeHmacHash(keyBytes, clientIp),
        PathHash = ComputeHmacHash(keyBytes, path),
        RefererHash = string.IsNullOrEmpty(referer) ? "none" : ComputeHmacHash(keyBytes, referer)
    };
}

// Compute HMAC-SHA256 hash and return truncated hex string
static string ComputeHmacHash(byte[] key, string input)
{
    if (string.IsNullOrEmpty(input))
        return "unknown";

    using var hmac = new HMACSHA256(key);
    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));

    // Truncate to 128 bits (16 bytes) for compact, collision-resistant IDs
    return Convert.ToHexString(hash[..16]).ToLowerInvariant();
}

// Build JSON object with optional pretty-printing (AOT-compatible)
static string BuildJsonObject(Dictionary<string, object> obj, int indent = 0)
{
    var sb = new StringBuilder();
    var prefix = indent > 0 ? new string(' ', indent) : "";
    var innerPrefix = indent > 0 ? new string(' ', indent + 2) : "";
    var nl = indent > 0 ? "\n" : "";

    sb.Append('{');
    if (indent > 0) sb.Append('\n');

    var items = obj.ToList();
    for (int i = 0; i < items.Count; i++)
    {
        var kvp = items[i];
        if (indent > 0) sb.Append(innerPrefix);

        sb.Append('"').Append(EscapeJson(kvp.Key)).Append("\":");
        if (indent > 0) sb.Append(' ');

        if (kvp.Value is string str)
        {
            sb.Append('"').Append(EscapeJson(str)).Append('"');
        }
        else if (kvp.Value is double d)
        {
            sb.Append(d.ToString("0.0###############"));
        }
        else if (kvp.Value is bool b)
        {
            sb.Append(b ? "true" : "false");
        }
        else if (kvp.Value is Dictionary<string, object> nested)
        {
            sb.Append(BuildJsonObject(nested, indent > 0 ? indent + 2 : 0));
        }
        else
        {
            sb.Append(kvp.Value?.ToString() ?? "null");
        }

        if (i < items.Count - 1) sb.Append(',');
        if (indent > 0) sb.Append('\n');
    }

    if (indent > 0) sb.Append(prefix);
    sb.Append('}');
    return sb.ToString();
}

// Load signatures from JSON-L files on startup
static async Task LoadSignaturesFromJsonL(IServiceProvider services, Serilog.ILogger logger)
{
    try
    {
        // Find all signatures-*.jsonl files in the current directory
        var signatureFiles = Directory.GetFiles(".", "signatures-*.jsonl");
        if (signatureFiles.Length == 0)
        {
            logger.Information("No signature files found");
            return;
        }

        var totalSignatures = 0;
        var signaturesByDate = new Dictionary<string, int>();

        foreach (var file in signatureFiles)
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(file);
                var fileName = Path.GetFileName(file);
                signaturesByDate[fileName] = lines.Where(l => !string.IsNullOrWhiteSpace(l)).Count();
                totalSignatures += signaturesByDate[fileName];
            }
            catch (Exception fileEx)
            {
                logger.Warning(fileEx, "Failed to read signature file {File}", Path.GetFileName(file));
            }
        }

        logger.Information("Found {TotalSignatures} signatures across {FileCount} file(s)",
            totalSignatures, signatureFiles.Length);

        foreach (var kvp in signaturesByDate.OrderBy(x => x.Key))
        {
            logger.Debug("  {File}: {Count} signatures", kvp.Key, kvp.Value);
        }
    }
    catch (Exception ex)
    {
        logger.Error(ex, "Failed to load signatures from JSON-L files");
    }
}

// Configuration for signature logging
record SignatureLoggingConfig
{
    public bool Enabled { get; init; }
    public double MinConfidence { get; init; }
    public bool PrettyPrintJsonLd { get; init; }
    public required string SignatureHashKey { get; init; }
    public bool LogRawPii { get; init; }  // DEFAULT: false (zero-PII)
}

// Multi-factor signature with privacy-safe HMAC hashes
record MultiFactorSignature
{
    public required string Primary { get; init; }      // Combined hash
    public required string UaHash { get; init; }       // User-Agent hash
    public required string IpHash { get; init; }       // IP address hash
    public required string PathHash { get; init; }     // Request path hash
    public required string RefererHash { get; init; }  // Referer hash
}
