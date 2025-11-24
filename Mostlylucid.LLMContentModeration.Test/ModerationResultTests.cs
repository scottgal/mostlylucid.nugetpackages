using Mostlylucid.LLMContentModeration.Models;

namespace Mostlylucid.LLMContentModeration.Test;

public class ModerationResultTests
{
    [Fact]
    public void IsFlagged_NoFlagsOrPii_ReturnsFalse()
    {
        var result = new ModerationResult();

        Assert.False(result.IsFlagged);
    }

    [Fact]
    public void IsFlagged_WithFlags_ReturnsTrue()
    {
        var result = new ModerationResult
        {
            Flags = [new ContentFlag { Category = ContentCategory.Toxicity }]
        };

        Assert.True(result.IsFlagged);
    }

    [Fact]
    public void IsFlagged_WithPii_ReturnsTrue()
    {
        var result = new ModerationResult
        {
            PiiMatches = [new PiiMatch { Type = PiiType.Email }]
        };

        Assert.True(result.IsFlagged);
    }

    [Fact]
    public void Success_NoErrors_ReturnsTrue()
    {
        var result = new ModerationResult();

        Assert.True(result.Success);
    }

    [Fact]
    public void Success_WithErrors_ReturnsFalse()
    {
        var result = new ModerationResult
        {
            Errors = ["Test error"]
        };

        Assert.False(result.Success);
    }

    [Fact]
    public void Summary_WhenFlagged_ContainsFlagCategories()
    {
        var result = new ModerationResult
        {
            Flags =
            [
                new ContentFlag { Category = ContentCategory.Toxicity },
                new ContentFlag { Category = ContentCategory.Spam }
            ]
        };

        Assert.NotNull(result.Summary);
        Assert.Contains("Toxicity", result.Summary);
        Assert.Contains("Spam", result.Summary);
    }

    [Fact]
    public void Summary_WhenFlaggedWithPii_ContainsPii()
    {
        var result = new ModerationResult
        {
            PiiMatches = [new PiiMatch { Type = PiiType.Email }]
        };

        Assert.NotNull(result.Summary);
        Assert.Contains("PII", result.Summary);
    }

    [Fact]
    public void Summary_NotFlagged_ReturnsNull()
    {
        var result = new ModerationResult();

        Assert.Null(result.Summary);
    }

    [Fact]
    public void NewResult_HasUniqueId()
    {
        var result1 = new ModerationResult();
        var result2 = new ModerationResult();

        Assert.NotEqual(result1.Id, result2.Id);
    }

    [Fact]
    public void NewResult_HasTimestamp()
    {
        var before = DateTimeOffset.UtcNow;
        var result = new ModerationResult();
        var after = DateTimeOffset.UtcNow;

        Assert.True(result.Timestamp >= before);
        Assert.True(result.Timestamp <= after);
    }
}