using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmSeoMetadata.Models;
using Mostlylucid.LlmSeoMetadata.Telemetry;
using OllamaSharp;

namespace Mostlylucid.LlmSeoMetadata.Services;

/// <summary>
///     SEO metadata generation service using Ollama local LLM
/// </summary>
public partial class OllamaSeoMetadataService : ISeoMetadataService
{
    private readonly ILogger<OllamaSeoMetadataService> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly SeoMetadataOptions _options;
    private long _cacheHits;
    private long _failedGenerations;
    private DateTime? _lastSuccessfulGeneration;
    private bool _llmHealthy;
    private long _successfulGenerations;
    private long _totalGenerationTimeMs;

    // Statistics tracking (thread-safe)
    private long _totalRequests;

    public OllamaSeoMetadataService(
        IOptions<SeoMetadataOptions> options,
        IMemoryCache memoryCache,
        ILogger<OllamaSeoMetadataService> logger)
    {
        _options = options.Value;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsReady => _options.Enabled && !string.IsNullOrEmpty(_options.OllamaEndpoint);

    /// <inheritdoc />
    public async Task<GenerationResponse> GenerateMetadataAsync(GenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = SeoMetadataTelemetry.StartGenerateMetadataActivity(
            request.Content.ContentType,
            request.Content.Title);

        Interlocked.Increment(ref _totalRequests);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var cacheKey = request.Content.GetCacheKey();

            // Check cache if not forcing regeneration
            if (request.UseCache && !request.ForceRegenerate)
            {
                var cached = await GetCachedMetadataAsync(cacheKey, cancellationToken);
                if (cached != null)
                {
                    Interlocked.Increment(ref _cacheHits);
                    SeoMetadataTelemetry.RecordCacheHit(activity, cacheKey);
                    var cacheResponse = new GenerationResponse
                    {
                        Success = true,
                        Metadata = cached,
                        FromCache = true,
                        CacheKey = cacheKey,
                        GenerationTimeMs = stopwatch.ElapsedMilliseconds
                    };
                    SeoMetadataTelemetry.RecordResult(activity, cacheResponse);
                    return cacheResponse;
                }
            }

            SeoMetadataTelemetry.RecordCacheMiss(activity);

            var metadata = new SeoMetadata
            {
                GeneratedAt = DateTime.UtcNow,
                GeneratedByModel = _options.Model,
                Mode = GenerationMode.Runtime
            };

            // Generate requested metadata types in parallel where possible
            var tasks = new List<Task>();

            if (request.GenerateMetaDescription)
                tasks.Add(Task.Run(
                    async () =>
                    {
                        metadata.MetaDescription =
                            await GenerateMetaDescriptionAsync(request.Content, cancellationToken);
                    }, cancellationToken));

            if (request.GenerateOpenGraph)
                tasks.Add(Task.Run(
                    async () =>
                    {
                        metadata.OpenGraph = await GenerateOpenGraphAsync(request.Content, cancellationToken);
                    }, cancellationToken));

            if (request.GenerateJsonLd)
                tasks.Add(Task.Run(
                    async () => { metadata.JsonLd = await GenerateJsonLdAsync(request.Content, cancellationToken); },
                    cancellationToken));

            if (request.GenerateKeywords)
                tasks.Add(Task.Run(
                    async () =>
                    {
                        metadata.Keywords = await GenerateKeywordsAsync(request.Content, 10, cancellationToken);
                    }, cancellationToken));

            await Task.WhenAll(tasks);

            // Set canonical URL if provided
            if (!string.IsNullOrEmpty(request.Content.Url)) metadata.CanonicalUrl = request.Content.Url;

            // Cache the result
            if (request.UseCache) await CacheMetadataAsync(cacheKey, metadata, cancellationToken);

            stopwatch.Stop();
            Interlocked.Increment(ref _successfulGenerations);
            Interlocked.Add(ref _totalGenerationTimeMs, stopwatch.ElapsedMilliseconds);
            _lastSuccessfulGeneration = DateTime.UtcNow;
            _llmHealthy = true;

            var response = new GenerationResponse
            {
                Success = true,
                Metadata = metadata,
                FromCache = false,
                CacheKey = cacheKey,
                GenerationTimeMs = stopwatch.ElapsedMilliseconds
            };
            SeoMetadataTelemetry.RecordResult(activity, response);
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Interlocked.Increment(ref _failedGenerations);
            _logger.LogError(ex, "Failed to generate SEO metadata");
            SeoMetadataTelemetry.RecordException(activity, ex);

            return new GenerationResponse
            {
                Success = false,
                Error = ex.Message,
                GenerationTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <inheritdoc />
    public async Task<string?> GenerateMetaDescriptionAsync(ContentInput content,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildMetaDescriptionPrompt(content);
        var response = await CallLlmAsync(prompt, cancellationToken);

        if (string.IsNullOrEmpty(response))
            return null;

        // Clean and truncate the response
        var description = CleanResponse(response);
        if (description.Length > _options.MaxMetaDescriptionLength)
            description = description[..(_options.MaxMetaDescriptionLength - 3)] + "...";

        return description;
    }

    /// <inheritdoc />
    public async Task<OpenGraphMetadata?> GenerateOpenGraphAsync(ContentInput content,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildOpenGraphPrompt(content);
        var response = await CallLlmAsync(prompt, cancellationToken);

        if (string.IsNullOrEmpty(response))
            return null;

        try
        {
            // Try to parse JSON response
            var jsonMatch = JsonRegex().Match(response);
            if (jsonMatch.Success)
            {
                var json = JsonSerializer.Deserialize<OpenGraphLlmResponse>(jsonMatch.Value);
                if (json != null)
                {
                    var og = new OpenGraphMetadata
                    {
                        Title = json.Title ?? content.Title,
                        Description = TruncateText(json.Description, _options.MaxOgDescriptionLength),
                        Type = MapContentTypeToOgType(content.ContentType),
                        Url = content.Url,
                        Image = content.ImageUrl ?? _options.DefaultOgImage,
                        ImageAlt = content.ImageAlt,
                        SiteName = _options.SiteName,
                        Locale = content.Language == "en" ? "en_US" : content.Language,
                        PublishedTime = content.PublishedDate,
                        ModifiedTime = content.ModifiedDate,
                        Author = content.Author,
                        Section = content.Category,
                        Tags = content.Tags,
                        TwitterCard = _options.TwitterCardType,
                        TwitterSite = _options.TwitterSite
                    };
                    return og;
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse OpenGraph JSON response, using fallback");
        }

        // Fallback: use content directly
        return new OpenGraphMetadata
        {
            Title = content.Title,
            Description = TruncateText(CleanResponse(response), _options.MaxOgDescriptionLength),
            Type = MapContentTypeToOgType(content.ContentType),
            Url = content.Url,
            Image = content.ImageUrl ?? _options.DefaultOgImage,
            SiteName = _options.SiteName,
            TwitterCard = _options.TwitterCardType,
            TwitterSite = _options.TwitterSite
        };
    }

    /// <inheritdoc />
    public async Task<JsonLdMetadata?> GenerateJsonLdAsync(ContentInput content,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildJsonLdPrompt(content);
        var response = await CallLlmAsync(prompt, cancellationToken);

        var jsonLd = new JsonLdMetadata
        {
            Type = MapContentTypeToSchemaType(content.ContentType),
            Headline = content.Title,
            InLanguage = content.Language,
            DatePublished = content.PublishedDate?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            DateModified = (content.ModifiedDate ?? content.PublishedDate)?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Image = content.ImageUrl,
            MainEntityOfPage = !string.IsNullOrEmpty(content.Url) ? new JsonLdMainEntity { Id = content.Url } : null,
            ArticleSection = content.Category,
            Keywords = content.Tags != null ? string.Join(", ", content.Tags) : null,
            WordCount = CountWords(content.Content)
        };

        // Parse LLM response for description
        if (!string.IsNullOrEmpty(response))
            try
            {
                var jsonMatch = JsonRegex().Match(response);
                if (jsonMatch.Success)
                {
                    var parsed = JsonSerializer.Deserialize<JsonLdLlmResponse>(jsonMatch.Value);
                    if (parsed != null) jsonLd.Description = parsed.Description;
                }
                else
                {
                    jsonLd.Description = CleanResponse(response);
                }
            }
            catch
            {
                jsonLd.Description = CleanResponse(response);
            }

        // Add author if provided
        if (!string.IsNullOrEmpty(content.Author))
            jsonLd.Author = new JsonLdAuthor
            {
                Name = content.Author,
                Url = content.AuthorUrl
            };

        // Handle product-specific properties
        if (content.ContentType == SeoContentType.Product)
        {
            jsonLd.Name = content.Title;
            if (content.Price.HasValue && !string.IsNullOrEmpty(content.Currency))
                jsonLd.Offers = new JsonLdOffer
                {
                    Price = content.Price.Value.ToString("F2"),
                    PriceCurrency = content.Currency,
                    Availability = MapAvailability(content.Availability)
                };
            if (!string.IsNullOrEmpty(content.Brand)) jsonLd.Brand = new JsonLdBrand { Name = content.Brand };
            if (!string.IsNullOrEmpty(content.Sku)) jsonLd.Sku = content.Sku;
            if (content.Rating.HasValue && content.ReviewCount.HasValue)
                jsonLd.AggregateRating = new JsonLdAggregateRating
                {
                    RatingValue = content.Rating.Value.ToString("F1"),
                    ReviewCount = content.ReviewCount.Value.ToString(),
                    BestRating = "5",
                    WorstRating = "1"
                };
        }

        return jsonLd;
    }

    /// <inheritdoc />
    public async Task<List<string>> GenerateKeywordsAsync(ContentInput content, int maxKeywords = 10,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildKeywordsPrompt(content, maxKeywords);
        var response = await CallLlmAsync(prompt, cancellationToken);

        if (string.IsNullOrEmpty(response))
            return content.Tags ?? [];

        // Parse keywords from response
        var keywords = new List<string>();

        // Try JSON array first
        try
        {
            var jsonMatch = JsonArrayRegex().Match(response);
            if (jsonMatch.Success)
            {
                var parsed = JsonSerializer.Deserialize<List<string>>(jsonMatch.Value);
                if (parsed != null) keywords.AddRange(parsed);
            }
        }
        catch
        {
            /* Fallback below */
        }

        // Fallback: parse comma-separated or line-separated
        if (keywords.Count == 0)
        {
            var cleaned = CleanResponse(response);
            var split = cleaned.Split([',', '\n', ';'], StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim().Trim('"', '\'', '-', '*', '•'))
                .Where(k => !string.IsNullOrWhiteSpace(k) && k.Length > 2)
                .Take(maxKeywords);
            keywords.AddRange(split);
        }

        return keywords.Take(maxKeywords).ToList();
    }

    /// <inheritdoc />
    public Task<SeoMetadata?> GetCachedMetadataAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        var key = $"seo_metadata_{cacheKey}";
        if (_memoryCache.TryGetValue<SeoMetadata>(key, out var cached)) return Task.FromResult(cached);
        return Task.FromResult<SeoMetadata?>(null);
    }

    /// <inheritdoc />
    public Task CacheMetadataAsync(string cacheKey, SeoMetadata metadata, CancellationToken cancellationToken = default)
    {
        var key = $"seo_metadata_{cacheKey}";
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _options.CacheDuration
        };
        _memoryCache.Set(key, metadata, options);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearCacheAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        var key = $"seo_metadata_{cacheKey}";
        _memoryCache.Remove(key);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public SeoMetadataStatistics GetStatistics()
    {
        var totalGen = Interlocked.Read(ref _successfulGenerations);
        var totalTimeMs = Interlocked.Read(ref _totalGenerationTimeMs);

        return new SeoMetadataStatistics
        {
            TotalRequests = Interlocked.Read(ref _totalRequests),
            SuccessfulGenerations = totalGen,
            FailedGenerations = Interlocked.Read(ref _failedGenerations),
            CacheHits = Interlocked.Read(ref _cacheHits),
            AverageGenerationTimeMs = totalGen > 0 ? (double)totalTimeMs / totalGen : 0,
            Model = _options.Model,
            LlmConnectionHealthy = _llmHealthy,
            LastSuccessfulGeneration = _lastSuccessfulGeneration
        };
    }

    #region Private Methods

    private async Task<string?> CallLlmAsync(string prompt, CancellationToken cancellationToken)
    {
        using var activity = SeoMetadataTelemetry.StartLlmCallActivity(_options.Model);

        if (!IsReady)
        {
            _logger.LogWarning("SEO metadata service not ready - Ollama endpoint not configured");
            return null;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            var ollama = new OllamaApiClient(_options.OllamaEndpoint)
            {
                SelectedModel = _options.Model
            };

            if (_options.EnableDiagnosticLogging) _logger.LogDebug("Sending prompt to Ollama:\n{Prompt}", prompt);

            var chat = new Chat(ollama);
            var responseBuilder = new StringBuilder();

            await foreach (var token in chat.SendAsync(prompt, cts.Token)) responseBuilder.Append(token);

            var response = responseBuilder.ToString();

            if (_options.EnableDiagnosticLogging)
                _logger.LogDebug("Received response from Ollama:\n{Response}", response);

            activity?.SetTag("mostlylucid.seometadata.llm_response_length", response.Length);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return response;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("LLM request timed out after {Timeout}s", _options.TimeoutSeconds);
            _llmHealthy = false;
            SeoMetadataTelemetry.RecordTimeout(activity, _options.TimeoutSeconds);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Ollama API");
            _llmHealthy = false;
            SeoMetadataTelemetry.RecordException(activity, ex);
            return null;
        }
    }

    private string BuildMetaDescriptionPrompt(ContentInput content)
    {
        if (!string.IsNullOrEmpty(_options.MetaDescriptionPromptTemplate))
            return _options.MetaDescriptionPromptTemplate
                .Replace("{title}", content.Title)
                .Replace("{content}", TruncateForPrompt(content.Content))
                .Replace("{maxLength}", _options.MaxMetaDescriptionLength.ToString())
                .Replace("{language}", content.Language);

        return $"""
                Generate a compelling SEO meta description for a web page.

                Title: {content.Title}
                Content Type: {content.ContentType}
                Language: {content.Language}

                Content Summary:
                {TruncateForPrompt(content.Content)}

                Requirements:
                - Maximum {_options.MaxMetaDescriptionLength} characters
                - Include relevant keywords naturally
                - Be compelling and encourage clicks
                - Accurately summarize the content
                - Use active voice
                - Do not use quotes or special characters

                Respond with ONLY the meta description text, nothing else.
                """;
    }

    private string BuildOpenGraphPrompt(ContentInput content)
    {
        if (!string.IsNullOrEmpty(_options.OpenGraphPromptTemplate))
            return _options.OpenGraphPromptTemplate
                .Replace("{title}", content.Title)
                .Replace("{content}", TruncateForPrompt(content.Content))
                .Replace("{maxLength}", _options.MaxOgDescriptionLength.ToString())
                .Replace("{language}", content.Language)
                .Replace("{contentType}", content.ContentType.ToString());

        return $$"""
                 Generate OpenGraph metadata for social media sharing.

                 Title: {{content.Title}}
                 Content Type: {{content.ContentType}}
                 Language: {{content.Language}}

                 Content Summary:
                 {{TruncateForPrompt(content.Content)}}

                 Respond with a JSON object in this exact format:
                 {
                   "title": "engaging title for social sharing (max 70 chars)",
                   "description": "compelling description for social sharing (max {{_options.MaxOgDescriptionLength}} chars)"
                 }

                 Requirements:
                 - Title should be catchy and engaging
                 - Description should encourage sharing and clicks
                 - Use the same language as the content ({{content.Language}})
                 """;
    }

    private string BuildJsonLdPrompt(ContentInput content)
    {
        if (!string.IsNullOrEmpty(_options.JsonLdPromptTemplate))
            return _options.JsonLdPromptTemplate
                .Replace("{title}", content.Title)
                .Replace("{content}", TruncateForPrompt(content.Content))
                .Replace("{contentType}", content.ContentType.ToString())
                .Replace("{language}", content.Language);

        return $$"""
                 Generate a description for JSON-LD structured data (schema.org).

                 Title: {{content.Title}}
                 Content Type: {{content.ContentType}} (schema.org type: {{MapContentTypeToSchemaType(content.ContentType)}})
                 Language: {{content.Language}}

                 Content Summary:
                 {{TruncateForPrompt(content.Content)}}

                 Respond with a JSON object in this exact format:
                 {
                   "description": "informative description suitable for schema.org structured data (max 300 chars)"
                 }

                 Requirements:
                 - Description should be factual and informative
                 - Suitable for search engine rich snippets
                 - Use the same language as the content ({{content.Language}})
                 """;
    }

    private string BuildKeywordsPrompt(ContentInput content, int maxKeywords)
    {
        return $"""
                Extract the most relevant SEO keywords from this content.

                Title: {content.Title}
                Content Type: {content.ContentType}
                Language: {content.Language}
                {(content.Category != null ? $"Category: {content.Category}" : "")}

                Content:
                {TruncateForPrompt(content.Content)}

                Respond with a JSON array of up to {maxKeywords} keywords:
                ["keyword1", "keyword2", "keyword3", ...]

                Requirements:
                - Include both short-tail and long-tail keywords
                - Order by relevance (most relevant first)
                - Use lowercase
                - Include variations users might search for
                - Focus on search intent
                """;
    }

    private static string TruncateForPrompt(string text, int maxLength = 2000)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Strip HTML tags for cleaner text
        var cleaned = HtmlTagRegex().Replace(text, " ");
        cleaned = WhitespaceRegex().Replace(cleaned, " ").Trim();

        if (cleaned.Length <= maxLength)
            return cleaned;

        return cleaned[..(maxLength - 3)] + "...";
    }

    private static string? TruncateText(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        if (text.Length <= maxLength)
            return text;

        return text[..(maxLength - 3)] + "...";
    }

    private static string CleanResponse(string response)
    {
        // Remove markdown formatting, quotes, and extra whitespace
        var cleaned = response.Trim();
        cleaned = cleaned.Trim('"', '\'', '`');
        cleaned = WhitespaceRegex().Replace(cleaned, " ");
        return cleaned;
    }

    private static string MapContentTypeToOgType(SeoContentType contentType)
    {
        return contentType switch
        {
            SeoContentType.Article or SeoContentType.BlogPosting or SeoContentType.NewsArticle => "article",
            SeoContentType.Product => "product",
            SeoContentType.Organization => "website",
            SeoContentType.Person => "profile",
            SeoContentType.Event => "event",
            _ => "website"
        };
    }

    private static string MapContentTypeToSchemaType(SeoContentType contentType)
    {
        return contentType switch
        {
            SeoContentType.Article => "Article",
            SeoContentType.BlogPosting => "BlogPosting",
            SeoContentType.NewsArticle => "NewsArticle",
            SeoContentType.Product => "Product",
            SeoContentType.Service => "Service",
            SeoContentType.Organization => "Organization",
            SeoContentType.Person => "Person",
            SeoContentType.Event => "Event",
            SeoContentType.Recipe => "Recipe",
            SeoContentType.FAQPage => "FAQPage",
            SeoContentType.HowTo => "HowTo",
            SeoContentType.WebPage => "WebPage",
            _ => "Article"
        };
    }

    private static string MapAvailability(string? availability)
    {
        return availability?.ToLowerInvariant() switch
        {
            "instock" or "in stock" => "https://schema.org/InStock",
            "outofstock" or "out of stock" => "https://schema.org/OutOfStock",
            "preorder" or "pre-order" => "https://schema.org/PreOrder",
            "backorder" or "back order" => "https://schema.org/BackOrder",
            "discontinued" => "https://schema.org/Discontinued",
            _ => "https://schema.org/InStock"
        };
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var cleaned = HtmlTagRegex().Replace(text, " ");
        return cleaned.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
    }

    [GeneratedRegex(@"\{[^{}]*\}")]
    private static partial Regex JsonRegex();

    [GeneratedRegex(@"\[[^\[\]]*\]")]
    private static partial Regex JsonArrayRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    #endregion

    #region LLM Response Models

    private class OpenGraphLlmResponse
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
    }

    private class JsonLdLlmResponse
    {
        public string? Description { get; set; }
    }

    #endregion
}