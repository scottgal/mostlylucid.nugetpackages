using Mostlylucid.BotDetection.Extensions;
using Mostlylucid.BotDetection.Filters;
using Mostlylucid.BotDetection.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add bot detection with configuration
builder.Services.AddBotDetection(options =>
{
    options.BotThreshold = 0.7;
    options.EnableUserAgentDetection = true;
    options.EnableHeaderAnalysis = true;
    options.EnableIpDetection = true;
    options.EnableBehavioralAnalysis = true;
    options.EnableTestMode = true; // Enable test mode for demo

    // Enable LLM detection (requires Ollama running)
    options.EnableLlmDetection = false;
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

// ==========================================
// Built-in diagnostic endpoints
// ==========================================
// Maps: /bot-detection/check, /bot-detection/stats, /bot-detection/health
app.MapBotDetectionEndpoints();

// ==========================================
// Demo endpoints using extension methods
// ==========================================

// Simple check using HttpContext extensions
app.MapGet("/", (HttpContext context) =>
{
    return Results.Ok(new
    {
        message = "Bot Detection Demo API - v1.0.0",
        isBot = context.IsBot(),
        isHuman = context.IsHuman(),
        isSearchEngine = context.IsSearchEngineBot(),
        isVerifiedBot = context.IsVerifiedBot(),
        confidence = context.GetBotConfidence(),
        botType = context.GetBotType()?.ToString(),
        botName = context.GetBotName(),
        endpoints = new
        {
            diagnostics = new[] { "/bot-detection/check", "/bot-detection/stats", "/bot-detection/health" },
            protected_endpoints = new[] { "/api/protected", "/api/humans-only", "/api/allow-search-engines" },
            test_instructions = "Use header 'ml-bot-test-mode: googlebot' to simulate different bots"
        }
    });
});

// ==========================================
// Protected endpoints using endpoint filters
// ==========================================

// Block all bots
app.MapGet("/api/protected", (HttpContext context) =>
{
    return Results.Ok(new
    {
        message = "This endpoint blocks all bots",
        accessGranted = true,
        visitor = context.IsHuman() ? "human" : "verified-bot"
    });
})
.BlockBots(allowVerifiedBots: true) // Allow Googlebot etc.
.WithName("ProtectedEndpoint")
.WithSummary("Protected endpoint - blocks unverified bots");

// Human only (blocks ALL bots)
app.MapGet("/api/humans-only", (HttpContext context) =>
{
    return Results.Ok(new
    {
        message = "This endpoint is for humans only",
        accessGranted = true
    });
})
.RequireHuman()
.WithName("HumansOnly")
.WithSummary("Human-only endpoint - blocks all bots including verified ones");

// Allow search engines
app.MapGet("/api/allow-search-engines", (HttpContext context) =>
{
    return Results.Ok(new
    {
        message = "Search engines welcome here!",
        isSearchEngine = context.IsSearchEngineBot(),
        botName = context.GetBotName()
    });
})
.BlockBots(allowSearchEngines: true)
.WithName("AllowSearchEngines")
.WithSummary("Allows search engine bots, blocks others");

// High confidence blocking only
app.MapGet("/api/strict-protection", (HttpContext context) =>
{
    return Results.Ok(new
    {
        message = "Only blocks high-confidence bots (>90%)",
        confidence = context.GetBotConfidence()
    });
})
.BlockBots(minConfidence: 0.9)
.WithName("StrictProtection")
.WithSummary("Only blocks bots with >90% confidence");

// ==========================================
// Test helpers
// ==========================================

app.MapGet("/api/test-modes", () =>
{
    return Results.Ok(new
    {
        message = "Test mode header values (use with 'ml-bot-test-mode' header)",
        modes = new Dictionary<string, string>
        {
            ["disable"] = "Bypass detection entirely",
            ["human"] = "Simulate human visitor",
            ["bot"] = "Simulate generic bot",
            ["googlebot"] = "Simulate Googlebot (SearchEngine, verified)",
            ["bingbot"] = "Simulate Bingbot (SearchEngine, verified)",
            ["scraper"] = "Simulate scraper bot",
            ["malicious"] = "Simulate malicious bot",
            ["social"] = "Simulate social media bot",
            ["monitor"] = "Simulate monitoring bot"
        },
        example = "curl -H 'ml-bot-test-mode: googlebot' http://localhost:5000/"
    });
});

app.Run();
