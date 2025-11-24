using Mostlylucid.LLMContentModeration.Attributes;
using Mostlylucid.LLMContentModeration.Extensions;
using Mostlylucid.LLMContentModeration.Middleware;
using Mostlylucid.LLMContentModeration.Models;
using Mostlylucid.LLMContentModeration.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add LLM Content Moderation
builder.Services.AddLLMContentModeration(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enable content moderation middleware
app.UseContentModeration();

// Serve static files
app.UseStaticFiles();
app.UseDefaultFiles();

// Health check endpoint (excluded from moderation)
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "LLM Content Moderation Demo" }))
    .WithName("Health")
    .WithOpenApi();

// Service status endpoint
app.MapGet("/api/moderation/status", async (IContentModerationService moderationService) =>
    {
        var status = await moderationService.GetStatusAsync();
        return Results.Ok(status);
    })
    .WithName("ModerationStatus")
    .WithOpenApi();

// Direct moderation endpoint (test content without middleware)
app.MapPost("/api/moderation/analyze", async (
        AnalyzeRequest request,
        IContentModerationService moderationService) =>
    {
        var result = await moderationService.ModerateAsync(request.Content, request.Mode);
        return Results.Ok(result);
    })
    .WithName("AnalyzeContent")
    .WithOpenApi();

// Blog comments endpoint - moderated by default (Block mode)
app.MapPost("/api/comments", (CommentRequest request) =>
    {
        // If we reach here, the content passed moderation
        var comment = new CommentResponse
        {
            Id = Guid.NewGuid().ToString("N"),
            Author = request.Author,
            Content = request.Content,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = "Approved"
        };

        return Results.Created($"/api/comments/{comment.Id}", comment);
    })
    .WithName("CreateComment")
    .WithOpenApi();

// Comments with MaskAndAllow mode - PII will be masked but content allowed
app.MapPost("/api/comments/masked", (CommentRequest request, HttpContext context) =>
    {
        // Get the moderation result to see what was masked
        var moderationResult = context.Items[ContentModerationMiddleware.ModerationResultKey] as ModerationResult;

        var comment = new CommentResponse
        {
            Id = Guid.NewGuid().ToString("N"),
            Author = request.Author,
            Content = moderationResult?.ModeratedContent ?? request.Content,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = moderationResult?.IsFlagged == true ? "Moderated" : "Approved",
            ModerationInfo = moderationResult?.Summary
        };

        return Results.Created($"/api/comments/{comment.Id}", comment);
    })
    .WithMetadata(new ModerationPolicyAttribute(ModerationMode.MaskAndAllow))
    .WithName("CreateMaskedComment")
    .WithOpenApi();

// Comments with DetectOnly mode - log but don't block
app.MapPost("/api/comments/detect-only", (CommentRequest request, HttpContext context) =>
    {
        var moderationResult = context.Items[ContentModerationMiddleware.ModerationResultKey] as ModerationResult;

        var comment = new CommentResponse
        {
            Id = Guid.NewGuid().ToString("N"),
            Author = request.Author,
            Content = request.Content,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = moderationResult?.IsFlagged == true ? "Flagged for Review" : "Approved",
            ModerationInfo = moderationResult?.Summary
        };

        return Results.Created($"/api/comments/{comment.Id}", comment);
    })
    .WithMetadata(new ModerationPolicyAttribute(ModerationMode.DetectOnly))
    .WithName("CreateDetectOnlyComment")
    .WithOpenApi();

// Skip moderation for this endpoint
app.MapPost("/api/feedback",
        (FeedbackRequest request) =>
        {
            return Results.Ok(new { Message = "Feedback received (unmoderated)", request.Content });
        })
    .WithMetadata(new SkipModerationAttribute())
    .WithName("SubmitFeedback")
    .WithOpenApi();

// PII detection test endpoint
app.MapPost("/api/pii/detect", (PiiRequest request, IPiiDetector piiDetector) =>
    {
        var options = new PiiDetectionOptions
        {
            DetectEmail = true,
            DetectPhone = true,
            DetectAddress = true,
            DetectIban = true,
            DetectCreditCard = true,
            DetectSocialSecurityNumber = true
        };

        var matches = piiDetector.DetectPii(request.Content, options);

        return Results.Ok(new
        {
            request.Content,
            PiiFound = matches.Count,
            Matches = matches.Select(m => new
            {
                m.Type,
                m.OriginalValue,
                m.StartIndex,
                m.EndIndex,
                m.Confidence
            })
        });
    })
    .WithMetadata(new SkipModerationAttribute())
    .WithName("DetectPii")
    .WithOpenApi();

// PII masking test endpoint
app.MapPost("/api/pii/mask", (PiiRequest request, IPiiDetector piiDetector) =>
    {
        var options = new PiiDetectionOptions
        {
            DetectEmail = true,
            DetectPhone = true,
            DetectAddress = true,
            DetectIban = true,
            DetectCreditCard = true,
            DetectSocialSecurityNumber = true,
            MaskCharacter = '*',
            UnmaskedChars = 2
        };

        var matches = piiDetector.DetectPii(request.Content, options);
        var maskedContent = piiDetector.MaskPii(request.Content, matches, options);

        return Results.Ok(new
        {
            Original = request.Content,
            Masked = maskedContent,
            PiiMasked = matches.Select(m => new
            {
                m.Type,
                m.OriginalValue,
                m.MaskedValue
            })
        });
    })
    .WithMetadata(new SkipModerationAttribute())
    .WithName("MaskPii")
    .WithOpenApi();

app.Run();

// Request/Response DTOs
internal record AnalyzeRequest(string Content, ModerationMode? Mode = null);

internal record CommentRequest(string Author, string Content, string? Email = null);

internal record CommentResponse
{
    public string Id { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ModerationInfo { get; set; }
}

internal record FeedbackRequest(string Content);

internal record PiiRequest(string Content);