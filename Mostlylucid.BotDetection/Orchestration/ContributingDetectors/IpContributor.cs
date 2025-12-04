using System.Collections.Immutable;
using System.Net;
using Microsoft.Extensions.Logging;
using Mostlylucid.BotDetection.Models;

namespace Mostlylucid.BotDetection.Orchestration.ContributingDetectors;

/// <summary>
///     IP address analysis for bot detection.
///     Runs in the first wave (no dependencies).
///     Analyzes client IP for bot indicators.
/// </summary>
public class IpContributor : ContributingDetectorBase
{
    private readonly ILogger<IpContributor> _logger;

    // Common datacenter/cloud provider IP ranges (sample)
    private static readonly (string Name, string[] Prefixes)[] DatacenterRanges =
    [
        ("AWS", ["3.", "13.", "18.", "35.", "52.", "54."]),
        ("Google Cloud", ["34.", "35."]),
        ("Azure", ["13.", "20.", "40.", "52."]),
        ("DigitalOcean", ["104.131.", "104.236.", "159.65.", "167.99."]),
        ("Linode", ["45.33.", "45.56.", "45.79."]),
        ("Vultr", ["45.32.", "45.63.", "45.76.", "45.77."]),
        ("OVH", ["51.38.", "51.68.", "51.77.", "51.91."]),
        ("Hetzner", ["65.21.", "95.216.", "135.181.", "168.119."])
    ];

    // Local/private IP ranges
    private static readonly string[] LocalPrefixes =
        ["127.", "10.", "172.16.", "172.17.", "172.18.", "172.19.", "172.20.", "172.21.",
         "172.22.", "172.23.", "172.24.", "172.25.", "172.26.", "172.27.", "172.28.",
         "172.29.", "172.30.", "172.31.", "192.168.", "::1", "fe80:"];

    public IpContributor(ILogger<IpContributor> logger)
    {
        _logger = logger;
    }

    public override string Name => "Ip";
    public override int Priority => 12; // Run early

    // No triggers - runs in first wave
    public override IReadOnlyList<TriggerCondition> TriggerConditions => Array.Empty<TriggerCondition>();

    public override Task<IReadOnlyList<DetectionContribution>> ContributeAsync(
        BlackboardState state,
        CancellationToken cancellationToken = default)
    {
        var contributions = new List<DetectionContribution>();
        var clientIp = state.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
        var signals = ImmutableDictionary.CreateBuilder<string, object>();

        signals.Add(SignalKeys.ClientIp, clientIp);

        // Check if IP is empty/null
        if (string.IsNullOrEmpty(clientIp))
        {
            contributions.Add(DetectionContribution.Bot(
                Name, "IP", 0.6,
                "Missing client IP address",
                Models.BotType.Unknown)
                with { Signals = signals.ToImmutable() });
            return Task.FromResult<IReadOnlyList<DetectionContribution>>(contributions);
        }

        // Check for localhost/local network
        var isLocal = IsLocalIp(clientIp);
        var isLoopback = clientIp is "::1" or "127.0.0.1" or "localhost";
        signals.Add(SignalKeys.IpIsLocal, isLocal);

        if (isLocal)
        {
            // Loopback addresses (::1, 127.0.0.1) are typical dev/test environments - neutral
            // Other private IPs (192.168.x.x, 10.x.x.x) could be internal tools - slight indicator
            contributions.Add(new DetectionContribution
            {
                DetectorName = Name,
                Category = "IP",
                ConfidenceDelta = isLoopback ? 0 : 0.1, // Loopback is neutral, other private IPs slight indicator
                Weight = 0.5,
                Reason = isLoopback
                    ? $"Localhost/loopback address: {MaskIp(clientIp)} (neutral in dev)"
                    : $"Private network IP: {MaskIp(clientIp)}",
                Signals = signals.ToImmutable()
            });
        }

        // Check for datacenter IP
        var (isDatacenter, datacenterName) = CheckDatacenterIp(clientIp);
        signals.Add(SignalKeys.IpIsDatacenter, isDatacenter);

        if (isDatacenter)
        {
            signals.Add("ip.datacenter_name", datacenterName!);
            contributions.Add(DetectionContribution.Bot(
                Name, "IP", 0.6,
                $"Datacenter IP detected: {datacenterName}",
                Models.BotType.Unknown,
                weight: 1.2)
                with { Signals = signals.ToImmutable() });
        }

        // Check for IPv6 (less common for bots currently, but this varies)
        var isIpv6 = clientIp.Contains(':');
        signals.Add("ip.is_ipv6", isIpv6);

        // No bot indicators found
        if (contributions.Count == 0)
        {
            contributions.Add(new DetectionContribution
            {
                DetectorName = Name,
                Category = "IP",
                ConfidenceDelta = -0.1, // Slight human indicator
                Weight = 1.0,
                Reason = $"IP appears normal: {MaskIp(clientIp)}",
                Signals = signals.ToImmutable()
            });
        }

        return Task.FromResult<IReadOnlyList<DetectionContribution>>(contributions);
    }

    private static bool IsLocalIp(string ip)
    {
        foreach (var prefix in LocalPrefixes)
        {
            if (ip.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Also check with IPAddress parsing for accuracy
        if (IPAddress.TryParse(ip, out var addr))
        {
            if (IPAddress.IsLoopback(addr))
                return true;

            // Check for link-local IPv6
            if (addr.IsIPv6LinkLocal)
                return true;
        }

        return false;
    }

    private static (bool isDatacenter, string? name) CheckDatacenterIp(string ip)
    {
        foreach (var (name, prefixes) in DatacenterRanges)
        {
            foreach (var prefix in prefixes)
            {
                if (ip.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return (true, name);
            }
        }
        return (false, null);
    }

    private static string MaskIp(string ip)
    {
        // Mask last octet for privacy in logs
        var parts = ip.Split('.');
        if (parts.Length == 4)
            return $"{parts[0]}.{parts[1]}.{parts[2]}.xxx";

        // For IPv6, truncate
        if (ip.Length > 10)
            return ip[..10] + "...";

        return ip;
    }
}
