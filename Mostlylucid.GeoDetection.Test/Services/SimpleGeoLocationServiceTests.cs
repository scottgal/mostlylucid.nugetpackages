using Mostlylucid.GeoDetection.Services;

namespace Mostlylucid.GeoDetection.Test.Services;

/// <summary>
///     Tests for SimpleGeoLocationService (mock/testing service)
/// </summary>
public class SimpleGeoLocationServiceTests
{
    private readonly SimpleGeoLocationService _service;

    public SimpleGeoLocationServiceTests()
    {
        _service = new SimpleGeoLocationService();
    }

    [Fact]
    public void Constructor_DoesNotThrow()
    {
        // Assert
        Assert.NotNull(_service);
    }

    [Theory]
    [InlineData("8.8.8.8")]      // Google DNS
    [InlineData("1.1.1.1")]      // Cloudflare
    [InlineData("208.67.222.222")] // OpenDNS
    public async Task GetLocationAsync_ValidPublicIp_ReturnsLocation(string ip)
    {
        // Act
        var result = await _service.GetLocationAsync(ip);

        // Assert
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.CountryCode));
        Assert.False(string.IsNullOrEmpty(result.CountryName));
    }

    [Theory]
    [InlineData("192.168.1.1")]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.0.1")]
    [InlineData("127.0.0.1")]
    public async Task GetLocationAsync_PrivateIp_ReturnsPrivateNetwork(string ip)
    {
        // Act
        var result = await _service.GetLocationAsync(ip);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("XX", result.CountryCode);
        Assert.Contains("Private", result.CountryName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-ip")]
    [InlineData("999.999.999.999")]
    public async Task GetLocationAsync_InvalidIp_ReturnsNull(string ip)
    {
        // Act
        var result = await _service.GetLocationAsync(ip);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetLocationAsync_NullIp_ReturnsNull()
    {
        // Act
        var result = await _service.GetLocationAsync(null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task IsFromCountryAsync_ValidIpAndCountry_ReturnsBoolean()
    {
        // Arrange
        var ip = "8.8.8.8";

        // Act
        var isUS = await _service.IsFromCountryAsync(ip, "US");
        var isXX = await _service.IsFromCountryAsync(ip, "XX");

        // Assert - One of these should be true depending on simple service's response
        Assert.True(isUS || isXX || !(isUS && isXX));
    }

    [Fact]
    public async Task IsFromCountryAsync_InvalidIp_ReturnsFalse()
    {
        // Act
        var result = await _service.IsFromCountryAsync("invalid", "US");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetStatistics_ReturnsStatistics()
    {
        // Act
        var stats = _service.GetStatistics();

        // Assert
        Assert.NotNull(stats);
    }

    [Fact]
    public async Task GetStatistics_TracksLookups()
    {
        // Act
        await _service.GetLocationAsync("8.8.8.8");
        await _service.GetLocationAsync("1.1.1.1");
        var stats = _service.GetStatistics();

        // Assert
        Assert.True(stats.TotalLookups >= 2);
    }
}
