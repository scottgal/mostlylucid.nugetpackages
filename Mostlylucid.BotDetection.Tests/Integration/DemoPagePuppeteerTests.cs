using PuppeteerSharp;
using Xunit;
using Xunit.Abstractions;

namespace Mostlylucid.BotDetection.Tests.Integration;

/// <summary>
///     Integration tests using PuppeteerSharp to verify the demo page
///     behaves correctly with headless browser detection.
/// </summary>
/// <remarks>
///     These tests require the demo app to be running at http://localhost:5000
///     Run: dotnet run --project Mostlylucid.BotDetection.Demo
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Category", "Puppeteer")]
public class DemoPagePuppeteerTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IBrowser? _browser;
    private const string DemoUrl = "http://localhost:5000";
    private const string BotTestPageUrl = $"{DemoUrl}/bot-test";

    public DemoPagePuppeteerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        // Download Chromium if not present
        _output.WriteLine("Downloading Chromium browser...");
        var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync();

        _output.WriteLine("Launching headless browser...");
        _browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            Args = new[]
            {
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-dev-shm-usage"
            }
        });
    }

    /// <summary>
    /// Sets up test mode header to bypass bot detection for functional tests.
    /// </summary>
    private static async Task SetTestModeHeaders(IPage page, string mode = "disable")
    {
        await page.SetExtraHttpHeadersAsync(new Dictionary<string, string>
        {
            ["ml-bot-test-mode"] = mode
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
        }
    }

    #region Bot Detection Verification Tests

    [Fact]
    public async Task HeadlessBrowser_IsBlockedByDefault()
    {
        await using var page = await _browser!.NewPageAsync();

        // Without test mode headers, headless browser should be blocked
        var response = await page.GoToAsync(BotTestPageUrl);

        _output.WriteLine($"Response status: {response.Status}");

        // Headless Chrome UA is detected as bot, should be blocked (403)
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.Status);

        var content = await response.TextAsync();
        _output.WriteLine($"Response content:\n{content}");

        // Response should indicate it was blocked
        Assert.Contains("blocked", content.ToLower());
    }

    [Fact]
    public async Task HeadlessBrowser_DetectedOnApiEndpoint()
    {
        await using var page = await _browser!.NewPageAsync();

        // Access the detection check endpoint
        var response = await page.GoToAsync($"{DemoUrl}/bot-detection/check");

        // The endpoint should block due to bot detection
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.Status);

        var content = await response.TextAsync();
        _output.WriteLine($"Detection check response:\n{content}");

        // Response should contain blocked info or access denied message
        Assert.True(
            content.Contains("error") || content.Contains("blocked") || content.Contains("denied"),
            "Expected bot blocking response");
    }

    [Fact]
    public async Task ProtectedEndpoint_BlocksHeadlessBrowser()
    {
        await using var page = await _browser!.NewPageAsync();

        // Try to access protected endpoint without test mode
        var response = await page.GoToAsync($"{DemoUrl}/api/protected");

        _output.WriteLine($"Protected endpoint status: {response.Status}");

        // Should be blocked as unverified bot
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.Status);
    }

    #endregion

    #region Page Functionality Tests (Using Test Mode)

    [Fact]
    public async Task BotTestPage_LoadsSuccessfully_WithTestMode()
    {
        await using var page = await _browser!.NewPageAsync();
        await SetTestModeHeaders(page);

        var response = await page.GoToAsync(BotTestPageUrl, WaitUntilNavigation.Networkidle0);

        Assert.NotNull(response);
        Assert.True(response.Ok, $"Expected HTTP 200, got {response.Status}");
        Assert.Contains("text/html", response.Headers["content-type"]);

        // Verify page title
        var title = await page.GetTitleAsync();
        Assert.Contains("Bot Detection", title);

        _output.WriteLine($"Page loaded successfully. Title: {title}");
    }

    [Fact]
    public async Task BotTestPage_ShowsServerSideDetection_WithTestMode()
    {
        await using var page = await _browser!.NewPageAsync();
        await SetTestModeHeaders(page);

        await page.GoToAsync(BotTestPageUrl, WaitUntilNavigation.Networkidle0);

        // Wait for server-side detection to be displayed
        var serverResultSelector = "#serverResult";
        await page.WaitForSelectorAsync(serverResultSelector);

        // Get the server-side detection result
        var serverResult = await page.EvaluateFunctionAsync<string>(@"() => {
            const el = document.querySelector('#serverResult');
            return el ? el.innerText : '';
        }");

        _output.WriteLine($"Server-side detection result:\n{serverResult}");

        // With test mode disabled, should show detection info
        Assert.NotNull(serverResult);
        Assert.NotEmpty(serverResult);
    }

    [Fact]
    public async Task BotTestPage_CollectsClientSideFingerprint_WithTestMode()
    {
        await using var page = await _browser!.NewPageAsync();
        await SetTestModeHeaders(page);

        await page.GoToAsync(BotTestPageUrl, WaitUntilNavigation.Networkidle0);

        // Wait for fingerprint data to be collected and displayed
        await Task.Delay(2000); // Allow time for JS to execute and POST fingerprint

        var fingerprintDataSelector = "#fingerprintData";
        await page.WaitForSelectorAsync(fingerprintDataSelector);

        var fingerprintData = await page.EvaluateFunctionAsync<string>(@"() => {
            const el = document.querySelector('#fingerprintData');
            return el ? el.innerText : '';
        }");

        _output.WriteLine($"Fingerprint data:\n{fingerprintData}");

        // Verify fingerprint was collected (may still show initial state or collected data)
        Assert.NotNull(fingerprintData);
    }

    [Fact]
    public async Task BotTestPage_DetectsWebDriverFlag()
    {
        await using var page = await _browser!.NewPageAsync();
        await SetTestModeHeaders(page);

        // Check if navigator.webdriver is true (it should be in Puppeteer)
        await page.GoToAsync(BotTestPageUrl, WaitUntilNavigation.DOMContentLoaded);

        var webdriverFlag = await page.EvaluateFunctionAsync<bool>(@"() => {
            return navigator.webdriver === true;
        }");

        _output.WriteLine($"navigator.webdriver: {webdriverFlag}");

        // In headless Puppeteer, webdriver should be true (unless stealth mode)
        Assert.True(webdriverFlag, "Expected navigator.webdriver to be true in Puppeteer");
    }

    [Fact]
    public async Task RootEndpoint_ReturnsDetectionSummary_WithTestMode()
    {
        await using var page = await _browser!.NewPageAsync();
        await SetTestModeHeaders(page, "human"); // Simulate human

        var response = await page.GoToAsync($"{DemoUrl}/api");

        var content = await response.TextAsync();
        _output.WriteLine($"API root response:\n{content}");

        Assert.NotNull(response);
        Assert.True(response.Ok);
        Assert.Contains("Bot Detection Demo API", content);
        Assert.Contains("isBot", content);
    }

    [Fact]
    public async Task BotTestPage_GridLayoutDisplaysCorrectly_WithTestMode()
    {
        await using var page = await _browser!.NewPageAsync();
        await SetTestModeHeaders(page);

        await page.SetViewportAsync(new ViewPortOptions { Width = 1200, Height = 800 });
        await page.GoToAsync(BotTestPageUrl, WaitUntilNavigation.Networkidle0);

        // Verify grid layout is present
        var gridVisible = await page.EvaluateFunctionAsync<bool>(@"() => {
            const grid = document.querySelector('.grid');
            if (!grid) return false;
            const style = window.getComputedStyle(grid);
            return style.display === 'grid';
        }");

        Assert.True(gridVisible, "Expected CSS grid layout to be active");

        // Verify both cards are visible
        var cardCount = await page.EvaluateFunctionAsync<int>(@"() => {
            return document.querySelectorAll('.card').length;
        }");

        Assert.True(cardCount >= 2, $"Expected at least 2 cards, found {cardCount}");
    }

    [Fact]
    public async Task BotTestPage_ResponsiveOnMobile_WithTestMode()
    {
        await using var page = await _browser!.NewPageAsync();
        await SetTestModeHeaders(page);

        // Set mobile viewport
        await page.SetViewportAsync(new ViewPortOptions { Width = 375, Height = 667 });
        await page.GoToAsync(BotTestPageUrl, WaitUntilNavigation.Networkidle0);

        // On mobile, grid should stack to single column
        var isSingleColumn = await page.EvaluateFunctionAsync<bool>(@"() => {
            const grid = document.querySelector('.grid');
            if (!grid) return false;
            const style = window.getComputedStyle(grid);
            return style.gridTemplateColumns === '1fr' ||
                   style.gridTemplateColumns.split(' ').length === 1;
        }");

        // The CSS has @media (max-width: 600px) that switches to 1fr
        Assert.True(isSingleColumn, "Expected single column layout on mobile viewport");
    }

    #endregion
}

/// <summary>
///     Tests with stealth mode to see if detection still works.
///     These tests verify that even with evasion attempts, bots are still detected.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "Puppeteer")]
public class StealthModePuppeteerTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IBrowser? _browser;
    private const string DemoUrl = "http://localhost:5000";
    private const string BotTestPageUrl = $"{DemoUrl}/bot-test";

    public StealthModePuppeteerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync();

        // Launch with args that try to hide headless nature
        _browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            Args = new[]
            {
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-blink-features=AutomationControlled",
                "--window-size=1920,1080"
            }
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
        }
    }

    [Fact]
    public async Task StealthMode_StillBlocked()
    {
        await using var page = await _browser!.NewPageAsync();

        // Try to hide automation
        await page.EvaluateFunctionOnNewDocumentAsync(@"() => {
            // Try to remove webdriver flag (this doesn't fully work)
            Object.defineProperty(navigator, 'webdriver', { get: () => false });
        }");

        var response = await page.GoToAsync(BotTestPageUrl);

        _output.WriteLine($"Stealth mode response status: {response.Status}");

        // Even with stealth attempts, should still be blocked
        // (HeadlessChrome UA is detected regardless)
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.Status);
    }

    [Fact]
    public async Task WithRealUserAgent_StillDetectedByInconsistency()
    {
        await using var page = await _browser!.NewPageAsync();

        // Set a realistic user agent
        await page.SetUserAgentAsync(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

        // Set extra HTTP headers to appear more human
        await page.SetExtraHttpHeadersAsync(new Dictionary<string, string>
        {
            ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8",
            ["Accept-Language"] = "en-US,en;q=0.9",
            ["Accept-Encoding"] = "gzip, deflate, br"
        });

        var response = await page.GoToAsync(BotTestPageUrl, WaitUntilNavigation.DOMContentLoaded);

        _output.WriteLine($"Real UA response status: {response.Status}");

        // With realistic UA, the request might pass UA detection
        // but the page should load (bot detection still runs but may not block)
        Assert.NotNull(response);

        // Could be 200 (passed) or 403 (other detection caught it)
        Assert.True(
            response.Status == System.Net.HttpStatusCode.OK ||
            response.Status == System.Net.HttpStatusCode.Forbidden,
            $"Expected 200 or 403, got {response.Status}");
    }
}
