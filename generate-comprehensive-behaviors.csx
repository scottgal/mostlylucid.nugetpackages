#!/usr/bin/env dotnet-script
#r "nuget: OllamaSharp, 5.4.12"

using OllamaSharp;
using System.Text;
using System.Threading;
using System.Text.Json;
using System.IO;

Console.WriteLine("=".PadRight(80, '='));
Console.WriteLine("COMPREHENSIVE BOT DETECTION SIGNATURE GENERATOR");
Console.WriteLine("Using GPT-OSS:120B-CLOUD for maximum intelligence");
Console.WriteLine("Creating signature repository structure...");
Console.WriteLine("=".PadRight(80, '='));
Console.WriteLine();

var ollama = new OllamaApiClient("http://localhost:11434")
{
    SelectedModel = "gpt-oss:120b-cloud"
};

// Real datacenter IP prefixes from our IpContributor
var datacenterIpPrefixes = new (string Name, string[] Prefixes)[]
{
    ("AWS", new[] {"3.", "13.", "18.", "35.", "52.", "54."}),
    ("Google Cloud", new[] {"34.", "35."}),
    ("Azure", new[] {"13.", "20.", "40.", "52."}),
    ("DigitalOcean", new[] {"104.131.", "104.236.", "159.65.", "167.99."}),
    ("Linode", new[] {"45.33.", "45.56.", "45.79."}),
    ("Vultr", new[] {"45.32.", "45.63.", "45.76.", "45.77."}),
    ("OVH", new[] {"51.38.", "51.68.", "51.77.", "51.91."}),
    ("Hetzner", new[] {"65.21.", "95.216.", "135.181.", "168.119."})
};

// Real bot User-Agents from our UserAgentContributor
var botUserAgents = new (string Pattern, string Name)[]
{
    ("curl/8.4.0", "curl"),
    ("wget/1.21.4", "wget"),
    ("python-requests/2.31.0", "python-requests"),
    ("python-urllib/3.11", "python-urllib"),
    ("Scrapy/2.11.0 (+https://scrapy.org)", "Scrapy"),
    ("SeleniumHQ/4.15.0 (Headless; Chrome/120.0)", "Selenium"),
    ("HeadlessChrome/120.0.6099.109", "Headless browser"),
    ("PhantomJS/2.1.1", "PhantomJS"),
    ("Puppeteer/21.6.0", "Puppeteer"),
    ("Playwright/1.40.0 (node)", "Playwright"),
    ("HTTrack/3.49", "HTTrack"),
    ("libwww-perl/6.67", "libwww-perl"),
    ("Java/17.0.9", "Java HTTP client"),
    ("Apache-HttpClient/4.5.14 (Java/17.0.9)", "Apache HttpClient"),
    ("okhttp/4.12.0", "OkHttp"),
    ("Go-http-client/2.0", "Go HTTP client"),
    ("node-fetch/2.7.0 (+https://github.com/node-fetch/node-fetch)", "node-fetch"),
    ("axios/1.6.2", "axios")
};

// Good human User-Agents for contrast
var humanUserAgents = new[]
{
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0",
    "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_2) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Safari/605.1.15",
    "Mozilla/5.0 (iPhone; CPU iPhone OS 17_2 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Mobile/15E148 Safari/604.1",
    "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0"
};

// Good residential IP ranges (not datacenter)
var residentialIpPrefixes = new[]
{
    "90.196.", // European residential
    "73.158.", // US residential (Comcast)
    "86.144.", // UK residential (BT)
    "203.221.", // Australian residential
    "201.151.", // South American residential
    "180.149." // Asian residential
};

var random = new Random();

// Create signature repository directory structure
var repoPath = Path.Combine(AppContext.BaseDirectory, "bot-signatures");
Directory.CreateDirectory(repoPath);
Directory.CreateDirectory(Path.Combine(repoPath, "human"));
Directory.CreateDirectory(Path.Combine(repoPath, "bot"));
Directory.CreateDirectory(Path.Combine(repoPath, "_metadata"));

Console.WriteLine($"📁 Repository path: {repoPath}");
Console.WriteLine();

// All the detection dimensions we need to cover
var behaviorCategories = new Dictionary<string, string[]>
{
    ["request-timing"] = new[]
    {
        "Time between requests (human: 2-30s, bot: <1s or exactly timed)",
        "Request bursts vs steady flow",
        "Day/night patterns (humans sleep)",
        "Weekend vs weekday patterns",
        "Session duration (human: 2-15min, bot: hours)"
    },

    ["path-patterns"] = new[]
    {
        "Navigation sequence (human: homepage → browse → detail, bot: direct deep links)",
        "Backtracking and revisits (humans go back, bots rarely do)",
        "Honeypot links (invisible traps only bots click)",
        "Robots.txt violations",
        "Sitemap.xml crawling patterns",
        "Sequential ID scraping (user/1, user/2, user/3...)",
        "Static asset patterns (CSS/JS requests matching browser)",
        "Search crawling (Google: follows links, respects crawl-delay)"
    },

    ["headers"] = new[]
    {
        "User-Agent consistency with other headers",
        "Accept headers matching claimed browser",
        "Accept-Language (human: 1-3 languages, bot: missing or 'en-US,en;q=0.9')",
        "Accept-Encoding (gzip, deflate, br for modern browsers)",
        "Referer chains (logical navigation path)",
        "DNT (Do Not Track) header presence",
        "Sec-Fetch-* headers (modern browsers)",
        "Upgrade-Insecure-Requests header",
        "Cookie handling (humans accept, bots often don't)",
        "Connection keep-alive patterns",
        "TE (Transfer-Encoding) header",
        "HTTP version (HTTP/1.1, HTTP/2, HTTP/3)"
    },

    ["user-agent"] = new[]
    {
        "Browser version age (human: current -2 months, bot: outdated or too new)",
        "OS version consistency",
        "Device type consistency (desktop/mobile/tablet)",
        "Rendering engine match (Chrome=Chromium, Safari=WebKit)",
        "Known bot signatures (Googlebot, curl, python-requests)",
        "Generic/minimal UAs (missing detail)",
        "Rare browser/OS combinations"
    },

    ["client-side"] = new[]
    {
        "JavaScript execution (Canvas fingerprint, WebGL)",
        "Screen resolution and color depth",
        "Timezone vs IP geolocation match",
        "Browser plugins (PDF, Flash - though Flash is dead)",
        "Do Not Track setting",
        "Cookie enabled status",
        "Local storage available",
        "Session storage available",
        "IndexedDB support",
        "WebRTC leak detection",
        "Battery API (mobile)",
        "Device memory, hardware concurrency",
        "Touch support matching device type"
    },

    ["ip-network"] = new[]
    {
        "IP geolocation vs Accept-Language match",
        "Datacenter IP ranges (AWS, Azure, DigitalOcean)",
        "VPN/Proxy detection (Project Honeypot, IPQualityScore)",
        "IP reputation (spam lists, bot lists)",
        "ASN (Autonomous System Number) - hosting vs residential",
        "IP rotation patterns (bots rotate, humans don't)",
        "IPv4 vs IPv6 usage patterns",
        "TLS fingerprint (JA3 hash - unique per client)",
        "HTTP/2 fingerprint (SETTINGS frame order)",
        "TCP/IP stack fingerprint (TTL, window size, options)"
    },

    ["behavioral"] = new[]
    {
        "Mouse movements (human: curves, bot: straight lines or missing)",
        "Keystroke dynamics (timing between keys)",
        "Scroll patterns (smooth human scroll vs instant jump)",
        "Focus/blur events (tab switching)",
        "Page visibility changes",
        "Copy/paste vs typing",
        "Right-click context menu usage",
        "Form interaction patterns",
        "Time on page before interaction",
        "Rage clicks (frustrated humans)",
        "Dead clicks (clicks on non-interactive elements)",
        "Hover patterns before clicking"
    },

    ["cache-state"] = new[]
    {
        "If-Modified-Since header usage",
        "ETag validation",
        "Cache-Control respect",
        "Cookie persistence across sessions",
        "Session cookie vs persistent cookie handling",
        "Third-party cookie blocking (privacy-conscious users)",
        "Local storage data from previous visits",
        "Browser cache hit ratios"
    },

    ["response-behavior"] = new[]
    {
        "Handling of redirects (3xx codes)",
        "Response to 404s (humans stop, bots continue)",
        "Response to rate limiting (429)",
        "Response to CAPTCHAs",
        "Response to JavaScript challenges",
        "Content-Type header respect (not requesting images as HTML)",
        "Following meta refresh directives",
        "Handling of HTTP errors gracefully"
    },

    ["content-interaction"] = new[]
    {
        "Reading time vs content length (humans read, bots scrape instantly)",
        "Scrolling depth (do they read the whole page?)",
        "Link click patterns (which links are most interesting)",
        "Video/audio playback indicators",
        "Download patterns (PDFs, documents)",
        "Search query patterns (human: typos, refinement; bot: perfect queries)",
        "Form submission patterns (human: corrections, bot: one-shot)",
        "Image loading patterns (lazy loading triggers)"
    }
};

Console.WriteLine($"Total behavior categories: {behaviorCategories.Count}");
Console.WriteLine($"Total behavior signals: {behaviorCategories.Values.Sum(v => v.Length)}");
Console.WriteLine();

// Generate metadata index
var metadataIndex = new
{
    Version = "1.0.0",
    Generated = DateTime.UtcNow,
    Model = "gpt-oss:120b-cloud",
    Categories = behaviorCategories.Keys.ToArray(),
    TotalSignals = behaviorCategories.Values.Sum(v => v.Length),
    TotalScenarios = behaviorCategories.Count * 2, // human + bot per category
    Description = "Comprehensive bot detection signature repository with realistic HTTP behavior patterns"
};

File.WriteAllText(
    Path.Combine(repoPath, "_metadata", "index.json"),
    JsonSerializer.Serialize(metadataIndex, new JsonSerializerOptions { WriteIndented = true }));

// Generate README for the repository
var readme = $@"# Bot Detection Signature Repository

**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
**Model:** gpt-oss:120b-cloud
**Version:** 1.0.0

## Overview

This repository contains {behaviorCategories.Count * 2} comprehensive behavioral signatures for bot detection, covering {behaviorCategories.Values.Sum(v => v.Length)} unique detection signals across {behaviorCategories.Count} categories.

## Structure

```
bot-signatures/
├── human/           # Legitimate human behavior patterns
├── bot/             # Bot/scraper behavior patterns
├── _metadata/       # Repository metadata and indexes
└── README.md        # This file
```

## Categories

{string.Join("\n", behaviorCategories.Select((kvp, i) => $"{i + 1}. **{kvp.Key}** ({kvp.Value.Length} signals)"))}

## File Format

Each signature file contains:
- **Scenario**: Descriptive title
- **Category**: Behavior category
- **Type**: Human or Bot
- **Pattern**: Detailed behavior description
- **HTTP Details**: Complete request/response examples
- **Detection Signals**: Which signals are triggered
- **Reasoning**: Why this pattern is suspicious or legitimate
- **Metadata**: Generated timestamp, version, model

## Usage

These signatures can be used for:
1. **Training ML models** - Labeled human/bot examples
2. **Testing detection systems** - Comprehensive test coverage
3. **Signature matching** - Pattern-based detection
4. **Documentation** - Understanding bot behaviors
5. **Research** - Analyzing bot detection techniques

## License

Generated for bot detection research and development.
";

File.WriteAllText(Path.Combine(repoPath, "README.md"), readme);

// Generate category metadata
var categoryMetadata = new Dictionary<string, object>();
foreach (var category in behaviorCategories)
{
    categoryMetadata[category.Key] = new
    {
        Name = category.Key,
        SignalCount = category.Value.Length,
        Signals = category.Value,
        HumanFile = $"human/{category.Key}.json",
        BotFile = $"bot/{category.Key}.json"
    };
}

File.WriteAllText(
    Path.Combine(repoPath, "_metadata", "categories.json"),
    JsonSerializer.Serialize(categoryMetadata, new JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine("✅ Repository structure created");
Console.WriteLine("✅ Metadata files written");
Console.WriteLine();

// Generate scenarios for each category
var generatedCount = 0;

foreach (var category in behaviorCategories)
{
    Console.WriteLine($"⚙️  Generating signatures for: {category.Key}");
    Console.WriteLine();

    // Generate 1 human scenario and 1 bot scenario for this category
    var humanScenario = await GenerateScenario(ollama, category.Key, category.Value, isBot: false);
    var botScenario = await GenerateScenario(ollama, category.Key, category.Value, isBot: true);

    // Save human signature
    var humanFile = Path.Combine(repoPath, "human", $"{category.Key}.json");
    var humanData = new
    {
        Meta = new
        {
            Version = "1.0.0",
            Category = category.Key,
            Type = "human",
            Generated = DateTime.UtcNow,
            Model = "gpt-oss:120b-cloud",
            SignalCount = category.Value.Length
        },
        Signals = category.Value,
        Scenario = humanScenario
    };
    File.WriteAllText(humanFile, JsonSerializer.Serialize(humanData, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"  ✅ Saved: {Path.GetFileName(humanFile)}");

    // Save bot signature
    var botFile = Path.Combine(repoPath, "bot", $"{category.Key}.json");
    var botData = new
    {
        Meta = new
        {
            Version = "1.0.0",
            Category = category.Key,
            Type = "bot",
            Generated = DateTime.UtcNow,
            Model = "gpt-oss:120b-cloud",
            SignalCount = category.Value.Length
        },
        Signals = category.Value,
        Scenario = botScenario
    };
    File.WriteAllText(botFile, JsonSerializer.Serialize(botData, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"  ✅ Saved: {Path.GetFileName(botFile)}");

    Console.WriteLine($"  📝 Preview (human): {humanScenario.Substring(0, Math.Min(80, humanScenario.Length))}...");
    Console.WriteLine($"  📝 Preview (bot): {botScenario.Substring(0, Math.Min(80, botScenario.Length))}...");
    Console.WriteLine();

    generatedCount += 2;
}

Console.WriteLine();
Console.WriteLine("=".PadRight(80, '='));
Console.WriteLine($"✅ Generated {generatedCount} signature files");
Console.WriteLine($"📁 Repository: {repoPath}");
Console.WriteLine($"📊 Categories: {behaviorCategories.Count}");
Console.WriteLine($"🔍 Total signals: {behaviorCategories.Values.Sum(v => v.Length)}");
Console.WriteLine("=".PadRight(80, '='));

async Task<string> GenerateScenario(OllamaApiClient client, string category, string[] signals, bool isBot)
{
    var signalsList = string.Join("\n   - ", signals);
    var userType = isBot ? "sophisticated bot" : "real human user";

    // Select random real values for bot scenarios
    var selectedBotUA = botUserAgents[random.Next(botUserAgents.Length)];
    var selectedDatacenter = datacenterIpPrefixes[random.Next(datacenterIpPrefixes.Length)];
    var selectedDcIP = $"{selectedDatacenter.Prefixes[random.Next(selectedDatacenter.Prefixes.Length)]}{random.Next(1, 254)}.{random.Next(1, 254)}";

    // Select random real values for human scenarios
    var selectedHumanUA = humanUserAgents[random.Next(humanUserAgents.Length)];
    var selectedResidentialPrefix = residentialIpPrefixes[random.Next(residentialIpPrefixes.Length)];
    var selectedResidentialIP = $"{selectedResidentialPrefix}{random.Next(1, 254)}.{random.Next(1, 254)}";

    var realDataInstructions = isBot
        ? $@"

IMPORTANT - Use these REAL bot values (from our detection lists):
- User-Agent: {selectedBotUA.Pattern}
- IP Address: {selectedDcIP} ({selectedDatacenter.Name} datacenter)
- These values WILL trigger our detectors - use them!"
        : $@"

IMPORTANT - Use these REAL human values:
- User-Agent: {selectedHumanUA}
- IP Address: {selectedResidentialIP} (residential ISP)
- These values WILL NOT trigger our detectors - use them!";

    var prompt = $@"You are a bot detection expert. Generate a realistic, detailed test scenario for {category}.

Signals in this category:
   - {signalsList}

Generate a complete test scenario for a {userType} that would trigger these signals.
{realDataInstructions}

Requirements:
1. Be SPECIFIC with exact values, timings, headers
2. Make it realistic and executable as a test case
3. Include HTTP request/response details with actual values
4. Explain WHY this pattern is {(isBot ? "suspicious" : "legitimate")}
5. Keep it under 300 words
6. Use the REAL values provided above, not placeholders!
7. For bot scenarios: combine the bot UA + datacenter IP for maximum detection
8. For human scenarios: combine the human UA + residential IP for realistic legitimacy

Format:
**Scenario:** [brief title]
**Pattern:** [specific behavior description]
**HTTP Details:** [headers, timing, sequences - use the REAL values!]
**Detection Signals:** [which signals this triggers]
**Why {(isBot ? "Suspicious" : "Legitimate")}:** [explanation]

Generate ONLY the scenario, no preamble.";

    try
    {
        var chat = new Chat(client);
        var response = new StringBuilder();

        await foreach (var token in chat.SendAsync(prompt, CancellationToken.None))
        {
            response.Append(token);
        }

        return response.ToString().Trim();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ❌ Error generating scenario: {ex.Message}");
        return $"Error: {ex.Message}";
    }
}
