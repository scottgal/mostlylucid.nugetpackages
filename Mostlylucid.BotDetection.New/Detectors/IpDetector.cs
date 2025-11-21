using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.BotDetection.Models;

namespace Mostlylucid.BotDetection.Detectors;

/// <summary>
///     Detects bots based on IP address analysis
/// </summary>
public class IpDetector(
    ILogger<IpDetector> logger,
    IOptions<BotDetectionOptions> options)
    : IDetector
{
    private readonly ILogger<IpDetector> _logger = logger;
    private readonly BotDetectionOptions _options = options.Value;

    public string Name => "IP Detector";

    public Task<DetectorResult> DetectAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        var result = new DetectorResult();
        var ipAddress = GetClientIpAddress(context);

        if (ipAddress == null) return Task.FromResult(result);

        var confidence = 0.0;
        var reasons = new List<DetectionReason>();

        // Check if IP is in datacenter range
        if (IsDatacenterIp(ipAddress))
        {
            confidence += 0.4;
            reasons.Add(new DetectionReason
            {
                Category = "IP",
                Detail = $"IP {ipAddress} is in datacenter range",
                ConfidenceImpact = 0.4
            });
        }

        // Check for known cloud providers
        var cloudProvider = GetCloudProvider(ipAddress);
        if (cloudProvider != null)
        {
            confidence += 0.3;
            reasons.Add(new DetectionReason
            {
                Category = "IP",
                Detail = $"IP from cloud provider: {cloudProvider}",
                ConfidenceImpact = 0.3
            });
        }

        // Check if it's a Tor exit node (simplified check)
        // In production, you'd want to maintain a list of Tor exit nodes
        if (IsTorExitNode(ipAddress))
        {
            confidence += 0.5;
            reasons.Add(new DetectionReason
            {
                Category = "IP",
                Detail = "IP is a Tor exit node",
                ConfidenceImpact = 0.5
            });
            result.BotType = BotType.MaliciousBot;
        }

        result.Confidence = Math.Min(confidence, 1.0);
        result.Reasons = reasons;

        return Task.FromResult(result);
    }

    private IPAddress? GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded IP first (behind proxy/load balancer)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ips = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (ips.Length > 0 && IPAddress.TryParse(ips[0].Trim(), out var ip)) return ip;
        }

        // Fall back to connection remote IP
        return context.Connection.RemoteIpAddress;
    }

    private bool IsDatacenterIp(IPAddress ipAddress)
    {
        foreach (var prefix in _options.DatacenterIpPrefixes)
            try
            {
                if (IsInSubnet(ipAddress, prefix)) return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking IP prefix: {Prefix}", prefix);
            }

        return false;
    }

    private bool IsInSubnet(IPAddress ipAddress, string cidr)
    {
        var parts = cidr.Split('/');
        if (parts.Length != 2)
            return false;

        if (!IPAddress.TryParse(parts[0], out var networkAddress))
            return false;

        if (!int.TryParse(parts[1], out var prefixLength))
            return false;

        var ipBytes = ipAddress.GetAddressBytes();
        var networkBytes = networkAddress.GetAddressBytes();

        if (ipBytes.Length != networkBytes.Length)
            return false;

        var maskBytes = prefixLength / 8;
        var maskBits = prefixLength % 8;

        // Check full bytes
        for (var i = 0; i < maskBytes; i++)
            if (ipBytes[i] != networkBytes[i])
                return false;

        // Check remaining bits
        if (maskBits > 0 && maskBytes < ipBytes.Length)
        {
            var mask = (byte)(0xFF << (8 - maskBits));
            if ((ipBytes[maskBytes] & mask) != (networkBytes[maskBytes] & mask))
                return false;
        }

        return true;
    }

    private string? GetCloudProvider(IPAddress ipAddress)
    {
        var bytes = ipAddress.GetAddressBytes();
        if (bytes.Length != 4)
            return null; // Only check IPv4 for simplicity

        var firstOctet = bytes[0];

        // Simple heuristic based on first octet
        return firstOctet switch
        {
            3 or 13 or 18 or 52 => "AWS",
            20 or 40 or 104 => "Azure",
            34 or 35 => "Google Cloud",
            138 or 139 or 140 => "Oracle Cloud",
            _ => null
        };
    }

    private bool IsTorExitNode(IPAddress ipAddress)
    {
        // This is a placeholder - in production, you'd maintain a list of Tor exit nodes
        // You can get this from https://check.torproject.org/exit-addresses
        // For now, we'll return false
        return false;
    }
}