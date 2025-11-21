namespace Mostlylucid.BotDetection.Data;

/// <summary>
///     URLs for authoritative bot detection lists
/// </summary>
public static class BotListSources
{
    /// <summary>
    ///     Matomo Device Detector bot list (YAML format)
    ///     Contains 1000+ bot patterns with categories
    /// </summary>
    public const string MatomoBotList =
        "https://raw.githubusercontent.com/matomo-org/device-detector/master/regexes/bots.yml";

    /// <summary>
    ///     Crawler User Agents (JSON format)
    ///     Community-maintained list of known crawlers
    /// </summary>
    public const string CrawlerUserAgents =
        "https://raw.githubusercontent.com/monperrus/crawler-user-agents/master/crawler-user-agents.json";

    /// <summary>
    ///     AWS IP ranges (JSON format)
    ///     Official list from Amazon
    /// </summary>
    public const string AwsIpRanges = "https://ip-ranges.amazonaws.com/ip-ranges.json";

    /// <summary>
    ///     Google Cloud IP ranges (JSON format)
    ///     Official list from Google
    /// </summary>
    public const string GcpIpRanges = "https://www.gstatic.com/ipranges/cloud.json";

    /// <summary>
    ///     Cloudflare IP ranges (TXT format)
    ///     Official list from Cloudflare
    /// </summary>
    public const string CloudflareIpv4 = "https://www.cloudflare.com/ips-v4";

    public const string CloudflareIpv6 = "https://www.cloudflare.com/ips-v6";

    /// <summary>
    ///     MyIP.ms datacenter detection
    ///     Updated list of datacenter IP ranges
    /// </summary>
    public const string MyIpMsDatacenters = "https://www.myip.ms/files/bots/live_webcrawlers.txt";

    /// <summary>
    ///     ISBot patterns (from npm package)
    ///     Used by many JavaScript bot detection libraries
    /// </summary>
    public const string IsBotPatterns = "https://unpkg.com/isbot@latest/src/list.json";
}