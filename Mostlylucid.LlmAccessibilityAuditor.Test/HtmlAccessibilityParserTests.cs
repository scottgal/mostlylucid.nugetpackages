using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmAccessibilityAuditor.Models;
using Mostlylucid.LlmAccessibilityAuditor.Services;

namespace Mostlylucid.LlmAccessibilityAuditor.Test;

public class HtmlAccessibilityParserTests
{
    private readonly HtmlAccessibilityParser _parser;
    private readonly Mock<ILogger<HtmlAccessibilityParser>> _loggerMock;

    public HtmlAccessibilityParserTests()
    {
        _loggerMock = new Mock<ILogger<HtmlAccessibilityParser>>();
        var options = Options.Create(new AccessibilityAuditorOptions());
        _parser = new HtmlAccessibilityParser(_loggerMock.Object, options);
    }

    [Fact]
    public async Task ParseAndAnalyzeAsync_ValidHtml_ReturnsNoIssues()
    {
        // Arrange
        var html = @"<!DOCTYPE html>
<html lang=""en"">
<head><title>Test Page</title></head>
<body>
    <a href=""#main"" class=""sr-only"">Skip to main content</a>
    <main id=""main"">
        <h1>Main Heading</h1>
        <h2>Sub Heading</h2>
        <p>Content</p>
        <img src=""test.jpg"" alt=""Test image"">
        <form>
            <label for=""name"">Name:</label>
            <input type=""text"" id=""name"" name=""name"">
        </form>
        <button>Click me</button>
        <a href=""/link"">Link text</a>
    </main>
</body>
</html>";

        // Act
        var issues = await _parser.ParseAndAnalyzeAsync(html);

        // Assert
        Assert.Empty(issues.Where(i => i.Severity == IssueSeverity.Critical));
    }

    [Fact]
    public async Task ParseAndAnalyzeAsync_MissingLanguage_DetectsIssue()
    {
        // Arrange
        var html = @"<!DOCTYPE html>
<html>
<head><title>Test Page</title></head>
<body><h1>Hello</h1></body>
</html>";

        // Act
        var issues = await _parser.ParseAndAnalyzeAsync(html);

        // Assert
        Assert.Contains(issues, i => i.Type == AccessibilityIssueType.MissingLanguage);
    }

    [Fact]
    public async Task ParseAndAnalyzeAsync_MissingTitle_DetectsIssue()
    {
        // Arrange
        var html = @"<!DOCTYPE html>
<html lang=""en"">
<head></head>
<body><h1>Hello</h1></body>
</html>";

        // Act
        var issues = await _parser.ParseAndAnalyzeAsync(html);

        // Assert
        Assert.Contains(issues, i => i.Type == AccessibilityIssueType.MissingTitle);
    }

    [Fact]
    public async Task ParseAndAnalyzeAsync_MissingAltText_DetectsIssue()
    {
        // Arrange
        var html = @"<!DOCTYPE html>
<html lang=""en"">
<head><title>Test</title></head>
<body>
    <main><h1>Test</h1></main>
    <img src=""test.jpg"">
</body>
</html>";

        // Act
        var issues = await _parser.ParseAndAnalyzeAsync(html);

        // Assert
        Assert.Contains(issues, i => i.Type == AccessibilityIssueType.MissingAltText);
        var issue = issues.First(i => i.Type == AccessibilityIssueType.MissingAltText);
        Assert.Equal(IssueSeverity.Critical, issue.Severity);
    }

    [Fact]
    public async Task ParseAndAnalyzeAsync_EmptyAltText_DetectsIssue()
    {
        // Arrange
        var html = @"<!DOCTYPE html>
<html lang=""en"">
<head><title>Test</title></head>
<body>
    <main><h1>Test</h1></main>
    <img src=""test.jpg"" alt="""">
</body>
</html>";

        // Act
        var issues = await _parser.ParseAndAnalyzeAsync(html);

        // Assert
        Assert.Contains(issues, i => i.Type == AccessibilityIssueType.EmptyAltText);
    }

    [Fact]
    public async Task ParseAndAnalyzeAsync_DecorativeImage_NoIssue()
    {
        // Arrange
        var html = @"<!DOCTYPE html>
<html lang=""en"">
<head><title>Test</title></head>
<body>
    <main><h1>Test</h1></main>
    <img src=""test.jpg"" alt="""" role=""presentation"">
</body>
</html>";

        // Act
        var issues = await _parser.ParseAndAnalyzeAsync(html);

        // Assert
        Assert.DoesNotContain(issues, i => i.Type == AccessibilityIssueType.MissingAltText);
        Assert.DoesNotContain(issues, i => i.Type == AccessibilityIssueType.EmptyAltText);
    }

    [Fact]
    public async Task ParseAndAnalyzeAsync_SkippedHeadingLevel_DetectsIssue()
    {
        // Arrange
        var html = @"<!DOCTYPE html>
<html lang=""en"">
<head><title>Test</title></head>
<body>
    <main>
        <h1>Main Heading</h1>
        <h4>Skipped to H4</h4>
    </main>
</body>
</html>";

        // Act
        var issues = await _parser.ParseAndAnalyzeAsync(html);

        // Assert
        Assert.Contains(issues, i => i.Type == AccessibilityIssueType.SkippedHeadingLevel);
    }

    [Fact]
    public async Task ParseAndAnalyzeAsync_MultipleH1_DetectsIssue()
    {
        // Arrange
        var html = @"<!DOCTYPE html>
<html lang=""en"">
<head><title>Test</title></head>
<body>
    <main>
        <h1>First Heading</h1>
        <h1>Second Heading</h1>
    </main>
</body>
</html>";

        // Act
        var issues = await _parser.ParseAndAnalyzeAsync(html);

        // Assert
        Assert.Contains(issues, i => i.Type == AccessibilityIssueType.MultipleH1Elements);
    }

    [Fact]
    public async Task ParseAndAnalyzeAsync_ButtonNoAccessibleName_DetectsIssue()
    {
        // Arrange
        var html = @"<!DOCTYPE html>
<html lang=""en"">
<head><title>Test</title></head>
<body>
    <main><h1>Test</h1></main>
    <button></button>
</body>
</html>";

        // Act
        var issues = await _parser.ParseAndAnalyzeAsync(html);

        // Assert
        Assert.Contains(issues, i => i.Type == AccessibilityIssueType.ButtonNoAccessibleName);
    }

    [Fact]
    public async Task ParseAndAnalyzeAsync_ButtonWithAriaLabel_NoIssue()
    {
        // Arrange
        var html = @"<!DOCTYPE html>
<html lang=""en"">
<head><title>Test</title></head>
<body>
    <main><h1>Test</h1></main>
    <button aria-label=""Close dialog""></button>
</body>
</html>";

        // Act
        var issues = await _parser.ParseAndAnalyzeAsync(html);

        // Assert
        Assert.DoesNotContain(issues, i => i.Type == AccessibilityIssueType.ButtonNoAccessibleName);
    }

    [Fact]
    public async Task ParseAndAnalyzeAsync_LinkNoAccessibleName_DetectsIssue()
    {
        // Arrange
        var html = @"<!DOCTYPE html>
<html lang=""en"">
<head><title>Test</title></head>
<body>
    <main><h1>Test</h1></main>
    <a href=""/test""></a>
</body>
</html>";

        // Act
        var issues = await _parser.ParseAndAnalyzeAsync(html);

        // Assert
        Assert.Contains(issues, i => i.Type == AccessibilityIssueType.LinkNoAccessibleName);
    }

    [Fact]
    public async Task ParseAndAnalyzeAsync_FormInputNoLabel_DetectsIssue()
    {
        // Arrange
        var html = @"<!DOCTYPE html>
<html lang=""en"">
<head><title>Test</title></head>
<body>
    <main><h1>Test</h1></main>
    <form>
        <input type=""text"" id=""email"" name=""email"">
    </form>
</body>
</html>";

        // Act
        var issues = await _parser.ParseAndAnalyzeAsync(html);

        // Assert
        Assert.Contains(issues, i => i.Type == AccessibilityIssueType.FormInputNoLabel);
    }

    [Fact]
    public async Task ParseAndAnalyzeAsync_FormInputWithLabel_NoIssue()
    {
        // Arrange
        var html = @"<!DOCTYPE html>
<html lang=""en"">
<head><title>Test</title></head>
<body>
    <main><h1>Test</h1></main>
    <form>
        <label for=""email"">Email:</label>
        <input type=""text"" id=""email"" name=""email"">
    </form>
</body>
</html>";

        // Act
        var issues = await _parser.ParseAndAnalyzeAsync(html);

        // Assert
        Assert.DoesNotContain(issues, i => i.Type == AccessibilityIssueType.FormInputNoLabel);
    }

    [Fact]
    public async Task ParseAndAnalyzeAsync_FormInputWithImplicitLabel_NoIssue()
    {
        // Arrange
        var html = @"<!DOCTYPE html>
<html lang=""en"">
<head><title>Test</title></head>
<body>
    <main><h1>Test</h1></main>
    <form>
        <label>Email: <input type=""text"" name=""email""></label>
    </form>
</body>
</html>";

        // Act
        var issues = await _parser.ParseAndAnalyzeAsync(html);

        // Assert
        Assert.DoesNotContain(issues, i => i.Type == AccessibilityIssueType.FormInputNoLabel);
    }

    [Fact]
    public async Task ParseAndAnalyzeAsync_MissingMain_DetectsIssue()
    {
        // Arrange
        var html = @"<!DOCTYPE html>
<html lang=""en"">
<head><title>Test</title></head>
<body>
    <h1>Test</h1>
</body>
</html>";

        // Act
        var issues = await _parser.ParseAndAnalyzeAsync(html);

        // Assert
        Assert.Contains(issues, i => i.Type == AccessibilityIssueType.MissingLandmark);
    }

    [Fact]
    public async Task ParseAndAnalyzeAsync_TableNoHeaders_DetectsIssue()
    {
        // Arrange
        var html = @"<!DOCTYPE html>
<html lang=""en"">
<head><title>Test</title></head>
<body>
    <main><h1>Test</h1></main>
    <table>
        <tr><td>Data 1</td><td>Data 2</td></tr>
    </table>
</body>
</html>";

        // Act
        var issues = await _parser.ParseAndAnalyzeAsync(html);

        // Assert
        Assert.Contains(issues, i => i.Type == AccessibilityIssueType.TableNoHeaders);
    }

    [Fact]
    public async Task ParseAndAnalyzeAsync_TableWithHeaders_NoIssue()
    {
        // Arrange
        var html = @"<!DOCTYPE html>
<html lang=""en"">
<head><title>Test</title></head>
<body>
    <main><h1>Test</h1></main>
    <table>
        <tr><th>Header 1</th><th>Header 2</th></tr>
        <tr><td>Data 1</td><td>Data 2</td></tr>
    </table>
</body>
</html>";

        // Act
        var issues = await _parser.ParseAndAnalyzeAsync(html);

        // Assert
        Assert.DoesNotContain(issues, i => i.Type == AccessibilityIssueType.TableNoHeaders);
    }

    [Fact]
    public async Task ParseAndAnalyzeAsync_EmptyAriaLabel_DetectsIssue()
    {
        // Arrange
        var html = @"<!DOCTYPE html>
<html lang=""en"">
<head><title>Test</title></head>
<body>
    <main><h1>Test</h1></main>
    <button aria-label="""">Click</button>
</body>
</html>";

        // Act
        var issues = await _parser.ParseAndAnalyzeAsync(html);

        // Assert
        Assert.Contains(issues, i => i.Type == AccessibilityIssueType.MissingAriaLabel);
    }

    [Fact]
    public void SimplifyForLlm_LargeHtml_Truncates()
    {
        // Arrange
        var html = @"<!DOCTYPE html>
<html lang=""en"">
<head><title>Test</title></head>
<body>" + new string('x', 10000) + "</body></html>";

        // Act
        var result = _parser.SimplifyForLlm(html, 500);

        // Assert
        Assert.True(result.Length <= 500);
        Assert.Contains("truncated", result.ToLower());
    }

    [Fact]
    public void SimplifyForLlm_RemovesScripts()
    {
        // Arrange
        var html = @"<!DOCTYPE html>
<html lang=""en"">
<head><title>Test</title><script>alert('hi');</script></head>
<body><h1>Test</h1><script>console.log('x');</script></body>
</html>";

        // Act
        var result = _parser.SimplifyForLlm(html, 10000);

        // Assert
        Assert.DoesNotContain("alert", result);
        Assert.DoesNotContain("console.log", result);
    }

    [Fact]
    public void ExtractTitle_ReturnsTitle()
    {
        // Arrange
        var html = @"<!DOCTYPE html><html><head><title>My Page Title</title></head><body></body></html>";

        // Act
        var title = _parser.ExtractTitle(html);

        // Assert
        Assert.Equal("My Page Title", title);
    }

    [Fact]
    public void ExtractTitle_NoTitle_ReturnsNull()
    {
        // Arrange
        var html = @"<!DOCTYPE html><html><head></head><body></body></html>";

        // Act
        var title = _parser.ExtractTitle(html);

        // Assert
        Assert.Null(title);
    }
}
