using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmAccessibilityAuditor.Models;
using Mostlylucid.LlmAccessibilityAuditor.Services;

namespace Mostlylucid.LlmAccessibilityAuditor.Test;

public class AccessibilityAuditorTests
{
    private readonly Mock<ILogger<AccessibilityAuditor>> _loggerMock;
    private readonly Mock<IAccessibilityOllamaClient> _ollamaClientMock;
    private readonly AccessibilityAuditorOptions _options;
    private readonly Mock<IHtmlAccessibilityParser> _parserMock;

    public AccessibilityAuditorTests()
    {
        _parserMock = new Mock<IHtmlAccessibilityParser>();
        _ollamaClientMock = new Mock<IAccessibilityOllamaClient>();
        _loggerMock = new Mock<ILogger<AccessibilityAuditor>>();
        _options = new AccessibilityAuditorOptions();
    }

    private AccessibilityAuditor CreateAuditor()
    {
        return new AccessibilityAuditor(
            _parserMock.Object,
            _ollamaClientMock.Object,
            Options.Create(_options),
            _loggerMock.Object);
    }

    [Fact]
    public async Task AuditAsync_ReturnsReport()
    {
        // Arrange
        var html = "<html><body><h1>Test</h1></body></html>";
        _parserMock.Setup(p => p.ParseAndAnalyzeAsync(html, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccessibilityIssue>());
        _parserMock.Setup(p => p.ExtractTitle(html)).Returns("Test");
        _options.EnableLlmAnalysis = false;

        var auditor = CreateAuditor();

        // Act
        var report = await auditor.AuditAsync(html, "https://example.com");

        // Assert
        Assert.NotNull(report);
        Assert.Equal("https://example.com", report.PageUrl);
        Assert.Equal("Test", report.PageTitle);
    }

    [Fact]
    public async Task AuditAsync_CallsHtmlParser()
    {
        // Arrange
        var html = "<html><body><h1>Test</h1></body></html>";
        _parserMock.Setup(p => p.ParseAndAnalyzeAsync(html, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccessibilityIssue>());
        _options.EnableLlmAnalysis = false;

        var auditor = CreateAuditor();

        // Act
        await auditor.AuditAsync(html);

        // Assert
        _parserMock.Verify(p => p.ParseAndAnalyzeAsync(html, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuditAsync_WithLlmEnabled_ChecksAvailability()
    {
        // Arrange
        var html = "<html><body><h1>Test</h1></body></html>";
        _parserMock.Setup(p => p.ParseAndAnalyzeAsync(html, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccessibilityIssue>());
        _parserMock.Setup(p => p.SimplifyForLlm(html, It.IsAny<int>())).Returns(html);
        _ollamaClientMock.Setup(o => o.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _options.EnableLlmAnalysis = true;

        var auditor = CreateAuditor();

        // Act
        await auditor.AuditAsync(html);

        // Assert
        _ollamaClientMock.Verify(o => o.IsAvailableAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuditAsync_WithLlmEnabled_CallsLlmAnalysis()
    {
        // Arrange
        var html = "<html><body><h1>Test</h1></body></html>";
        _parserMock.Setup(p => p.ParseAndAnalyzeAsync(html, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccessibilityIssue>());
        _parserMock.Setup(p => p.SimplifyForLlm(html, It.IsAny<int>())).Returns(html);
        _ollamaClientMock.Setup(o => o.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _ollamaClientMock.Setup(o => o.AnalyzeHtmlAsync(It.IsAny<string>(),
                It.IsAny<IReadOnlyList<AccessibilityIssue>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccessibilityIssue>());
        _ollamaClientMock.Setup(o => o.ModelName).Returns("test-model");
        _options.EnableLlmAnalysis = true;

        var auditor = CreateAuditor();

        // Act
        var report = await auditor.AuditAsync(html);

        // Assert
        _ollamaClientMock.Verify(
            o => o.AnalyzeHtmlAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<AccessibilityIssue>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(report.LlmAnalysisPerformed);
        Assert.Equal("test-model", report.LlmModel);
    }

    [Fact]
    public async Task AuditAsync_CalculatesScore()
    {
        // Arrange
        var html = "<html><body><h1>Test</h1></body></html>";
        var issues = new List<AccessibilityIssue>
        {
            new() { Type = AccessibilityIssueType.MissingAltText, Severity = IssueSeverity.Critical },
            new() { Type = AccessibilityIssueType.MissingAriaLabel, Severity = IssueSeverity.Serious }
        };
        _parserMock.Setup(p => p.ParseAndAnalyzeAsync(html, It.IsAny<CancellationToken>()))
            .ReturnsAsync(issues);
        _options.EnableLlmAnalysis = false;

        var auditor = CreateAuditor();

        // Act
        var report = await auditor.AuditAsync(html);

        // Assert
        Assert.NotNull(report.OverallScore);
        Assert.True(report.OverallScore < 100); // Should have deductions
    }

    [Fact]
    public async Task AuditAsync_CalculatesSummary()
    {
        // Arrange
        var html = "<html><body><h1>Test</h1></body></html>";
        var issues = new List<AccessibilityIssue>
        {
            new()
            {
                Type = AccessibilityIssueType.MissingAltText, Severity = IssueSeverity.Critical,
                Source = DetectionSource.HtmlParser
            },
            new()
            {
                Type = AccessibilityIssueType.MissingAriaLabel, Severity = IssueSeverity.Serious,
                Source = DetectionSource.HtmlParser
            },
            new()
            {
                Type = AccessibilityIssueType.MissingLanguage, Severity = IssueSeverity.Moderate,
                Source = DetectionSource.HtmlParser
            }
        };
        _parserMock.Setup(p => p.ParseAndAnalyzeAsync(html, It.IsAny<CancellationToken>()))
            .ReturnsAsync(issues);
        _options.EnableLlmAnalysis = false;

        var auditor = CreateAuditor();

        // Act
        var report = await auditor.AuditAsync(html);

        // Assert
        Assert.Equal(3, report.Summary.TotalIssues);
        Assert.Equal(1, report.Summary.CriticalCount);
        Assert.Equal(1, report.Summary.SeriousCount);
        Assert.Equal(1, report.Summary.ModerateCount);
    }

    [Fact]
    public async Task AuditAsync_TruncatesLargeHtml()
    {
        // Arrange
        var html = new string('x', 2 * 1024 * 1024); // 2MB
        _parserMock.Setup(p => p.ParseAndAnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccessibilityIssue>());
        _options.EnableLlmAnalysis = false;
        _options.MaxHtmlSizeBytes = 1024 * 1024; // 1MB

        var auditor = CreateAuditor();

        // Act
        var report = await auditor.AuditAsync(html);

        // Assert
        Assert.True(report.WasTruncated);
    }

    [Fact]
    public async Task QuickAuditAsync_OnlyUsesHtmlParser()
    {
        // Arrange
        var html = "<html><body><h1>Test</h1></body></html>";
        var issues = new List<AccessibilityIssue>
        {
            new() { Type = AccessibilityIssueType.MissingAltText, Severity = IssueSeverity.Critical }
        };
        _parserMock.Setup(p => p.ParseAndAnalyzeAsync(html, It.IsAny<CancellationToken>()))
            .ReturnsAsync(issues);

        var auditor = CreateAuditor();

        // Act
        var result = await auditor.QuickAuditAsync(html);

        // Assert
        Assert.True(result.HasIssues);
        Assert.Equal(1, result.IssueCount);
        _ollamaClientMock.Verify(
            o => o.AnalyzeHtmlAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<AccessibilityIssue>>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IsReadyAsync_WithoutLlm_ReturnsTrue()
    {
        // Arrange
        _options.EnableLlmAnalysis = false;
        var auditor = CreateAuditor();

        // Act
        var ready = await auditor.IsReadyAsync();

        // Assert
        Assert.True(ready);
    }

    [Fact]
    public async Task IsReadyAsync_WithLlm_ChecksOllama()
    {
        // Arrange
        _options.EnableLlmAnalysis = true;
        _ollamaClientMock.Setup(o => o.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var auditor = CreateAuditor();

        // Act
        var ready = await auditor.IsReadyAsync();

        // Assert
        Assert.True(ready);
        _ollamaClientMock.Verify(o => o.IsAvailableAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}