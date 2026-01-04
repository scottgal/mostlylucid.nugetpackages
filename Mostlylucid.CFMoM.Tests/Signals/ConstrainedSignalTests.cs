using System.Text.Json;
using FluentAssertions;
using Mostlylucid.CFMoM.Signals;
using Xunit;

namespace Mostlylucid.CFMoM.Tests.Signals;

public class ConstrainedSignalTests
{
    [Fact]
    public void Create_WithJsonElement_ShouldSetAllProperties()
    {
        // Arrange
        var factsJson = JsonSerializer.SerializeToElement(new { key = "value" });

        // Act
        var signal = ConstrainedSignal.Create(
            sourceId: "test-proposer",
            factsSchemaId: "test.v1",
            facts: factsJson,
            confidence: 0.85f,
            correlationId: "corr-123",
            subjectId: "subject-456");

        // Assert
        signal.Id.Should().NotBeNullOrEmpty();
        signal.SourceId.Should().Be("test-proposer");
        signal.FactsSchemaId.Should().Be("test.v1");
        signal.Confidence.Should().Be(0.85f);
        signal.CorrelationId.Should().Be("corr-123");
        signal.SubjectId.Should().Be("subject-456");
        signal.At.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        signal.TriggerEarlyExit.Should().BeFalse();
    }

    [Fact]
    public void Create_WithTypedFacts_ShouldSerializeCorrectly()
    {
        // Arrange
        var facts = new TestFacts { Intent = "question", Confidence = 0.9 };

        // Act
        var signal = ConstrainedSignal.Create(
            sourceId: "intent-classifier",
            factsSchemaId: "intent.v1",
            facts: facts,
            confidence: 0.9f);

        // Assert
        var deserializedFacts = signal.GetFacts<TestFacts>();
        deserializedFacts.Should().NotBeNull();
        deserializedFacts!.Intent.Should().Be("question");
        deserializedFacts.Confidence.Should().Be(0.9);
    }

    [Fact]
    public void Create_ConfidenceAboveOne_ShouldClampToOne()
    {
        // Act
        var signal = ConstrainedSignal.Create(
            sourceId: "test",
            factsSchemaId: "test.v1",
            facts: JsonSerializer.SerializeToElement(new { }),
            confidence: 1.5f);

        // Assert
        signal.Confidence.Should().Be(1.0f);
    }

    [Fact]
    public void Create_ConfidenceBelowZero_ShouldClampToZero()
    {
        // Act
        var signal = ConstrainedSignal.Create(
            sourceId: "test",
            factsSchemaId: "test.v1",
            facts: JsonSerializer.SerializeToElement(new { }),
            confidence: -0.5f);

        // Assert
        signal.Confidence.Should().Be(0.0f);
    }

    [Fact]
    public void WithMetadata_ShouldReturnNewSignalWithMetadata()
    {
        // Arrange
        var signal = ConstrainedSignal.Create(
            sourceId: "test",
            factsSchemaId: "test.v1",
            facts: JsonSerializer.SerializeToElement(new { }));

        // Act
        var withMetadata = signal.WithMetadata("key", "value");

        // Assert
        withMetadata.Should().NotBeSameAs(signal);
        withMetadata.Metadata.Should().ContainKey("key");
        withMetadata.Metadata["key"].Should().Be("value");
        signal.Metadata.Should().NotContainKey("key"); // Original unchanged
    }

    [Fact]
    public void WithEarlyExit_ShouldReturnNewSignalWithEarlyExitSet()
    {
        // Arrange
        var signal = ConstrainedSignal.Create(
            sourceId: "test",
            factsSchemaId: "test.v1",
            facts: JsonSerializer.SerializeToElement(new { }));

        // Act
        var withEarlyExit = signal.WithEarlyExit("whitelisted");

        // Assert
        withEarlyExit.Should().NotBeSameAs(signal);
        withEarlyExit.TriggerEarlyExit.Should().BeTrue();
        withEarlyExit.EarlyExitClassification.Should().Be("whitelisted");
        signal.TriggerEarlyExit.Should().BeFalse(); // Original unchanged
    }

    [Fact]
    public void Evidence_DefaultsToEmptyList()
    {
        // Act
        var signal = ConstrainedSignal.Create(
            sourceId: "test",
            factsSchemaId: "test.v1",
            facts: JsonSerializer.SerializeToElement(new { }));

        // Assert
        signal.Evidence.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithEvidence_ShouldIncludeEvidence()
    {
        // Arrange
        var evidence = new List<EvidenceRef>
        {
            EvidenceRef.Chunk("documents", "chunk-1", 0, 100),
            EvidenceRef.Request("logs", "req-123")
        };

        // Act
        var signal = ConstrainedSignal.Create(
            sourceId: "test",
            factsSchemaId: "test.v1",
            facts: JsonSerializer.SerializeToElement(new { }),
            evidence: evidence);

        // Assert
        signal.Evidence.Should().HaveCount(2);
        signal.Evidence[0].Kind.Should().Be("chunk");
        signal.Evidence[1].Kind.Should().Be("request");
    }

    [Fact]
    public void SignalIsImmutable_RecordWithExpression_CreatesNewInstance()
    {
        // Arrange
        var original = ConstrainedSignal.Create(
            sourceId: "original",
            factsSchemaId: "test.v1",
            facts: JsonSerializer.SerializeToElement(new { }));

        // Act
        var modified = original with { SourceId = "modified" };

        // Assert
        original.SourceId.Should().Be("original");
        modified.SourceId.Should().Be("modified");
        original.Should().NotBeSameAs(modified);
    }

    private record TestFacts
    {
        public required string Intent { get; init; }
        public required double Confidence { get; init; }
    }
}
