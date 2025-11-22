using Mostlylucid.LlmPiiRedactor.Extensions;
using Mostlylucid.LlmPiiRedactor.Filters;
using Mostlylucid.LlmPiiRedactor.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add PII redaction services with custom configuration
builder.Services.AddPiiRedaction(
    configureRedaction: options =>
    {
        options.DefaultStyle = RedactionStyle.PartialMask;
        options.DetectionTypes = PiiType.All;
        options.MinConfidenceThreshold = 0.7;

        // Show partial info for these types
        options.StyleOverrides[PiiType.Email] = RedactionStyle.PartialMask;
        options.StyleOverrides[PiiType.CreditCard] = RedactionStyle.PartialMask;
        options.StyleOverrides[PiiType.PhoneNumber] = RedactionStyle.PartialMask;

        // Use tokens for debugging
        options.StyleOverrides[PiiType.Name] = RedactionStyle.Tokenized;
    },
    configureMiddleware: options =>
    {
        options.Enabled = true;
        options.RedactRequestBody = true;
        options.RedactResponseBody = true;
        options.RedactQueryStrings = true;
        options.ExcludedPaths.Add("/swagger/*");
    },
    configureLogging: options =>
    {
        options.Enabled = true;
        options.RedactExceptions = true;
        options.RedactStackTraces = false;
    }
);

builder.Services.AddControllers(options =>
{
    // Add global exception filter
    options.Filters.Add<PiiExceptionFilter>();
});

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Enable PII redaction middleware
app.UsePiiRedaction();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Demo endpoints
app.MapGet("/", () => "PII Redaction Demo API - Try the /api/demo endpoints!");

app.MapGet("/api/demo/redact", (string text, Mostlylucid.LlmPiiRedactor.Services.IPiiRedactionService redactionService) =>
{
    var result = redactionService.Redact(text);
    return Results.Ok(new
    {
        original = result.OriginalText,
        redacted = result.RedactedText,
        containedPii = result.ContainedPii,
        matches = result.Matches.Select(m => new
        {
            type = m.Type.ToString(),
            original = m.OriginalValue,
            redacted = m.RedactedValue,
            confidence = m.Confidence
        })
    });
});

app.MapGet("/api/demo/detect", (string text, Mostlylucid.LlmPiiRedactor.Services.IPiiRedactionService redactionService) =>
{
    var matches = redactionService.Detect(text);
    return Results.Ok(new
    {
        text,
        containsPii = matches.Any(),
        matchCount = matches.Count,
        matches = matches.Select(m => new
        {
            type = m.Type.ToString(),
            value = m.OriginalValue,
            confidence = m.Confidence,
            position = new { start = m.StartIndex, length = m.Length }
        })
    });
});

app.MapGet("/api/demo/stats", (Mostlylucid.LlmPiiRedactor.Services.IPiiRedactionService redactionService) =>
{
    var stats = redactionService.GetStatistics();
    return Results.Ok(stats);
});

app.MapPost("/api/demo/user", (UserDto user, Mostlylucid.LlmPiiRedactor.Services.IPiiRedactionService redactionService, ILogger<Program> logger) =>
{
    // This will be logged with PII redacted if using the redacting logger
    logger.LogInformation("Received user data: {Email}, {Phone}", user.Email, user.Phone);

    // Process and return redacted response
    return Results.Ok(new
    {
        message = "User data processed",
        redactedEmail = redactionService.Redact(user.Email ?? "").RedactedText,
        redactedPhone = redactionService.Redact(user.Phone ?? "").RedactedText
    });
});

app.MapGet("/api/demo/styles", (Mostlylucid.LlmPiiRedactor.Services.IPiiRedactionService _) =>
{
    var testEmail = "john.doe@example.com";
    var testCard = "4111-1111-1111-1111";
    var testPhone = "+1-555-123-4567";

    return Results.Ok(new
    {
        sampleData = new { email = testEmail, creditCard = testCard, phone = testPhone },
        styles = Enum.GetNames<RedactionStyle>()
    });
});

app.Run();

public record UserDto(string? Name, string? Email, string? Phone, string? Address);
