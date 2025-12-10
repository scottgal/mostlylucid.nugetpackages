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
            var tlsFeature = state.HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.ITlsConnectionFeature>();

            if (tlsFeature == null)
            {
                // No TLS info available (could be HTTP, reverse proxy, etc.)
                signals.Add("tls.available", false);
                contributions.Add(new DetectionContribution
                {
                    DetectorName = Name,
                    Category = "TLS",
                    ConfidenceDelta = 0.05, // Slight bot indicator - many bots use HTTP
                    Weight = 0.3,
                    Reason = "No TLS connection info available",
                    Signals = signals.ToImmutable()
                });
                return Task.FromResult<IReadOnlyList<DetectionContribution>>(contributions);
            }

            signals.Add("tls.available", true);

            // Extract TLS protocol version
            var protocol = tlsFeature.Protocol.ToString();
            signals.Add("tls.protocol", protocol);

            // Analyze protocol version
            AnalyzeTlsProtocol(protocol, contributions, signals);

            // Get cipher suite if available
            var cipherAlgo = tlsFeature.CipherAlgorithm.ToString();
            var hashAlgo = tlsFeature.HashAlgorithm.ToString();
            var keyExchange = tlsFeature.KeyExchangeAlgorithm.ToString();

            signals.Add("tls.cipher_algo", cipherAlgo);
            signals.Add("tls.hash_algo", hashAlgo);
            signals.Add("tls.key_exchange", keyExchange);

            // Analyze cipher strength
            AnalyzeCipherSuite(cipherAlgo, hashAlgo, keyExchange, contributions, signals);

            // Generate JA3-style fingerprint from available data
            var ja3String = GenerateJa3Fingerprint(state.HttpContext);
            if (!string.IsNullOrEmpty(ja3String))
            {
                var ja3Hash = ComputeMd5Hash(ja3String);
                signals.Add("tls.ja3_hash", ja3Hash);
                signals.Add("tls.ja3_string", ja3String);

                // Check against known fingerprints
                if (KnownBotFingerprints.Contains(ja3Hash))
                {
                    contributions.Add(DetectionContribution.Bot(
                        Name, "TLS", 0.85,
                        $"Known bot TLS fingerprint detected: {ja3Hash[..8]}...",
                        BotType.ToolAutomation,
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
                        Reason = $"Known legitimate browser fingerprint: {ja3Hash[..8]}...",
                        Signals = signals.ToImmutable()
                    });
                }
                else
                {
                    // Unknown fingerprint - neutral
                    signals.Add("tls.fingerprint_known", false);
                }
            }

            // Analyze certificate client auth (if used)
            var clientCert = tlsFeature.ClientCertificate;
            if (clientCert != null)
            {
                signals.Add("tls.client_cert_present", true);
                signals.Add("tls.client_cert_issuer", clientCert.Issuer);

                // Client certs are uncommon for browsers, more common for automation
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

    private void AnalyzeTlsProtocol(string protocol, List<DetectionContribution> contributions, ImmutableDictionary<string, object>.Builder signals)
    {
        // Outdated protocols are suspicious
        if (protocol.Contains("Ssl2") || protocol.Contains("Ssl3"))
        {
            contributions.Add(DetectionContribution.Bot(
                Name, "TLS", 0.7,
                $"Outdated TLS protocol: {protocol}",
                BotType.ToolAutomation,
                weight: 1.5));
        }
        else if (protocol.Contains("Tls") || protocol.Contains("Tls11"))
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

    private void AnalyzeCipherSuite(string cipher, string hash, string keyExchange,
        List<DetectionContribution> contributions, ImmutableDictionary<string, object>.Builder signals)
    {
        // Weak ciphers indicate old/custom clients
        if (cipher.Contains("Null") || cipher.Contains("None") ||
            hash.Contains("Md5") || hash.Contains("Sha1"))
        {
            contributions.Add(new DetectionContribution
            {
                DetectorName = Name,
                Category = "TLS",
                ConfidenceDelta = 0.4,
                Weight = 1.3,
                Reason = $"Weak cipher suite detected: {cipher}/{hash}",
                Signals = signals.ToImmutable()
            });
        }

        // Export-grade or DES ciphers
        if (cipher.Contains("Des") || cipher.Contains("Export"))
        {
            contributions.Add(DetectionContribution.Bot(
                Name, "TLS", 0.6,
                "Export-grade or DES cipher (very outdated)",
                BotType.ToolAutomation,
                weight: 1.4));
        }
    }

    /// <summary>
    ///     Generate a JA3-style fingerprint from HTTP context.
    ///
    ///     JA3 Format: SSLVersion,Ciphers,Extensions,EllipticCurves,EllipticCurvePointFormats
    ///     Note: Full JA3 requires packet capture. We approximate from available data.
    ///
    ///     In production, integrate with reverse proxy (nginx/HAProxy) that can
    ///     extract full TLS handshake and pass via header (X-JA3-Hash).
    /// </summary>
    private string GenerateJa3Fingerprint(HttpContext context)
    {
        var parts = new List<string>();

        var tlsFeature = context.Features.Get<Microsoft.AspNetCore.Http.Features.ITlsConnectionFeature>();
        if (tlsFeature == null) return string.Empty;

        // SSL Version
        parts.Add(tlsFeature.Protocol.ToString());

        // Cipher Suite (simplified - full JA3 needs all offered ciphers)
        parts.Add($"{tlsFeature.CipherAlgorithm}-{tlsFeature.HashAlgorithm}-{tlsFeature.KeyExchangeAlgorithm}");

        // Check for JA3 hash passed from reverse proxy
        if (context.Request.Headers.TryGetValue("X-JA3-Hash", out var ja3Hash))
        {
            // Reverse proxy (nginx/HAProxy) already calculated it
            return ja3Hash.ToString();
        }

        // For production: implement full packet capture integration
        // This is a simplified approximation
        return string.Join(",", parts);
    }

    private static string ComputeMd5Hash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = MD5.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
