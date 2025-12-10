using System.Collections.Immutable;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Mostlylucid.BotDetection.Models;

namespace Mostlylucid.BotDetection.Orchestration.ContributingDetectors;

/// <summary>
///     HTTP/2 fingerprinting contributor using AKAMAI-style fingerprinting.
///     Analyzes HTTP/2 frame sequences, settings, and priority to detect automation.
///
///     Best-in-breed approach:
///     - HTTP/2 SETTINGS frame analysis
///     - WINDOW_UPDATE patterns
///     - Stream priority patterns
///     - Header compression (HPACK) usage patterns
///     - Pseudoheader order fingerprinting
///
///     Raises signals for behavioral waveform:
///     - h2.settings_fingerprint
///     - h2.priority_pattern
///     - h2.window_update_behavior
///     - h2.pseudoheader_order
///     - h2.stream_behavior
/// </summary>
public class Http2FingerprintContributor : ContributingDetectorBase
{
    private readonly ILogger<Http2FingerprintContributor> _logger;

    // Known HTTP/2 fingerprints for different clients
    // Format: settings_frame|priority_pattern|window_behavior
    private static readonly Dictionary<string, string> KnownFingerprints = new()
    {
        // Chrome fingerprints
        { "1:65536,2:0,3:1000,4:6291456,6:262144", "Chrome" },
        { "1:65536,2:0,3:100,4:6291456,6:262144", "Chrome_Mobile" },

        // Firefox fingerprints
        { "1:65536,2:0,3:100,4:131072,5:16384", "Firefox" },

        // Safari fingerprints
        { "1:32768,2:0,3:100,4:2097152", "Safari" },

        // Bot fingerprints - often minimal or non-standard settings
        { "3:100,4:65536", "Go_HTTP2_Client" },
        { "3:100,4:6291456", "Python_httpx" },
        { "1:4096,3:100,4:65536", "Node_HTTP2_Bot" }
    };

    public Http2FingerprintContributor(ILogger<Http2FingerprintContributor> logger)
    {
        _logger = logger;
    }

    public override string Name => "Http2Fingerprint";
    public override int Priority => 10; // Run early

    public override IReadOnlyList<TriggerCondition> TriggerConditions => Array.Empty<TriggerCondition>();

    public override Task<IReadOnlyList<DetectionContribution>> ContributeAsync(
        BlackboardState state,
        CancellationToken cancellationToken = default)
    {
        var contributions = new List<DetectionContribution>();
        var signals = ImmutableDictionary.CreateBuilder<string, object>();

        try
        {
            var protocol = state.HttpContext.Request.Protocol;
            signals.Add("h2.protocol", protocol);

            // Check if HTTP/2 is being used
            var isHttp2 = protocol.Equals("HTTP/2", StringComparison.OrdinalIgnoreCase) ||
                          protocol.Equals("HTTP/2.0", StringComparison.OrdinalIgnoreCase);

            signals.Add("h2.is_http2", isHttp2);

            if (!isHttp2)
            {
                // HTTP/1.x usage could be legitimate or suspicious depending on context
                // Modern browsers support HTTP/2, but some automation tools don't
                if (protocol.StartsWith("HTTP/1"))
                {
                    contributions.Add(new DetectionContribution
                    {
                        DetectorName = Name,
                        Category = "HTTP/2",
                        ConfidenceDelta = 0.1,
                        Weight = 0.5,
                        Reason = $"Using {protocol} instead of HTTP/2 (common for bots)",
                        Signals = signals.ToImmutable()
                    });
                }

                return Task.FromResult<IReadOnlyList<DetectionContribution>>(contributions);
            }

            // HTTP/2 SETTINGS fingerprinting (requires reverse proxy to capture and forward)
            if (state.HttpContext.Request.Headers.TryGetValue("X-HTTP2-Settings", out var settingsHeader))
            {
                var settings = settingsHeader.ToString();
                signals.Add("h2.settings_fingerprint", settings);

                // Match against known fingerprints
                var matchedClient = MatchFingerprint(settings);
                if (matchedClient != null)
                {
                    signals.Add("h2.client_type", matchedClient);

                    if (matchedClient.Contains("Bot") || matchedClient.Contains("HTTP2_Client"))
                    {
                        contributions.Add(DetectionContribution.Bot(
                            Name, "HTTP/2", 0.7,
                            $"HTTP/2 fingerprint matches known automation client: {matchedClient}",
                            BotType.ToolAutomation,
                            weight: 1.6));
                    }
                    else
                    {
                        // Known browser
                        contributions.Add(new DetectionContribution
                        {
                            DetectorName = Name,
                            Category = "HTTP/2",
                            ConfidenceDelta = -0.2,
                            Weight = 1.4,
                            Reason = $"HTTP/2 fingerprint matches browser: {matchedClient}",
                            Signals = signals.ToImmutable()
                        });
                    }
                }
                else
                {
                    // Unknown fingerprint
                    signals.Add("h2.fingerprint_unknown", true);
                }
            }

            // Analyze pseudoheader order (:method, :path, :scheme, :authority)
            // Browsers have consistent ordering, bots may vary
            var pseudoHeaderOrder = ExtractPseudoHeaderOrder(state.HttpContext);
            if (!string.IsNullOrEmpty(pseudoHeaderOrder))
            {
                signals.Add("h2.pseudoheader_order", pseudoHeaderOrder);

                // Standard browser order: method,path,authority,scheme or method,path,scheme,authority
                if (pseudoHeaderOrder != "method,path,authority,scheme" &&
                    pseudoHeaderOrder != "method,path,scheme,authority" &&
                    pseudoHeaderOrder != "method,scheme,authority,path")
                {
                    contributions.Add(new DetectionContribution
                    {
                        DetectorName = Name,
                        Category = "HTTP/2",
                        ConfidenceDelta = 0.3,
                        Weight = 1.2,
                        Reason = $"Non-standard HTTP/2 pseudoheader order: {pseudoHeaderOrder}",
                        Signals = signals.ToImmutable()
                    });
                }
            }

            // Check for HTTP/2 stream priority usage
            if (state.HttpContext.Request.Headers.TryGetValue("X-HTTP2-Stream-Priority", out var priority))
            {
                signals.Add("h2.stream_priority", priority.ToString());
                signals.Add("h2.uses_priority", true);
            }
            else
            {
                signals.Add("h2.uses_priority", false);
                // Lack of priority is slightly suspicious - browsers use it
                contributions.Add(new DetectionContribution
                {
                    DetectorName = Name,
                    Category = "HTTP/2",
                    ConfidenceDelta = 0.1,
                    Weight = 0.6,
                    Reason = "No HTTP/2 stream priority (browsers typically use this)",
                    Signals = signals.ToImmutable()
                });
            }

            // Check for WINDOW_UPDATE behavior patterns
            if (state.HttpContext.Request.Headers.TryGetValue("X-HTTP2-Window-Updates", out var windowUpdates))
            {
                var updates = windowUpdates.ToString();
                signals.Add("h2.window_update_pattern", updates);

                // Analyze window update frequency and sizes
                if (int.TryParse(updates, out var updateCount))
                {
                    signals.Add("h2.window_update_count", updateCount);

                    if (updateCount == 0)
                    {
                        // No window updates is unusual for browsers
                        contributions.Add(new DetectionContribution
                        {
                            DetectorName = Name,
                            Category = "HTTP/2",
                            ConfidenceDelta = 0.15,
                            Weight = 0.8,
                            Reason = "No HTTP/2 WINDOW_UPDATE frames (unusual for browsers)",
                            Signals = signals.ToImmutable()
                        });
                    }
                }
            }

            // Check for HTTP/2 Push support/usage
            if (state.HttpContext.Request.Headers.TryGetValue("X-HTTP2-Push-Enabled", out var pushEnabled))
            {
                var supportsPush = pushEnabled == "1";
                signals.Add("h2.push_enabled", supportsPush);

                if (!supportsPush)
                {
                    // Many bots disable push
                    contributions.Add(new DetectionContribution
                    {
                        DetectorName = Name,
                        Category = "HTTP/2",
                        ConfidenceDelta = 0.12,
                        Weight = 0.7,
                        Reason = "HTTP/2 Server Push disabled (common for bots)",
                        Signals = signals.ToImmutable()
                    });
                }
            }

            // Analyze connection preface
            if (state.HttpContext.Request.Headers.TryGetValue("X-HTTP2-Preface-Valid", out var prefaceValid))
            {
                var valid = prefaceValid == "1";
                signals.Add("h2.preface_valid", valid);

                if (!valid)
                {
                    // Invalid preface = definitely suspicious
                    contributions.Add(DetectionContribution.Bot(
                        Name, "HTTP/2", 0.8,
                        "Invalid HTTP/2 connection preface",
                        BotType.ToolAutomation,
                        weight: 1.8));
                }
            }

        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error analyzing HTTP/2 fingerprint");
            signals.Add("h2.analysis_error", ex.Message);
        }

        // If no contributions yet, add neutral with signals
        if (contributions.Count == 0)
        {
            contributions.Add(new DetectionContribution
            {
                DetectorName = Name,
                Category = "HTTP/2",
                ConfidenceDelta = 0.0,
                Weight = 1.0,
                Reason = "HTTP/2 analysis complete (no anomalies detected)",
                Signals = signals.ToImmutable()
            });
        }
        else
        {
            // Ensure last contribution has all signals
            if (contributions.Count > 0)
            {
                var last = contributions[^1];
                contributions[^1] = last with { Signals = signals.ToImmutable() };
            }
        }

        return Task.FromResult<IReadOnlyList<DetectionContribution>>(contributions);
    }

    private string? MatchFingerprint(string settings)
    {
        foreach (var (fingerprint, client) in KnownFingerprints)
        {
            if (settings.Contains(fingerprint, StringComparison.OrdinalIgnoreCase))
            {
                return client;
            }
        }
        return null;
    }

    private string ExtractPseudoHeaderOrder(HttpContext context)
    {
        // In HTTP/2, pseudoheaders start with ":"
        // Note: ASP.NET Core doesn't expose raw HTTP/2 frames directly
        // This would need to be captured by reverse proxy and passed via header

        if (context.Request.Headers.TryGetValue("X-HTTP2-Pseudoheader-Order", out var order))
        {
            return order.ToString();
        }

        // Fallback: infer from standard headers presence
        var parts = new List<string>();
        if (context.Request.Method != null) parts.Add("method");
        if (context.Request.Path.HasValue) parts.Add("path");
        if (context.Request.Scheme != null) parts.Add("scheme");
        if (context.Request.Host.HasValue) parts.Add("authority");

        return string.Join(",", parts);
    }
}
