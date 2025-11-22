using FluentAssertions;
using Mostlylucid.LlmPiiRedactor.Detectors;
using Mostlylucid.LlmPiiRedactor.Models;

namespace Mostlylucid.LlmPiiRedactor.Tests.Detectors;

public class PhoneDetectorTests
{
    private readonly PhoneDetector _detector = new();

    [Theory]
    [InlineData("+1-234-567-8901")]
    [InlineData("(123) 456-7890")]
    [InlineData("123.456.7890")]
    [InlineData("+44 20 7946 0958")]
    [InlineData("555-123-4567")]
    [InlineData("1234567890")]
    public void Detect_ValidPhoneNumbers_ReturnsMatches(string phone)
    {
        var text = $"Call us at {phone}";

        var matches = _detector.Detect(text).ToList();

        matches.Should().ContainSingle();
        matches[0].Type.Should().Be(PiiType.PhoneNumber);
    }

    [Theory]
    [InlineData("123")]  // Too short
    [InlineData("1990")]  // Looks like a year
    [InlineData("2024")]  // Looks like a year
    public void Detect_InvalidPhoneNumbers_ReturnsEmpty(string text)
    {
        var matches = _detector.Detect(text).ToList();

        matches.Should().BeEmpty();
    }

    [Fact]
    public void Detect_FormattedPhone_HasHigherConfidence()
    {
        var formatted = "(555) 123-4567";
        var unformatted = "5551234567";

        var formattedMatches = _detector.Detect($"Call {formatted}").ToList();
        var unformattedMatches = _detector.Detect($"Call {unformatted}").ToList();

        formattedMatches[0].Confidence.Should().BeGreaterThan(unformattedMatches[0].Confidence);
    }
}
