using Mostlylucid.BotDetection.Extensions;
using Mostlylucid.BotDetection.Middleware;
using Mostlylucid.BotDetection.Models;
using Mostlylucid.BotDetection.Services;

var builder = WebApplication.CreateBuilder(args);

// Add bot detection with configuration
builder.Services.AddBotDetection(options =>
{
    options.BotThreshold = 0.7;
    options.EnableUserAgentDetection = true;
    options.EnableHeaderAnalysis = true;
    options.EnableIpDetection = true;
    options.EnableBehavioralAnalysis = true;

    // Enable LLM detection (requires Ollama running)
    options.EnableLlmDetection = false; // Set to true if you have Ollama running
    options.OllamaEndpoint = "http://localhost:11434";
    options.OllamaModel = "qwen2.5:1.5b";
    options.LlmTimeoutMs = 2000;

    options.MaxRequestsPerMinute = 60;
    options.CacheDurationSeconds = 300;
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Enable Swagger for testing
app.UseSwagger();
app.UseSwaggerUI();

// Add bot detection middleware
app.UseBotDetection();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Simple test endpoints
app.MapGet("/", (HttpContext context) =>
{
    var result = context.Items[BotDetectionMiddleware.BotDetectionResultKey] as BotDetectionResult;

    return Results.Ok(new
    {
        message = "Bot Detection Demo API",
        isBot = result?.IsBot ?? false,
        confidence = result?.ConfidenceScore ?? 0.0,
        botType = result?.BotType?.ToString(),
        botName = result?.BotName,
        processingTime = result?.ProcessingTimeMs ?? 0,
        reasons = result?.Reasons.Select(r => new
        {
            r.Category,
            r.Detail,
            r.ConfidenceImpact
        })
    });
});

app.MapGet("/api/bot-check", (HttpContext context) =>
{
    var result = context.Items[BotDetectionMiddleware.BotDetectionResultKey] as BotDetectionResult;
    return Results.Ok(result);
});

app.MapGet("/api/stats", (IBotDetectionService botService) =>
{
    var stats = botService.GetStatistics();
    return Results.Ok(stats);
});

app.MapGet("/api/test-bot", () =>
{
    return Results.Ok(new
    {
        message = "To test bot detection, make requests with different User-Agents:",
        examples = new[]
        {
            new { userAgent = "curl/7.68.0", expected = "Likely bot" },
            new { userAgent = "python-requests/2.28.0", expected = "Likely bot" },
            new { userAgent = "Googlebot/2.1", expected = "Verified bot (allowed)" },
            new
            {
                userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36", expected = "Likely human"
            },
            new { userAgent = "", expected = "Likely bot (missing)" }
        },
        instructions = "Use: curl -H 'User-Agent: curl/7.68.0' http://localhost:5000/"
    });
});

app.Run();