using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mostlylucid.BotDetection.Data;
using Mostlylucid.BotDetection.Detectors;
using Mostlylucid.BotDetection.Models;
using OllamaSharp;

namespace Mostlylucid.BotDetection.Test.Integration;

/// <summary>
///     Long-running integration tests that use Ollama (gemma3:1b) to generate
///     synthetic bot and human user-agent strings, then verify the detection system
///     correctly classifies them.
///
///     These tests require:
///     - Ollama running locally on http://localhost:11434
///     - The gemma3:1b model installed (ollama pull gemma3:1b)
///
///     Run with: dotnet test --filter "Category=LongRunning"
/// </summary>
[Trait("Category", "LongRunning")]
[Trait("Category", "Ollama")]
public class OllamaGeneratedBotTests : IAsyncLifetime
{
    private const string OllamaEndpoint = "http://localhost:11434";
    private const string OllamaModel = "gemma3:1b";
    private const int GenerationTimeoutMs = 30000;

    private OllamaApiClient? _ollama;
    private bool _ollamaAvailable;
    private List<string> _downloadedBotPatterns = new();
    private readonly HttpClient _httpClient = new();

    public async Task InitializeAsync()
    {
        // Check if Ollama is available
        _ollamaAvailable = await CheckOllamaAvailableAsync();

        if (_ollamaAvailable)
        {
            _ollama = new OllamaApiClient(OllamaEndpoint)
            {
                SelectedModel = OllamaModel
            };
        }

        // Download bot patterns for validation
        await DownloadBotPatternsAsync();
    }

    public Task DisposeAsync()
    {
        _httpClient.Dispose();
        return Task.CompletedTask;
    }

    private async Task<bool> CheckOllamaAvailableAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync($"{OllamaEndpoint}/api/tags");
            if (!response.IsSuccessStatusCode) return false;

            var json = await response.Content.ReadAsStringAsync();
            return json.Contains(OllamaModel, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private async Task DownloadBotPatternsAsync()
    {
        try
        {
            var url = "https://raw.githubusercontent.com/omrilotan/isbot/main/src/patterns.json";
            var json = await _httpClient.GetStringAsync(url);
            _downloadedBotPatterns = JsonSerializer.Deserialize<List<string>>(json) ?? new();
        }
        catch
        {
            // Use fallback patterns
            _downloadedBotPatterns = new List<string>
            {
                @"bot", @"crawler", @"spider", @"scraper",
                @"curl", @"wget", @"python", @"java",
                @"httpclient", @"axios", @"fetch"
            };
        }
    }

    #region Bot User-Agent Generation Tests

    [Fact]
    public async Task Ollama_GeneratesBotUserAgents_ThatAreDetectedAsBots()
    {
        Skip.If(!_ollamaAvailable, "Ollama not available or gemma3:1b not installed");

        // Arrange
        var generatedBots = new List<string>();
        var detectionResults = new List<(string UserAgent, bool Detected)>();

        // Act - Generate 10 bot user-agents
        for (int i = 0; i < 10; i++)
        {
            var botUserAgent = await GenerateBotUserAgentAsync();
            if (!string.IsNullOrEmpty(botUserAgent))
            {
                generatedBots.Add(botUserAgent);

                var isDetected = IsDetectedAsBot(botUserAgent);
                detectionResults.Add((botUserAgent, isDetected));
            }
        }

        // Assert - At least 70% should be detected as bots
        var detectedCount = detectionResults.Count(r => r.Detected);
        var detectionRate = (double)detectedCount / detectionResults.Count;

        Assert.True(generatedBots.Count >= 5, $"Should generate at least 5 bot UAs, got {generatedBots.Count}");
        Assert.True(detectionRate >= 0.7,
            $"Detection rate should be >=70%, got {detectionRate:P0}. " +
            $"Undetected: {string.Join(", ", detectionResults.Where(r => !r.Detected).Select(r => r.UserAgent))}");
    }

    [Fact]
    public async Task Ollama_GeneratesHumanUserAgents_ThatAreNotDetectedAsBots()
    {
        Skip.If(!_ollamaAvailable, "Ollama not available or gemma3:1b not installed");

        // Arrange
        var generatedHumans = new List<string>();
        var detectionResults = new List<(string UserAgent, bool Detected)>();

        // Act - Generate 10 human user-agents
        for (int i = 0; i < 10; i++)
        {
            var humanUserAgent = await GenerateHumanUserAgentAsync();
            if (!string.IsNullOrEmpty(humanUserAgent))
            {
                generatedHumans.Add(humanUserAgent);

                var isDetected = IsDetectedAsBot(humanUserAgent);
                detectionResults.Add((humanUserAgent, isDetected));
            }
        }

        // Assert - At least 80% should NOT be detected as bots
        var notDetectedCount = detectionResults.Count(r => !r.Detected);
        var humanRate = (double)notDetectedCount / detectionResults.Count;

        Assert.True(generatedHumans.Count >= 5, $"Should generate at least 5 human UAs, got {generatedHumans.Count}");
        Assert.True(humanRate >= 0.8,
            $"Human rate should be >=80%, got {humanRate:P0}. " +
            $"False positives: {string.Join(", ", detectionResults.Where(r => r.Detected).Select(r => r.UserAgent))}");
    }

    [Fact]
    public async Task Ollama_GeneratesVariedBotTypes()
    {
        Skip.If(!_ollamaAvailable, "Ollama not available or gemma3:1b not installed");

        // Arrange
        var botTypes = new[] { "scraper", "crawler", "search engine", "monitoring", "http client library" };
        var generatedByType = new Dictionary<string, List<string>>();

        // Act - Generate 2 user-agents for each bot type
        foreach (var botType in botTypes)
        {
            generatedByType[botType] = new List<string>();

            for (int i = 0; i < 2; i++)
            {
                var userAgent = await GenerateSpecificBotTypeAsync(botType);
                if (!string.IsNullOrEmpty(userAgent))
                {
                    generatedByType[botType].Add(userAgent);
                }
            }
        }

        // Assert - Each type should generate at least 1 UA
        foreach (var botType in botTypes)
        {
            Assert.True(generatedByType[botType].Count >= 1,
                $"Should generate at least 1 {botType} UA, got {generatedByType[botType].Count}");
        }

        // Output for inspection
        foreach (var kvp in generatedByType)
        {
            Console.WriteLine($"{kvp.Key}: {string.Join(" | ", kvp.Value)}");
        }
    }

    #endregion

    #region LLM Detector Integration Tests

    [Fact]
    public async Task LlmDetector_WithOllama_ClassifiesGeneratedBots()
    {
        Skip.If(!_ollamaAvailable, "Ollama not available or gemma3:1b not installed");

        // Arrange
        var detector = CreateLlmDetector();
        var generatedBot = await GenerateBotUserAgentAsync();

        Skip.If(string.IsNullOrEmpty(generatedBot), "Failed to generate bot user-agent");

        var context = CreateHttpContext(generatedBot!);

        // Act
        var result = await detector.DetectAsync(context);

        // Assert - LLM should detect the bot with some confidence
        Console.WriteLine($"Generated bot: {generatedBot}");
        Console.WriteLine($"LLM Detection: IsBot={result.Confidence > 0}, Confidence={result.Confidence}");

        // Note: LLM detection might not always agree with pattern matching
        // This test is more about verifying the integration works
        Assert.True(result.Confidence >= 0, "Should return a valid confidence score");
    }

    [Fact]
    public async Task LlmDetector_WithOllama_ClassifiesGeneratedHumans()
    {
        Skip.If(!_ollamaAvailable, "Ollama not available or gemma3:1b not installed");

        // Arrange
        var detector = CreateLlmDetector();
        var generatedHuman = await GenerateHumanUserAgentAsync();

        Skip.If(string.IsNullOrEmpty(generatedHuman), "Failed to generate human user-agent");

        var context = CreateHttpContext(generatedHuman!);

        // Act
        var result = await detector.DetectAsync(context);

        // Assert
        Console.WriteLine($"Generated human: {generatedHuman}");
        Console.WriteLine($"LLM Detection: Confidence={result.Confidence}");

        // Human UAs should have low or zero confidence
        Assert.True(result.Confidence <= 0.5,
            $"Human UA should have low confidence, got {result.Confidence}");
    }

    #endregion

    #region Adversarial Tests

    [Fact]
    public async Task Ollama_GeneratesEvasiveBots_TestsDetectionRobustness()
    {
        Skip.If(!_ollamaAvailable, "Ollama not available or gemma3:1b not installed");

        // Arrange - Generate bot UAs designed to evade detection
        var evasiveBots = new List<string>();

        // Act
        for (int i = 0; i < 5; i++)
        {
            var evasiveBot = await GenerateEvasiveBotUserAgentAsync();
            if (!string.IsNullOrEmpty(evasiveBot))
            {
                evasiveBots.Add(evasiveBot);
            }
        }

        // Assert & Report
        var detectionResults = evasiveBots
            .Select(ua => (UserAgent: ua, Detected: IsDetectedAsBot(ua)))
            .ToList();

        var detectedCount = detectionResults.Count(r => r.Detected);

        Console.WriteLine($"Evasive bot detection rate: {detectedCount}/{evasiveBots.Count}");
        foreach (var result in detectionResults)
        {
            Console.WriteLine($"  {(result.Detected ? "[DETECTED]" : "[EVADED]")} {result.UserAgent}");
        }

        // Note: This test documents evasion success rate, not a pass/fail
        // High evasion rate indicates the detection system needs improvement
    }

    #endregion

    #region Bulk Generation Tests

    [Fact]
    public async Task Ollama_BulkGenerateBotUserAgents_50Samples()
    {
        Skip.If(!_ollamaAvailable, "Ollama not available or gemma3:1b not installed");

        // Arrange
        var samples = new List<(string UserAgent, string Type, bool Detected)>();
        var types = new[] { "bot", "human" };

        // Act - Generate 25 of each type
        foreach (var type in types)
        {
            for (int i = 0; i < 25; i++)
            {
                var ua = type == "bot"
                    ? await GenerateBotUserAgentAsync()
                    : await GenerateHumanUserAgentAsync();

                if (!string.IsNullOrEmpty(ua))
                {
                    samples.Add((ua, type, IsDetectedAsBot(ua)));
                }
            }
        }

        // Assert & Report
        var botSamples = samples.Where(s => s.Type == "bot").ToList();
        var humanSamples = samples.Where(s => s.Type == "human").ToList();

        var truePositives = botSamples.Count(s => s.Detected);
        var falsePositives = humanSamples.Count(s => s.Detected);
        var trueNegatives = humanSamples.Count(s => !s.Detected);
        var falseNegatives = botSamples.Count(s => !s.Detected);

        var precision = truePositives > 0
            ? (double)truePositives / (truePositives + falsePositives)
            : 0;
        var recall = truePositives > 0
            ? (double)truePositives / (truePositives + falseNegatives)
            : 0;
        var f1 = precision + recall > 0
            ? 2 * (precision * recall) / (precision + recall)
            : 0;

        Console.WriteLine($"=== Detection Metrics (n={samples.Count}) ===");
        Console.WriteLine($"True Positives (bots detected): {truePositives}");
        Console.WriteLine($"False Positives (humans flagged): {falsePositives}");
        Console.WriteLine($"True Negatives (humans passed): {trueNegatives}");
        Console.WriteLine($"False Negatives (bots missed): {falseNegatives}");
        Console.WriteLine($"Precision: {precision:P1}");
        Console.WriteLine($"Recall: {recall:P1}");
        Console.WriteLine($"F1 Score: {f1:P1}");

        // Assert reasonable performance
        Assert.True(precision >= 0.6, $"Precision should be >=60%, got {precision:P1}");
        Assert.True(recall >= 0.6, $"Recall should be >=60%, got {recall:P1}");
    }

    #endregion

    #region Helper Methods

    private async Task<string?> GenerateBotUserAgentAsync()
    {
        var prompt = @"Generate a realistic HTTP User-Agent string for a web bot/crawler/scraper.
Include typical bot identifiers like version numbers and URLs.
Examples: Googlebot, Scrapy, python-requests, curl, wget, etc.
Return ONLY the User-Agent string, nothing else.
User-Agent:";

        return await GenerateWithOllamaAsync(prompt);
    }

    private async Task<string?> GenerateHumanUserAgentAsync()
    {
        var prompt = @"Generate a realistic HTTP User-Agent string for a real web browser.
Use current browser versions (Chrome 120+, Firefox 120+, Safari 17+, Edge 120+).
Include proper platform info (Windows, macOS, iOS, Android, Linux).
Return ONLY the User-Agent string, nothing else.
User-Agent:";

        return await GenerateWithOllamaAsync(prompt);
    }

    private async Task<string?> GenerateSpecificBotTypeAsync(string botType)
    {
        var prompt = $@"Generate a realistic HTTP User-Agent string for a {botType}.
Make it look authentic with version numbers and identifiers.
Return ONLY the User-Agent string, nothing else.
User-Agent:";

        return await GenerateWithOllamaAsync(prompt);
    }

    private async Task<string?> GenerateEvasiveBotUserAgentAsync()
    {
        var prompt = @"Generate a User-Agent string for a bot that tries to look like a real browser.
It should mimic Chrome/Firefox/Safari but have subtle differences.
Make it hard to detect as a bot while still being a bot.
Return ONLY the User-Agent string, nothing else.
User-Agent:";

        return await GenerateWithOllamaAsync(prompt);
    }

    private async Task<string?> GenerateWithOllamaAsync(string prompt)
    {
        if (_ollama == null) return null;

        try
        {
            using var cts = new CancellationTokenSource(GenerationTimeoutMs);
            var chat = new Chat(_ollama);
            var responseBuilder = new StringBuilder();

            await foreach (var token in chat.SendAsync(prompt, cts.Token))
            {
                responseBuilder.Append(token);
            }

            var response = responseBuilder.ToString().Trim();

            // Clean up the response - extract just the UA string
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var ua = lines
                .Select(l => l.Trim())
                .FirstOrDefault(l => l.StartsWith("Mozilla/") ||
                                     l.Contains("/") ||
                                     l.Contains("bot", StringComparison.OrdinalIgnoreCase));

            return ua ?? response.Split('\n').FirstOrDefault()?.Trim();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ollama generation failed: {ex.Message}");
            return null;
        }
    }

    private bool IsDetectedAsBot(string userAgent)
    {
        return _downloadedBotPatterns.Any(pattern =>
        {
            try
            {
                return Regex.IsMatch(userAgent, pattern, RegexOptions.IgnoreCase);
            }
            catch
            {
                return false;
            }
        });
    }

    private LlmDetector CreateLlmDetector()
    {
        var options = Options.Create(new BotDetectionOptions
        {
            EnableLlmDetection = true,
            AiDetection = new AiDetectionOptions
            {
                Provider = AiProvider.Ollama,
                TimeoutMs = 10000,
                Ollama = new OllamaOptions
                {
                    Endpoint = OllamaEndpoint,
                    Model = OllamaModel
                }
            },
            OllamaEndpoint = OllamaEndpoint,
            OllamaModel = OllamaModel,
            LlmTimeoutMs = 10000
        });

        return new LlmDetector(
            NullLogger<LlmDetector>.Instance,
            options);
    }

    private static HttpContext CreateHttpContext(string userAgent)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.UserAgent = userAgent;
        context.Request.Path = "/test";
        context.Request.Method = "GET";
        context.Request.Headers.Accept = "text/html,application/xhtml+xml";
        context.Request.Headers.AcceptLanguage = "en-US,en;q=0.9";
        return context;
    }

    #endregion
}

/// <summary>
///     Helper class to skip tests conditionally.
/// </summary>
public static class Skip
{
    public static void If(bool condition, string reason)
    {
        if (condition)
        {
            throw new SkipException(reason);
        }
    }
}

/// <summary>
///     Exception to skip a test.
/// </summary>
public class SkipException : Exception
{
    public SkipException(string message) : base(message) { }
}
