using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Mostlylucid.YarpGateway.Tests.Fixtures;

namespace Mostlylucid.YarpGateway.Tests.Integration;

/// <summary>
/// Tests for the Admin API endpoints.
/// </summary>
public class AdminEndpointsTests : IClassFixture<GatewayTestFixture>
{
    private readonly GatewayTestFixture _fixture;

    public AdminEndpointsTests(GatewayTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Health_WithoutSecret_ReturnsUnauthorized()
    {
        // Act
        var response = await _fixture.GatewayClient.GetAsync("/admin/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Health_WithValidSecret_ReturnsOk()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/admin/health");
        request.Headers.Add("X-Admin-Secret", "test-secret");

        // Act
        var response = await _fixture.GatewayClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<HealthResponse>();
        content.Should().NotBeNull();
        content!.Status.Should().Be("ok");
        content.RoutesConfigured.Should().BeGreaterOrEqualTo(0);
        content.UptimeSeconds.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task Health_WithInvalidSecret_ReturnsUnauthorized()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/admin/health");
        request.Headers.Add("X-Admin-Secret", "wrong-secret");

        // Act
        var response = await _fixture.GatewayClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EffectiveConfig_WithValidSecret_ReturnsConfiguration()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/admin/config/effective");
        request.Headers.Add("X-Admin-Secret", "test-secret");

        // Act
        var response = await _fixture.GatewayClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<EffectiveConfigResponse>();
        content.Should().NotBeNull();
        content!.Gateway.Should().NotBeNull();
        content.Database.Should().NotBeNull();
        content.Yarp.Should().NotBeNull();
    }

    [Fact]
    public async Task ConfigSources_WithValidSecret_ReturnsSources()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/admin/config/sources");
        request.Headers.Add("X-Admin-Secret", "test-secret");

        // Act
        var response = await _fixture.GatewayClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("sources");
        json.Should().Contain("built-in");
    }

    [Fact]
    public async Task Routes_WithValidSecret_ReturnsRoutes()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/admin/routes");
        request.Headers.Add("X-Admin-Secret", "test-secret");

        // Act
        var response = await _fixture.GatewayClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<RoutesResponse>();
        content.Should().NotBeNull();
        content!.Count.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task Clusters_WithValidSecret_ReturnsClusters()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/admin/clusters");
        request.Headers.Add("X-Admin-Secret", "test-secret");

        // Act
        var response = await _fixture.GatewayClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<ClustersResponse>();
        content.Should().NotBeNull();
        content!.Count.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task Metrics_WithValidSecret_ReturnsMetrics()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/admin/metrics");
        request.Headers.Add("X-Admin-Secret", "test-secret");

        // Act
        var response = await _fixture.GatewayClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<MetricsResponse>();
        content.Should().NotBeNull();
        content!.UptimeSeconds.Should().BeGreaterOrEqualTo(0);
        content.RequestsTotal.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task FileSystems_WithValidSecret_ReturnsDirectories()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/admin/fs");
        request.Headers.Add("X-Admin-Secret", "test-secret");

        // Act
        var response = await _fixture.GatewayClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("directories");
    }

    // Response DTOs for deserialization
    private record HealthResponse(string Status, long UptimeSeconds, int RoutesConfigured, int ClustersConfigured, string Mode, string Db);
    private record EffectiveConfigResponse(GatewayConfig Gateway, DatabaseConfig Database, YarpConfig Yarp);
    private record GatewayConfig(int HttpPort, string AdminBasePath, bool HasAdminSecret, string? DefaultUpstream, string LogLevel);
    private record DatabaseConfig(string Provider, bool Enabled, bool MigrateOnStartup);
    private record YarpConfig(int RouteCount, int ClusterCount);
    private record RoutesResponse(int Count);
    private record ClustersResponse(int Count);
    private record MetricsResponse(long UptimeSeconds, long RequestsTotal, double RequestsPerSecond, long ErrorsTotal, long ActiveConnections, long BytesIn, long BytesOut);
}
