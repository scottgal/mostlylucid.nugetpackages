using System.Globalization;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.XPath;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.ArchiveOrg.Config;
using Mostlylucid.ArchiveOrg.Models;
using ReverseMarkdown;

namespace Mostlylucid.ArchiveOrg.Services;

public partial class HtmlToMarkdownConverter : IHtmlToMarkdownConverter
{
    private readonly ArchiveOrgOptions _archiveOptions;
    private readonly HttpClient _httpClient;
    private readonly ILogger<HtmlToMarkdownConverter> _logger;
    private readonly Converter _markdownConverter;
    private readonly MarkdownConversionOptions _options;
    private readonly IOllamaTagGenerator _tagGenerator;

    public HtmlToMarkdownConverter(
        IOptions<MarkdownConversionOptions> options,
        IOptions<ArchiveOrgOptions> archiveOptions,
        IOllamaTagGenerator tagGenerator,
        HttpClient httpClient,
        ILogger<HtmlToMarkdownConverter> logger)
    {
        _options = options.Value;
        _archiveOptions = archiveOptions.Value;
        _tagGenerator = tagGenerator;
        _httpClient = httpClient;
        _logger = logger;

        // Configure ReverseMarkdown
        _markdownConverter = new Converter(new ReverseMarkdown.Config
        {
            GithubFlavored = true,
            RemoveComments = true,
            SmartHrefHandling = true,
            UnknownTags = ReverseMarkdown.Config.UnknownTagsOption.Bypass,
            TableWithoutHeaderRowHandling = ReverseMarkdown.Config.TableWithoutHeaderRowHandlingOption.EmptyRow
        });
    }

    public async Task<List<MarkdownArticle>> ConvertAllAsync(
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var articles = new List<MarkdownArticle>();

        // Ensure directories exist
        Directory.CreateDirectory(_options.OutputDirectory);
        var imagesDir = Path.Combine(_options.OutputDirectory, _options.ImagesDirectory);
        Directory.CreateDirectory(imagesDir);

        // Get all HTML files
        var htmlFiles = Directory.GetFiles(_options.InputDirectory, _options.FilePattern, SearchOption.AllDirectories);
        var progressReport = new ConversionProgress { TotalFiles = htmlFiles.Length };

        _logger.LogInformation("Found {Count} HTML files to convert", htmlFiles.Length);

        var skippedCount = 0;
        foreach (var htmlFile in htmlFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            progressReport.CurrentFile = htmlFile;
            progress?.Report(progressReport);

            try
            {
                var fileArticles = await ConvertFileAsync(htmlFile, cancellationToken);
                if (fileArticles.Count > 0)
                {
                    articles.AddRange(fileArticles);
                    progressReport.SuccessfulConversions += fileArticles.Count;

                    if (fileArticles.Count > 1)
                        _logger.LogInformation("Split multi-post page into {Count} articles: {File}",
                            fileArticles.Count, htmlFile);
                }
                else
                {
                    // Article was skipped (already exists)
                    skippedCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert {File}", htmlFile);
                progressReport.FailedConversions++;
            }

            progressReport.ProcessedFiles++;
            progress?.Report(progressReport);
        }

        if (skippedCount > 0) _logger.LogInformation("Skipped {Count} already converted files", skippedCount);

        return articles;
    }

    public async Task<List<MarkdownArticle>> ConvertFileAsync(
        string htmlFilePath,
        CancellationToken cancellationToken = default)
    {
        var articles = new List<MarkdownArticle>();

        try
        {
            var html = await File.ReadAllTextAsync(htmlFilePath, cancellationToken);

            // Extract metadata from our archive comment
            var metadata = ExtractArchiveMetadata(html);

            // Check if archive date is within the configured range
            if (metadata.archiveDate != DateTime.MinValue)
            {
                if (_options.StartDate.HasValue && metadata.archiveDate < _options.StartDate.Value)
                {
                    _logger.LogDebug("Skipping file (archive date {Date:yyyy-MM-dd} before StartDate {Start:yyyy-MM-dd}): {File}",
                        metadata.archiveDate, _options.StartDate.Value, Path.GetFileName(htmlFilePath));
                    return articles;
                }

                if (_options.EndDate.HasValue && metadata.archiveDate > _options.EndDate.Value)
                {
                    _logger.LogDebug("Skipping file (archive date {Date:yyyy-MM-dd} after EndDate {End:yyyy-MM-dd}): {File}",
                        metadata.archiveDate, _options.EndDate.Value, Path.GetFileName(htmlFilePath));
                    return articles;
                }
            }

            // Check if this is an index/archive page that should be skipped
            var fileName = Path.GetFileName(htmlFilePath);
            if (ShouldSkipUrl(metadata.originalUrl) || ShouldSkipUrl(fileName))
            {
                _logger.LogInformation("Skipping index/archive page: {File}", fileName);
                return articles;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // IMPORTANT: Extract content FIRST before removing elements
            // (because form removal would destroy div.post which is inside the form)
            var contentNode = ExtractMainContent(doc);
            if (contentNode == null)
            {
                _logger.LogWarning("Could not find main content in {File}", htmlFilePath);
                return articles;
            }

            // Check if this is a multi-post page
            if (!string.IsNullOrEmpty(_options.PostSelector))
            {
                var postNodes = SelectNodesList(contentNode, _options.PostSelector);
                if (postNodes.Count > 1)
                {
                    // This looks like an index page with multiple posts - skip it
                    _logger.LogInformation("Skipping multi-post page ({Count} posts) - likely an index: {Url}",
                        postNodes.Count, metadata.originalUrl);
                    return articles;
                }
            }

            // Now remove unwanted elements from within the content node
            RemoveUnwantedElementsFromNode(contentNode);

            // Single post page - use original logic
            var singleArticle =
                await ConvertSinglePageAsync(contentNode, doc, html, metadata, htmlFilePath, cancellationToken);
            if (singleArticle != null)
            {
                // Check minimum content length
                if (singleArticle.MarkdownContent.Length < _options.MinContentLength)
                {
                    _logger.LogDebug("Skipping page with insufficient content ({Length} chars): {Url}",
                        singleArticle.MarkdownContent.Length, metadata.originalUrl);
                    return articles;
                }

                articles.Add(singleArticle);
            }

            return articles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting {File}", htmlFilePath);
            return articles;
        }
    }

    /// <summary>
    ///     Check if URL matches any skip patterns (index/archive pages)
    /// </summary>
    private bool ShouldSkipUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return false;

        foreach (var pattern in _options.SkipUrlPatterns)
            try
            {
                if (Regex.IsMatch(url, pattern, RegexOptions.IgnoreCase)) return true;
            }
            catch
            {
                // Invalid regex pattern - skip it
            }

        return false;
    }

    /// <summary>
    ///     Convert a single post node from a multi-post page
    /// </summary>
    private async Task<MarkdownArticle?> ConvertPostNodeAsync(
        HtmlNode postNode,
        HtmlDocument doc,
        (string originalUrl, DateTime archiveDate) metadata,
        string htmlFilePath,
        int postIndex,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract title from the post
            var title = ExtractPostTitle(postNode);
            if (string.IsNullOrEmpty(title))
                title = $"Post {postIndex} from {Path.GetFileNameWithoutExtension(htmlFilePath)}";

            // Extract permalink if available
            var permalink = ExtractPostPermalink(postNode);
            var slug = !string.IsNullOrEmpty(permalink)
                ? GenerateSlug(title, permalink)
                : GenerateSlug(title, metadata.originalUrl) + $"-{postIndex}";

            // Check if already converted
            var outputPath = Path.Combine(_options.OutputDirectory, $"{slug}.md");
            if (File.Exists(outputPath))
            {
                var htmlLastWrite = File.GetLastWriteTimeUtc(htmlFilePath);
                var mdLastWrite = File.GetLastWriteTimeUtc(outputPath);
                if (mdLastWrite >= htmlLastWrite)
                {
                    _logger.LogDebug("Skipping already converted post: {Slug}", slug);
                    return null;
                }
            }

            // Rewrite links within this post
            RewriteLinks(postNode, metadata.originalUrl);

            // Handle images in this post
            var images = await ProcessImagesAsync(postNode, metadata.originalUrl, cancellationToken);

            // Convert to markdown
            var markdown = _markdownConverter.Convert(postNode.OuterHtml);
            markdown = CleanMarkdown(markdown);

            // Extract publish date from this post
            DateTime? publishDate = null;
            if (_options.ExtractDates)
                publishDate = ExtractPublishDateFromNode(postNode, _options.DateSelector) ?? metadata.archiveDate;

            // Generate tags if enabled
            List<string> categories = [];
            if (_options.GenerateTags)
                categories = await _tagGenerator.GenerateTagsAsync(title, markdown, cancellationToken);

            var article = new MarkdownArticle
            {
                Title = title,
                Slug = slug,
                OriginalUrl = permalink ?? metadata.originalUrl,
                ArchiveDate = metadata.archiveDate,
                PublishDate = publishDate,
                Categories = categories,
                MarkdownContent = markdown,
                SourceFilePath = htmlFilePath,
                Images = images,
                OutputFilePath = outputPath
            };

            await File.WriteAllTextAsync(outputPath, article.ToFullMarkdown(), cancellationToken);
            _logger.LogInformation("Converted post: {Title} -> {Output}", title, outputPath);

            return article;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting post {Index} from {File}", postIndex, htmlFilePath);
            return null;
        }
    }

    /// <summary>
    ///     Convert a single-post page (original logic)
    /// </summary>
    /// <summary>
    ///     Convert a single-post page to markdown
    /// </summary>
    private async Task<MarkdownArticle?> ConvertSinglePageAsync(
        HtmlNode contentNode,
        HtmlDocument doc,
        string html,
        (string originalUrl, DateTime archiveDate) metadata,
        string htmlFilePath,
        CancellationToken cancellationToken)
    {
        // Check if already converted (reentrant support)
        var potentialSlug = GenerateSlug(Path.GetFileNameWithoutExtension(htmlFilePath), metadata.originalUrl);
        var potentialOutputPath = Path.Combine(_options.OutputDirectory, $"{potentialSlug}.md");
        if (File.Exists(potentialOutputPath))
        {
            var htmlLastWrite = File.GetLastWriteTimeUtc(htmlFilePath);
            var mdLastWrite = File.GetLastWriteTimeUtc(potentialOutputPath);

            // Check if this archive is newer by looking at the archive date in the existing markdown
            var existingMd = await File.ReadAllTextAsync(potentialOutputPath, cancellationToken);
            var existingArchiveDateMatch = Regex.Match(existingMd, @"archiveDate:\s*(\d{4}-\d{2}-\d{2})");
            if (existingArchiveDateMatch.Success &&
                DateTime.TryParse(existingArchiveDateMatch.Groups[1].Value, out var existingArchiveDate))
            {
                if (metadata.archiveDate > existingArchiveDate)
                {
                    _logger.LogInformation(
                        "Newer archive found ({New:yyyy-MM-dd} > {Old:yyyy-MM-dd}), re-converting: {File}",
                        metadata.archiveDate, existingArchiveDate, Path.GetFileName(htmlFilePath));
                    // Continue to re-convert
                }
                else if (mdLastWrite >= htmlLastWrite)
                {
                    _logger.LogDebug("Skipping already converted (archive {Date:yyyy-MM-dd}): {File}",
                        existingArchiveDate, Path.GetFileName(htmlFilePath));
                    return null;
                }
            }
            else if (mdLastWrite >= htmlLastWrite)
            {
                _logger.LogDebug("Skipping already converted: {File}", Path.GetFileName(htmlFilePath));
                return null;
            }
        }

        // Content node is already extracted and passed in

        // Extract title (before removing it from content)
        var title = ExtractTitle(doc, contentNode);
        if (string.IsNullOrEmpty(title)) title = Path.GetFileNameWithoutExtension(htmlFilePath);

        // Remove title element from content (it goes in frontmatter)
        RemoveTitleFromContent(contentNode);

        // Remove date/footer elements from content (date goes in frontmatter)
        RemoveDateFooterFromContent(contentNode);

        // Rewrite links to be blog-relative
        RewriteLinks(contentNode, metadata.originalUrl);

        // Handle images
        var images = await ProcessImagesAsync(contentNode, metadata.originalUrl, cancellationToken);

        // Convert to markdown
        var markdown = _markdownConverter.Convert(contentNode.OuterHtml);
        markdown = CleanMarkdown(markdown);

        // Extract or infer publish date
        DateTime? publishDate = null;
        if (_options.ExtractDates)
            publishDate = ExtractPublishDate(doc, html, _options.DateSelector) ?? metadata.archiveDate;

        // Generate slug
        var slug = GenerateSlug(title, metadata.originalUrl);

        // Generate tags using LLM if enabled
        List<string> categories = [];
        if (_options.GenerateTags)
            categories = await _tagGenerator.GenerateTagsAsync(title, markdown, cancellationToken);

        var article = new MarkdownArticle
        {
            Title = title,
            Slug = slug,
            OriginalUrl = metadata.originalUrl,
            ArchiveDate = metadata.archiveDate,
            PublishDate = publishDate,
            Categories = categories,
            MarkdownContent = markdown,
            SourceFilePath = htmlFilePath,
            Images = images
        };

        // Write the markdown file
        var outputPath = Path.Combine(_options.OutputDirectory, $"{slug}.md");
        article.OutputFilePath = outputPath;
        await File.WriteAllTextAsync(outputPath, article.ToFullMarkdown(), cancellationToken);

        _logger.LogInformation("Converted: {File} -> {Output}", htmlFilePath, outputPath);
        return article;
    }

    /// <summary>
    ///     Extract the title from a post node using the configured PostTitleSelector
    /// </summary>
    private string ExtractPostTitle(HtmlNode postNode)
    {
        if (!string.IsNullOrEmpty(_options.PostTitleSelector))
        {
            var titleNode = SelectSingleNode(postNode, _options.PostTitleSelector);
            if (titleNode != null) return HttpUtility.HtmlDecode(titleNode.InnerText.Trim());
        }

        // Fallback to h1, h2, h3
        foreach (var tag in new[] { "h1", "h2", "h3" })
        {
            var heading = postNode.SelectSingleNode($".//{tag}");
            if (heading != null) return HttpUtility.HtmlDecode(heading.InnerText.Trim());
        }

        return string.Empty;
    }

    /// <summary>
    ///     Extract the permalink from a post node using the configured PostLinkSelector
    /// </summary>
    private string ExtractPostPermalink(HtmlNode postNode)
    {
        if (!string.IsNullOrEmpty(_options.PostLinkSelector))
        {
            var linkNode = SelectSingleNode(postNode, _options.PostLinkSelector);
            if (linkNode != null)
            {
                var href = linkNode.GetAttributeValue("href", null);
                if (!string.IsNullOrEmpty(href)) return href;
            }
        }

        // Fallback - look for any link in the title
        var titleLink = postNode.SelectSingleNode(".//h1/a | .//h2/a | .//h3/a");
        return titleLink?.GetAttributeValue("href", string.Empty) ?? string.Empty;
    }

    /// <summary>
    ///     Extract publish date from a specific node
    /// </summary>
    private DateTime? ExtractPublishDateFromNode(HtmlNode node, string dateSelector)
    {
        if (!string.IsNullOrEmpty(dateSelector))
        {
            var dateNode = SelectSingleNode(node, dateSelector);
            if (dateNode != null)
            {
                var dateText = HttpUtility.HtmlDecode(dateNode.InnerText.Trim());
                var extractedDate = ParsePostFootDate(dateText);
                if (extractedDate.HasValue)
                    return extractedDate;

                if (DateTime.TryParse(dateText, out var date))
                    return date;
            }
        }

        // Try common patterns within the node
        var timeNode = node.SelectSingleNode(".//time[@datetime]");
        if (timeNode != null)
        {
            var dateStr = timeNode.GetAttributeValue("datetime", null);
            if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var date))
                return date;
        }

        return null;
    }

    /// <summary>
    ///     Helper to select nodes using CSS-like selectors
    /// </summary>
    private static List<HtmlNode> SelectNodesList(HtmlNode node, string selector)
    {
        // Handle class selector (e.g., .post)
        if (selector.StartsWith('.'))
        {
            var className = selector.TrimStart('.');
            return node.Descendants()
                .Where(n => HasExactClass(n, className))
                .ToList();
        }

        // Handle ID selector (e.g., #content)
        if (selector.StartsWith('#'))
        {
            var id = selector.TrimStart('#');
            var result = node.Descendants()
                .FirstOrDefault(n => n.GetAttributeValue("id", "") == id);
            return result != null ? [result] : [];
        }

        // Handle element.class selector (e.g., div.post)
        if (selector.Contains('.') && !selector.StartsWith('.'))
        {
            var parts = selector.Split('.', 2);
            var elementName = parts[0];
            var className = parts[1];
            return node.Descendants()
                .Where(n =>
                    n.Name.Equals(elementName, StringComparison.OrdinalIgnoreCase) &&
                    HasExactClass(n, className))
                .ToList();
        }

        // Handle element selector (e.g., article)
        return node.Descendants()
            .Where(n => n.Name.Equals(selector, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    ///     Helper to select nodes - returns HtmlNodeCollection-like wrapper for compatibility
    /// </summary>
    private static HtmlNodeCollection? SelectNodes(HtmlNode node, string selector)
    {
        var list = SelectNodesList(node, selector);
        if (list.Count == 0)
            return null;

        // Create a collection from the list
        var collection = new HtmlNodeCollection(node);
        foreach (var item in list) collection.Add(item);
        return collection;
    }

    /// <summary>
    ///     Helper to select a single node using CSS-like selectors
    /// </summary>
    private static HtmlNode? SelectSingleNode(HtmlNode node, string selector)
    {
        // Handle class selector (e.g., .post-title)
        if (selector.StartsWith('.'))
        {
            var className = selector.TrimStart('.');
            return node.Descendants()
                .FirstOrDefault(n => HasExactClass(n, className));
        }

        // Handle ID selector
        if (selector.StartsWith('#'))
        {
            var id = selector.TrimStart('#');
            return node.Descendants()
                .FirstOrDefault(n => n.GetAttributeValue("id", "") == id);
        }

        // Handle element.class selector (e.g., div.post)
        if (selector.Contains('.') && !selector.StartsWith('.'))
        {
            var parts = selector.Split('.', 2);
            var elementName = parts[0];
            var className = parts[1];
            return node.Descendants()
                .FirstOrDefault(n =>
                    n.Name.Equals(elementName, StringComparison.OrdinalIgnoreCase) &&
                    HasExactClass(n, className));
        }

        // Handle element selector
        return node.Descendants()
            .FirstOrDefault(n => n.Name.Equals(selector, StringComparison.OrdinalIgnoreCase));
    }

    private static (string originalUrl, DateTime archiveDate) ExtractArchiveMetadata(string html)
    {
        var originalUrl = string.Empty;
        var archiveDate = DateTime.MinValue;

        var urlMatch = OriginalUrlRegex().Match(html);
        if (urlMatch.Success) originalUrl = urlMatch.Groups[1].Value.Trim();

        var dateMatch = ArchiveDateRegex().Match(html);
        if (dateMatch.Success && DateTime.TryParse(dateMatch.Groups[1].Value.Trim(), out var date)) archiveDate = date;

        return (originalUrl, archiveDate);
    }

    private void RemoveUnwantedElements(HtmlDocument doc)
    {
        foreach (var selector in _options.RemoveSelectors)
        {
            HtmlNodeCollection? nodes = null;

            try
            {
                // Handle class selector (e.g., .sidebar)
                if (selector.StartsWith('.'))
                {
                    var className = selector.TrimStart('.');
                    nodes = doc.DocumentNode.SelectNodes($"//*[contains(@class, '{className}')]");
                }
                // Handle ID selector (e.g., #header)
                else if (selector.StartsWith('#'))
                {
                    var id = selector.TrimStart('#');
                    nodes = doc.DocumentNode.SelectNodes($"//*[@id='{id}']");
                }
                // Handle element selector (e.g., nav, script)
                else
                {
                    nodes = doc.DocumentNode.SelectNodes($"//{selector}");
                }
            }
            catch (XPathException)
            {
                // Skip invalid selectors
                _logger.LogWarning("Invalid selector skipped: {Selector}", selector);
            }

            if (nodes != null)
                foreach (var node in nodes.ToList())
                    node.Remove();
        }
    }

    /// <summary>
    ///     Remove unwanted elements from within a specific node (not the whole document)
    ///     Uses HtmlAgilityPack Descendants for reliable element matching
    /// </summary>
    private void RemoveUnwantedElementsFromNode(HtmlNode contentNode)
    {
        foreach (var selector in _options.RemoveSelectors)
        {
            var nodesToRemove = new List<HtmlNode>();

            // Handle class selector (e.g., .sidebar)
            if (selector.StartsWith('.'))
            {
                var className = selector.TrimStart('.');
                nodesToRemove.AddRange(contentNode.Descendants()
                    .Where(n => HasExactClass(n, className)));
            }
            // Handle ID selector (e.g., #header)
            else if (selector.StartsWith('#'))
            {
                var id = selector.TrimStart('#');
                var node = contentNode.Descendants()
                    .FirstOrDefault(n => n.GetAttributeValue("id", "") == id);
                if (node != null)
                    nodesToRemove.Add(node);
            }
            // Handle element selector (e.g., nav, script, form)
            else
            {
                nodesToRemove.AddRange(contentNode.Descendants()
                    .Where(n => n.Name.Equals(selector, StringComparison.OrdinalIgnoreCase)));
            }

            foreach (var node in nodesToRemove) node.Remove();
        }
    }

    /// <summary>
    ///     Remove the title h2 element from content (since title goes in frontmatter)
    /// </summary>
    private static void RemoveTitleFromContent(HtmlNode contentNode)
    {
        // Remove h2.postTitle (2005+ template)
        var h2PostTitle = contentNode.Descendants("h2")
            .FirstOrDefault(n => HasExactClass(n, "postTitle"));
        h2PostTitle?.Remove();

        // Remove div.posttitle (singlepost template)
        var divPostTitle = contentNode.Descendants("div")
            .FirstOrDefault(n => HasExactClass(n, "posttitle"));
        divPostTitle?.Remove();

        // Remove first h2 if it exists (2003-2004 template) - but only if it's the title
        // Check if the h2 is at the start of the content
        var firstH2 = contentNode.Descendants("h2").FirstOrDefault();
        if (firstH2 != null)
        {
            // Only remove if it's near the start of the content (within first few elements)
            var childIndex = contentNode.ChildNodes.ToList().IndexOf(firstH2);
            if (childIndex >= 0 && childIndex < 3) firstH2.Remove();
        }
    }

    /// <summary>
    ///     Remove date/footer elements from content (since date goes in frontmatter)
    /// </summary>
    private static void RemoveDateFooterFromContent(HtmlNode contentNode)
    {
        // Remove .postfoot (2003-2004 template)
        var postfoot = contentNode.Descendants()
            .Where(n => HasExactClass(n, "postfoot"))
            .ToList();
        foreach (var node in postfoot) node.Remove();

        // Remove .postfooter (2005+ template)
        var postfooter = contentNode.Descendants()
            .Where(n => HasExactClass(n, "postfooter"))
            .ToList();
        foreach (var node in postfooter) node.Remove();

        // Remove p.postfooter
        var pPostfooter = contentNode.Descendants("p")
            .Where(n => HasExactClass(n, "postfooter"))
            .ToList();
        foreach (var node in pPostfooter) node.Remove();

        // Remove .itemdesc (singlepost template)
        var itemdesc = contentNode.Descendants()
            .Where(n => HasExactClass(n, "itemdesc"))
            .ToList();
        foreach (var node in itemdesc) node.Remove();
    }

    private HtmlNode? ExtractMainContent(HtmlDocument doc)
    {
        // Try configured selector first
        if (!string.IsNullOrEmpty(_options.ContentSelector))
        {
            var selector = _options.ContentSelector;
            HtmlNode? node = null;

            _logger.LogDebug("Looking for content with selector: '{Selector}'", selector);

            // Handle element.class format (e.g., div.post)
            if (selector.Contains('.') && !selector.StartsWith('.') && !selector.StartsWith('#'))
            {
                var parts = selector.Split('.', 2);
                var elementName = parts[0].ToLowerInvariant();
                var className = parts[1];

                _logger.LogDebug("Parsed as element.class: element='{Element}', class='{Class}'",
                    elementName, className);

                // List all divs with their classes for debugging
                var allDivs = doc.DocumentNode.Descendants("div").ToList();
                _logger.LogDebug("Found {Count} div elements in document", allDivs.Count);

                foreach (var div in allDivs.Take(10))
                {
                    var cls = div.GetAttributeValue("class", "(no class)");
                    _logger.LogDebug("  div class='{Class}'", cls);
                }

                // Use HtmlAgilityPack's Descendants to find the element
                node = doc.DocumentNode.Descendants()
                    .FirstOrDefault(n =>
                        n.Name.Equals(elementName, StringComparison.OrdinalIgnoreCase) &&
                        HasExactClass(n, className));

                if (node != null)
                {
                    _logger.LogInformation("Found content using selector '{Selector}', node has {Length} chars",
                        selector, node.InnerHtml.Length);
                    return node;
                }

                // Try fallback selectors for different template versions
                // 2003-2004 used div.post, another 2003 version used div.singlepost, 2005+ used div.blogpost
                var fallbackSelectors = new[] { "div.blogpost", "div.singlepost", "div.post-content", "article" };
                foreach (var fallback in fallbackSelectors)
                {
                    var fallbackParts = fallback.Split('.', 2);
                    if (fallbackParts.Length == 2)
                    {
                        var fbElement = fallbackParts[0];
                        var fbClass = fallbackParts[1];
                        node = doc.DocumentNode.Descendants()
                            .FirstOrDefault(n =>
                                n.Name.Equals(fbElement, StringComparison.OrdinalIgnoreCase) &&
                                HasExactClass(n, fbClass));
                    }
                    else
                    {
                        node = doc.DocumentNode.Descendants()
                            .FirstOrDefault(n => n.Name.Equals(fallback, StringComparison.OrdinalIgnoreCase));
                    }

                    if (node != null)
                    {
                        _logger.LogInformation(
                            "Found content using fallback selector '{Selector}', node has {Length} chars",
                            fallback, node.InnerHtml.Length);
                        return node;
                    }
                }

                _logger.LogWarning("No element found matching selector '{Selector}' or fallbacks", selector);
            }
            // Handle ID selector (e.g., #PostBody)
            else if (selector.StartsWith('#'))
            {
                var id = selector.TrimStart('#');
                node = doc.DocumentNode.Descendants()
                    .FirstOrDefault(n => n.GetAttributeValue("id", "") == id);
            }
            // Handle class selector (e.g., .post-content)
            else if (selector.StartsWith('.'))
            {
                var className = selector.TrimStart('.');
                node = doc.DocumentNode.Descendants()
                    .FirstOrDefault(n => HasExactClass(n, className));
            }
            // Handle element selector (e.g., article)
            else
            {
                node = doc.DocumentNode.Descendants()
                    .FirstOrDefault(n => n.Name.Equals(selector, StringComparison.OrdinalIgnoreCase));
            }

            if (node != null)
            {
                _logger.LogInformation("Found content using selector '{Selector}', node has {Length} chars",
                    selector, node.InnerHtml.Length);
                return node;
            }

            _logger.LogWarning("ContentSelector '{Selector}' not found, falling back", selector);
        }

        // Try common content selectors using Descendants
        var commonSelectors = new (string elementName, string? className, string? id)[]
        {
            ("article", null, null),
            ("main", null, null),
            (null!, null, "content"),
            (null!, null, "main-content"),
            (null!, null, "PostBody"),
            (null!, "post-content", null),
            (null!, "entry-content", null),
            (null!, "article-content", null),
            (null!, "blog-post", null)
        };

        foreach (var (elementName, className, id) in commonSelectors)
        {
            HtmlNode? node = null;

            if (!string.IsNullOrEmpty(id))
                node = doc.DocumentNode.Descendants()
                    .FirstOrDefault(n => n.GetAttributeValue("id", "") == id);
            else if (!string.IsNullOrEmpty(className))
                node = doc.DocumentNode.Descendants()
                    .FirstOrDefault(n => HasExactClass(n, className));
            else if (!string.IsNullOrEmpty(elementName))
                node = doc.DocumentNode.Descendants()
                    .FirstOrDefault(n => n.Name.Equals(elementName, StringComparison.OrdinalIgnoreCase));

            if (node != null)
                return node;
        }

        // Fall back to body
        return doc.DocumentNode.Descendants()
            .FirstOrDefault(n => n.Name.Equals("body", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Check if a node has an exact class match (not partial substring)
    /// </summary>
    private static bool HasExactClass(HtmlNode node, string className)
    {
        var classAttr = node.GetAttributeValue("class", "");
        if (string.IsNullOrEmpty(classAttr))
            return false;

        // Split class attribute and check for exact match
        var classes = classAttr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return classes.Contains(className, StringComparer.OrdinalIgnoreCase);
    }

    private static string ExtractTitle(HtmlDocument doc, HtmlNode contentNode)
    {
        // Look for h2 inside the content node (the blog post title)
        // Try h2.postTitle first (2005+ template)
        var h2PostTitle = contentNode.Descendants("h2")
            .FirstOrDefault(n => HasExactClass(n, "postTitle"));
        if (h2PostTitle != null)
            return HttpUtility.HtmlDecode(h2PostTitle.InnerText.Trim());

        // Try div.posttitle with a.singleposttitle (another 2003 template)
        var divPostTitle = contentNode.Descendants("div")
            .FirstOrDefault(n => HasExactClass(n, "posttitle"));
        if (divPostTitle != null)
            return HttpUtility.HtmlDecode(divPostTitle.InnerText.Trim());

        // Try a.singleposttitle directly
        var aSinglePostTitle = contentNode.Descendants("a")
            .FirstOrDefault(n => HasExactClass(n, "singleposttitle"));
        if (aSinglePostTitle != null)
            return HttpUtility.HtmlDecode(aSinglePostTitle.InnerText.Trim());

        // Regular h2 (2003-2004 template)
        var h2 = contentNode.Descendants("h2").FirstOrDefault();
        if (h2 != null)
            return HttpUtility.HtmlDecode(h2.InnerText.Trim());

        // Fallback to h1 in content
        var h1 = contentNode.Descendants("h1").FirstOrDefault();
        if (h1 != null)
            return HttpUtility.HtmlDecode(h1.InnerText.Trim());

        return string.Empty;
    }

    private void RewriteLinks(HtmlNode contentNode, string originalUrl)
    {
        if (string.IsNullOrEmpty(originalUrl))
            return;

        Uri? baseUri = null;
        try
        {
            baseUri = new Uri(originalUrl);
        }
        catch
        {
            return;
        }

        var targetHost = !string.IsNullOrEmpty(_archiveOptions.TargetUrl)
            ? new Uri(_archiveOptions.TargetUrl).Host
            : baseUri.Host;

        // Get list of downloaded files to check for local link availability
        var downloadedUrls = GetDownloadedUrlSet();

        // Rewrite all href and src attributes
        var linksAndImages = contentNode.SelectNodes(".//*[@href or @src]");
        if (linksAndImages == null)
            return;

        foreach (var node in linksAndImages)
        {
            // Handle href
            var href = node.GetAttributeValue("href", null);
            if (!string.IsNullOrEmpty(href))
            {
                var rewritten = RewriteUrlWithArchiveFallback(href, baseUri, targetHost, downloadedUrls);
                node.SetAttributeValue("href", rewritten);
            }

            // Handle src (images, scripts - less likely to need archive fallback)
            var src = node.GetAttributeValue("src", null);
            if (!string.IsNullOrEmpty(src))
            {
                var rewritten = RewriteUrl(src, baseUri, targetHost);
                node.SetAttributeValue("src", rewritten);
            }
        }
    }

    /// <summary>
    ///     Get a set of URLs that have been downloaded (from archive metadata in HTML files)
    /// </summary>
    private HashSet<string> GetDownloadedUrlSet()
    {
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Use the input directory where HTML files are stored (same as markdown conversion input)
        if (!Directory.Exists(_options.InputDirectory))
            return urls;

        foreach (var htmlFile in Directory.GetFiles(_options.InputDirectory, "*.html"))
            try
            {
                // Read just the first few lines to get metadata
                using var reader = new StreamReader(htmlFile);
                var buffer = new char[1000];
                reader.Read(buffer, 0, 1000);
                var header = new string(buffer);

                var urlMatch = OriginalUrlRegex().Match(header);
                if (urlMatch.Success)
                {
                    var url = urlMatch.Groups[1].Value.Trim();
                    urls.Add(url);

                    // Also add the path-only version for relative link matching
                    if (Uri.TryCreate(url, UriKind.Absolute, out var uri)) urls.Add(uri.AbsolutePath);
                }
            }
            catch
            {
                // Skip files we can't read
            }

        return urls;
    }

    /// <summary>
    ///     Rewrite URL with fallback to Archive.org for broken local links
    /// </summary>
    private string RewriteUrlWithArchiveFallback(string url, Uri baseUri, string targetHost,
        HashSet<string> downloadedUrls)
    {
        // Skip empty, mailto, tel, javascript, and anchor links
        if (string.IsNullOrEmpty(url) ||
            url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith('#'))
            return url;

        // Remove Wayback Machine URL prefix if present
        var waybackMatch = Regex.Match(url, @"https?://web\.archive\.org/web/\d+[a-z_]*/(.+)");
        if (waybackMatch.Success) url = waybackMatch.Groups[1].Value;

        // Check if it's a local/relative link
        var isLocalLink = url.StartsWith('/') ||
                          (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                           !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

        if (isLocalLink)
        {
            // Resolve to absolute URL for checking
            string absolutePath;
            string fullUrl;

            if (url.StartsWith('/'))
            {
                absolutePath = url;
                fullUrl = $"{baseUri.Scheme}://{baseUri.Host}{url}";
            }
            else
            {
                // Relative path
                if (Uri.TryCreate(baseUri, url, out var resolvedUri))
                {
                    absolutePath = resolvedUri.AbsolutePath;
                    fullUrl = resolvedUri.ToString();
                }
                else
                {
                    return url; // Can't resolve, return as-is
                }
            }

            // Check if we have this URL downloaded
            var hasLocalCopy = downloadedUrls.Contains(absolutePath) ||
                               downloadedUrls.Contains(fullUrl) ||
                               downloadedUrls.Any(u => u.EndsWith(absolutePath, StringComparison.OrdinalIgnoreCase));

            if (!hasLocalCopy)
            {
                // Convert to Archive.org wayback URL
                var archiveUrl = $"https://web.archive.org/web/{fullUrl}";
                _logger.LogDebug("Broken local link {Url} -> Archive.org fallback", url);
                return archiveUrl;
            }

            // Convert to blog-style URL: /articles/foo.htm -> /blog/foo
            var blogUrl = ConvertToBlogUrl(absolutePath);
            _logger.LogDebug("Local link {Url} -> {BlogUrl}", url, blogUrl);
            return blogUrl;
        }

        // For external absolute URLs, use the standard rewrite
        return RewriteUrl(url, baseUri, targetHost);
    }

    /// <summary>
    ///     Convert archive-style paths to blog format
    ///     e.g., /articles/nestedrepeaters.htm -> /blog/nestedrepeaters
    ///     /archive/2004/05/27/1054.aspx -> /blog/1054
    /// </summary>
    private static string ConvertToBlogUrl(string absolutePath)
    {
        // Extract the filename without extension
        var fileName = Path.GetFileNameWithoutExtension(absolutePath);

        if (string.IsNullOrEmpty(fileName) || fileName.Equals("index", StringComparison.OrdinalIgnoreCase))
        {
            // For index pages, use the parent directory name
            var segments = absolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 1) fileName = segments[^2]; // Second to last segment
        }

        // Sanitize the slug
        var slug = fileName.ToLowerInvariant();
        slug = Regex.Replace(slug, @"[\s_]+", "-");
        slug = Regex.Replace(slug, @"[^a-z0-9\-]", string.Empty);
        slug = Regex.Replace(slug, @"-+", "-").Trim('-');

        return $"/blog/{slug}";
    }

    private static string RewriteUrl(string url, Uri baseUri, string targetHost)
    {
        // Skip empty, mailto, tel, javascript, and anchor links
        if (string.IsNullOrEmpty(url) ||
            url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith('#'))
            return url;

        // Remove Wayback Machine URL prefix if present
        var waybackMatch = Regex.Match(url, @"https?://web\.archive\.org/web/\d+[a-z_]*/(.+)");
        if (waybackMatch.Success) url = waybackMatch.Groups[1].Value;

        // Try to parse as absolute URL
        if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
        {
            // If it's from the same domain, convert to relative
            if (absoluteUri.Host.Equals(targetHost, StringComparison.OrdinalIgnoreCase) ||
                absoluteUri.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase))
                return absoluteUri.PathAndQuery;

            // External link - keep as-is
            return url;
        }

        // Already relative or invalid - return as-is
        return url;
    }

    private async Task<List<ImageInfo>> ProcessImagesAsync(
        HtmlNode contentNode,
        string originalUrl,
        CancellationToken cancellationToken)
    {
        var images = new List<ImageInfo>();

        if (!_options.PreserveImages)
            return images;

        var imgNodes = contentNode.SelectNodes(".//img[@src]");
        if (imgNodes == null)
            return images;

        Uri? baseUri = null;
        if (!string.IsNullOrEmpty(originalUrl)) Uri.TryCreate(originalUrl, UriKind.Absolute, out baseUri);

        var imagesDir = Path.Combine(_options.OutputDirectory, _options.ImagesDirectory);

        // Ensure images directory exists
        Directory.CreateDirectory(imagesDir);

        foreach (var img in imgNodes)
        {
            var src = img.GetAttributeValue("src", null);
            if (string.IsNullOrEmpty(src))
                continue;

            try
            {
                // Resolve to absolute URL
                var absoluteUrl = src;
                if (!Uri.TryCreate(src, UriKind.Absolute, out var imgUri))
                {
                    if (baseUri != null && Uri.TryCreate(baseUri, src, out var resolvedUri))
                        absoluteUrl = resolvedUri.ToString();
                    else
                        continue;
                }

                // Generate local filename
                var fileName = GenerateImageFileName(absoluteUrl);
                var localPath = Path.Combine(imagesDir, fileName);
                // Just use the filename - the blog renderer fixes up the path
                var markdownPath = fileName;

                var imageInfo = new ImageInfo
                {
                    OriginalUrl = absoluteUrl,
                    LocalPath = localPath,
                    MarkdownPath = markdownPath
                };

                // Download if not already exists
                if (!File.Exists(localPath))
                {
                    var downloaded = false;

                    // Try Archive.org Wayback Machine first (original images are likely 404)
                    var waybackUrl = $"https://web.archive.org/web/{absoluteUrl}";
                    try
                    {
                        var imageData = await _httpClient.GetByteArrayAsync(waybackUrl, cancellationToken);
                        await File.WriteAllBytesAsync(localPath, imageData, cancellationToken);
                        downloaded = true;
                        _logger.LogDebug("Downloaded image from Wayback: {Url}", absoluteUrl);
                    }
                    catch
                    {
                        // Wayback failed, try original URL as fallback
                        try
                        {
                            var imageData = await _httpClient.GetByteArrayAsync(absoluteUrl, cancellationToken);
                            await File.WriteAllBytesAsync(localPath, imageData, cancellationToken);
                            downloaded = true;
                            _logger.LogDebug("Downloaded image from original: {Url}", absoluteUrl);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(
                                "Failed to download image from both Wayback and original: {Url} - {Error}",
                                absoluteUrl, ex.Message);
                        }
                    }

                    imageInfo.Downloaded = downloaded;
                }
                else
                {
                    imageInfo.Downloaded = true;
                }

                // Update the img src to the local path
                if (imageInfo.Downloaded) img.SetAttributeValue("src", markdownPath);

                images.Add(imageInfo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing image: {Src}", src);
            }
        }

        return images;
    }

    private static string GenerateImageFileName(string url)
    {
        var uri = new Uri(url);
        var fileName = Path.GetFileName(uri.AbsolutePath);

        if (string.IsNullOrEmpty(fileName)) fileName = $"image_{Guid.NewGuid():N}.jpg";

        // Sanitize filename
        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var c in invalidChars) fileName = fileName.Replace(c, '_');

        // Ensure uniqueness by adding hash
        var hash = url.GetHashCode().ToString("X8");
        var ext = Path.GetExtension(fileName);
        var name = Path.GetFileNameWithoutExtension(fileName);

        return $"{name}_{hash}{ext}";
    }

    private static DateTime? ExtractPublishDate(HtmlDocument doc, string html, string dateSelector)
    {
        // Try configured date selector first (e.g., ".postfoot")
        if (!string.IsNullOrEmpty(dateSelector))
        {
            HtmlNode? dateNode = null;

            // Handle class selector (e.g., .postfoot)
            if (dateSelector.StartsWith('.'))
            {
                var className = dateSelector.TrimStart('.');
                dateNode = doc.DocumentNode.SelectSingleNode($"//*[contains(@class, '{className}')]");
            }
            // Handle ID selector (e.g., #postdate)
            else if (dateSelector.StartsWith('#'))
            {
                var id = dateSelector.TrimStart('#');
                dateNode = doc.DocumentNode.SelectSingleNode($"//*[@id='{id}']");
            }
            else
            {
                dateNode = doc.DocumentNode.SelectSingleNode($"//{dateSelector}");
            }

            if (dateNode != null)
            {
                var dateText = HttpUtility.HtmlDecode(dateNode.InnerText.Trim());
                var extractedDate = ParsePostFootDate(dateText);
                if (extractedDate.HasValue)
                    return extractedDate;
            }
        }

        // Try common date meta tags and class selectors
        // Include templates: .postfoot (2003-2004), .postfooter (2005+), .itemdesc (singlepost)
        var metaSelectors = new[]
        {
            "//meta[@property='article:published_time']",
            "//meta[@name='date']",
            "//meta[@name='pubdate']",
            "//meta[@name='DC.date.issued']",
            "//time[@datetime]",
            "//*[@class='date']",
            "//*[@class='post-date']",
            "//*[@class='entry-date']",
            "//*[@class='postfoot']",
            "//*[@class='postfooter']",
            "//p[contains(@class, 'postfooter')]",
            "//*[@class='itemdesc']"
        };

        foreach (var selector in metaSelectors)
        {
            var node = doc.DocumentNode.SelectSingleNode(selector);
            if (node != null)
            {
                var dateStr = node.GetAttributeValue("content", null) ??
                              node.GetAttributeValue("datetime", null) ??
                              HttpUtility.HtmlDecode(node.InnerText);

                // Try parsing "posted on ..." format first
                var extractedDate = ParsePostFootDate(dateStr);
                if (extractedDate.HasValue)
                    return extractedDate;

                // Try standard date parsing
                if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var date)) return date;
            }
        }

        // Try to find date patterns in the HTML
        var datePatterns = new[]
        {
            @"(\d{4}-\d{2}-\d{2})",
            @"(\d{1,2}/\d{1,2}/\d{4})",
            @"((?:January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2},?\s+\d{4})"
        };

        foreach (var pattern in datePatterns)
        {
            var match = Regex.Match(html, pattern);
            if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var date)) return date;
        }

        return null;
    }

    /// <summary>
    ///     Parse date from "posted on Thursday, May 27, 2004 11:21 PM" format
    /// </summary>
    private static DateTime? ParsePostFootDate(string text)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        // Pattern: "posted on DayName, Month Day, Year Time AM/PM"
        var postFootPattern = PostFootDateRegex();
        var match = postFootPattern.Match(text);

        if (match.Success)
        {
            var dateStr = match.Groups[1].Value.Trim();
            // Try to parse with multiple formats
            var formats = new[]
            {
                "MMMM d, yyyy h:mm tt",
                "MMMM dd, yyyy h:mm tt",
                "MMMM d, yyyy hh:mm tt",
                "MMMM dd, yyyy hh:mm tt",
                "MMMM d, yyyy h:mm:ss tt",
                "MMMM dd, yyyy h:mm:ss tt"
            };

            foreach (var format in formats)
                if (DateTime.TryParseExact(dateStr, format,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var parsedDate))
                    return parsedDate;

            // Fall back to standard parsing
            if (DateTime.TryParse(dateStr, out var date))
                return date;
        }

        return null;
    }

    [GeneratedRegex(@"posted\s+on\s+\w+,?\s*(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex PostFootDateRegex();

    private static string GenerateSlug(string title, string originalUrl)
    {
        // Try to get slug from URL path first
        if (!string.IsNullOrEmpty(originalUrl))
            try
            {
                var uri = new Uri(originalUrl);
                var path = uri.AbsolutePath.Trim('/');

                // Take the last path segment as slug
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length > 0)
                {
                    var lastSegment = segments[^1];
                    // Remove file extensions
                    lastSegment = Path.GetFileNameWithoutExtension(lastSegment);
                    if (!string.IsNullOrEmpty(lastSegment) && lastSegment != "index") return SanitizeSlug(lastSegment);
                }
            }
            catch
            {
                // Fall through to title-based slug
            }

        // Generate from title
        return SanitizeSlug(title);
    }

    private static string SanitizeSlug(string input)
    {
        // Convert to lowercase
        var slug = input.ToLowerInvariant();

        // Replace spaces and common separators with hyphens
        slug = Regex.Replace(slug, @"[\s_]+", "-");

        // Remove non-alphanumeric characters except hyphens
        slug = Regex.Replace(slug, @"[^a-z0-9\-]", string.Empty);

        // Remove multiple consecutive hyphens
        slug = Regex.Replace(slug, @"-+", "-");

        // Trim hyphens from start and end
        slug = slug.Trim('-');

        return slug;
    }

    private static string CleanMarkdown(string markdown)
    {
        // Remove leading whitespace from each line (except code blocks)
        // This fixes old HTML that had indented content which markdown interprets as code blocks
        var lines = markdown.Split('\n');
        var cleanedLines = new List<string>();
        var inCodeBlock = false;

        foreach (var line in lines)
        {
            // Track fenced code blocks
            if (line.TrimStart().StartsWith("```"))
            {
                inCodeBlock = !inCodeBlock;
                cleanedLines.Add(line.TrimStart());
                continue;
            }

            if (inCodeBlock)
                // Preserve code block content as-is
                cleanedLines.Add(line);
            else
                // Strip leading whitespace from non-code content
                cleanedLines.Add(line.TrimStart());
        }

        markdown = string.Join('\n', cleanedLines);

        // Remove excessive blank lines
        markdown = Regex.Replace(markdown, @"\n{3,}", "\n\n");

        // Remove trailing whitespace from lines
        markdown = Regex.Replace(markdown, @"[ \t]+\n", "\n");

        // Ensure proper spacing around headers
        markdown = Regex.Replace(markdown, @"(\n#{1,6}\s)", "\n$1");

        return markdown.Trim();
    }

    [GeneratedRegex(@"Original URL:\s*(.+?)[\r\n]")]
    private static partial Regex OriginalUrlRegex();

    [GeneratedRegex(@"Archive Date:\s*(.+?)[\r\n]")]
    private static partial Regex ArchiveDateRegex();
}