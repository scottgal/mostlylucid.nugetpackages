using FluentAssertions;
using Mostlylucid.LlmPiiRedactor.Models;
using Mostlylucid.LlmPiiRedactor.Services;
using Xunit;

namespace Mostlylucid.LlmPiiRedactor.Tests.Services;

public class RedactionStrategyTests
{
    private readonly PiiRedactionOptions _defaultOptions = new();

    [Fact]
    public void FullMaskStrategy_ReplacesEntireValue()
    {
        var strategy = new FullMaskStrategy();
        var original = "john@example.com";

        var result = strategy.Redact(original, PiiType.Email, _defaultOptions);

        result.Should().HaveLength(original.Length);
        result.Should().Be(new string('*', original.Length));
    }

    [Fact]
    public void PartialMaskStrategy_Email_ShowsPrefixAndDomain()
    {
        var strategy = new PartialMaskStrategy();
        var original = "john.doe@example.com";

        var result = strategy.Redact(original, PiiType.Email, _defaultOptions);

        result.Should().StartWith("jo");
        result.Should().EndWith("@example.com");
        result.Should().Contain("****");
    }

    [Fact]
    public void PartialMaskStrategy_CreditCard_ShowsLastFour()
    {
        var strategy = new PartialMaskStrategy();
        var original = "4111-1111-1111-1111";

        var result = strategy.Redact(original, PiiType.CreditCard, _defaultOptions);

        result.Should().EndWith("1111");
        result.Should().Contain("****");
    }

    [Fact]
    public void TokenizedStrategy_SameInput_SameToken()
    {
        var strategy = new TokenizedStrategy();
        var original = "john@example.com";

        var result1 = strategy.Redact(original, PiiType.Email, _defaultOptions);
        var result2 = strategy.Redact(original, PiiType.Email, _defaultOptions);

        result1.Should().Be(result2);
        result1.Should().Match("[EMAIL_*]");
    }

    [Fact]
    public void TokenizedStrategy_DifferentInputs_DifferentTokens()
    {
        var strategy = new TokenizedStrategy();
        strategy.ClearCache();

        var result1 = strategy.Redact("john@example.com", PiiType.Email, _defaultOptions);
        var result2 = strategy.Redact("jane@example.com", PiiType.Email, _defaultOptions);

        result1.Should().NotBe(result2);
    }

    [Fact]
    public void TypeLabelStrategy_ReturnsTypeLabel()
    {
        var strategy = new TypeLabelStrategy();

        var emailResult = strategy.Redact("john@example.com", PiiType.Email, _defaultOptions);
        var phoneResult = strategy.Redact("555-1234", PiiType.PhoneNumber, _defaultOptions);

        emailResult.Should().Be("[EMAIL]");
        phoneResult.Should().Be("[PHONE]");
    }

    [Fact]
    public void HashedStrategy_SameInput_SameHash()
    {
        var strategy = new HashedStrategy();
        var original = "john@example.com";

        var result1 = strategy.Redact(original, PiiType.Email, _defaultOptions);
        var result2 = strategy.Redact(original, PiiType.Email, _defaultOptions);

        result1.Should().Be(result2);
        result1.Should().Match("[EMAIL:*]");
    }

    [Fact]
    public void RemoveStrategy_ReturnsEmpty()
    {
        var strategy = new RemoveStrategy();

        var result = strategy.Redact("john@example.com", PiiType.Email, _defaultOptions);

        result.Should().BeEmpty();
    }
}
