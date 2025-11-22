using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Mostlylucid.GeoDetection.Models;
using Mostlylucid.GeoDetection.Services;
using System.Net;

namespace Mostlylucid.GeoDetection.Test.Services;

/// <summary>
///     Tests for DataHubGeoLocationService (CSV-based IP lookup)
/// </summary>
public class DataHubGeoLocationServiceTests
{
    private readonly Mock<ILogger<DataHubGeoLocationService>> _loggerMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly IMemoryCache _memoryCache;
    private readonly IOptions<GeoLite2Options> _options;

    public DataHubGeoLocationServiceTests()
    {
        _loggerMock = new Mock<ILogger<DataHubGeoLocationService>>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _options = Options.Create(new GeoLite2Options
        {
            Provider = GeoProvider.DataHubCsv,
            CacheDuration = TimeSpan.FromMinutes(5)
        });
    }

    [Fact]
    public void Constructor_DoesNotThrow()
    {
        // Arrange & Act
        var service = CreateService();

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public async Task GetLocationAsync_InvalidIp_ReturnsNull()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GetLocationAsync("not-an-ip");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetLocationAsync_PrivateIp_ReturnsPrivateNetwork()
    {
        // Arrange
        var service = CreateService();

        // Act - Test various private IP ranges
        var result1 = await service.GetLocationAsync("192.168.1.1");
        var result2 = await service.GetLocationAsync("10.0.0.1");
        var result3 = await service.GetLocationAsync("172.16.0.1");
        var result4 = await service.GetLocationAsync("127.0.0.1");

        // Assert
        Assert.NotNull(result1);
        Assert.Equal("XX", result1.CountryCode);
        Assert.Equal("Private Network", result1.CountryName);

        Assert.NotNull(result2);
        Assert.Equal("XX", result2.CountryCode);

        Assert.NotNull(result3);
        Assert.Equal("XX", result3.CountryCode);

        Assert.NotNull(result4);
        Assert.Equal("XX", result4.CountryCode);
    }

    [Fact]
    public async Task GetLocationAsync_LinkLocalIp_ReturnsPrivateNetwork()
    {
        // Arrange
        var service = CreateService();

        // Act - Link-local addresses (169.254.x.x)
        var result = await service.GetLocationAsync("169.254.1.1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("XX", result.CountryCode);
    }

    [Fact]
    public async Task GetLocationAsync_IPv6_ReturnsNull()
    {
        // Arrange (DataHub only supports IPv4)
        var service = CreateService();

        // Act
        var result = await service.GetLocationAsync("2001:4860:4860::8888");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetStatistics_ReturnsValidStatistics()
    {
        // Arrange
        var service = CreateService();

        // Act
        var stats = service.GetStatistics();

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(0, stats.TotalLookups);
        Assert.Equal(0, stats.CacheHits);
    }

    [Fact]
    public async Task GetLocationAsync_IncrementsStatistics()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.GetLocationAsync("192.168.1.1");
        await service.GetLocationAsync("10.0.0.1");
        var stats = service.GetStatistics();

        // Assert
        Assert.Equal(2, stats.TotalLookups);
    }

    [Fact]
    public async Task IsFromCountryAsync_PrivateIp_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.IsFromCountryAsync("192.168.1.1", "US");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetLocationAsync_CachesResults()
    {
        // Arrange
        var service = CreateService();
        var ip = "192.168.1.1";

        // Act - First call
        await service.GetLocationAsync(ip);
        var statsAfterFirst = service.GetStatistics();

        // Second call (should hit cache)
        await service.GetLocationAsync(ip);
        var statsAfterSecond = service.GetStatistics();

        // Assert
        Assert.Equal(2, statsAfterSecond.TotalLookups);
        Assert.Equal(1, statsAfterSecond.CacheHits);
    }

    private DataHubGeoLocationService CreateService()
    {
        // Setup mock HTTP client
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("")
            });

        var httpClient = new HttpClient(mockHandler.Object);
        _httpClientFactoryMock
            .Setup(f => f.CreateClient("DataHub"))
            .Returns(httpClient);

        return new DataHubGeoLocationService(
            _loggerMock.Object,
            _options,
            _httpClientFactoryMock.Object,
            _memoryCache);
    }
}
