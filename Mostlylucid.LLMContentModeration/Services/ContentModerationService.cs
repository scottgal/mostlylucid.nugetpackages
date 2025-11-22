using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LLMContentModeration.Models;
using Mostlylucid.LLMContentModeration.Telemetry;

namespace Mostlylucid.LLMContentModeration.Services;

/// <summary>
/// Main content moderation service that orchestrates LLM classification and PII detection
/// </summary>
public class ContentModerationService : IContentModerationService
{
    private readonly ILogger<ContentModerationService> _logger;
    private readonly IModerationOllamaClient _ollamaClient;
    private readonly IPiiDetector _piiDetector;
    private readonly ModerationOptions _options;

    public ContentModerationService(
        ILogger<ContentModerationService> logger,
        IModerationOllamaClient ollamaClient,
        IPiiDetector piiDetector,
        IOptions<ModerationOptions> options)
    {
        _logger = logger;
        _ollamaClient = ollamaClient;
        _piiDetector = piiDetector;
        _options = options.Value;
    }

    public async Task<ModerationResult> ModerateAsync(
        string content,
        ModerationMode? mode = null,
        CancellationToken cancellationToken = default)
    {
        return await ModerateInternalAsync(content, _options, mode ?? _options.DefaultMode, cancellationToken);
    }

    public async Task<ModerationResult> ModerateAsync(
        string content,
        ModerationOptions customOptions,
        CancellationToken cancellationToken = default)
    {
        return await ModerateInternalAsync(content, customOptions, customOptions.DefaultMode, cancellationToken);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        return await _ollamaClient.IsAvailableAsync(cancellationToken);
    }

    public async Task<ModerationServiceStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var isAvailable = await _ollamaClient.IsAvailableAsync(cancellationToken);
        List<string>? models = null;

        if (isAvailable)
        {
            models = await _ollamaClient.GetModelsAsync(cancellationToken);
        }

        return new ModerationServiceStatus
        {
            IsAvailable = isAvailable,
            OllamaEndpoint = _options.Ollama.Endpoint,
            OllamaModel = _options.Ollama.Model,
            AvailableModels = models,
            ContentClassificationEnabled = _options.ContentClassification.EnableToxicity ||
                                           _options.ContentClassification.EnableSpam ||
                                           _options.ContentClassification.EnableSelfHarm ||
                                           _options.ContentClassification.EnableNsfw,
            PiiDetectionEnabled = _options.PiiDetection.Enabled,
            DefaultMode = _options.DefaultMode
        };
    }

    private async Task<ModerationResult> ModerateInternalAsync(
        string content,
        ModerationOptions options,
        ModerationMode mode,
        CancellationToken cancellationToken)
    {
        using var activity = ContentModerationTelemetry.StartModerationActivity(content.Length, mode);

        var stopwatch = Stopwatch.StartNew();
        var result = new ModerationResult
        {
            OriginalContent = content,
            Mode = mode
        };

        if (string.IsNullOrWhiteSpace(content))
        {
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            ContentModerationTelemetry.RecordResult(activity, result);
            return result;
        }

        // Truncate if too long
        var contentToAnalyze = content.Length > options.MaxContentLength
            ? content[..options.MaxContentLength]
            : content;

        try
        {
            // Run PII detection and content classification in parallel
            var piiTask = DetectPiiAsync(contentToAnalyze, options, cancellationToken);
            var classificationTask = ClassifyContentAsync(contentToAnalyze, options, cancellationToken);

            await Task.WhenAll(piiTask, classificationTask);

            result.PiiMatches = await piiTask;
            result.Flags = await classificationTask;

            // Determine if content should be blocked
            result.IsBlocked = DetermineIfBlocked(result, mode);

            // Apply masking if needed
            if (mode == ModerationMode.MaskAndAllow && result.PiiMatches.Count > 0)
            {
                result.ModeratedContent = _piiDetector.MaskPii(content, result.PiiMatches, options.PiiDetection);
            }

            // Invoke callbacks
            await InvokeCallbacksAsync(result, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during content moderation");
            result.Errors.Add($"Moderation error: {ex.Message}");
            ContentModerationTelemetry.RecordException(activity, ex);
        }

        result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

        _logger.LogInformation(
            "Content moderation completed in {Ms}ms. Flagged: {Flagged}, Blocked: {Blocked}, Mode: {Mode}",
            result.ProcessingTimeMs, result.IsFlagged, result.IsBlocked, mode);

        ContentModerationTelemetry.RecordResult(activity, result);
        return result;
    }

    private async Task<List<PiiMatch>> DetectPiiAsync(
        string content,
        ModerationOptions options,
        CancellationToken cancellationToken)
    {
        if (!options.PiiDetection.Enabled)
            return [];

        using var activity = ContentModerationTelemetry.StartPiiDetectionActivity(
            content.Length, options.PiiDetection.UseLlmEnhancement);

        try
        {
            // First pass: regex-based detection
            var regexMatches = _piiDetector.DetectPii(content, options.PiiDetection);

            // Second pass: LLM enhancement (if enabled)
            if (options.PiiDetection.UseLlmEnhancement)
            {
                try
                {
                    regexMatches = await _ollamaClient.EnhancePiiDetectionAsync(
                        content, regexMatches, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "LLM PII enhancement failed, using regex results only");
                }
            }

            ContentModerationTelemetry.RecordPiiResult(activity, regexMatches);
            return regexMatches;
        }
        catch (Exception ex)
        {
            ContentModerationTelemetry.RecordException(activity, ex);
            throw;
        }
    }

    private async Task<List<ContentFlag>> ClassifyContentAsync(
        string content,
        ModerationOptions options,
        CancellationToken cancellationToken)
    {
        var classificationOptions = options.ContentClassification;

        // Skip if no classification types are enabled
        if (!classificationOptions.EnableToxicity &&
            !classificationOptions.EnableSpam &&
            !classificationOptions.EnableSelfHarm &&
            !classificationOptions.EnableNsfw)
        {
            return [];
        }

        using var activity = ContentModerationTelemetry.StartClassificationActivity(content.Length);

        try
        {
            var flags = await _ollamaClient.ClassifyContentAsync(content, classificationOptions, cancellationToken);
            ContentModerationTelemetry.RecordClassificationResult(activity, flags);
            return flags;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Content classification failed");
            ContentModerationTelemetry.RecordException(activity, ex);
            return [];
        }
    }

    private static bool DetermineIfBlocked(ModerationResult result, ModerationMode mode)
    {
        if (mode == ModerationMode.DetectOnly)
            return false;

        if (mode == ModerationMode.MaskAndAllow)
        {
            // Only block if there are content flags (PII will be masked)
            return result.Flags.Count > 0;
        }

        // Block mode: block if anything is flagged
        return result.IsFlagged;
    }

    private async Task InvokeCallbacksAsync(ModerationResult result, ModerationOptions options)
    {
        try
        {
            if (result.IsBlocked && options.OnContentBlocked != null)
            {
                await options.OnContentBlocked(result);
            }
            else if (result.IsFlagged && options.OnContentFlagged != null)
            {
                await options.OnContentFlagged(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking moderation callback");
        }
    }
}
