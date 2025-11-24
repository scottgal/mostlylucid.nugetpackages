using Microsoft.Extensions.Logging;
using Mostlylucid.LLMContentModeration.Models;
using Mostlylucid.LLMContentModeration.Services;

namespace Mostlylucid.LLMContentModeration.Test;

public class PiiDetectorTests
{
    private readonly PiiDetectionOptions _defaultOptions;
    private readonly PiiDetector _detector;

    public PiiDetectorTests()
    {
        var logger = Mock.Of<ILogger<PiiDetector>>();
        _detector = new PiiDetector(logger);
        _defaultOptions = new PiiDetectionOptions
        {
            Enabled = true,
            DetectEmail = true,
            DetectPhone = true,
            DetectAddress = true,
            DetectIban = true,
            DetectCreditCard = true,
            DetectSocialSecurityNumber = true,
            MaskCharacter = '*',
            UnmaskedChars = 2
        };
    }

    #region Phone Detection Tests

    [Theory]
    [InlineData("Call me at +1-555-123-4567")]
    [InlineData("Phone: (555) 123-4567")]
    [InlineData("My number is 555.123.4567")]
    [InlineData("+44 20 7946 0958")]
    public void DetectPii_ValidPhone_DetectsPhone(string content)
    {
        var matches = _detector.DetectPii(content, _defaultOptions);

        Assert.Contains(matches, m => m.Type == PiiType.Phone);
    }

    #endregion

    #region IBAN Detection Tests

    [Theory]
    [InlineData("My IBAN is DE89370400440532013000")]
    [InlineData("Transfer to GB82WEST12345698765432")]
    [InlineData("Account: FR7630006000011234567890189")]
    public void DetectPii_ValidIban_DetectsIban(string content)
    {
        var matches = _detector.DetectPii(content, _defaultOptions);

        Assert.Contains(matches, m => m.Type == PiiType.Iban);
    }

    #endregion

    #region Credit Card Detection Tests

    [Theory]
    [InlineData("Card: 4111111111111111")] // Visa test
    [InlineData("Card number: 5500000000000004")] // Mastercard test
    [InlineData("Amex: 378282246310005")] // Amex test
    public void DetectPii_ValidCreditCard_DetectsCreditCard(string content)
    {
        var matches = _detector.DetectPii(content, _defaultOptions);

        Assert.Contains(matches, m => m.Type == PiiType.CreditCard);
    }

    #endregion

    #region Address Detection Tests

    [Theory]
    [InlineData("I live at 123 Main Street")]
    [InlineData("Office: 456 Oak Avenue")]
    [InlineData("Send to 789 Pine Road")]
    [InlineData("Located at 321 Maple Boulevard")]
    public void DetectPii_ValidAddress_DetectsAddress(string content)
    {
        var matches = _detector.DetectPii(content, _defaultOptions);

        Assert.Contains(matches, m => m.Type == PiiType.Address);
    }

    #endregion

    #region Position Tracking Tests

    [Fact]
    public void DetectPii_TracksPosition_Correctly()
    {
        var content = "Contact john@example.com";
        var matches = _detector.DetectPii(content, _defaultOptions);

        var emailMatch = matches.First(m => m.Type == PiiType.Email);

        Assert.Equal(8, emailMatch.StartIndex);
        Assert.Equal(24, emailMatch.EndIndex);
        Assert.Equal("john@example.com", content[emailMatch.StartIndex..emailMatch.EndIndex]);
    }

    #endregion

    #region Email Detection Tests

    [Theory]
    [InlineData("Contact me at john@example.com for more info")]
    [InlineData("Email: test.user+tag@subdomain.example.co.uk")]
    [InlineData("user_name123@domain.org")]
    public void DetectPii_ValidEmail_DetectsEmail(string content)
    {
        var matches = _detector.DetectPii(content, _defaultOptions);

        Assert.Contains(matches, m => m.Type == PiiType.Email);
    }

    [Theory]
    [InlineData("This is not an email")]
    [InlineData("Invalid @email format")]
    [InlineData("user@")]
    public void DetectPii_InvalidEmail_DoesNotDetect(string content)
    {
        var matches = _detector.DetectPii(content, _defaultOptions);

        Assert.DoesNotContain(matches, m => m.Type == PiiType.Email);
    }

    #endregion

    #region SSN Detection Tests

    [Theory]
    [InlineData("SSN: 123-45-6789")]
    [InlineData("Social security: 987-65-4321")]
    public void DetectPii_ValidSsn_DetectsSsn(string content)
    {
        var matches = _detector.DetectPii(content, _defaultOptions);

        Assert.Contains(matches, m => m.Type == PiiType.SocialSecurityNumber);
    }

    [Fact]
    public void DetectPii_InvalidSsn_DoesNotDetect()
    {
        var content = "Not a SSN: 12-345-6789";
        var matches = _detector.DetectPii(content, _defaultOptions);

        Assert.DoesNotContain(matches, m => m.Type == PiiType.SocialSecurityNumber);
    }

    #endregion

    #region Masking Tests

    [Fact]
    public void MaskPii_Email_MasksCorrectly()
    {
        var content = "Email me at john@example.com please";
        var matches = _detector.DetectPii(content, _defaultOptions);
        var masked = _detector.MaskPii(content, matches, _defaultOptions);

        Assert.DoesNotContain("john@example.com", masked);
        Assert.Contains("jo", masked); // First 2 chars unmasked
        Assert.Contains("om", masked); // Last 2 chars unmasked
        Assert.Contains("*", masked); // Contains mask chars
    }

    [Fact]
    public void MaskPii_MultipleItems_MasksAll()
    {
        var content = "Contact john@example.com or call 555-123-4567";
        var matches = _detector.DetectPii(content, _defaultOptions);
        var masked = _detector.MaskPii(content, matches, _defaultOptions);

        Assert.DoesNotContain("john@example.com", masked);
        Assert.DoesNotContain("555-123-4567", masked);
    }

    [Fact]
    public void MaskPii_ShortValue_FullyMasks()
    {
        var options = new PiiDetectionOptions
        {
            DetectEmail = true,
            MaskCharacter = '*',
            UnmaskedChars = 5 // More than email length parts
        };

        var content = "Email: a@b.co";
        var matches = _detector.DetectPii(content, options);
        var masked = _detector.MaskPii(content, matches, options);

        // Short values should be fully masked
        Assert.Contains("*", masked);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void DetectPii_EmptyContent_ReturnsEmpty()
    {
        var matches = _detector.DetectPii(string.Empty, _defaultOptions);

        Assert.Empty(matches);
    }

    [Fact]
    public void DetectPii_NullContent_ReturnsEmpty()
    {
        var matches = _detector.DetectPii(null!, _defaultOptions);

        Assert.Empty(matches);
    }

    [Fact]
    public void DetectPii_WhitespaceContent_ReturnsEmpty()
    {
        var matches = _detector.DetectPii("   \t\n  ", _defaultOptions);

        Assert.Empty(matches);
    }

    [Fact]
    public void DetectPii_DisabledOptions_ReturnsEmpty()
    {
        var options = new PiiDetectionOptions
        {
            Enabled = false,
            DetectEmail = false,
            DetectPhone = false,
            DetectAddress = false,
            DetectIban = false,
            DetectCreditCard = false,
            DetectSocialSecurityNumber = false
        };

        var content = "Email: john@example.com, Phone: 555-123-4567";
        var matches = _detector.DetectPii(content, options);

        Assert.Empty(matches);
    }

    [Fact]
    public void DetectPii_SelectiveDetection_OnlyDetectsEnabled()
    {
        var options = new PiiDetectionOptions
        {
            DetectEmail = true,
            DetectPhone = false,
            DetectAddress = false,
            DetectIban = false,
            DetectCreditCard = false,
            DetectSocialSecurityNumber = false
        };

        var content = "Email: john@example.com, Phone: 555-123-4567";
        var matches = _detector.DetectPii(content, options);

        Assert.Single(matches);
        Assert.Equal(PiiType.Email, matches[0].Type);
    }

    #endregion
}