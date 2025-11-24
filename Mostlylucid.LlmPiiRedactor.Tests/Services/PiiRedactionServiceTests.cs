using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Mostlylucid.LlmPiiRedactor.Detectors;
using Mostlylucid.LlmPiiRedactor.Models;
using Mostlylucid.LlmPiiRedactor.Services;
using Xunit;

namespace Mostlylucid.LlmPiiRedactor.Tests.Services;

public class PiiRedactionServiceTests
{
    private readonly IPiiRedactionService _service;

    public PiiRedactionServiceTests()
    {
        var logger = Mock.Of<ILogger<PiiRedactionService>>();
        var options = Options.Create(new PiiRedactionOptions());

        var detectors = new IPiiDetector[]
        {
            new EmailDetector(),
            new PhoneDetector(),
            new CreditCardDetector(),
            new SsnDetector(),
            new IpAddressDetector()
        };

        var strategies = new IRedactionStrategy[]
        {
            new FullMaskStrategy(),
            new PartialMaskStrategy(),
            new TokenizedStrategy(),
            new TypeLabelStrategy(),
            new HashedStrategy(),
            new RemoveStrategy()
        };

        _service = new PiiRedactionService(logger, options, detectors, strategies);
    }

    [Fact]
    public void Redact_TextWithEmail_RedactsEmail()
    {
        var text = "Contact john@example.com for help";

        var result = _service.Redact(text);

        result.ContainedPii.Should().BeTrue();
        result.RedactedText.Should().NotContain("john@example.com");
        result.Matches.Should().ContainSingle();
        result.Matches[0].Type.Should().Be(PiiType.Email);
    }

    [Fact]
    public void Redact_TextWithMultiplePii_RedactsAll()
    {
        var text = "Contact john@example.com or call 555-123-4567";

        var result = _service.Redact(text);

        result.ContainedPii.Should().BeTrue();
        result.RedactedText.Should().NotContain("john@example.com");
        result.RedactedText.Should().NotContain("555-123-4567");
        result.Matches.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void Redact_TextWithoutPii_ReturnsOriginal()
    {
        var text = "This is a normal message without any PII";

        var result = _service.Redact(text);

        result.ContainedPii.Should().BeFalse();
        result.RedactedText.Should().Be(text);
        result.Matches.Should().BeEmpty();
    }

    [Fact]
    public void Redact_CreditCard_RedactsWithLuhnValidation()
    {
        var text = "Card: 4111111111111111"; // Valid Visa test number

        var result = _service.Redact(text);

        result.ContainedPii.Should().BeTrue();
        result.RedactedText.Should().NotContain("4111111111111111");
    }

    [Fact]
    public void ContainsPii_WithPii_ReturnsTrue()
    {
        var text = "Email: john@example.com";

        var containsPii = _service.ContainsPii(text);

        containsPii.Should().BeTrue();
    }

    [Fact]
    public void ContainsPii_WithoutPii_ReturnsFalse()
    {
        var text = "Hello world";

        var containsPii = _service.ContainsPii(text);

        containsPii.Should().BeFalse();
    }

    [Fact]
    public void Detect_ReturnsMatchesWithoutRedacting()
    {
        var text = "Email: john@example.com, Phone: 555-123-4567";

        var matches = _service.Detect(text);

        matches.Should().HaveCountGreaterThanOrEqualTo(2);
        matches.Should().Contain(m => m.Type == PiiType.Email);
        matches.Should().Contain(m => m.Type == PiiType.PhoneNumber);
    }

    [Fact]
    public void GetStatistics_ReturnsStats()
    {
        _service.Redact("john@example.com");
        _service.Redact("555-123-4567");

        var stats = _service.GetStatistics();

        stats.TotalScans.Should().BeGreaterThanOrEqualTo(2);
        stats.TotalRedactions.Should().BeGreaterThanOrEqualTo(2);
    }
}