using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LLMContentModeration.Attributes;
using Mostlylucid.LLMContentModeration.Models;
using Mostlylucid.LLMContentModeration.Services;

namespace Mostlylucid.LLMContentModeration.Middleware;

/// <summary>
///     Middleware that intercepts requests and responses for content moderation
/// </summary>
public class ContentModerationMiddleware(
    RequestDelegate next,
    ILogger<ContentModerationMiddleware> logger,
    IOptions<ModerationOptions> options)
{
    public const string ModerationResultKey = "ContentModerationResult";
    private readonly ModerationOptions _options = options.Value;

    public async Task InvokeAsync(HttpContext context, IContentModerationService moderationService)
    {
        if (!_options.Enabled)
        {
            await next(context);
            return;
        }

        // Check if path should be excluded
        if (ShouldSkipPath(context.Request.Path))
        {
            await next(context);
            return;
        }

        // Check for skip attribute
        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<SkipModerationAttribute>() != null)
        {
            await next(context);
            return;
        }

        // Get policy from attribute (if present)
        var policyAttribute = endpoint?.Metadata.GetMetadata<ModerationPolicyAttribute>();
        if (policyAttribute?.Skip == true)
        {
            await next(context);
            return;
        }

        var effectiveOptions = policyAttribute?.ToOptions(_options) ?? _options;

        // Moderate request body (POST, PUT, PATCH)
        if (_options.ModerateRequests && IsModerableMethod(context.Request.Method))
        {
            var moderationResult = await ModerateRequestAsync(context, moderationService, effectiveOptions);

            if (moderationResult != null)
            {
                context.Items[ModerationResultKey] = moderationResult;

                if (moderationResult.IsBlocked)
                {
                    await WriteBlockedResponseAsync(context, moderationResult);
                    return;
                }
            }
        }

        // Continue to next middleware
        if (_options.ModerateResponses)
        {
            // Wrap response stream to capture output
            var originalBodyStream = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            await next(context);

            // Moderate response
            await ModerateResponseAsync(context, moderationService, effectiveOptions, originalBodyStream, responseBody);
        }
        else
        {
            await next(context);
        }
    }

    private async Task<ModerationResult?> ModerateRequestAsync(
        HttpContext context,
        IContentModerationService moderationService,
        ModerationOptions options)
    {
        if (!HasModerableContentType(context.Request.ContentType))
            return null;

        try
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            if (string.IsNullOrWhiteSpace(body))
                return null;

            // Extract text content from JSON if needed
            var contentToModerate = ExtractTextContent(body, context.Request.ContentType);

            if (string.IsNullOrWhiteSpace(contentToModerate))
                return null;

            logger.LogDebug("Moderating request content ({Length} chars)", contentToModerate.Length);

            var result = await moderationService.ModerateAsync(contentToModerate, options);

            // If masking was applied, rewrite the request body
            if (result.Mode == ModerationMode.MaskAndAllow &&
                !string.IsNullOrEmpty(result.ModeratedContent) &&
                result.ModeratedContent != contentToModerate)
            {
                var modifiedBody = ReplaceTextContent(body, contentToModerate, result.ModeratedContent,
                    context.Request.ContentType);
                var bytes = Encoding.UTF8.GetBytes(modifiedBody);
                context.Request.Body = new MemoryStream(bytes);
                context.Request.ContentLength = bytes.Length;
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error moderating request content");
            return null;
        }
    }

    private async Task ModerateResponseAsync(
        HttpContext context,
        IContentModerationService moderationService,
        ModerationOptions options,
        Stream originalBodyStream,
        MemoryStream responseBody)
    {
        try
        {
            responseBody.Position = 0;
            var responseContent = await new StreamReader(responseBody).ReadToEndAsync();

            if (!string.IsNullOrWhiteSpace(responseContent) &&
                HasModerableContentType(context.Response.ContentType))
            {
                var contentToModerate = ExtractTextContent(responseContent, context.Response.ContentType);

                if (!string.IsNullOrWhiteSpace(contentToModerate))
                {
                    var result = await moderationService.ModerateAsync(contentToModerate, options);

                    if (result.Mode == ModerationMode.MaskAndAllow &&
                        !string.IsNullOrEmpty(result.ModeratedContent))
                        responseContent = ReplaceTextContent(
                            responseContent, contentToModerate, result.ModeratedContent, context.Response.ContentType);
                }
            }

            // Write (potentially modified) response to original stream
            responseBody.Position = 0;
            var outputBytes = Encoding.UTF8.GetBytes(responseContent);
            context.Response.ContentLength = outputBytes.Length;
            await originalBodyStream.WriteAsync(outputBytes);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error moderating response content");
            // On error, write original response
            responseBody.Position = 0;
            await responseBody.CopyToAsync(originalBodyStream);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private static async Task WriteBlockedResponseAsync(HttpContext context, ModerationResult result)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/json";

        var response = new ModerationBlockedResponse
        {
            Error = "Content Blocked",
            Message = "Your content was blocked due to policy violations.",
            Reasons = result.Flags.Select(f => f.Category.ToString()).ToArray(),
            ModerationId = result.Id
        };

        await context.Response.WriteAsJsonAsync(response);
    }

    private bool ShouldSkipPath(PathString path)
    {
        var pathValue = path.Value ?? string.Empty;

        // Check required paths first (override exclusions)
        foreach (var required in _options.RequiredPaths)
            if (MatchesWildcard(pathValue, required))
                return false;

        // Check excluded paths
        foreach (var excluded in _options.ExcludedPaths)
            if (MatchesWildcard(pathValue, excluded))
                return true;

        return false;
    }

    private static bool MatchesWildcard(string path, string pattern)
    {
        if (pattern.EndsWith('*'))
        {
            var prefix = pattern[..^1];
            return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return path.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsModerableMethod(string method)
    {
        return method is "POST" or "PUT" or "PATCH";
    }

    private bool HasModerableContentType(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
            return false;

        return _options.ContentTypes.Any(ct =>
            contentType.Contains(ct, StringComparison.OrdinalIgnoreCase));
    }

    private static string ExtractTextContent(string body, string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
            return body;

        if (contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            return ExtractJsonTextFields(body);

        return body;
    }

    private static string ExtractJsonTextFields(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var textBuilder = new StringBuilder();
            ExtractJsonStrings(doc.RootElement, textBuilder);
            return textBuilder.ToString();
        }
        catch
        {
            return json;
        }
    }

    private static void ExtractJsonStrings(JsonElement element, StringBuilder builder)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var value = element.GetString();
                if (!string.IsNullOrWhiteSpace(value)) builder.AppendLine(value);
                break;

            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject()) ExtractJsonStrings(property.Value, builder);
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) ExtractJsonStrings(item, builder);
                break;
        }
    }

    private static string ReplaceTextContent(string original, string oldContent, string newContent, string? contentType)
    {
        if (contentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
            // For JSON, we need to handle this more carefully
            // Simple approach: replace string values
            return original.Replace(oldContent, newContent);

        return newContent;
    }
}

/// <summary>
///     Extension methods for content moderation middleware
/// </summary>
public static class ContentModerationMiddlewareExtensions
{
    /// <summary>
    ///     Add content moderation middleware to the pipeline
    /// </summary>
    public static IApplicationBuilder UseContentModeration(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ContentModerationMiddleware>();
    }
}