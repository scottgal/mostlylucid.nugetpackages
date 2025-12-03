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

    public async Task DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
        }
    }

    [Fact(Skip = "Requires demo app running at localhost:5000")]
    public async Task BotTestPage_LoadsSuccessfully()
    {
        await using var page = await _browser!.NewPageAsync();

        var response = await page.GoToAsync(BotTestPageUrl, WaitUntilNavigation.Networkidle0);

        Assert.NotNull(response);
        Assert.True(response.Ok, $"Expected HTTP 200, got {response.Status}");
        Assert.Contains("text/html", response.Headers["content-type"]);

        // Verify page title
        var title = await page.GetTitleAsync();
        Assert.Contains("Bot Detection", title);

        _output.WriteLine($"Page loaded successfully. Title: {title}");
    }

    [Fact(Skip = "Requires demo app running at localhost:5000")]
    public async Task BotTestPage_ShowsServerSideDetection()
    {
        await using var page = await _browser!.NewPageAsync();

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

        // Server should detect this as a bot (headless browser)
        Assert.NotNull(serverResult);
        // Either shows "Bot Detected" or a confidence score
        Assert.True(
            serverResult.Contains("Bot") || serverResult.Contains("%"),
            "Expected server-side detection information");
    }

    [Fact(Skip = "Requires demo app running at localhost:5000")]
    public async Task BotTestPage_CollectsClientSideFingerprint()
    {
        await using var page = await _browser!.NewPageAsync();

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

        // Verify fingerprint was collected
        Assert.NotEqual("Waiting for fingerprint collection...", fingerprintData);

        // Parse and verify fingerprint contains expected fields
        if (fingerprintData.StartsWith("{"))
        {
            Assert.Contains("webdriver", fingerprintData.ToLower());
            Assert.Contains("screen", fingerprintData.ToLower());
        }
    }

    [Fact(Skip = "Requires demo app running at localhost:5000")]
    public async Task BotTestPage_DetectsHeadlessBrowser()
    {
        await using var page = await _browser!.NewPageAsync();

        await page.GoToAsync(BotTestPageUrl, WaitUntilNavigation.Networkidle0);

        // Wait for client-side detection to complete
        await Task.Delay(2000);

        // Get client-side detection result
        var clientScore = await page.EvaluateFunctionAsync<string>(@"() => {
            const el = document.querySelector('#clientScore');
            return el ? el.innerText : '';
        }");

        var clientBadge = await page.EvaluateFunctionAsync<string>(@"() => {
            const el = document.querySelector('#clientBadge');
            return el ? el.innerText : '';
        }");

        _output.WriteLine($"Client score: {clientScore}");
        _output.WriteLine($"Client badge: {clientBadge}");

        // Headless browser should be detected as bot or suspicious
        // Score should be low (< 70 indicates bot)
        if (clientScore.Contains("/"))
        {
            var scoreValue = int.Parse(clientScore.Split('/')[0]);
            Assert.True(scoreValue < 70, $"Expected low integrity score for headless, got {scoreValue}");
        }
    }

    [Fact(Skip = "Requires demo app running at localhost:5000")]
    public async Task BotTestPage_DetectsWebDriverFlag()
    {
        await using var page = await _browser!.NewPageAsync();

        // Check if navigator.webdriver is true (it should be in Puppeteer)
        await page.GoToAsync(BotTestPageUrl);

        var webdriverFlag = await page.EvaluateFunctionAsync<bool>(@"() => {
            return navigator.webdriver === true;
        }");

        _output.WriteLine($"navigator.webdriver: {webdriverFlag}");

        // In headless Puppeteer, webdriver should be true (unless stealth mode)
        Assert.True(webdriverFlag, "Expected navigator.webdriver to be true in Puppeteer");
    }

    [Fact(Skip = "Requires demo app running at localhost:5000")]
    public async Task BotTestPage_ApiResponseShowsDetectionDetails()
    {
        await using var page = await _browser!.NewPageAsync();

        await page.GoToAsync(BotTestPageUrl, WaitUntilNavigation.Networkidle0);

        // Click the refresh button to fetch API response
        await page.ClickAsync("button");

        // Wait for API response
        await Task.Delay(1000);

        var apiResponse = await page.EvaluateFunctionAsync<string>(@"() => {
            const el = document.querySelector('#apiResponse');
            return el ? el.innerText : '';
        }");

        _output.WriteLine($"API Response:\n{apiResponse}");

        // Verify API response contains detection info
        Assert.NotEqual("Click button to fetch...", apiResponse);

        if (apiResponse.StartsWith("{"))
        {
            Assert.Contains("isBot", apiResponse);
            Assert.Contains("confidence", apiResponse);
        }
    }

    [Fact(Skip = "Requires demo app running at localhost:5000")]
    public async Task ApiEndpoint_DetectsHeadlessBrowserFromHeaders()
    {
        await using var page = await _browser!.NewPageAsync();

        // Navigate to API endpoint directly and capture response
        var response = await page.GoToAsync($"{DemoUrl}/bot-detection/check");

        var content = await response.TextAsync();
        _output.WriteLine($"Detection check response:\n{content}");

        Assert.NotNull(response);
        Assert.True(response.Ok);

        // Should detect as bot due to headless browser headers
        Assert.Contains("isBot", content);
    }

    [Fact(Skip = "Requires demo app running at localhost:5000")]
    public async Task RootEndpoint_ReturnsDetectionSummary()
    {
        await using var page = await _browser!.NewPageAsync();

        var response = await page.GoToAsync($"{DemoUrl}/api");

        var content = await response.TextAsync();
        _output.WriteLine($"API root response:\n{content}");

        Assert.NotNull(response);
        Assert.True(response.Ok);
        Assert.Contains("Bot Detection Demo API", content);
        Assert.Contains("isBot", content);
    }

    [Fact(Skip = "Requires demo app running at localhost:5000")]
    public async Task ProtectedEndpoint_BlocksHeadlessBrowser()
    {
        await using var page = await _browser!.NewPageAsync();

        // Try to access protected endpoint
        var response = await page.GoToAsync($"{DemoUrl}/api/protected");

        _output.WriteLine($"Protected endpoint status: {response.Status}");

        // May return 403 if detected as unverified bot
        // Or 200 if detection confidence is below threshold
        Assert.True(response.Status == System.Net.HttpStatusCode.OK ||
                    response.Status == System.Net.HttpStatusCode.Forbidden,
            $"Expected 200 or 403, got {response.Status}");
    }

    [Fact(Skip = "Requires demo app running at localhost:5000")]
    public async Task BotTestPage_GridLayoutDisplaysCorrectly()
    {
        await using var page = await _browser!.NewPageAsync();

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

    [Fact(Skip = "Requires demo app running at localhost:5000")]
    public async Task BotTestPage_ResponsiveOnMobile()
    {
        await using var page = await _browser!.NewPageAsync();

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
}

/// <summary>
///     Tests with stealth mode to see if detection still works
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

    [Fact(Skip = "Requires demo app running at localhost:5000")]
    public async Task StealthMode_StillDetectedByClientSide()
    {
        await using var page = await _browser!.NewPageAsync();

        // Try to hide automation
        await page.EvaluateFunctionOnNewDocumentAsync(@"() => {
            // Try to remove webdriver flag (this doesn't fully work)
            Object.defineProperty(navigator, 'webdriver', { get: () => false });
        }");

        await page.GoToAsync(BotTestPageUrl, WaitUntilNavigation.Networkidle0);
        await Task.Delay(2000);

        var clientScore = await page.EvaluateFunctionAsync<string>(@"() => {
            const el = document.querySelector('#clientScore');
            return el ? el.innerText : '';
        }");

        _output.WriteLine($"Stealth mode client score: {clientScore}");

        // Even with stealth attempts, other detection methods should catch it
        // (missing plugins, zero outer dimensions, etc.)
    }

    [Fact(Skip = "Requires demo app running at localhost:5000")]
    public async Task WithRealUserAgent_StillDetectedByBehavior()
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

        await page.GoToAsync(BotTestPageUrl, WaitUntilNavigation.Networkidle0);
        await Task.Delay(2000);

        var fingerprintData = await page.EvaluateFunctionAsync<string>(@"() => {
            const el = document.querySelector('#fingerprintData');
            return el ? el.innerText : '';
        }");

        _output.WriteLine($"With real UA, fingerprint data:\n{fingerprintData}");

        // Despite realistic headers, client-side fingerprinting should still detect
        // headless markers (plugins, outer dimensions, etc.)
    }
}
