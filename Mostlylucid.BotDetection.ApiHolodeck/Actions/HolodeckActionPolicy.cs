using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.BotDetection.Actions;
using Mostlylucid.BotDetection.ApiHolodeck.Models;
using Mostlylucid.BotDetection.Orchestration;

namespace Mostlylucid.BotDetection.ApiHolodeck.Actions;

/// <summary>
///     Action policy that redirects detected bots to a fake API "holodeck" powered by MockLLMApi.
///     Bots receive realistic-looking but useless data, wasting their resources while you study their behavior.
/// </summary>
/// <remarks>
///     <para>
///         The holodeck maintains a consistent "fake world" per bot fingerprint/IP, so repeated
///         requests return coherent (but fake) data. This makes it harder for bots to detect
///         they're being sandboxed.
///     </para>
///     <para>
///         Configuration example:
///         <code>
///         {
///           "BotDetection": {
///             "ActionPolicies": {
///               "holodeck": {
///                 "Type": "Holodeck",
///                 "MockApiBaseUrl": "http://localhost:5116/api/mock",
///                 "Mode": "realistic-but-useless",
///                 "MaxStudyRequests": 50
///               }
///             }
///           }
///         }
///         </code>
///     </para>
/// </remarks>
public class HolodeckActionPolicy : IActionPolicy
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HolodeckActionPolicy> _logger;
    private readonly HolodeckOptions _options;

    // Track requests per context for study cutoff
    private static readonly ConcurrentDictionary<string, int> _requestCounts = new();

    public HolodeckActionPolicy(
        IHttpClientFactory httpClientFactory,
        IOptions<HolodeckOptions> options,
        ILogger<HolodeckActionPolicy> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Holodeck");
        _options = options.Value;
        _logger = logger;

        // Configure timeout
        _httpClient.Timeout = TimeSpan.FromMilliseconds(_options.MockApiTimeoutMs);
    }

    /// <inheritdoc />
    public string Name => "holodeck";

    /// <inheritdoc />
    public ActionType ActionType => ActionType.Custom;

    /// <inheritdoc />
    public async Task<ActionResult> ExecuteAsync(
        HttpContext context,
        AggregatedEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        // Generate context key for this bot
        var contextKey = GetContextKey(context, evidence);

        // Check if we've studied this bot enough
        var requestCount = _requestCounts.AddOrUpdate(contextKey, 1, (_, count) => count + 1);

        if (_options.MaxStudyRequests > 0 && requestCount > _options.MaxStudyRequests)
        {
            _logger.LogInformation(
                "Holodeck cutoff reached for {Context}: {Count}/{Max} requests. Hard blocking.",
                contextKey, requestCount, _options.MaxStudyRequests);

            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Access denied",
                code = "RATE_LIMITED"
            }, cancellationToken);

            return ActionResult.Blocked(403, $"Holodeck cutoff: {requestCount} requests from {contextKey}");
        }

        _logger.LogInformation(
            "Routing to holodeck: {Method} {Path} -> {MockApi} (context={Context}, mode={Mode}, request #{Count})",
            context.Request.Method,
            context.Request.Path,
            _options.MockApiBaseUrl,
            contextKey,
            _options.Mode,
            requestCount);

        try
        {
            // Build the mock API URL
            var mockUrl = BuildMockUrl(context, contextKey);

            // Forward the request to MockLLMApi
            using var proxyRequest = CreateProxyRequest(context, mockUrl);

            // Add holodeck-specific headers
            proxyRequest.Headers.TryAddWithoutValidation("X-Holodeck-Context", contextKey);
            proxyRequest.Headers.TryAddWithoutValidation("X-Holodeck-Mode", _options.Mode.ToString());
            proxyRequest.Headers.TryAddWithoutValidation("X-Holodeck-Request-Number", requestCount.ToString());

            // Add mode-specific configuration
            AddModeHeaders(proxyRequest);

            var response = await _httpClient.SendAsync(
                proxyRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            // Copy response to client
            context.Response.StatusCode = (int)response.StatusCode;

            // Copy headers (except transfer-encoding which ASP.NET handles)
            foreach (var header in response.Headers)
            {
                if (!header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.Headers[header.Key] = header.Value.ToArray();
                }
            }

            foreach (var header in response.Content.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            // Add holodeck indicator headers (for debugging)
            context.Response.Headers["X-Holodeck"] = "true";
            context.Response.Headers["X-Holodeck-Context"] = contextKey;

            // Stream response body
            await response.Content.CopyToAsync(context.Response.Body, cancellationToken);

            return new ActionResult
            {
                Continue = false,
                StatusCode = (int)response.StatusCode,
                Description = $"Holodeck response from MockLLMApi (mode={_options.Mode})",
                Metadata = new Dictionary<string, object>
                {
                    ["context"] = contextKey,
                    ["mode"] = _options.Mode.ToString(),
                    ["requestNumber"] = requestCount,
                    ["mockUrl"] = mockUrl
                }
            };
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Holodeck request timed out for {Context}", contextKey);
            return await FallbackResponse(context, "Holodeck timeout", cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Holodeck request failed for {Context}", contextKey);
            return await FallbackResponse(context, "Holodeck unavailable", cancellationToken);
        }
    }

    private string GetContextKey(HttpContext context, AggregatedEvidence evidence)
    {
        return _options.ContextSource switch
        {
            ContextSource.Fingerprint => evidence.PrimaryBotName ?? evidence.Signals
                .Where(s => s.Key.Contains("Fingerprint", StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Value?.ToString())
                .FirstOrDefault() ?? GetIpKey(context),

            ContextSource.Ip => GetIpKey(context),

            ContextSource.Session => context.Session?.Id ?? GetIpKey(context),

            ContextSource.Combined => $"{GetIpKey(context)}:{evidence.PrimaryBotName ?? "unknown"}",

            _ => GetIpKey(context)
        };
    }

    private static string GetIpKey(HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private string BuildMockUrl(HttpContext context, string contextKey)
    {
        var baseUrl = _options.MockApiBaseUrl.TrimEnd('/');
        var originalPath = context.Request.Path.ToString();
        var originalQuery = context.Request.QueryString.ToString();

        // Add context to query string for MockLLMApi
        var separator = string.IsNullOrEmpty(originalQuery) ? "?" : "&";
        var contextParam = $"{separator}context={Uri.EscapeDataString(contextKey)}";

        return $"{baseUrl}{originalPath}{originalQuery}{contextParam}";
    }

    private HttpRequestMessage CreateProxyRequest(HttpContext context, string targetUrl)
    {
        var request = new HttpRequestMessage(
            new HttpMethod(context.Request.Method),
            targetUrl);

        // Copy headers from original request
        foreach (var header in context.Request.Headers)
        {
            // Skip headers that shouldn't be forwarded
            if (header.Key.StartsWith("Host", StringComparison.OrdinalIgnoreCase) ||
                header.Key.StartsWith("Content-Length", StringComparison.OrdinalIgnoreCase))
                continue;

            request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }

        // Copy body for POST/PUT/PATCH
        if (context.Request.ContentLength > 0 &&
            (context.Request.Method == "POST" ||
             context.Request.Method == "PUT" ||
             context.Request.Method == "PATCH"))
        {
            context.Request.EnableBuffering();
            request.Content = new StreamContent(context.Request.Body);

            if (context.Request.ContentType != null)
            {
                request.Content.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue(context.Request.ContentType);
            }
        }

        return request;
    }

    private void AddModeHeaders(HttpRequestMessage request)
    {
        switch (_options.Mode)
        {
            case HolodeckMode.Realistic:
                // No special headers - let MockLLMApi generate realistic data
                break;

            case HolodeckMode.RealisticButUseless:
                // Request generic/demo data
                request.Headers.TryAddWithoutValidation("X-Response-Shape", "generic");
                request.Headers.TryAddWithoutValidation("X-Mock-Profile", "demo");
                break;

            case HolodeckMode.Chaos:
                // Enable error simulation
                var random = Random.Shared;
                if (random.NextDouble() < 0.3) // 30% error rate
                {
                    request.Headers.TryAddWithoutValidation("X-Simulate-Error", "true");
                    request.Headers.TryAddWithoutValidation("X-Error-Code",
                        random.Next(2) == 0 ? "500" : "503");
                }

                // Occasionally add delays
                if (random.NextDouble() < 0.2)
                {
                    request.Headers.TryAddWithoutValidation("X-Simulate-Delay",
                        random.Next(1000, 5000).ToString());
                }

                break;

            case HolodeckMode.StrictSchema:
                // Use OpenAPI-based mocking
                request.Headers.TryAddWithoutValidation("X-Use-Schema", "true");
                break;

            case HolodeckMode.Adversarial:
                // Mix of tactics
                var rand = Random.Shared;
                var tactic = rand.Next(4);
                switch (tactic)
                {
                    case 0:
                        request.Headers.TryAddWithoutValidation("X-Response-Shape", "inconsistent");
                        break;
                    case 1:
                        request.Headers.TryAddWithoutValidation("X-Simulate-Error", "true");
                        break;
                    case 2:
                        request.Headers.TryAddWithoutValidation("X-Simulate-Delay", "2000");
                        break;
                    // case 3: normal response
                }

                break;
        }
    }

    private async Task<ActionResult> FallbackResponse(
        HttpContext context,
        string reason,
        CancellationToken cancellationToken)
    {
        // When MockLLMApi is unavailable, return a simple JSON response
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(new
        {
            data = new object[] { },
            message = "No results found",
            timestamp = DateTime.UtcNow
        }, cancellationToken);

        return new ActionResult
        {
            Continue = false,
            StatusCode = 200,
            Description = $"Holodeck fallback: {reason}"
        };
    }

    /// <summary>
    ///     Reset the request count for a context (for testing).
    /// </summary>
    public static void ResetRequestCount(string contextKey)
    {
        _requestCounts.TryRemove(contextKey, out _);
    }

    /// <summary>
    ///     Get the current request count for a context.
    /// </summary>
    public static int GetRequestCount(string contextKey)
    {
        return _requestCounts.TryGetValue(contextKey, out var count) ? count : 0;
    }
}
