using FluentAssertions;
using Mostlylucid.LlmPiiRedactor.Detectors;
using Mostlylucid.LlmPiiRedactor.Models;

namespace Mostlylucid.LlmPiiRedactor.Tests.Detectors;

public class CreditCardDetectorTests
{
    private readonly CreditCardDetector _detector = new();

    [Theory]
    [InlineData("4111111111111111", "Visa")]  // Visa test number
    [InlineData("4111-1111-1111-1111", "Visa with dashes")]
    [InlineData("4111 1111 1111 1111", "Visa with spaces")]
    [InlineData("5500000000000004", "MasterCard")]  // MasterCard test number
    [InlineData("340000000000009", "Amex")]  // Amex test number
    [InlineData("6011000000000004", "Discover")]  // Discover test number
    public void Detect_ValidCreditCards_ReturnsMatches(string cardNumber, string description)
    {
        var text = $"Payment card: {cardNumber}";

        var matches = _detector.Detect(text).ToList();

        matches.Should().ContainSingle($"Should detect {description}");
        matches[0].Type.Should().Be(PiiType.CreditCard);
    }

    [Theory]
    [InlineData("1234567890123456")]  // Fails Luhn check
    [InlineData("4111111111111112")]  // Fails Luhn check (wrong check digit)
    [InlineData("123456789")]  // Too short
    public void Detect_InvalidCreditCards_ReturnsEmpty(string cardNumber)
    {
        var text = $"Number: {cardNumber}";

        var matches = _detector.Detect(text).ToList();

        matches.Should().BeEmpty("Invalid cards should not match");
    }

    [Fact]
    public void Detect_CardInText_ReturnsCorrectPosition()
    {
        var text = "Payment processed for card 4111111111111111 successfully";

        var matches = _detector.Detect(text).ToList();

        matches.Should().ContainSingle();
        matches[0].StartIndex.Should().Be(27);
        matches[0].Length.Should().Be(16);
    }
}
