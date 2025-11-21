using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmAltText.Data;
using Mostlylucid.LlmAltText.Models;
using Mostlylucid.LlmAltText.Services;

namespace Mostlylucid.LlmAltText.TagHelpers;

/// <summary>
///     TagHelper that automatically generates alt text for img tags that don't have one
/// </summary>
[HtmlTargetElement("img")]
public class AutoAltTextTagHelper : TagHelper
{
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IImageAnalysisService _imageAnalysisService;
    private readonly ILogger<AutoAltTextTagHelper> _logger;
    private readonly AltTextOptions _options;
    private readonly IAltTextRepository? _repository;

    public AutoAltTextTagHelper(
        IImageAnalysisService imageAnalysisService,
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory,
        ILogger<AutoAltTextTagHelper> logger,
        IOptions<AltTextOptions> options,
        IAltTextRepository? repository = null)
    {
        _imageAnalysisService = imageAnalysisService;
        _repository = repository;
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    ///     Set to true to skip auto alt text generation for this image
    /// </summary>
    [HtmlAttributeName("data-skip-alt")]
    public bool SkipAlt { get; set; } = false;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        // Skip if TagHelper is disabled
        if (!_options.EnableTagHelper) return;

        // Skip if explicitly marked to skip
        if (SkipAlt) return;

        // Skip if alt text already provided (check if attribute exists and has a value)
        var existingAlt = context.AllAttributes["alt"]?.Value?.ToString();
        if (existingAlt != null) // Even empty string means alt was explicitly set
            return;

        // Get the src attribute
        var src = output.Attributes["src"]?.Value?.ToString();
        if (string.IsNullOrWhiteSpace(src)) return;

        // Skip data URIs and blob URLs
        if (_options.SkipSrcPrefixes.Any(prefix => src.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) return;

        // Check domain restrictions
        if (_options.AllowedImageDomains.Count > 0 && Uri.TryCreate(src, UriKind.Absolute, out var uri))
            if (!_options.AllowedImageDomains.Any(d => uri.Host.EndsWith(d, StringComparison.OrdinalIgnoreCase)))
                return;

        try
        {
            var altText = await GetOrGenerateAltTextAsync(src);

            if (!string.IsNullOrWhiteSpace(altText))
            {
                output.Attributes.SetAttribute("alt", altText);
                _logger.LogDebug("Auto-generated alt text for {Src}: {AltText}", src,
                    altText.Substring(0, Math.Min(50, altText.Length)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate alt text for {Src}", src);
            // Don't fail the page render - just leave alt empty
        }
    }

    private async Task<string?> GetOrGenerateAltTextAsync(string src)
    {
        var cacheKey = $"alttext_{AltTextRepository.ComputeHash(src)}";

        // Try memory cache first
        if (_cache.TryGetValue(cacheKey, out string? cachedAltText)) return cachedAltText;

        // Try database cache
        if (_repository != null && _options.EnableDatabase)
        {
            var dbEntry = await _repository.GetBySourceAsync(src);
            if (dbEntry != null)
            {
                // Update usage count
                await _repository.IncrementUsageAsync(dbEntry.Id);

                // Cache in memory
                _cache.Set(cacheKey, dbEntry.AltText, TimeSpan.FromMinutes(_options.CacheDurationMinutes));
                return dbEntry.AltText;
            }
        }

        // Generate new alt text
        var imageStream = await FetchImageAsync(src);
        if (imageStream == null) return null;

        using (imageStream)
        {
            var result = await _imageAnalysisService.AnalyzeWithClassificationAsync(imageStream);

            // Save to database
            if (_repository != null && _options.EnableDatabase)
            {
                var entry = new ImageAltTextEntry
                {
                    SourceHash = AltTextRepository.ComputeHash(src),
                    ImageSource = src,
                    AltText = result.AltText,
                    ExtractedText = result.ExtractedText,
                    ContentType = result.ContentType.ToString(),
                    ContentTypeConfidence = result.ContentTypeConfidence
                };
                await _repository.SaveAsync(entry);
            }

            // Cache in memory
            _cache.Set(cacheKey, result.AltText, TimeSpan.FromMinutes(_options.CacheDurationMinutes));

            return result.AltText;
        }
    }

    private async Task<Stream?> FetchImageAsync(string src)
    {
        try
        {
            // Handle relative paths
            if (!Uri.TryCreate(src, UriKind.Absolute, out var uri))
            {
                // For relative paths, we can't fetch the image
                // This would need to be handled differently (file system, etc.)
                _logger.LogWarning("Cannot fetch relative image path: {Src}", src);
                return null;
            }

            var client = _httpClientFactory.CreateClient("AltTextImageFetcher");
            client.Timeout = TimeSpan.FromSeconds(30);

            var response = await client.GetAsync(uri);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch image {Src}: {StatusCode}", src, response.StatusCode);
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType == null || !contentType.StartsWith("image/"))
            {
                _logger.LogWarning("URL {Src} is not an image: {ContentType}", src, contentType);
                return null;
            }

            return await response.Content.ReadAsStreamAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching image {Src}", src);
            return null;
        }
    }
}