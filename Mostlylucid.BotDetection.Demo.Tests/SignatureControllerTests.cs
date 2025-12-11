using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Mostlylucid.BotDetection.Demo.Controllers;
using Mostlylucid.BotDetection.Demo.Services;
using Mostlylucid.BotDetection.Orchestration;
using Xunit;

namespace Mostlylucid.BotDetection.Demo.Tests;

public class SignatureControllerTests
{
    [Fact]
    public void GetSignature_ShouldReturnSignatureWhenFound()
    {
        // Arrange
        var store = new SignatureStore(TestHelpers.CreateMockLogger<SignatureStore>().Object);
        var controller = new SignatureController(store, TestHelpers.CreateMockLogger<SignatureController>().Object);

        var signature = CreateTestStoredSignature("sig-123");
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
        store.StoreSignature("sig-123", signature.Evidence, httpContext);

        // Act
        var result = controller.GetSignature("sig-123");

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().Be(signature);
    }

    [Fact]
    public void GetSignature_ShouldReturnNotFoundWhenSignatureDoesNotExist()
    {
        // Arrange
        var store = new SignatureStore(TestHelpers.CreateMockLogger<SignatureStore>().Object);
        var controller = new SignatureController(store, TestHelpers.CreateMockLogger<SignatureController>().Object);

        // Act
        var result = controller.GetSignature("nonexistent");

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void GetRecentSignatures_ShouldReturnSignatures()
    {
        // Arrange
        var store = new SignatureStore(TestHelpers.CreateMockLogger<SignatureStore>().Object);
        var controller = new SignatureController(store, TestHelpers.CreateMockLogger<SignatureController>().Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");

        // Add test signatures
        for (int i = 1; i <= 3; i++)
        {
            var sig = CreateTestStoredSignature($"sig-{i}");
            store.StoreSignature($"sig-{i}", sig.Evidence, httpContext);
        }

        // Act
        var result = controller.GetRecentSignatures(count: 50);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        var returnedSigs = okResult!.Value as List<StoredSignature>;
        returnedSigs.Should().NotBeNull();
        returnedSigs!.Should().HaveCount(3);
    }

    [Fact]
    public void GetStats_ShouldReturnStatistics()
    {
        // Arrange
        var store = new SignatureStore(TestHelpers.CreateMockLogger<SignatureStore>().Object);
        var controller = new SignatureController(store, TestHelpers.CreateMockLogger<SignatureController>().Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");

        // Add some signatures
        store.StoreSignature("bot-1", TestHelpers.CreateTestEvidence(0.9), httpContext);
        store.StoreSignature("human-1", TestHelpers.CreateTestEvidence(0.3), httpContext);

        // Act
        var result = controller.GetStats();

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        var stats = okResult!.Value as SignatureStoreStats;
        stats.Should().NotBeNull();
        stats!.TotalSignatures.Should().Be(2);
    }

    [Fact]
    public void GetCurrentSignature_ShouldReturnSignatureFromHeader()
    {
        // Arrange
        var store = new SignatureStore(TestHelpers.CreateMockLogger<SignatureStore>().Object);
        var controller = new SignatureController(store, TestHelpers.CreateMockLogger<SignatureController>().Object);

        // Setup HttpContext with header
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Signature-ID"] = "current-sig-123";
        httpContext.Request.Headers["X-Bot-Detected"] = "true";
        httpContext.Request.Headers["X-Bot-Confidence"] = "0.85";

        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Store a signature
        var signature = CreateTestStoredSignature("current-sig-123");
        store.StoreSignature("current-sig-123", signature.Evidence, httpContext);

        // Act
        var result = controller.GetCurrentSignature();

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        var response = okResult!.Value as dynamic;
        response.Should().NotBeNull();
    }

    [Fact]
    public void GetCurrentSignature_ShouldReturnNotFoundWhenNoHeader()
    {
        // Arrange
        var store = new SignatureStore(TestHelpers.CreateMockLogger<SignatureStore>().Object);
        var controller = new SignatureController(store, TestHelpers.CreateMockLogger<SignatureController>().Object);

        // Setup HttpContext without X-Signature-ID header
        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Act
        var result = controller.GetCurrentSignature();

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    private static StoredSignature CreateTestStoredSignature(string signatureId)
    {
        return new StoredSignature
        {
            SignatureId = signatureId,
            Timestamp = DateTime.UtcNow,
            Evidence = TestHelpers.CreateTestEvidence(0.8),
            RequestMetadata = TestHelpers.CreateTestRequestMetadata("/test", "TestBot/1.0", "127.0.0.1")
        };
    }
}
