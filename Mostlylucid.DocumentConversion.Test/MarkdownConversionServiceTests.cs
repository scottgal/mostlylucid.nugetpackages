using Microsoft.Extensions.Logging;
using Mostlylucid.DocumentConversion.Models;
using Mostlylucid.DocumentConversion.Services;

namespace Mostlylucid.DocumentConversion.Test;

public class MarkdownConversionServiceTests
{
    private readonly MarkdownConversionService _service;

    public MarkdownConversionServiceTests()
    {
        var logger = new Mock<ILogger<MarkdownConversionService>>();
        _service = new MarkdownConversionService(logger.Object);
    }

    [Fact]
    public async Task ParseAsync_SimpleMarkdown_ExtractsHeading()
    {
        // Arrange
        var markdown = "# Hello World\n\nThis is a paragraph.";

        // Act
        var document = await _service.ParseAsync(markdown);

        // Assert
        Assert.Equal(2, document.Elements.Count);
        Assert.IsType<HeadingElement>(document.Elements[0]);
        Assert.IsType<ParagraphElement>(document.Elements[1]);

        var heading = (HeadingElement)document.Elements[0];
        Assert.Equal(1, heading.Level);
        Assert.Equal("Hello World", heading.Text);
    }

    [Fact]
    public async Task ParseAsync_MultipleHeadingLevels_ParsesCorrectly()
    {
        // Arrange
        var markdown = @"# H1
## H2
### H3
#### H4
##### H5
###### H6";

        // Act
        var document = await _service.ParseAsync(markdown);

        // Assert
        var headings = document.GetHeadings().ToList();
        Assert.Equal(6, headings.Count);
        for (int i = 0; i < 6; i++)
        {
            Assert.Equal(i + 1, headings[i].Level);
            Assert.Equal($"H{i + 1}", headings[i].Text);
        }
    }

    [Fact]
    public async Task ParseAsync_FormattedText_PreservesFormatting()
    {
        // Arrange
        var markdown = "This is **bold** and *italic* text.";

        // Act
        var document = await _service.ParseAsync(markdown);

        // Assert
        var paragraph = document.Elements.OfType<ParagraphElement>().First();
        Assert.Contains(paragraph.Runs, r => r.IsBold && r.Text == "bold");
        Assert.Contains(paragraph.Runs, r => r.IsItalic && r.Text == "italic");
    }

    [Fact]
    public async Task ParseAsync_CodeBlock_ExtractsLanguage()
    {
        // Arrange
        var markdown = @"```csharp
Console.WriteLine(""Hello"");
```";

        // Act
        var document = await _service.ParseAsync(markdown);

        // Assert
        var codeBlock = document.Elements.OfType<CodeBlockElement>().FirstOrDefault();
        Assert.NotNull(codeBlock);
        Assert.Equal("csharp", codeBlock.Language);
        Assert.Contains("Console.WriteLine", codeBlock.Code);
    }

    [Fact]
    public async Task ParseAsync_BulletList_ExtractsItems()
    {
        // Arrange
        var markdown = @"- Item 1
- Item 2
- Item 3";

        // Act
        var document = await _service.ParseAsync(markdown);

        // Assert
        var list = document.Elements.OfType<ListElement>().FirstOrDefault();
        Assert.NotNull(list);
        Assert.Equal(ListType.Bullet, list.ListType);
        Assert.Equal(3, list.Items.Count);
        Assert.Equal("Item 1", list.Items[0].Text);
    }

    [Fact]
    public async Task ParseAsync_NumberedList_ExtractsItems()
    {
        // Arrange
        var markdown = @"1. First
2. Second
3. Third";

        // Act
        var document = await _service.ParseAsync(markdown);

        // Assert
        var list = document.Elements.OfType<ListElement>().FirstOrDefault();
        Assert.NotNull(list);
        Assert.Equal(ListType.Numbered, list.ListType);
        Assert.Equal(3, list.Items.Count);
    }

    [Fact]
    public async Task ParseAsync_Table_ExtractsRowsAndCells()
    {
        // Arrange
        var markdown = @"| Header 1 | Header 2 |
|----------|----------|
| Cell 1   | Cell 2   |
| Cell 3   | Cell 4   |";

        // Act
        var document = await _service.ParseAsync(markdown);

        // Assert
        var table = document.Elements.OfType<TableElement>().FirstOrDefault();
        Assert.NotNull(table);
        Assert.True(table.HasHeaderRow);
        Assert.Equal(3, table.Rows.Count); // Header + 2 data rows
        Assert.Equal("Header 1", table.Rows[0].Cells[0].Text);
    }

    [Fact]
    public async Task ParseAsync_BlockQuote_ExtractsText()
    {
        // Arrange
        var markdown = "> This is a quote.";

        // Act
        var document = await _service.ParseAsync(markdown);

        // Assert
        var quote = document.Elements.OfType<BlockQuoteElement>().FirstOrDefault();
        Assert.NotNull(quote);
        Assert.Contains("This is a quote", quote.Text);
    }

    [Fact]
    public async Task ParseAsync_HorizontalRule_Detected()
    {
        // Arrange
        var markdown = "Before\n\n---\n\nAfter";

        // Act
        var document = await _service.ParseAsync(markdown);

        // Assert
        Assert.Contains(document.Elements, e => e is HorizontalRuleElement);
    }

    [Fact]
    public async Task ToMarkdownAsync_Document_GeneratesValidMarkdown()
    {
        // Arrange
        var document = new Document
        {
            Title = "Test Document",
            Elements =
            [
                new HeadingElement { Level = 1, Text = "Title", Order = 0 },
                new ParagraphElement { Text = "Introduction paragraph.", Order = 1 },
                new HeadingElement { Level = 2, Text = "Section", Order = 2 },
                new ParagraphElement { Text = "Content here.", Order = 3 }
            ]
        };

        // Act
        var markdown = await _service.ToMarkdownAsync(document);

        // Assert
        Assert.Contains("# Title", markdown);
        Assert.Contains("## Section", markdown);
        Assert.Contains("Introduction paragraph.", markdown);
    }

    [Fact]
    public async Task ToMarkdownAsync_FormattedRuns_GeneratesCorrectSyntax()
    {
        // Arrange
        var document = new Document
        {
            Elements =
            [
                new ParagraphElement
                {
                    Order = 0,
                    Text = "Bold and italic",
                    Runs =
                    [
                        new TextRun { Text = "Bold", IsBold = true },
                        new TextRun { Text = " and " },
                        new TextRun { Text = "italic", IsItalic = true }
                    ]
                }
            ]
        };

        // Act
        var markdown = await _service.ToMarkdownAsync(document);

        // Assert
        Assert.Contains("**Bold**", markdown);
        Assert.Contains("*italic*", markdown);
    }

    [Fact]
    public async Task ToHtmlAsync_Markdown_GeneratesHtml()
    {
        // Arrange
        var markdown = "# Hello\n\nWorld";

        // Act
        var html = await _service.ToHtmlAsync(markdown);

        // Assert
        Assert.Contains("<h1>Hello</h1>", html);
        Assert.Contains("<p>World</p>", html);
    }

    [Fact]
    public async Task ToPlainTextAsync_FormattedMarkdown_StripsFormatting()
    {
        // Arrange
        var markdown = "# Title\n\n**Bold** and *italic*.";

        // Act
        var plainText = await _service.ToPlainTextAsync(markdown);

        // Assert
        Assert.Contains("Title", plainText);
        Assert.Contains("Bold", plainText);
        Assert.Contains("italic", plainText);
        Assert.DoesNotContain("**", plainText);
        Assert.DoesNotContain("*", plainText);
    }

    [Fact]
    public async Task ParseAsync_Links_ExtractsUrl()
    {
        // Arrange
        var markdown = "Check out [this link](https://example.com).";

        // Act
        var document = await _service.ParseAsync(markdown);

        // Assert
        var paragraph = document.Elements.OfType<ParagraphElement>().First();
        Assert.Contains(paragraph.Runs, r =>
            r.HyperlinkUrl == "https://example.com" && r.Text == "this link");
    }

    [Fact]
    public async Task ParseAsync_InlineCode_MarksAsCode()
    {
        // Arrange
        var markdown = "Use the `Console.WriteLine()` method.";

        // Act
        var document = await _service.ParseAsync(markdown);

        // Assert
        var paragraph = document.Elements.OfType<ParagraphElement>().First();
        Assert.Contains(paragraph.Runs, r => r.IsCode && r.Text == "Console.WriteLine()");
    }

    [Fact]
    public async Task Document_GetWordCount_CalculatesCorrectly()
    {
        // Arrange
        var markdown = "# Title\n\nOne two three four five.";

        // Act
        var document = await _service.ParseAsync(markdown);
        var wordCount = document.GetWordCount();

        // Assert
        Assert.Equal(6, wordCount); // Title + one two three four five
    }

    [Fact]
    public async Task ToMarkdownAsync_Table_GeneratesGfmTable()
    {
        // Arrange
        var document = new Document
        {
            Elements =
            [
                new TableElement
                {
                    Order = 0,
                    HasHeaderRow = true,
                    Rows =
                    [
                        new Models.TableRow
                        {
                            IsHeader = true,
                            Cells = [new Models.TableCell { Text = "A" }, new Models.TableCell { Text = "B" }]
                        },
                        new Models.TableRow
                        {
                            Cells = [new Models.TableCell { Text = "1" }, new Models.TableCell { Text = "2" }]
                        }
                    ]
                }
            ]
        };

        // Act
        var markdown = await _service.ToMarkdownAsync(document);

        // Assert
        Assert.Contains("| A | B |", markdown);
        Assert.Contains("| --- | --- |", markdown);
        Assert.Contains("| 1 | 2 |", markdown);
    }
}
