using System.Text.Json;
using FluentAssertions;
using Mostlylucid.CFMoM.ConsensusSpace;
using Mostlylucid.CFMoM.Signals;
using NSubstitute;
using Xunit;

namespace Mostlylucid.CFMoM.Tests.ConsensusSpace;

public class ConsensusSpaceTests
{
    private static ConstrainedSignal CreateTestSignal(
        string sourceId = "test-source",
        string schemaId = "test.v1",
        float confidence = 0.5f,
        string? correlationId = null,
        string? subjectId = null,
        bool triggerEarlyExit = false,
        string? earlyExitClassification = null)
    {
        var signal = ConstrainedSignal.Create(
            sourceId: sourceId,
            factsSchemaId: schemaId,
            facts: JsonSerializer.SerializeToElement(new { test = true }),
            confidence: confidence,
            correlationId: correlationId,
            subjectId: subjectId);

        if (triggerEarlyExit)
        {
            signal = signal.WithEarlyExit(earlyExitClassification ?? "verified");
        }

        return signal;
    }

    [Fact]
    public void Ingest_ValidSignal_ShouldReturnTrue()
    {
        // Arrange
        var space = new CFMoM.ConsensusSpace.ConsensusSpace();
        var signal = CreateTestSignal();

        // Act
        var result = space.Ingest(signal, out var rejectionReason);

        // Assert
        result.Should().BeTrue();
        rejectionReason.Should().BeNull();
    }

    [Fact]
    public void Ingest_Signal_ShouldBeRetrievable()
    {
        // Arrange
        var space = new CFMoM.ConsensusSpace.ConsensusSpace();
        var signal = CreateTestSignal();
        space.Ingest(signal, out _);

        // Act
        var signals = space.GetSignals();

        // Assert
        signals.Should().HaveCount(1);
        signals[0].Id.Should().Be(signal.Id);
    }

    [Fact]
    public void GetSignalsFrom_ShouldFilterBySourceId()
    {
        // Arrange
        var space = new CFMoM.ConsensusSpace.ConsensusSpace();
        space.Ingest(CreateTestSignal(sourceId: "source-a"), out _);
        space.Ingest(CreateTestSignal(sourceId: "source-b"), out _);
        space.Ingest(CreateTestSignal(sourceId: "source-a"), out _);

        // Act
        var signalsFromA = space.GetSignalsFrom("source-a");
        var signalsFromB = space.GetSignalsFrom("source-b");

        // Assert
        signalsFromA.Should().HaveCount(2);
        signalsFromB.Should().HaveCount(1);
    }

    [Fact]
    public void GetSignalsBySchema_ShouldFilterBySchemaId()
    {
        // Arrange
        var space = new CFMoM.ConsensusSpace.ConsensusSpace();
        space.Ingest(CreateTestSignal(schemaId: "intent.v1"), out _);
        space.Ingest(CreateTestSignal(schemaId: "sentiment.v1"), out _);
        space.Ingest(CreateTestSignal(schemaId: "intent.v1"), out _);

        // Act
        var intentSignals = space.GetSignalsBySchema("intent.v1");
        var sentimentSignals = space.GetSignalsBySchema("sentiment.v1");

        // Assert
        intentSignals.Should().HaveCount(2);
        sentimentSignals.Should().HaveCount(1);
    }

    [Fact]
    public void GetSignalsForSubject_ShouldFilterBySubjectId()
    {
        // Arrange
        var space = new CFMoM.ConsensusSpace.ConsensusSpace();
        space.Ingest(CreateTestSignal(subjectId: "user-123"), out _);
        space.Ingest(CreateTestSignal(subjectId: "user-456"), out _);

        // Act
        var user123Signals = space.GetSignalsForSubject("user-123");

        // Assert
        user123Signals.Should().HaveCount(1);
    }

    [Fact]
    public void GetSignalsByCorrelation_ShouldFilterByCorrelationId()
    {
        // Arrange
        var space = new CFMoM.ConsensusSpace.ConsensusSpace();
        space.Ingest(CreateTestSignal(correlationId: "corr-1"), out _);
        space.Ingest(CreateTestSignal(correlationId: "corr-2"), out _);
        space.Ingest(CreateTestSignal(correlationId: "corr-1"), out _);

        // Act
        var corr1Signals = space.GetSignalsByCorrelation("corr-1");

        // Assert
        corr1Signals.Should().HaveCount(2);
    }

    [Fact]
    public void HasEarlyExit_NoEarlyExitSignal_ShouldBeFalse()
    {
        // Arrange
        var space = new CFMoM.ConsensusSpace.ConsensusSpace();
        space.Ingest(CreateTestSignal(), out _);

        // Assert
        space.HasEarlyExit.Should().BeFalse();
        space.EarlyExitSignal.Should().BeNull();
    }

    [Fact]
    public void HasEarlyExit_WithEarlyExitSignal_ShouldBeTrue()
    {
        // Arrange
        var space = new CFMoM.ConsensusSpace.ConsensusSpace();
        var earlyExitSignal = CreateTestSignal(triggerEarlyExit: true, earlyExitClassification: "whitelisted");
        space.Ingest(earlyExitSignal, out _);

        // Assert
        space.HasEarlyExit.Should().BeTrue();
        space.EarlyExitSignal.Should().NotBeNull();
        space.EarlyExitSignal!.EarlyExitClassification.Should().Be("whitelisted");
    }

    [Fact]
    public void EarlyExitSignal_OnlyFirstIsStored()
    {
        // Arrange
        var space = new CFMoM.ConsensusSpace.ConsensusSpace();
        var firstExit = CreateTestSignal(sourceId: "first", triggerEarlyExit: true, earlyExitClassification: "first-exit");
        var secondExit = CreateTestSignal(sourceId: "second", triggerEarlyExit: true, earlyExitClassification: "second-exit");

        space.Ingest(firstExit, out _);
        space.Ingest(secondExit, out _);

        // Assert
        space.EarlyExitSignal!.SourceId.Should().Be("first");
        space.EarlyExitSignal.EarlyExitClassification.Should().Be("first-exit");
    }

    [Fact]
    public void Clear_ShouldRemoveAllSignals()
    {
        // Arrange
        var space = new CFMoM.ConsensusSpace.ConsensusSpace();
        space.Ingest(CreateTestSignal(), out _);
        space.Ingest(CreateTestSignal(triggerEarlyExit: true), out _);

        // Act
        space.Clear();

        // Assert
        space.GetSignals().Should().BeEmpty();
        space.HasEarlyExit.Should().BeFalse();
        space.EarlyExitSignal.Should().BeNull();
    }

    [Fact]
    public void Ingest_MaxCapacityReached_ShouldReject()
    {
        // Arrange
        var options = new ConsensusSpaceOptions { MaxSignals = 2 };
        var space = new CFMoM.ConsensusSpace.ConsensusSpace(options);

        space.Ingest(CreateTestSignal(), out _);
        space.Ingest(CreateTestSignal(), out _);

        // Act
        var result = space.Ingest(CreateTestSignal(), out var rejectionReason);

        // Assert
        result.Should().BeFalse();
        rejectionReason.Should().Contain("capacity");
    }

    [Fact]
    public void Ingest_WithSchemaValidator_ValidSignal_ShouldAccept()
    {
        // Arrange
        var space = new CFMoM.ConsensusSpace.ConsensusSpace();
        var validator = Substitute.For<ISchemaValidator>();
        validator.Validate(Arg.Any<ConstrainedSignal>(), out Arg.Any<string?>())
            .Returns(x =>
            {
                x[1] = null;
                return true;
            });

        space.RegisterSchema("test.v1", validator);
        var signal = CreateTestSignal(schemaId: "test.v1");

        // Act
        var result = space.Ingest(signal, out var rejectionReason);

        // Assert
        result.Should().BeTrue();
        rejectionReason.Should().BeNull();
    }

    [Fact]
    public void Ingest_WithSchemaValidator_InvalidSignal_ShouldReject()
    {
        // Arrange
        var space = new CFMoM.ConsensusSpace.ConsensusSpace();
        var validator = Substitute.For<ISchemaValidator>();
        validator.Validate(Arg.Any<ConstrainedSignal>(), out Arg.Any<string?>())
            .Returns(x =>
            {
                x[1] = "Invalid field X";
                return false;
            });

        space.RegisterSchema("test.v1", validator);
        var signal = CreateTestSignal(schemaId: "test.v1");

        // Act
        var result = space.Ingest(signal, out var rejectionReason);

        // Assert
        result.Should().BeFalse();
        rejectionReason.Should().Contain("Invalid field X");
    }

    [Fact]
    public void Ingest_RequireRegisteredSchema_UnknownSchema_ShouldReject()
    {
        // Arrange
        var options = new ConsensusSpaceOptions { RequireRegisteredSchema = true };
        var space = new CFMoM.ConsensusSpace.ConsensusSpace(options);
        var signal = CreateTestSignal(schemaId: "unknown.v1");

        // Act
        var result = space.Ingest(signal, out var rejectionReason);

        // Assert
        result.Should().BeFalse();
        rejectionReason.Should().Contain("No validator registered");
    }

    [Fact]
    public void Ingest_DoNotRequireRegisteredSchema_UnknownSchema_ShouldAccept()
    {
        // Arrange
        var options = new ConsensusSpaceOptions { RequireRegisteredSchema = false };
        var space = new CFMoM.ConsensusSpace.ConsensusSpace(options);
        var signal = CreateTestSignal(schemaId: "unknown.v1");

        // Act
        var result = space.Ingest(signal, out _);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void PassThroughValidator_ShouldAlwaysReturnTrue()
    {
        // Arrange
        var validator = new PassThroughValidator("test.v1");
        var signal = CreateTestSignal();

        // Act
        var result = validator.Validate(signal, out var error);

        // Assert
        result.Should().BeTrue();
        error.Should().BeNull();
        validator.SchemaId.Should().Be("test.v1");
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentIngestion_ShouldNotLoseSignals()
    {
        // Arrange
        var space = new CFMoM.ConsensusSpace.ConsensusSpace();
        var signalCount = 100;
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < signalCount; i++)
        {
            var signalIndex = i;
            tasks.Add(Task.Run(() =>
            {
                var signal = CreateTestSignal(sourceId: $"source-{signalIndex}");
                space.Ingest(signal, out _);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        space.GetSignals().Should().HaveCount(signalCount);
    }
}
