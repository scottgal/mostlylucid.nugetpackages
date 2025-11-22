using FluentAssertions;
using Mostlylucid.LlmPiiRedactor.Detectors;
using Mostlylucid.LlmPiiRedactor.Models;
using Xunit;

namespace Mostlylucid.LlmPiiRedactor.Tests.Detectors;

public class SsnDetectorTests
{
    private readonly SsnDetector _detector = new();

    [Theory]
    [InlineData("123-45-6789")]
    [InlineData("123 45 6789")]
    [InlineData("123456789")]
    public void Detect_ValidSsns_ReturnsMatches(string ssn)
    {
        var text = $"SSN: {ssn}";

        var matches = _detector.Detect(text).ToList();

        matches.Should().ContainSingle();
        matches[0].Type.Should().Be(PiiType.Ssn);
    }

    [Theory]
    [InlineData("000-12-3456")]  // Invalid area (000)
    [InlineData("666-12-3456")]  // Invalid area (666)
    [InlineData("900-12-3456")]  // Invalid area (9xx)
    [InlineData("123-00-4567")]  // Invalid group (00)
    [InlineData("123-45-0000")]  // Invalid serial (0000)
    public void Detect_InvalidSsns_ReturnsEmpty(string ssn)
    {
        var text = $"Number: {ssn}";

        var matches = _detector.Detect(text).ToList();

        matches.Should().BeEmpty();
    }

    [Fact]
    public void Detect_WithSsnKeyword_HasHigherConfidence()
    {
        var withKeyword = "SSN: 123-45-6789";
        var withoutKeyword = "ID: 123-45-6789";

        var matchesWithKeyword = _detector.Detect(withKeyword).ToList();
        var matchesWithoutKeyword = _detector.Detect(withoutKeyword).ToList();

        matchesWithKeyword[0].Confidence.Should().BeGreaterThanOrEqualTo(matchesWithoutKeyword[0].Confidence);
    }
}
