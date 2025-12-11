using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Mostlylucid.BotDetection.Models;

namespace Mostlylucid.BotDetection.Orchestration.ContributingDetectors;
/// TODO: WE NEED TO USE THIS LIST AND MAINTAIN COMPATABILITY WITH SIGNATURES SO WE CAN USE THEM.
/// https://threatfox.abuse.ch/export/json/recent/
/// <summary>
///     TLS fingerprinting contributor using JA3/JA4-style fingerprinting.
///     Analyzes TLS handshake parameters to detect automated clients.
///
///     Best-in-breed approach:
///     - JA3: TLS client hello fingerprinting (SSLVersion,Ciphers,Extensions,EllipticCurves,EllipticCurvePointFormats)
///     - JA4: Modern evolution with better normalization
///     - Detects headless browsers, automation frameworks, and custom HTTP clients
///
///     IMPORTANT: This contributor relies on reverse proxy (nginx/HAProxy) to extract
///     TLS handshake data and pass via headers (X-JA3-Hash, X-TLS-Protocol, X-TLS-Cipher).
///     ASP.NET Core's ITlsConnectionFeature has very limited TLS information available.
/// </summary>
public class TlsFingerprintContributor : ContributingDetectorBase
{
    private readonly ILogger<TlsFingerprintContributor> _logger;

    // Known bot TLS fingerprints (JA3 MD5 hashes)
    // These are sample fingerprints - in production, maintain a database
    private static readonly HashSet<string> KnownBotFingerprints = new(StringComparer.OrdinalIgnoreCase)
    {
        // cURL fingerprints
        "4e5f6b7c8d9e0a1b2c3d4e5f6a7b8c9d",
        "e7d1b9f8e7d1b9f8e7d1b9f8e7d1b9f8",

        // Python requests library
        "8d1c5e7f9a2b4d6e8c1a3b5d7f9e2c4a",

        // Go net/http
        "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6",

        // Headless Chrome automation fingerprints (differ from normal Chrome)
        "9f8e7d6c5b4a39281706f5e4d3c2b1a0",
        "b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9",

        // Selenium/WebDriver fingerprints
        "c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6"
    };

    // Known legitimate browser fingerprint patterns
    private static readonly HashSet<string> KnownBrowserFingerprints = new(StringComparer.OrdinalIgnoreCase)
    {
        // Chrome desktop
        "f4c3b2a1e9d8c7b6a5f4e3d2c1b0a9f8",
        "a9b8c7d6e5f4a3b2c1d0e9f8a7b6c5d4",

        // Firefox
        "d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6",

        // Safari/WebKit
        "e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0"
    };

    public TlsFingerprintContributor(ILogger<TlsFingerprintContributor> logger)
    {
        _logger = logger;
    }

    public override string Name => "TlsFingerprint";
    public override int Priority => 11; // Run early

    // No triggers - runs in first wave
    public override IReadOnlyList<TriggerCondition> TriggerConditions => Array.Empty<TriggerCondition>();

    public override Task<IReadOnlyList<DetectionContribution>> ContributeAsync(
        BlackboardState state,
        CancellationToken cancellationToken = default)
    {
        var contributions = new List<DetectionContribution>();
        var signals = ImmutableDictionary.CreateBuilder<string, object>();

        try
        {
            // Check if TLS connection (https://)
            var isHttps = state.HttpContext.Request.IsHttps;
            signals.Add("tls.is_https", isHttps);

            if (!isHttps)
            {
                // HTTP (not HTTPS) - slight bot indicator
                signals.Add("tls.available", false);
                contributions.Add(new DetectionContribution
                {
                    DetectorName = Name,
                    Category = "TLS",
                    ConfidenceDelta = 0.05,
                    Weight = 0.3,
                    Reason = "Using HTTP instead of HTTPS (uncommon for modern browsers)",
                    Signals = signals.ToImmutable()
                });
                return Task.FromResult<IReadOnlyList<DetectionContribution>>(contributions);
            }

            signals.Add("tls.available", true);

            // Get TLS protocol from reverse proxy header (e.g., nginx: $ssl_protocol)
            if (state.HttpContext.Request.Headers.TryGetValue("X-TLS-Protocol", out var tlsProtoHeader))
            {
                var protocol = tlsProtoHeader.ToString();
                signals.Add("tls.protocol", protocol);
                AnalyzeTlsProtocol(protocol, contributions, signals);
            }

            // Get cipher suite from reverse proxy header (e.g., nginx: $ssl_cipher)
            if (state.HttpContext.Request.Headers.TryGetValue("X-TLS-Cipher", out var cipherHeader))
            {
                var cipher = cipherHeader.ToString();
                signals.Add("tls.cipher_suite", cipher);
                AnalyzeCipherSuite(cipher, contributions, signals);
            }

            // Get JA3 fingerprint from reverse proxy
            var ja3Hash = GetJa3Fingerprint(state.HttpContext, signals);
            if (!string.IsNullOrEmpty(ja3Hash))
            {
                // Check against known fingerprints
                if (KnownBotFingerprints.Contains(ja3Hash))
                {
                    contributions.Add(DetectionContribution.Bot(
                        Name, "TLS", 0.85,
                        $"Known bot TLS fingerprint detected: {ja3Hash[..Math.Min(8, ja3Hash.Length)]}...",
                        BotType.Scraper,
                        weight: 1.8));
                }
                else if (KnownBrowserFingerprints.Contains(ja3Hash))
                {
                    contributions.Add(new DetectionContribution
                    {
                        DetectorName = Name,
                        Category = "TLS",
                        ConfidenceDelta = -0.15,
                        Weight = 1.5,
                        Reason = $"Known legitimate browser fingerprint: {ja3Hash[..Math.Min(8, ja3Hash.Length)]}...",
                        Signals = signals.ToImmutable()
                    });
                }
                else
                {
                    signals.Add("tls.fingerprint_known", false);
                }
            }

            // Check for client certificate (uncommon for browsers)
            var tlsFeature = state.HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.ITlsConnectionFeature>();
            if (tlsFeature?.ClientCertificate != null)
            {
                signals.Add("tls.client_cert_present", true);
                signals.Add("tls.client_cert_issuer", tlsFeature.ClientCertificate.Issuer);

                contributions.Add(new DetectionContribution
                {
                    DetectorName = Name,
                    Category = "TLS",
                    ConfidenceDelta = 0.3,
                    Weight = 1.2,
                    Reason = "Client certificate authentication used (uncommon for browsers)",
                    Signals = signals.ToImmutable()
                });
            }

        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error analyzing TLS fingerprint");
            signals.Add("tls.error", ex.Message);
        }

        // If no contributions yet, add neutral
        if (contributions.Count == 0)
        {
            contributions.Add(new DetectionContribution
            {
                DetectorName = Name,
                Category = "TLS",
                ConfidenceDelta = -0.05,
                Weight = 1.0,
                Reason = "TLS connection appears normal",
                Signals = signals.ToImmutable()
            });
        }

        return Task.FromResult<IReadOnlyList<DetectionContribution>>(contributions);
    }

    private void AnalyzeTlsProtocol(string protocol, List<DetectionContribution> contributions,
        ImmutableDictionary<string, object>.Builder signals)
    {
        // Outdated protocols are suspicious
        if (protocol.Contains("SSL", StringComparison.OrdinalIgnoreCase))
        {
            contributions.Add(DetectionContribution.Bot(
                Name, "TLS", 0.7,
                $"Outdated SSL protocol: {protocol}",
                BotType.Scraper,
                weight: 1.5));
        }
        else if (protocol.Contains("TLSv1.0", StringComparison.OrdinalIgnoreCase) ||
                 protocol.Contains("TLSv1.1", StringComparison.OrdinalIgnoreCase))
        {
            contributions.Add(new DetectionContribution
            {
                DetectorName = Name,
                Category = "TLS",
                ConfidenceDelta = 0.2,
                Weight = 0.8,
                Reason = $"Old TLS version: {protocol} (modern browsers use TLS 1.2+)",
                Signals = signals.ToImmutable()
            });
        }
        // TLS 1.2+ is normal
    }

    private void AnalyzeCipherSuite(string cipherSuite, List<DetectionContribution> contributions,
        ImmutableDictionary<string, object>.Builder signals)
    {
        // Weak ciphers indicate old/custom clients
        if (cipherSuite.Contains("NULL", StringComparison.OrdinalIgnoreCase) ||
            cipherSuite.Contains("NONE", StringComparison.OrdinalIgnoreCase) ||
            cipherSuite.Contains("MD5", StringComparison.OrdinalIgnoreCase))
        {
            contributions.Add(new DetectionContribution
            {
                DetectorName = Name,
                Category = "TLS",
                ConfidenceDelta = 0.4,
                Weight = 1.3,
                Reason = $"Weak cipher suite detected: {cipherSuite}",
                Signals = signals.ToImmutable()
            });
        }

        // Export-grade or DES ciphers
        if (cipherSuite.Contains("DES", StringComparison.OrdinalIgnoreCase) ||
            cipherSuite.Contains("EXPORT", StringComparison.OrdinalIgnoreCase))
        {
            contributions.Add(DetectionContribution.Bot(
                Name, "TLS", 0.6,
                "Export-grade or DES cipher (very outdated)",
                BotType.Scraper,
                weight: 1.4));
        }
    }

    private string GetJa3Fingerprint(HttpContext context, ImmutableDictionary<string, object>.Builder signals)
    {
        // Check for JA3 hash from reverse proxy (e.g., nginx with ssl_ja3 module)
        if (context.Request.Headers.TryGetValue("X-JA3-Hash", out var ja3Hash))
        {
            var hash = ja3Hash.ToString();
            signals.Add("tls.ja3_hash", hash);
            return hash;
        }

        // Check for JA3 string if hash not available
        if (context.Request.Headers.TryGetValue("X-JA3-String", out var ja3String))
        {
            var str = ja3String.ToString();
            signals.Add("tls.ja3_string", str);

            // Compute hash from string
            var hash = ComputeMd5Hash(str);
            signals.Add("tls.ja3_hash", hash);
            return hash;
        }

        // No JA3 data available
        return string.Empty;
    }

    private static string ComputeMd5Hash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = MD5.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
