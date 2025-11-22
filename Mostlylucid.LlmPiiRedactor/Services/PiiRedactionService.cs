using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmPiiRedactor.Detectors;
using Mostlylucid.LlmPiiRedactor.Models;
using Mostlylucid.LlmPiiRedactor.Telemetry;

namespace Mostlylucid.LlmPiiRedactor.Services;

/// <summary>
/// Main service for PII detection and redaction.
/// </summary>
public class PiiRedactionService : IPiiRedactionService
{
    private readonly ILogger<PiiRedactionService> _logger;
    private readonly PiiRedactionOptions _options;
    private readonly IEnumerable<IPiiDetector> _detectors;
    private readonly Dictionary<RedactionStyle, IRedactionStrategy> _strategies;
    private readonly PiiRedactionStatistics _statistics = new();
    private readonly object _statsLock = new();

    public PiiRedactionService(
        ILogger<PiiRedactionService> logger,
        IOptions<PiiRedactionOptions> options,
        IEnumerable<IPiiDetector> detectors,
        IEnumerable<IRedactionStrategy> strategies)
    {
        _logger = logger;
        _options = options.Value;
        _detectors = detectors.OrderBy(d => d.Priority);
        _strategies = strategies.ToDictionary(s => s.Style);
    }

    /// <inheritdoc />
    public RedactionResult Redact(string text, CancellationToken cancellationToken = default)
    {
        using var activity = PiiRedactorTelemetry.StartRedactActivity(text?.Length ?? 0);

        try
        {
            if (!_options.Enabled || string.IsNullOrEmpty(text))
            {
                var emptyResult = RedactionResult.NoMatch(text ?? string.Empty);
                PiiRedactorTelemetry.RecordRedactionResult(activity, emptyResult);
                return emptyResult;
            }

            // Check max length
            var processText = _options.MaxTextLength > 0 && text.Length > _options.MaxTextLength
                ? text[.._options.MaxTextLength]
                : text;

            // Detect all PII
            var matches = DetectInternal(processText, cancellationToken).ToList();

            if (matches.Count == 0)
            {
                var noMatchResult = RedactionResult.NoMatch(text);
                PiiRedactorTelemetry.RecordRedactionResult(activity, noMatchResult);
                return noMatchResult;
            }

            // Apply redactions
            var redactedText = ApplyRedactions(processText, matches);

            // Update statistics
            if (_options.EnableStatistics)
            {
                UpdateStatistics(text.Length, matches);
            }

            _logger.LogDebug("Redacted {Count} PII instances from text", matches.Count);

            var result = new RedactionResult
            {
                OriginalText = text,
                RedactedText = redactedText,
                Matches = matches
            };

            PiiRedactorTelemetry.RecordRedactionResult(activity, result);
            return result;
        }
        catch (Exception ex)
        {
            PiiRedactorTelemetry.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public Task<RedactionResult> RedactAsync(string text, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Redact(text, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyList<PiiMatch> Detect(string text, CancellationToken cancellationToken = default)
    {
        using var activity = PiiRedactorTelemetry.StartDetectActivity(text?.Length ?? 0);

        try
        {
            if (!_options.Enabled || string.IsNullOrEmpty(text))
            {
                PiiRedactorTelemetry.RecordDetectionResult(activity, []);
                return [];
            }

            var matches = DetectInternal(text, cancellationToken).ToList();
            PiiRedactorTelemetry.RecordDetectionResult(activity, matches);
            return matches;
        }
        catch (Exception ex)
        {
            PiiRedactorTelemetry.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public bool ContainsPii(string text, CancellationToken cancellationToken = default)
    {
        using var activity = PiiRedactorTelemetry.StartContainsPiiActivity(text?.Length ?? 0);

        try
        {
            if (!_options.Enabled || string.IsNullOrEmpty(text))
            {
                PiiRedactorTelemetry.RecordContainsPiiResult(activity, false);
                return false;
            }

            // Short-circuit on first match
            foreach (var detector in _detectors)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!IsDetectorEnabled(detector))
                    continue;

                var matches = detector.Detect(text, cancellationToken);
                if (matches.Any(m => IsMatchValid(m)))
                {
                    PiiRedactorTelemetry.RecordContainsPiiResult(activity, true);
                    return true;
                }
            }

            PiiRedactorTelemetry.RecordContainsPiiResult(activity, false);
            return false;
        }
        catch (Exception ex)
        {
            PiiRedactorTelemetry.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public PiiRedactionStatistics GetStatistics()
    {
        lock (_statsLock)
        {
            return new PiiRedactionStatistics
            {
                TotalScans = _statistics.TotalScans,
                TotalRedactions = _statistics.TotalRedactions,
                TotalCharactersScanned = _statistics.TotalCharactersScanned,
                TotalCharactersRedacted = _statistics.TotalCharactersRedacted,
                RedactionsByType = new Dictionary<PiiType, long>(_statistics.RedactionsByType)
            };
        }
    }

    private IEnumerable<PiiMatch> DetectInternal(string text, CancellationToken cancellationToken)
    {
        var allMatches = new List<PiiMatch>();

        foreach (var detector in _detectors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsDetectorEnabled(detector))
                continue;

            try
            {
                var matches = detector.Detect(text, cancellationToken)
                    .Where(IsMatchValid)
                    .ToList();

                allMatches.AddRange(matches);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Detector {Detector} failed", detector.Name);
            }
        }

        // Remove overlapping matches (prefer higher priority/confidence)
        var finalMatches = RemoveOverlaps(allMatches);

        // Apply redaction values
        foreach (var match in finalMatches)
        {
            var strategy = GetStrategy(match.Type);
            var redactedValue = strategy.Redact(match.OriginalValue, match.Type, _options);

            // Create new match with redacted value (PiiMatch is immutable)
            yield return new PiiMatch
            {
                Type = match.Type,
                OriginalValue = match.OriginalValue,
                RedactedValue = redactedValue,
                StartIndex = match.StartIndex,
                Length = match.Length,
                Confidence = match.Confidence,
                DetectorName = match.DetectorName
            };
        }
    }

    private bool IsDetectorEnabled(IPiiDetector detector)
    {
        return detector.IsEnabled && _options.DetectionTypes.HasFlag(detector.PiiType);
    }

    private bool IsMatchValid(PiiMatch match)
    {
        // Check confidence threshold
        if (match.Confidence < _options.MinConfidenceThreshold)
            return false;

        // Check whitelist
        if (_options.Whitelist.Contains(match.OriginalValue))
            return false;

        // Check email domain whitelist for emails
        if (match.Type == PiiType.Email && match.OriginalValue.Contains('@'))
        {
            var domain = match.OriginalValue.Split('@').LastOrDefault();
            if (domain != null && _options.WhitelistedEmailDomains.Contains(domain))
                return false;
        }

        return true;
    }

    private IRedactionStrategy GetStrategy(PiiType piiType)
    {
        var style = _options.StyleOverrides.GetValueOrDefault(piiType, _options.DefaultStyle);
        return _strategies.GetValueOrDefault(style, _strategies[RedactionStyle.FullMask]);
    }

    private static List<PiiMatch> RemoveOverlaps(List<PiiMatch> matches)
    {
        if (matches.Count <= 1)
            return matches;

        // Sort by start index, then by confidence (descending)
        var sorted = matches
            .OrderBy(m => m.StartIndex)
            .ThenByDescending(m => m.Confidence)
            .ToList();

        var result = new List<PiiMatch>();
        var lastEnd = -1;

        foreach (var match in sorted)
        {
            // Skip if this match overlaps with a previous one
            if (match.StartIndex < lastEnd)
                continue;

            result.Add(match);
            lastEnd = match.StartIndex + match.Length;
        }

        return result;
    }

    private string ApplyRedactions(string text, List<PiiMatch> matches)
    {
        if (matches.Count == 0)
            return text;

        // Sort by start index descending to apply from end to start
        var sortedMatches = matches.OrderByDescending(m => m.StartIndex).ToList();

        var sb = new StringBuilder(text);

        foreach (var match in sortedMatches)
        {
            sb.Remove(match.StartIndex, match.Length);
            sb.Insert(match.StartIndex, match.RedactedValue);
        }

        return sb.ToString();
    }

    private void UpdateStatistics(int textLength, List<PiiMatch> matches)
    {
        lock (_statsLock)
        {
            _statistics.TotalScans++;
            _statistics.TotalRedactions += matches.Count;
            _statistics.TotalCharactersScanned += textLength;
            _statistics.TotalCharactersRedacted += matches.Sum(m => m.Length);

            foreach (var match in matches)
            {
                if (!_statistics.RedactionsByType.ContainsKey(match.Type))
                    _statistics.RedactionsByType[match.Type] = 0;

                _statistics.RedactionsByType[match.Type]++;
            }
        }
    }
}
