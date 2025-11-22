using Mostlylucid.DocumentConversion.Models;

namespace Mostlylucid.DocumentConversion.Test;

public class DocumentModelTests
{
    [Fact]
    public void Document_GetPlainText_CombinesAllElements()
    {
        // Arrange
        var document = new Document
        {
            Elements =
            [
                new HeadingElement { Text = "Title", Order = 0 },
                new ParagraphElement { Text = "Paragraph one.", Order = 1 },
                new ParagraphElement { Text = "Paragraph two.", Order = 2 }
            ]
        };

        // Act
        var text = document.GetPlainText();

        // Assert
        Assert.Contains("Title", text);
        Assert.Contains("Paragraph one.", text);
        Assert.Contains("Paragraph two.", text);
    }

    [Fact]
    public void Document_GetHeadings_ReturnsOnlyHeadings()
    {
        // Arrange
        var document = new Document
        {
            Elements =
            [
                new HeadingElement { Text = "H1", Level = 1, Order = 0 },
                new ParagraphElement { Text = "Para", Order = 1 },
                new HeadingElement { Text = "H2", Level = 2, Order = 2 }
            ]
        };

        // Act
        var headings = document.GetHeadings().ToList();

        // Assert
        Assert.Equal(2, headings.Count);
        Assert.Equal("H1", headings[0].Text);
        Assert.Equal("H2", headings[1].Text);
    }

    [Fact]
    public void Document_GetTables_ReturnsOnlyTables()
    {
        // Arrange
        var document = new Document
        {
            Elements =
            [
                new ParagraphElement { Text = "Para", Order = 0 },
                new TableElement { Order = 1 },
                new ParagraphElement { Text = "Another", Order = 2 }
            ]
        };

        // Act
        var tables = document.GetTables().ToList();

        // Assert
        Assert.Single(tables);
    }

    [Fact]
    public void Document_GetWordCount_WithEmptyDocument_ReturnsZero()
    {
        // Arrange
        var document = new Document();

        // Act
        var count = document.GetWordCount();

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public void Document_GetWordCount_CountsWordsCorrectly()
    {
        // Arrange
        var document = new Document
        {
            Elements =
            [
                new HeadingElement { Text = "Three Word Title", Order = 0 },
                new ParagraphElement { Text = "One two three four five.", Order = 1 }
            ]
        };

        // Act
        var count = document.GetWordCount();

        // Assert
        Assert.Equal(8, count);
    }

    [Fact]
    public void ConversionResult_SuccessText_SetsPropertiesCorrectly()
    {
        // Act
        var result = ConversionResult.SuccessText("content", DocFormat.Markdown, "test.md");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("content", result.OutputText);
        Assert.Equal(DocFormat.Markdown, result.OutputFormat);
        Assert.Equal("test.md", result.SuggestedFileName);
        Assert.Equal("text/markdown", result.ContentType);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ConversionResult_SuccessBytes_SetsPropertiesCorrectly()
    {
        // Arrange
        var bytes = new byte[] { 1, 2, 3 };

        // Act
        var result = ConversionResult.SuccessBytes(bytes, DocFormat.Pdf, "test.pdf");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(bytes, result.OutputBytes);
        Assert.Equal(DocFormat.Pdf, result.OutputFormat);
        Assert.Equal("application/pdf", result.ContentType);
    }

    [Fact]
    public void ConversionResult_Failure_SetsErrorMessage()
    {
        // Act
        var result = ConversionResult.Failure("Something went wrong");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Something went wrong", result.ErrorMessage);
        Assert.Null(result.OutputText);
        Assert.Null(result.OutputBytes);
    }

    [Fact]
    public void ImageElement_DataUri_GeneratesCorrectFormat()
    {
        // Arrange
        var image = new ImageElement
        {
            Data = new byte[] { 1, 2, 3 },
            ContentType = "image/png"
        };

        // Act
        var dataUri = image.DataUri;

        // Assert
        Assert.NotNull(dataUri);
        Assert.StartsWith("data:image/png;base64,", dataUri);
    }

    [Fact]
    public void ImageElement_DataUri_WithNoData_ReturnsNull()
    {
        // Arrange
        var image = new ImageElement { ContentType = "image/png" };

        // Act
        var dataUri = image.DataUri;

        // Assert
        Assert.Null(dataUri);
    }

    [Fact]
    public void TextRun_DefaultValues_AreCorrect()
    {
        // Act
        var run = new TextRun { Text = "Test" };

        // Assert
        Assert.Equal("Test", run.Text);
        Assert.False(run.IsBold);
        Assert.False(run.IsItalic);
        Assert.False(run.IsUnderline);
        Assert.False(run.IsStrikethrough);
        Assert.False(run.IsCode);
        Assert.Null(run.HyperlinkUrl);
    }

    [Fact]
    public void ConversionOptions_DefaultValues_AreCorrect()
    {
        // Act
        var options = new ConversionOptions();

        // Assert
        Assert.True(options.IncludeImages);
        Assert.True(options.IncludeTables);
        Assert.True(options.IncludeMetadata);
        Assert.True(options.PreserveFormatting);
        Assert.True(options.UseGitHubFlavoredMarkdown);
        Assert.True(options.EmbedImagesAsDataUri);
        Assert.Equal(PdfPageSize.A4, options.PageSize);
        Assert.Equal(PdfOrientation.Portrait, options.Orientation);
    }

    [Fact]
    public void DocumentElement_AllTypes_HaveCorrectElementType()
    {
        // Arrange & Act & Assert
        Assert.Equal(DocumentElementType.Heading, new HeadingElement().ElementType);
        Assert.Equal(DocumentElementType.Paragraph, new ParagraphElement().ElementType);
        Assert.Equal(DocumentElementType.Table, new TableElement().ElementType);
        Assert.Equal(DocumentElementType.List, new ListElement().ElementType);
        Assert.Equal(DocumentElementType.ListItem, new ListItemElement().ElementType);
        Assert.Equal(DocumentElementType.Image, new ImageElement().ElementType);
        Assert.Equal(DocumentElementType.CodeBlock, new CodeBlockElement().ElementType);
        Assert.Equal(DocumentElementType.BlockQuote, new BlockQuoteElement().ElementType);
        Assert.Equal(DocumentElementType.HorizontalRule, new HorizontalRuleElement().ElementType);
        Assert.Equal(DocumentElementType.Hyperlink, new HyperlinkElement().ElementType);
    }

    [Fact]
    public void TableElement_DefaultHasHeaderRow_IsFalse()
    {
        // Act
        var table = new TableElement();

        // Assert
        Assert.False(table.HasHeaderRow);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public void ListElement_DefaultListType_IsBullet()
    {
        // Act
        var list = new ListElement();

        // Assert
        Assert.Equal(ListType.Bullet, list.ListType);
        Assert.Equal(1, list.StartNumber);
        Assert.Empty(list.Items);
    }
}
