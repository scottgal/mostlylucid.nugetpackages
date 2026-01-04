using System.Text.Json;
using FluentAssertions;
using Mostlylucid.CFMoM.Signals;
using Xunit;

namespace Mostlylucid.CFMoM.Tests.Signals;

public class EvidenceRefTests
{
    [Fact]
    public void Chunk_WithSpan_ShouldCreateCorrectReference()
    {
        // Act
        var evidence = EvidenceRef.Chunk("documents", "chunk-42", start: 100, end: 500);

        // Assert
        evidence.Kind.Should().Be("chunk");
        evidence.Store.Should().Be("documents");
        evidence.Id.Should().Be("chunk-42");
        evidence.Locator.Should().NotBeNull();

        var locator = evidence.Locator!.Value;
        locator.GetProperty("start").GetInt32().Should().Be(100);
        locator.GetProperty("end").GetInt32().Should().Be(500);
    }

    [Fact]
    public void Chunk_WithoutSpan_ShouldHaveNullLocator()
    {
        // Act
        var evidence = EvidenceRef.Chunk("documents", "chunk-1");

        // Assert
        evidence.Kind.Should().Be("chunk");
        evidence.Locator.Should().BeNull();
    }

    [Fact]
    public void Chunk_WithHash_ShouldIncludeContentHash()
    {
        // Act
        var evidence = EvidenceRef.Chunk("documents", "chunk-1", hash: "sha256:abc123");

        // Assert
        evidence.ContentHash.Should().Be("sha256:abc123");
    }

    [Fact]
    public void Frame_WithBoundingBox_ShouldCreateCorrectReference()
    {
        // Act
        var evidence = EvidenceRef.Frame("video-frames", "frame-0042", x: 100, y: 200, w: 50, h: 75);

        // Assert
        evidence.Kind.Should().Be("frame");
        evidence.Store.Should().Be("video-frames");
        evidence.Id.Should().Be("frame-0042");
        evidence.Locator.Should().NotBeNull();

        var locator = evidence.Locator!.Value;
        locator.GetProperty("x").GetInt32().Should().Be(100);
        locator.GetProperty("y").GetInt32().Should().Be(200);
        locator.GetProperty("w").GetInt32().Should().Be(50);
        locator.GetProperty("h").GetInt32().Should().Be(75);
    }

    [Fact]
    public void Frame_WithoutBoundingBox_ShouldHaveNullLocator()
    {
        // Act
        var evidence = EvidenceRef.Frame("video-frames", "frame-0001");

        // Assert
        evidence.Kind.Should().Be("frame");
        evidence.Locator.Should().BeNull();
    }

    [Fact]
    public void Timestamp_ShouldCreateCorrectReference()
    {
        // Arrange
        var position = TimeSpan.FromMinutes(13) + TimeSpan.FromSeconds(21) + TimeSpan.FromMilliseconds(200);
        var duration = TimeSpan.FromSeconds(5);

        // Act
        var evidence = EvidenceRef.Timestamp("audio", position, duration);

        // Assert
        evidence.Kind.Should().Be("timestamp");
        evidence.Store.Should().Be("audio");
        evidence.Id.Should().StartWith("t=");
        evidence.Locator.Should().NotBeNull();

        var locator = evidence.Locator!.Value;
        locator.GetProperty("position").GetString().Should().NotBeNullOrEmpty();
        locator.GetProperty("duration").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Timestamp_WithoutDuration_ShouldHaveNullDurationInLocator()
    {
        // Arrange
        var position = TimeSpan.FromSeconds(30);

        // Act
        var evidence = EvidenceRef.Timestamp("audio", position);

        // Assert
        evidence.Kind.Should().Be("timestamp");
        var locator = evidence.Locator!.Value;
        locator.GetProperty("duration").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void Request_ShouldCreateCorrectReference()
    {
        // Act
        var evidence = EvidenceRef.Request("logs", "req-abc123", "sha256:xyz");

        // Assert
        evidence.Kind.Should().Be("request");
        evidence.Store.Should().Be("logs");
        evidence.Id.Should().Be("req-abc123");
        evidence.Locator.Should().BeNull();
        evidence.ContentHash.Should().Be("sha256:xyz");
    }

    [Fact]
    public void Row_ShouldCreateCorrectReference()
    {
        // Act
        var evidence = EvidenceRef.Row("database", "users:12345");

        // Assert
        evidence.Kind.Should().Be("row");
        evidence.Store.Should().Be("database");
        evidence.Id.Should().Be("users:12345");
        evidence.Locator.Should().BeNull();
    }

    [Fact]
    public void EvidenceRef_IsValueType_ShouldBeEqualByValue()
    {
        // Arrange
        var evidence1 = EvidenceRef.Request("logs", "req-123");
        var evidence2 = EvidenceRef.Request("logs", "req-123");

        // Assert
        evidence1.Should().Be(evidence2);
        evidence1.GetHashCode().Should().Be(evidence2.GetHashCode());
    }

    [Fact]
    public void EmbeddingRef_ShouldStoreModelAndVector()
    {
        // Arrange
        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };

        // Act
        var embeddingRef = new EmbeddingRef("all-MiniLM-L6-v2", vector);

        // Assert
        embeddingRef.ModelId.Should().Be("all-MiniLM-L6-v2");
        embeddingRef.Vector.Should().BeEquivalentTo(vector);
    }
}
