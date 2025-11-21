using Mostlylucid.LlmAltText.Models;

namespace Mostlylucid.LlmAltText.Test.Models;

/// <summary>
///     Comprehensive tests for AltTextOptions
/// </summary>
public class AltTextOptionsTests
{
    #region Immutability Tests

    [Fact]
    public void Options_AllPropertiesAreMutable()
    {
        // Arrange
        var options = new AltTextOptions();
        var originalPath = options.ModelPath;
        var originalTaskType = options.DefaultTaskType;

        // Act - Modify all properties
        options.ModelPath = "/new/path";
        options.AltTextPrompt = "New prompt";
        options.DefaultTaskType = "CAPTION";
        options.EnableDiagnosticLogging = false;
        options.MaxWords = 50;

        // Assert - All should have changed
        Assert.NotEqual(originalPath, options.ModelPath);
        Assert.NotEqual(originalTaskType, options.DefaultTaskType);
    }

    #endregion

    #region Default Value Tests

    [Fact]
    public void Constructor_SetsDefaultModelPath()
    {
        // Act
        var options = new AltTextOptions();

        // Assert
        Assert.Equal("./models", options.ModelPath);
    }

    [Fact]
    public void Constructor_SetsDefaultAltTextPrompt()
    {
        // Act
        var options = new AltTextOptions();

        // Assert
        Assert.NotNull(options.AltTextPrompt);
        Assert.NotEmpty(options.AltTextPrompt);
        Assert.Contains("alt text", options.AltTextPrompt.ToLower());
    }

    [Fact]
    public void Constructor_SetsDefaultTaskType()
    {
        // Act
        var options = new AltTextOptions();

        // Assert
        Assert.Equal("MORE_DETAILED_CAPTION", options.DefaultTaskType);
    }

    [Fact]
    public void Constructor_SetsDefaultEnableDiagnosticLogging()
    {
        // Act
        var options = new AltTextOptions();

        // Assert
        Assert.True(options.EnableDiagnosticLogging);
    }

    [Fact]
    public void Constructor_SetsDefaultMaxWords()
    {
        // Act
        var options = new AltTextOptions();

        // Assert
        Assert.Equal(90, options.MaxWords);
    }

    #endregion

    #region Property Setting Tests - ModelPath

    [Theory]
    [InlineData("./models")]
    [InlineData("/opt/models")]
    [InlineData("C:\\Models\\Florence2")]
    [InlineData("~/llm-models")]
    public void ModelPath_CanBeSet(string path)
    {
        // Arrange
        var options = new AltTextOptions();

        // Act
        options.ModelPath = path;

        // Assert
        Assert.Equal(path, options.ModelPath);
    }

    [Fact]
    public void ModelPath_CanBeEmpty()
    {
        // Arrange
        var options = new AltTextOptions();

        // Act
        options.ModelPath = "";

        // Assert
        Assert.Equal("", options.ModelPath);
    }

    [Fact]
    public void ModelPath_CanBeRelative()
    {
        // Arrange
        var options = new AltTextOptions();

        // Act
        options.ModelPath = "../shared/models";

        // Assert
        Assert.Equal("../shared/models", options.ModelPath);
    }

    [Fact]
    public void ModelPath_CanBeAbsolute()
    {
        // Arrange
        var options = new AltTextOptions();

        // Act
        options.ModelPath = "/var/lib/florence2";

        // Assert
        Assert.Equal("/var/lib/florence2", options.ModelPath);
    }

    #endregion

    #region Property Setting Tests - AltTextPrompt

    [Fact]
    public void AltTextPrompt_CanBeSet()
    {
        // Arrange
        var options = new AltTextOptions();
        var customPrompt = "Describe this image in detail";

        // Act
        options.AltTextPrompt = customPrompt;

        // Assert
        Assert.Equal(customPrompt, options.AltTextPrompt);
    }

    [Fact]
    public void AltTextPrompt_CanBeEmpty()
    {
        // Arrange
        var options = new AltTextOptions();

        // Act
        options.AltTextPrompt = "";

        // Assert
        Assert.Equal("", options.AltTextPrompt);
    }

    [Fact]
    public void AltTextPrompt_CanContainMultipleLanguages()
    {
        // Arrange
        var options = new AltTextOptions();
        var customPrompt = "Describe the image in English and provide translation in Spanish";

        // Act
        options.AltTextPrompt = customPrompt;

        // Assert
        Assert.Contains("English", options.AltTextPrompt);
        Assert.Contains("Spanish", options.AltTextPrompt);
    }

    [Fact]
    public void DefaultAltTextPrompt_ContainsAccessibilityGuidance()
    {
        // Act
        var options = new AltTextOptions();

        // Assert - Default prompt should have accessibility-focused content
        Assert.Contains("descriptive", options.AltTextPrompt.ToLower());
    }

    [Fact]
    public void DefaultAltTextPrompt_ContainsWordLimit()
    {
        // Act
        var options = new AltTextOptions();

        // Assert - Default prompt should mention word limit
        Assert.Contains("90 words", options.AltTextPrompt.ToLower());
    }

    #endregion

    #region Property Setting Tests - DefaultTaskType

    [Theory]
    [InlineData("CAPTION")]
    [InlineData("DETAILED_CAPTION")]
    [InlineData("MORE_DETAILED_CAPTION")]
    public void DefaultTaskType_ValidOptions(string taskType)
    {
        // Arrange
        var options = new AltTextOptions();

        // Act
        options.DefaultTaskType = taskType;

        // Assert
        Assert.Equal(taskType, options.DefaultTaskType);
    }

    [Fact]
    public void DefaultTaskType_DefaultIsMostDetailed()
    {
        // Act
        var options = new AltTextOptions();

        // Assert - Default should be the most detailed option
        Assert.Equal("MORE_DETAILED_CAPTION", options.DefaultTaskType);
    }

    [Fact]
    public void DefaultTaskType_CaptionIsLeastDetailed()
    {
        // Arrange & Act
        var options = new AltTextOptions
        {
            DefaultTaskType = "CAPTION"
        };

        // Assert
        Assert.Equal("CAPTION", options.DefaultTaskType);
    }

    [Fact]
    public void DefaultTaskType_CanBeCustomValue()
    {
        // Arrange
        var options = new AltTextOptions();

        // Act
        options.DefaultTaskType = "CUSTOM_TASK";

        // Assert - Should allow any string value
        Assert.Equal("CUSTOM_TASK", options.DefaultTaskType);
    }

    #endregion

    #region Property Setting Tests - EnableDiagnosticLogging

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EnableDiagnosticLogging_CanBeSet(bool enabled)
    {
        // Arrange
        var options = new AltTextOptions();

        // Act
        options.EnableDiagnosticLogging = enabled;

        // Assert
        Assert.Equal(enabled, options.EnableDiagnosticLogging);
    }

    [Fact]
    public void EnableDiagnosticLogging_DefaultIsTrue()
    {
        // Act
        var options = new AltTextOptions();

        // Assert - Default should be true for development visibility
        Assert.True(options.EnableDiagnosticLogging);
    }

    [Fact]
    public void EnableDiagnosticLogging_CanBeDisabledForProduction()
    {
        // Arrange
        var options = new AltTextOptions
        {
            EnableDiagnosticLogging = false
        };

        // Assert
        Assert.False(options.EnableDiagnosticLogging);
    }

    #endregion

    #region Property Setting Tests - MaxWords

    [Theory]
    [InlineData(50)]
    [InlineData(90)]
    [InlineData(125)]
    [InlineData(200)]
    public void MaxWords_CanBeSet(int maxWords)
    {
        // Arrange
        var options = new AltTextOptions();

        // Act
        options.MaxWords = maxWords;

        // Assert
        Assert.Equal(maxWords, options.MaxWords);
    }

    [Fact]
    public void MaxWords_DefaultIs90()
    {
        // Act
        var options = new AltTextOptions();

        // Assert - 90 words is recommended for accessibility
        Assert.Equal(90, options.MaxWords);
    }

    [Fact]
    public void MaxWords_CanBeZero()
    {
        // Arrange
        var options = new AltTextOptions();

        // Act
        options.MaxWords = 0;

        // Assert - Zero might mean no limit
        Assert.Equal(0, options.MaxWords);
    }

    [Fact]
    public void MaxWords_CanBeVeryLarge()
    {
        // Arrange
        var options = new AltTextOptions();

        // Act
        options.MaxWords = int.MaxValue;

        // Assert
        Assert.Equal(int.MaxValue, options.MaxWords);
    }

    #endregion

    #region Complete Configuration Tests

    [Fact]
    public void CompleteConfiguration_AccessibilityFocused()
    {
        // Act
        var options = new AltTextOptions
        {
            ModelPath = "./models/florence2",
            AltTextPrompt = "Provide clear, descriptive alt text for accessibility",
            DefaultTaskType = "DETAILED_CAPTION",
            EnableDiagnosticLogging = false,
            MaxWords = 100
        };

        // Assert
        Assert.Equal("./models/florence2", options.ModelPath);
        Assert.Contains("accessibility", options.AltTextPrompt.ToLower());
        Assert.Equal("DETAILED_CAPTION", options.DefaultTaskType);
        Assert.False(options.EnableDiagnosticLogging);
        Assert.Equal(100, options.MaxWords);
    }

    [Fact]
    public void CompleteConfiguration_HighDetail()
    {
        // Act
        var options = new AltTextOptions
        {
            ModelPath = "/opt/models",
            AltTextPrompt = "Describe every detail visible in this image",
            DefaultTaskType = "MORE_DETAILED_CAPTION",
            EnableDiagnosticLogging = true,
            MaxWords = 200
        };

        // Assert
        Assert.Equal("MORE_DETAILED_CAPTION", options.DefaultTaskType);
        Assert.Equal(200, options.MaxWords);
    }

    [Fact]
    public void CompleteConfiguration_Brief()
    {
        // Act
        var options = new AltTextOptions
        {
            ModelPath = "./models",
            AltTextPrompt = "Write a short caption",
            DefaultTaskType = "CAPTION",
            EnableDiagnosticLogging = false,
            MaxWords = 25
        };

        // Assert
        Assert.Equal("CAPTION", options.DefaultTaskType);
        Assert.Equal(25, options.MaxWords);
    }

    [Fact]
    public void CompleteConfiguration_ProductionReady()
    {
        // Act - Production settings
        var options = new AltTextOptions
        {
            ModelPath = "/var/lib/florence2",
            DefaultTaskType = "MORE_DETAILED_CAPTION",
            EnableDiagnosticLogging = false, // Disabled in production
            MaxWords = 90
        };

        // Assert
        Assert.False(options.EnableDiagnosticLogging);
        Assert.Equal(90, options.MaxWords);
    }

    #endregion

    #region Validation Edge Cases

    [Fact]
    public void DefaultPrompt_DoesNotContainSpecialCharacters()
    {
        // Act
        var options = new AltTextOptions();

        // Assert - Prompt should be clean text
        Assert.DoesNotContain("<", options.AltTextPrompt);
        Assert.DoesNotContain(">", options.AltTextPrompt);
        Assert.DoesNotContain("{{", options.AltTextPrompt);
    }

    [Fact]
    public void DefaultPrompt_IsReasonableLength()
    {
        // Act
        var options = new AltTextOptions();

        // Assert - Prompt should not be too long or too short
        Assert.InRange(options.AltTextPrompt.Length, 50, 500);
    }

    #endregion
}