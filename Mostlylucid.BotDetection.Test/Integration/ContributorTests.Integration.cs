using System.Collections.Immutable;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mostlylucid.BotDetection.Extensions;
using Mostlylucid.BotDetection.Models;
using Mostlylucid.BotDetection.Orchestration;

namespace Mostlylucid.BotDetection.Test.Integration;

/// <summary>
///     Integration tests to verify all contributors run and always report results.
/// </summary>
[Trait("Category", "Integration")]
public class ContributorIntegrationTests
{
    private readonly IServiceProvider _sp;

    public ContributorIntegrationTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BotDetection:EnableUserAgentDetection"] = "true",
                ["BotDetection:EnableHeaderAnalysis"] = "true",
                ["BotDetection:EnableIpDetection"] = "true",
                ["BotDetection:EnableBehavioralAnalysis"] = "true",
                ["BotDetection:EnableLlmDetection"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddMemoryCache();
        services.AddHttpClient();
        services.AddBotDetection(config);
        _sp = services.BuildServiceProvider();
    }

    [Fact]
    public void AllContributors_ShouldBeRegistered()
    {
        var contributors = _sp.GetServices<IContributingDetector>().Select(c => c.Name).ToList();
        Assert.Contains("FastPathReputation", contributors);
        Assert.Contains("UserAgent", contributors);
        Assert.Contains("Header", contributors);
        Assert.Contains("Ip", contributors);
        Assert.Contains("SecurityTool", contributors);
        Assert.Contains("ProjectHoneypot", contributors);
        Assert.Contains("Behavioral", contributors);
        Assert.Contains("ClientSide", contributors);
        Assert.Contains("Inconsistency", contributors);
        Assert.Contains("VersionAge", contributors);
        Assert.Contains("ReputationBias", contributors);
        Assert.Contains("Heuristic", contributors);
    }

    [Fact]
    public async Task FastPathReputationContributor_AlwaysContributes()
    {
        var c = GetContributor("FastPathReputation");
        var result = await c.ContributeAsync(CreateState("Mozilla/5.0 Chrome/120", "192.168.1.1"));
        Assert.NotEmpty(result);
        Assert.Contains(result, r => r.DetectorName == "FastPathReputation");
    }

    [Fact]
    public async Task SecurityToolContributor_AlwaysContributes()
    {
        var c = GetContributor("SecurityTool");
        var result = await c.ContributeAsync(CreateState("Mozilla/5.0 Chrome/120", "192.168.1.1"));
        Assert.NotEmpty(result);
        Assert.Contains(result, r => r.DetectorName == "SecurityTool");
    }

    [Fact]
    public async Task SecurityToolContributor_DetectsSqlmap()
    {
        var c = GetContributor("SecurityTool");
        var result = await c.ContributeAsync(CreateState("sqlmap/1.5.2#stable", "192.168.1.1"));
        Assert.NotEmpty(result);
        Assert.True(result.First().ConfidenceDelta > 0.5);
    }

    [Fact]
    public async Task ProjectHoneypotContributor_ContributesForLocalhost()
    {
        var c = GetContributor("ProjectHoneypot");
        var signals = new Dictionary<string, object>
        {
            ["ip.is_local"] = true,
            ["ip.address"] = "127.0.0.1"
        };
        var result = await c.ContributeAsync(CreateState("Mozilla/5.0", "127.0.0.1", signals));
        Assert.NotEmpty(result);
        Assert.Contains(result, r => r.DetectorName == "ProjectHoneypot");
    }

    [Fact]
    public async Task ReputationBiasContributor_AlwaysContributes()
    {
        var c = GetContributor("ReputationBias");
        var result = await c.ContributeAsync(CreateState("Mozilla/5.0 Chrome/120", "192.168.1.1"));
        Assert.NotEmpty(result);
        Assert.Contains(result, r => r.DetectorName == "ReputationBias");
    }

    [Fact]
    public async Task HeuristicContributor_ClassifiesNormalBrowserAsHuman()
    {
        var c = GetContributor("Heuristic");
        var signals = new Dictionary<string, object>
        {
            ["header.count"] = 15,
            ["header.has_accept"] = true,
            ["header.has_accept_language"] = true,
            ["header.has_accept_encoding"] = true
        };
        var result = await c.ContributeAsync(CreateState(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36",
            "192.168.1.1", signals));
        Assert.NotEmpty(result);
        Assert.True(result.First().ConfidenceDelta < 0, "Browser should have human signal");
    }

    [Fact]
    public async Task HeuristicContributor_ClassifiesCurlAsBot()
    {
        var c = GetContributor("Heuristic");
        var signals = new Dictionary<string, object>
        {
            ["header.count"] = 3,
            ["header.has_accept"] = true,
            ["header.has_accept_language"] = false,
            ["header.has_accept_encoding"] = false
        };
        var result = await c.ContributeAsync(CreateState("curl/7.68.0", "192.168.1.1", signals));
        Assert.NotEmpty(result);
        Assert.True(result.First().ConfidenceDelta > 0, "curl should have bot signal");
    }

    [Fact]
    public async Task HeuristicLateContributor_IncorporatesAiSignals()
    {
        var c = GetContributor("HeuristicLate");

        // Create a state with AI signals indicating "human" classification
        var aiSignals = new Dictionary<string, object>
        {
            [SignalKeys.AiPrediction] = "human",
            [SignalKeys.AiConfidence] = 0.95,
            [SignalKeys.UserAgent] = "Mozilla/5.0 Chrome/120"
        };

        // Create LLM contribution to simulate AI ran
        var llmContribution = new DetectionContribution
        {
            DetectorName = "Llm",
            Category = "AI",
            ConfidenceDelta = -0.95, // negative = human
            Weight = 2.0,
            Reason = "LLM classified as human",
            Signals = aiSignals.ToImmutableDictionary()
        };

        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0";
        ctx.Request.Headers.Accept = "text/html";
        ctx.Request.Headers.AcceptLanguage = "en-US";
        ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");

        var state = new BlackboardState
        {
            HttpContext = ctx,
            Signals = aiSignals.ToImmutableDictionary(),
            CurrentRiskScore = 0,
            CompletedDetectors = ImmutableHashSet.Create("Llm"),
            FailedDetectors = ImmutableHashSet<string>.Empty,
            Contributions = ImmutableList.Create(llmContribution),
            RequestId = Guid.NewGuid().ToString("N"),
            Elapsed = TimeSpan.FromMilliseconds(500)
        };

        var result = await c.ContributeAsync(state);
        Assert.NotEmpty(result);
        Assert.Contains(result, r => r.DetectorName == "HeuristicLate");
    }

    private IContributingDetector GetContributor(string name) =>
        _sp.GetServices<IContributingDetector>().First(c => c.Name == name);

    private static BlackboardState CreateState(string ua, string ip, Dictionary<string, object>? signals = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.UserAgent = ua;
        ctx.Connection.RemoteIpAddress = System.Net.IPAddress.TryParse(ip, out var addr) ? addr : null;

        return new BlackboardState
        {
            HttpContext = ctx,
            Signals = (signals ?? new Dictionary<string, object>()).ToImmutableDictionary(),
            CurrentRiskScore = 0,
            CompletedDetectors = ImmutableHashSet<string>.Empty,
            FailedDetectors = ImmutableHashSet<string>.Empty,
            Contributions = ImmutableList<DetectionContribution>.Empty,
            RequestId = Guid.NewGuid().ToString("N"),
            Elapsed = TimeSpan.Zero
        };
    }
}
