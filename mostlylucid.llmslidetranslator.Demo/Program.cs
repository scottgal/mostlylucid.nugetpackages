using System.Text.Json;
using mostlylucid.llmslidetranslator.Demo.Hubs;
using mostlylucid.llmslidetranslator.Demo.Services;
using mostlylucid.llmslidetranslator.Extensions;
using mostlylucid.llmslidetranslator.Models;
using mostlylucid.llmslidetranslator.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add SignalR
builder.Services.AddSignalR();

// Add CORS for SignalR
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// Add LLM Slide Translator services
builder.Services.AddLlmSlideTranslator(builder.Configuration);

// Add streaming translation service
builder.Services.AddSingleton<StreamingTranslationService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// Serve static files
app.UseStaticFiles();
app.UseDefaultFiles();

// Map SignalR hub
app.MapHub<TranslationHub>("/hubs/translation");

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "LLM Slide Translator Demo API" }))
    .WithName("Health")
    .WithOpenApi();

// Translate document (standard endpoint)
app.MapPost("/translate", async (
        TranslateRequest request,
        ILlmSlideTranslator translator) =>
    {
        var result = await translator.TranslateAsync(
            request.Markdown,
            request.DocumentId,
            request.SourceLanguage,
            request.TargetLanguage,
            request.Method);

        return Results.Ok(result);
    })
    .WithName("Translate")
    .WithOpenApi();

// Stream translation (SSE endpoint)
app.MapGet("/translate/stream/{documentId}", async (
        string documentId,
        string markdown,
        string sourceLanguage,
        string targetLanguage,
        TranslationMethod method,
        StreamingTranslationService streamingService,
        HttpContext context) =>
    {
        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";

        await foreach (var update in streamingService.StreamTranslationAsync(
                           markdown, documentId, sourceLanguage, targetLanguage, method))
        {
            var json = JsonSerializer.Serialize(update);
            await context.Response.WriteAsync($"data: {json}\n\n");
            await context.Response.Body.FlushAsync();
        }
    })
    .WithName("StreamTranslation")
    .WithOpenApi();

// Compare translations
app.MapPost("/compare", async (
        CompareRequest request,
        ILlmSlideTranslator translator,
        ITranslationComparer comparer) =>
    {
        // Translate with method 1
        var result1 = await translator.TranslateAsync(
            request.Markdown,
            request.DocumentId + "_method1",
            request.SourceLanguage,
            request.TargetLanguage,
            request.Method1);

        // Translate with method 2
        var result2 = await translator.TranslateAsync(
            request.Markdown,
            request.DocumentId + "_method2",
            request.SourceLanguage,
            request.TargetLanguage,
            request.Method2);

        // Compare
        var comparison = await comparer.CompareAsync(result1, result2);

        return Results.Ok(comparison);
    })
    .WithName("CompareTranslations")
    .WithOpenApi();

// Get translation progress
app.MapGet("/translate/{documentId}/progress", async (
        string documentId,
        ILlmSlideTranslator translator) =>
    {
        var progress = await translator.GetProgressAsync(documentId);
        return Results.Ok(progress);
    })
    .WithName("GetProgress")
    .WithOpenApi();

// Check service availability
app.MapGet("/services/status", async (
        IOllamaClient ollamaClient,
        INmtClient nmtClient) =>
    {
        var ollamaAvailable = await ollamaClient.IsAvailableAsync();
        var nmtAvailable = await nmtClient.IsAvailableAsync();

        List<string>? ollamaModels = null;
        if (ollamaAvailable) ollamaModels = await ollamaClient.GetModelsAsync();

        return Results.Ok(new
        {
            Ollama = new
            {
                Available = ollamaAvailable,
                Models = ollamaModels
            },
            Nmt = new
            {
                Available = nmtAvailable
            }
        });
    })
    .WithName("ServiceStatus")
    .WithOpenApi();

app.Run();

// Request DTOs
internal record TranslateRequest(
    string Markdown,
    string DocumentId,
    string SourceLanguage = "en",
    string TargetLanguage = "de",
    TranslationMethod Method = TranslationMethod.RagLlm);

internal record CompareRequest(
    string Markdown,
    string DocumentId,
    string SourceLanguage,
    string TargetLanguage,
    TranslationMethod Method1,
    TranslationMethod Method2);