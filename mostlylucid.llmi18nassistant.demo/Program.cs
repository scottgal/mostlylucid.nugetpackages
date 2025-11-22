using Mostlylucid.LlmI18nAssistant.Demo;
using Mostlylucid.LlmI18nAssistant.Extensions;
using Mostlylucid.LlmI18nAssistant.Models;
using Mostlylucid.LlmI18nAssistant.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// Add LLM I18n Assistant services
builder.Services.AddLlmI18nAssistant(builder.Configuration);

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// Health check
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }))
    .WithName("Health")
    .WithOpenApi();

// Service status
app.MapGet("/i18n/status", async (ILlmI18nAssistant assistant, CancellationToken ct) =>
{
    var status = await assistant.CheckServicesAsync(ct);
    return Results.Ok(status);
})
.WithName("GetServiceStatus")
.WithOpenApi()
.WithTags("Status");

// Translate a single string
app.MapPost("/i18n/translate/string", async (TranslateStringRequest request, ILlmI18nAssistant assistant, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Text))
        return Results.BadRequest(new { Error = "Text is required" });

    try
    {
        var translation = await assistant.TranslateStringAsync(
            request.Text,
            request.SourceLanguage ?? "en",
            request.TargetLanguage,
            request.Context,
            ct);

        return Results.Ok(new TranslateStringResponse
        {
            SourceText = request.Text,
            TranslatedText = translation,
            SourceLanguage = request.SourceLanguage ?? "en",
            TargetLanguage = request.TargetLanguage
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
})
.WithName("TranslateString")
.WithOpenApi()
.WithTags("Translation");

// Translate a batch of strings
app.MapPost("/i18n/translate/batch", async (TranslateBatchRequest request, ILlmI18nAssistant assistant, CancellationToken ct) =>
{
    if (request.Entries == null || request.Entries.Count == 0)
        return Results.BadRequest(new { Error = "Entries are required" });

    try
    {
        var options = new TranslationOptions
        {
            UseConsistencyMode = request.UseConsistencyMode ?? true
        };

        var translations = await assistant.TranslateBatchAsync(
            request.Entries,
            request.SourceLanguage ?? "en",
            request.TargetLanguage,
            options,
            ct);

        return Results.Ok(new TranslateBatchResponse
        {
            Entries = translations,
            SourceLanguage = request.SourceLanguage ?? "en",
            TargetLanguage = request.TargetLanguage,
            Count = translations.Count
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
})
.WithName("TranslateBatch")
.WithOpenApi()
.WithTags("Translation");

// Translate a resource file (multipart/form-data)
app.MapPost("/i18n/translate/file", async (
    IFormFile file,
    string targetLanguage,
    string? sourceLanguage,
    bool? useConsistencyMode,
    ILlmI18nAssistant assistant,
    IResourceFileParser parser,
    CancellationToken ct) =>
{
    if (file.Length == 0)
        return Results.BadRequest(new { Error = "File is required" });

    try
    {
        var fileType = parser.GetFileType(file.FileName);

        using var stream = file.OpenReadStream();

        var options = new TranslationOptions
        {
            UseConsistencyMode = useConsistencyMode ?? true
        };

        var result = await assistant.TranslateResourceStreamAsync(
            stream,
            fileType,
            sourceLanguage ?? "en",
            targetLanguage,
            options,
            ct);

        // Return as downloadable file
        using var outputStream = new MemoryStream();
        await parser.WriteAsync(result, outputStream, fileType, ct);
        outputStream.Position = 0;

        var outputFileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}.{targetLanguage}{Path.GetExtension(file.FileName)}";

        return Results.File(
            outputStream.ToArray(),
            "application/octet-stream",
            outputFileName);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
})
.WithName("TranslateFile")
.WithOpenApi()
.WithTags("Translation")
.DisableAntiforgery();

// Translate and return JSON result (for inspection)
app.MapPost("/i18n/translate/file/json", async (
    IFormFile file,
    string targetLanguage,
    string? sourceLanguage,
    bool? useConsistencyMode,
    ILlmI18nAssistant assistant,
    IResourceFileParser parser,
    CancellationToken ct) =>
{
    if (file.Length == 0)
        return Results.BadRequest(new { Error = "File is required" });

    try
    {
        var fileType = parser.GetFileType(file.FileName);

        using var stream = file.OpenReadStream();

        var options = new TranslationOptions
        {
            UseConsistencyMode = useConsistencyMode ?? true
        };

        var result = await assistant.TranslateResourceStreamAsync(
            stream,
            fileType,
            sourceLanguage ?? "en",
            targetLanguage,
            options,
            ct);

        return Results.Ok(new TranslateFileResponse
        {
            SourceLanguage = result.SourceLanguage,
            TargetLanguage = result.TargetLanguage,
            Success = result.Success,
            Statistics = result.Statistics,
            Entries = result.Entries.Select(e => new TranslatedEntry
            {
                Key = e.Key,
                OriginalValue = e.Value,
                TranslatedValue = e.TranslatedValue,
                Method = e.TranslationMethod?.ToString()
            }).ToList(),
            Errors = result.Errors.Select(e => e.Message).ToList(),
            DurationMs = (int)result.Duration.TotalMilliseconds
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
})
.WithName("TranslateFileJson")
.WithOpenApi()
.WithTags("Translation")
.DisableAntiforgery();

app.Run();

namespace Mostlylucid.LlmI18nAssistant.Demo
{
    // Request/Response models

    public class TranslateStringRequest
    {
        public string Text { get; set; } = "";
        public string? SourceLanguage { get; set; }
        public string TargetLanguage { get; set; } = "";
        public string? Context { get; set; }
    }

    public class TranslateStringResponse
    {
        public string SourceText { get; set; } = "";
        public string TranslatedText { get; set; } = "";
        public string SourceLanguage { get; set; } = "";
        public string TargetLanguage { get; set; } = "";
    }

    public class TranslateBatchRequest
    {
        public Dictionary<string, string> Entries { get; set; } = new();
        public string? SourceLanguage { get; set; }
        public string TargetLanguage { get; set; } = "";
        public bool? UseConsistencyMode { get; set; }
    }

    public class TranslateBatchResponse
    {
        public Dictionary<string, string> Entries { get; set; } = new();
        public string SourceLanguage { get; set; } = "";
        public string TargetLanguage { get; set; } = "";
        public int Count { get; set; }
    }

    public class TranslateFileResponse
    {
        public string SourceLanguage { get; set; } = "";
        public string TargetLanguage { get; set; } = "";
        public bool Success { get; set; }
        public TranslationStatistics Statistics { get; set; } = new();
        public List<TranslatedEntry> Entries { get; set; } = [];
        public List<string> Errors { get; set; } = [];
        public int DurationMs { get; set; }
    }

    public class TranslatedEntry
    {
        public string Key { get; set; } = "";
        public string OriginalValue { get; set; } = "";
        public string? TranslatedValue { get; set; }
        public string? Method { get; set; }
    }
}
