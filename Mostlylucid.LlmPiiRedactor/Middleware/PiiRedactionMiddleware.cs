using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmPiiRedactor.Models;
using Mostlylucid.LlmPiiRedactor.Services;

namespace Mostlylucid.LlmPiiRedactor.Middleware;

/// <summary>
/// Middleware that redacts PII from request/response bodies and headers.
/// </summary>
public class PiiRedactionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PiiRedactionMiddleware> _logger;
    private readonly PiiMiddlewareOptions _options;

    public PiiRedactionMiddleware(
        RequestDelegate next,
        ILogger<PiiRedactionMiddleware> logger,
        IOptions<PiiMiddlewareOptions> options)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context, IPiiRedactionService redactionService)
    {
        if (!_options.Enabled || IsPathExcluded(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Redact request headers
        if (_options.RedactRequestHeaders)
        {
            RedactHeaders(context.Request.Headers, redactionService);
        }

        // Redact query strings
        if (_options.RedactQueryStrings)
        {
            RedactQueryString(context, redactionService);
        }

        // Handle request body redaction
        if (_options.RedactRequestBody && IsProcessableContentType(context.Request.ContentType))
        {
            await RedactRequestBody(context, redactionService);
        }

        // Handle response body redaction
        if (_options.RedactResponseBody)
        {
            await RedactResponseBody(context, redactionService);
        }
        else
        {
            await _next(context);
        }
    }

    private bool IsPathExcluded(PathString path)
    {
        var pathValue = path.Value ?? "";

        foreach (var excludedPath in _options.ExcludedPaths)
        {
            if (excludedPath.EndsWith('*'))
            {
                var prefix = excludedPath[..^1];
                if (pathValue.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (pathValue.Equals(excludedPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsProcessableContentType(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
            return false;

        // Extract the base content type without charset, etc.
        var baseType = contentType.Split(';')[0].Trim();
        return _options.ProcessableContentTypes.Contains(baseType);
    }

    private void RedactHeaders(IHeaderDictionary headers, IPiiRedactionService redactionService)
    {
        var headersToRedact = new List<(string Key, string Value)>();

        foreach (var header in headers)
        {
            // Always redact sensitive headers completely
            if (_options.SensitiveHeaders.Contains(header.Key))
            {
                headersToRedact.Add((header.Key, "[REDACTED]"));
                continue;
            }

            // Check header values for PII
            var values = header.Value.ToArray();
            var hasChanges = false;
            var newValues = new List<string>();

            foreach (var value in values)
            {
                if (string.IsNullOrEmpty(value))
                {
                    newValues.Add(value);
                    continue;
                }

                var result = redactionService.Redact(value);
                if (result.ContainedPii)
                {
                    newValues.Add(result.RedactedText);
                    hasChanges = true;
                }
                else
                {
                    newValues.Add(value);
                }
            }

            if (hasChanges)
            {
                headersToRedact.Add((header.Key, string.Join(", ", newValues)));
            }
        }

        // Apply changes
        foreach (var (key, value) in headersToRedact)
        {
            headers[key] = value;
        }
    }

    private static void RedactQueryString(HttpContext context, IPiiRedactionService redactionService)
    {
        if (!context.Request.QueryString.HasValue)
            return;

        var query = context.Request.Query;
        var hasChanges = false;
        var newQueryItems = new List<KeyValuePair<string, string>>();

        foreach (var item in query)
        {
            var values = item.Value.ToArray();
            foreach (var value in values)
            {
                if (string.IsNullOrEmpty(value))
                {
                    newQueryItems.Add(new KeyValuePair<string, string>(item.Key, value));
                    continue;
                }

                var result = redactionService.Redact(value);
                if (result.ContainedPii)
                {
                    newQueryItems.Add(new KeyValuePair<string, string>(item.Key, result.RedactedText));
                    hasChanges = true;
                }
                else
                {
                    newQueryItems.Add(new KeyValuePair<string, string>(item.Key, value));
                }
            }
        }

        if (hasChanges)
        {
            // Note: QueryString is read-only in ASP.NET Core, but we've logged the original
            // The redacted values are available in context.Items for logging purposes
            context.Items["PII_RedactedQuery"] = new QueryString("?" +
                string.Join("&", newQueryItems.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}")));
        }
    }

    private async Task RedactRequestBody(HttpContext context, IPiiRedactionService redactionService)
    {
        context.Request.EnableBuffering();

        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();

        if (!string.IsNullOrEmpty(body) && body.Length <= _options.MaxBodySize)
        {
            var result = redactionService.Redact(body);
            if (result.ContainedPii)
            {
                var bytes = Encoding.UTF8.GetBytes(result.RedactedText);
                context.Request.Body = new MemoryStream(bytes);
                context.Request.ContentLength = bytes.Length;

                _logger.LogDebug("Redacted PII from request body");
            }
            else
            {
                context.Request.Body.Position = 0;
            }
        }
        else
        {
            context.Request.Body.Position = 0;
        }
    }

    private async Task RedactResponseBody(HttpContext context, IPiiRedactionService redactionService)
    {
        var originalBodyStream = context.Response.Body;

        using var newBodyStream = new MemoryStream();
        context.Response.Body = newBodyStream;

        try
        {
            await _next(context);

            if (IsProcessableContentType(context.Response.ContentType) &&
                newBodyStream.Length <= _options.MaxBodySize)
            {
                newBodyStream.Position = 0;
                var body = await new StreamReader(newBodyStream).ReadToEndAsync();

                if (!string.IsNullOrEmpty(body))
                {
                    var result = redactionService.Redact(body);

                    if (result.ContainedPii)
                    {
                        var bytes = Encoding.UTF8.GetBytes(result.RedactedText);
                        context.Response.ContentLength = bytes.Length;

                        if (_options.AddRedactionHeader)
                        {
                            context.Response.Headers[_options.RedactionHeaderName] = "true";
                        }

                        // Redact response headers
                        if (_options.RedactResponseHeaders)
                        {
                            RedactHeaders(context.Response.Headers, redactionService);
                        }

                        await originalBodyStream.WriteAsync(bytes);
                        _logger.LogDebug("Redacted PII from response body");
                        return;
                    }
                }
            }

            // No redaction needed, copy original response
            newBodyStream.Position = 0;
            await newBodyStream.CopyToAsync(originalBodyStream);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }
}
