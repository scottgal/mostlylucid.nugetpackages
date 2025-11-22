using FluentAssertions;
using Mostlylucid.LlmPiiRedactor.Detectors;
using Mostlylucid.LlmPiiRedactor.Models;

namespace Mostlylucid.LlmPiiRedactor.Tests.Detectors;

public class EmailDetectorTests
{
    private readonly EmailDetector _detector = new();

    [Theory]
    [InlineData("john@example.com")]
    [InlineData("john.doe@example.com")]
    [InlineData("john+tag@example.com")]
    [InlineData("john.doe@subdomain.example.com")]
    [InlineData("john_doe@example.co.uk")]
    [InlineData("JOHN@EXAMPLE.COM")]
    public void Detect_ValidEmails_ReturnsMatches(string email)
    {
        var text = $"Contact us at {email} for more info";

        var matches = _detector.Detect(text).ToList();

        matches.Should().ContainSingle();
        matches[0].OriginalValue.Should().Be(email);
        matches[0].Type.Should().Be(PiiType.Email);
    }

    [Theory]
    [InlineData("john@")]
    [InlineData("@example.com")]
    [InlineData("john@.com")]
    [InlineData("john@example")]
    [InlineData("notanemail")]
    public void Detect_InvalidEmails_ReturnsEmpty(string text)
    {
        var matches = _detector.Detect(text).ToList();

        matches.Should().BeEmpty();
    }

    [Fact]
    public void Detect_MultipleEmails_ReturnsAllMatches()
    {
        var text = "Contact john@example.com or jane@example.org for help";

        var matches = _detector.Detect(text).ToList();

        matches.Should().HaveCount(2);
        matches.Select(m => m.OriginalValue).Should()
            .Contain("john@example.com")
            .And.Contain("jane@example.org");
    }

    [Fact]
    public void Detect_CommonDomains_HasHighConfidence()
    {
        var text = "Email me at user@gmail.com";

        var matches = _detector.Detect(text).ToList();

        matches.Should().ContainSingle();
        matches[0].Confidence.Should().Be(1.0);
    }
}
