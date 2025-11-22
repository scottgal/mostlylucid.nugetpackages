using Mostlylucid.LlmLogSummarizer.Models;

namespace Mostlylucid.LlmLogSummarizer.Test;

public class LogEntryTests
{
    [Fact]
    public void GetClusteringFingerprint_IncludesExceptionType()
    {
        // Arrange
        var entry = new LogEntry
        {
            ExceptionType = "System.NullReferenceException",
            Message = "Some error"
        };

        // Act
        var fingerprint = entry.GetClusteringFingerprint();

        // Assert
        Assert.Contains("System.NullReferenceException", fingerprint);
    }

    [Fact]
    public void GetClusteringFingerprint_IncludesSourceContext()
    {
        // Arrange
        var entry = new LogEntry
        {
            SourceContext = "MyApp.Services.UserService",
            Message = "Some error"
        };

        // Act
        var fingerprint = entry.GetClusteringFingerprint();

        // Assert
        Assert.Contains("MyApp.Services.UserService", fingerprint);
    }

    [Fact]
    public void GetClusteringFingerprint_NormalizesGuids()
    {
        // Arrange
        var entry1 = new LogEntry
        {
            Message = "User 12345678-1234-1234-1234-123456789012 not found"
        };
        var entry2 = new LogEntry
        {
            Message = "User abcdefab-abcd-abcd-abcd-abcdefabcdef not found"
        };

        // Act
        var fingerprint1 = entry1.GetClusteringFingerprint();
        var fingerprint2 = entry2.GetClusteringFingerprint();

        // Assert
        Assert.Equal(fingerprint1, fingerprint2);
        Assert.Contains("<GUID>", fingerprint1);
    }

    [Fact]
    public void GetClusteringFingerprint_NormalizesNumbers()
    {
        // Arrange
        var entry1 = new LogEntry
        {
            Message = "Request 12345 failed"
        };
        var entry2 = new LogEntry
        {
            Message = "Request 67890 failed"
        };

        // Act
        var fingerprint1 = entry1.GetClusteringFingerprint();
        var fingerprint2 = entry2.GetClusteringFingerprint();

        // Assert
        Assert.Equal(fingerprint1, fingerprint2);
        Assert.Contains("<N>", fingerprint1);
    }

    [Fact]
    public void GetClusteringFingerprint_NormalizesQuotedStrings()
    {
        // Arrange
        var entry1 = new LogEntry
        {
            Message = "Failed to find file 'config.json'"
        };
        var entry2 = new LogEntry
        {
            Message = "Failed to find file 'settings.xml'"
        };

        // Act
        var fingerprint1 = entry1.GetClusteringFingerprint();
        var fingerprint2 = entry2.GetClusteringFingerprint();

        // Assert
        Assert.Equal(fingerprint1, fingerprint2);
    }

    [Fact]
    public void GetClusteringFingerprint_IncludesFirstStackTraceLine()
    {
        // Arrange
        var entry = new LogEntry
        {
            Message = "Error occurred",
            StackTrace = "   at MyApp.Services.UserService.GetUser(Int32 id)\n   at MyApp.Controllers.UsersController.Get(Int32 id)"
        };

        // Act
        var fingerprint = entry.GetClusteringFingerprint();

        // Assert
        Assert.Contains("at MyApp.Services.UserService.GetUser(Int32 id)", fingerprint);
    }

    [Fact]
    public void LogEntry_DefaultTimestamp_IsNow()
    {
        // Arrange & Act
        var entry = new LogEntry();

        // Assert
        // Default should be close to MinValue or default
        Assert.Equal(default, entry.Timestamp);
    }

    [Fact]
    public void LogEntry_Properties_CanBeSet()
    {
        // Arrange
        var entry = new LogEntry
        {
            Properties =
            {
                ["RequestId"] = "abc123",
                ["UserId"] = 42
            }
        };

        // Assert
        Assert.Equal("abc123", entry.Properties["RequestId"]);
        Assert.Equal(42, entry.Properties["UserId"]);
    }
}
