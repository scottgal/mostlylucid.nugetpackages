using System.Net;
using System.Text.RegularExpressions;
using Mostlylucid.LlmPiiRedactor.Models;

namespace Mostlylucid.LlmPiiRedactor.Detectors;

/// <summary>
/// Detects IPv4 and IPv6 addresses.
/// </summary>
public class IpAddressDetector : BasePiiDetector
{
    public override PiiType PiiType => PiiType.IpAddress;
    public override string Name => "IpAddressDetector";
    public override int Priority => 30;

    // Combined IPv4 and IPv6 pattern
    protected override string Pattern =>
        @"(?<!\d)(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)(?!\d)|(?:[0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}|(?:[0-9a-fA-F]{1,4}:){1,7}:|(?:[0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}|::(?:[0-9a-fA-F]{1,4}:){0,5}[0-9a-fA-F]{1,4}|[0-9a-fA-F]{1,4}::(?:[0-9a-fA-F]{1,4}:){0,5}[0-9a-fA-F]{1,4}";

    // Common non-PII IPs to exclude
    private static readonly HashSet<string> ExcludedIps = new()
    {
        "0.0.0.0",
        "127.0.0.1",
        "255.255.255.255",
        "::1",
        "::"
    };

    protected override bool ValidateMatch(Match match, string originalText)
    {
        var ip = match.Value;

        // Exclude localhost and broadcast addresses
        if (ExcludedIps.Contains(ip))
            return false;

        // Validate it's actually a valid IP
        return IPAddress.TryParse(ip, out _);
    }

    protected override double CalculateConfidence(Match match, string originalText)
    {
        var ip = match.Value;

        // Lower confidence for private/local ranges
        if (IPAddress.TryParse(ip, out var address))
        {
            var bytes = address.GetAddressBytes();

            // Check for private ranges
            if (bytes.Length == 4)
            {
                // 10.x.x.x
                if (bytes[0] == 10)
                    return 0.7;
                // 172.16.x.x - 172.31.x.x
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                    return 0.7;
                // 192.168.x.x
                if (bytes[0] == 192 && bytes[1] == 168)
                    return 0.7;
            }
        }

        return 0.9;
    }
}
