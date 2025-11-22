using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.ArchiveOrg.Config;
using Mostlylucid.ArchiveOrg.Models;
using ReverseMarkdown;

namespace Mostlylucid.ArchiveOrg.Services;

public partial class HtmlToMarkdownConverter : IHtmlToMarkdownConverter
{
    private readonly MarkdownConversionOptions _options;
    private readonly ArchiveOrgOptions _archiveOptions;
    private readonly IOllamaTagGenerator _tagGenerator;
    private readonly ILogger<HtmlToMarkdownConverter> _logger;
    private readonly Converter _markdownConverter;
    private readonly HttpClient _httpClient;

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
                    {
                        _logger.LogInformation("Split multi-post page into {Count} articles: {File}",
                            fileArticles.Count, htmlFile);
                    }
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

        if (skippedCount > 0)
        {
            _logger.LogInformation("Skipped {Count} already converted files", skippedCount);
        }

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

            // Check if this is an index/archive page that should be skipped
            if (ShouldSkipUrl(metadata.originalUrl))
            {
                _logger.LogDebug("Skipping index/archive page: {Url}", metadata.originalUrl);
                return articles;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Remove unwanted elements
            RemoveUnwantedElements(doc);

            // Check if this is a multi-post page
            if (!string.IsNullOrEmpty(_options.PostSelector))
            {
                var postNodes = SelectNodes(doc.DocumentNode, _options.PostSelector);
                if (postNodes != null && postNodes.Count > 1)
                {
                    // This looks like an index page with multiple posts - skip it
                    // The individual posts should have their own archived pages
                    _logger.LogInformation("Skipping multi-post page ({Count} posts) - likely an index: {Url}",
                        postNodes.Count, metadata.originalUrl);
                    return articles;
                }
            }

            // Single post page - use original logic
            var singleArticle = await ConvertSinglePageAsync(doc, html, metadata, htmlFilePath, cancellationToken);
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
    /// Check if URL matches any skip patterns (index/archive pages)
    /// </summary>
    private bool ShouldSkipUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return false;

        foreach (var pattern in _options.SkipUrlPatterns)
        {
            try
            {
                if (Regex.IsMatch(url, pattern, RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
                // Invalid regex pattern - skip it
            }
        }

        return false;
    }

    /// <summary>
    /// Convert a single post node from a multi-post page
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
            {
                title = $"Post {postIndex} from {Path.GetFileNameWithoutExtension(htmlFilePath)}";
            }

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
            {
                publishDate = ExtractPublishDateFromNode(postNode, _options.DateSelector) ?? metadata.archiveDate;
            }

            // Generate tags if enabled
            List<string> categories = [];
            if (_options.GenerateTags)
            {
                categories = await _tagGenerator.GenerateTagsAsync(title, markdown, cancellationToken);
            }

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
    /// Convert a single-post page (original logic)
    /// </summary>
    private async Task<MarkdownArticle?> ConvertSinglePageAsync(
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
            if (mdLastWrite >= htmlLastWrite)
            {
                _logger.LogDebug("Skipping already converted: {File}", htmlFilePath);
                return null;
            }
        }

        // Extract the main content
        var contentNode = ExtractMainContent(doc);
        if (contentNode == null)
        {
            _logger.LogWarning("Could not find main content in {File}", htmlFilePath);
            return null;
        }

        // Extract title
        var title = ExtractTitle(doc, contentNode);
        if (string.IsNullOrEmpty(title))
        {
            title = Path.GetFileNameWithoutExtension(htmlFilePath);
        }

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
        {
            publishDate = ExtractPublishDate(doc, html, _options.DateSelector) ?? metadata.archiveDate;
        }

        // Generate slug
        var slug = GenerateSlug(title, metadata.originalUrl);

        // Generate tags using LLM if enabled
        List<string> categories = [];
        if (_options.GenerateTags)
        {
            categories = await _tagGenerator.GenerateTagsAsync(title, markdown, cancellationToken);
        }

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
    /// Extract the title from a post node using the configured PostTitleSelector
    /// </summary>
    private string ExtractPostTitle(HtmlNode postNode)
    {
        if (!string.IsNullOrEmpty(_options.PostTitleSelector))
        {
            var titleNode = SelectSingleNode(postNode, _options.PostTitleSelector);
            if (titleNode != null)
            {
                return HttpUtility.HtmlDecode(titleNode.InnerText.Trim());
            }
        }

        // Fallback to h1, h2, h3
        foreach (var tag in new[] { "h1", "h2", "h3" })
        {
            var heading = postNode.SelectSingleNode($".//{tag}");
            if (heading != null)
            {
                return HttpUtility.HtmlDecode(heading.InnerText.Trim());
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Extract the permalink from a post node using the configured PostLinkSelector
    /// </summary>
    private string ExtractPostPermalink(HtmlNode postNode)
    {
        if (!string.IsNullOrEmpty(_options.PostLinkSelector))
        {
            var linkNode = SelectSingleNode(postNode, _options.PostLinkSelector);
            if (linkNode != null)
            {
                var href = linkNode.GetAttributeValue("href", null);
                if (!string.IsNullOrEmpty(href))
                {
                    return href;
                }
            }
        }

        // Fallback - look for any link in the title
        var titleLink = postNode.SelectSingleNode(".//h1/a | .//h2/a | .//h3/a");
        return titleLink?.GetAttributeValue("href", string.Empty) ?? string.Empty;
    }

    /// <summary>
    /// Extract publish date from a specific node
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
    /// Helper to select nodes using CSS-like selectors
    /// </summary>
    private static HtmlNodeCollection? SelectNodes(HtmlNode node, string selector)
    {
        // Handle class selector (e.g., .post)
        if (selector.StartsWith('.'))
        {
            var className = selector.TrimStart('.');
            return node.SelectNodes($".//*[contains(@class, '{className}')]");
        }
        // Handle ID selector (e.g., #content)
        if (selector.StartsWith('#'))
        {
            var id = selector.TrimStart('#');
            return node.SelectNodes($".//*[@id='{id}']");
        }
        // Handle element selector (e.g., article)
        return node.SelectNodes($".//{selector}");
    }

    /// <summary>
    /// Helper to select a single node using CSS-like selectors
    /// </summary>
    private static HtmlNode? SelectSingleNode(HtmlNode node, string selector)
    {
        // Handle class selector (e.g., .post-title)
        if (selector.StartsWith('.'))
        {
            var className = selector.TrimStart('.');
            return node.SelectSingleNode($".//*[contains(@class, '{className}')]");
        }
        // Handle ID selector
        if (selector.StartsWith('#'))
        {
            var id = selector.TrimStart('#');
            return node.SelectSingleNode($".//*[@id='{id}']");
        }
        // Handle element selector
        return node.SelectSingleNode($".//{selector}");
    }

    private static (string originalUrl, DateTime archiveDate) ExtractArchiveMetadata(string html)
    {
        var originalUrl = string.Empty;
        var archiveDate = DateTime.MinValue;

        var urlMatch = OriginalUrlRegex().Match(html);
        if (urlMatch.Success)
        {
            originalUrl = urlMatch.Groups[1].Value.Trim();
        }

        var dateMatch = ArchiveDateRegex().Match(html);
        if (dateMatch.Success && DateTime.TryParse(dateMatch.Groups[1].Value.Trim(), out var date))
        {
            archiveDate = date;
        }

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
            catch (System.Xml.XPath.XPathException)
            {
                // Skip invalid selectors
                _logger.LogWarning("Invalid selector skipped: {Selector}", selector);
            }

            if (nodes != null)
            {
                foreach (var node in nodes.ToList())
                {
                    node.Remove();
                }
            }
        }
    }

    private HtmlNode? ExtractMainContent(HtmlDocument doc)
    {
        // Try configured selector first
        if (!string.IsNullOrEmpty(_options.ContentSelector))
        {
            HtmlNode? node = null;

            // Handle ID selector (e.g., #PostBody)
            if (_options.ContentSelector.StartsWith('#'))
            {
                var id = _options.ContentSelector.TrimStart('#');
                node = doc.DocumentNode.SelectSingleNode($"//*[@id='{id}']");
            }
            // Handle class selector (e.g., .post-content)
            else if (_options.ContentSelector.StartsWith('.'))
            {
                var className = _options.ContentSelector.TrimStart('.');
                node = doc.DocumentNode.SelectSingleNode($"//*[contains(@class, '{className}')]");
            }
            // Handle element selector (e.g., article)
            else
            {
                node = doc.DocumentNode.SelectSingleNode($"//{_options.ContentSelector}");
            }

            if (node != null)
                return node;
        }

        // Try common content selectors
        var commonSelectors = new[]
        {
            "//article",
            "//main",
            "//*[@id='content']",
            "//*[@id='main-content']",
            "//*[@id='PostBody']",
            "//*[contains(@class, 'post-content')]",
            "//*[contains(@class, 'entry-content')]",
            "//*[contains(@class, 'article-content')]",
            "//*[contains(@class, 'blog-post')]",
            "//div[@role='main']"
        };

        foreach (var selector in commonSelectors)
        {
            var node = doc.DocumentNode.SelectSingleNode(selector);
            if (node != null)
                return node;
        }

        // Fall back to body
        return doc.DocumentNode.SelectSingleNode("//body");
    }

    private static string ExtractTitle(HtmlDocument doc, HtmlNode contentNode)
    {
        // Try h1 in content
        var h1 = contentNode.SelectSingleNode(".//h1");
        if (h1 != null)
            return HttpUtility.HtmlDecode(h1.InnerText.Trim());

        // Try title tag
        var title = doc.DocumentNode.SelectSingleNode("//title");
        if (title != null)
        {
            var titleText = HttpUtility.HtmlDecode(title.InnerText.Trim());
            // Remove common suffixes
            var separators = new[] { " | ", " - ", " :: ", " » " };
            foreach (var sep in separators)
            {
                var idx = titleText.IndexOf(sep, StringComparison.Ordinal);
                if (idx > 0)
                {
                    titleText = titleText[..idx].Trim();
                    break;
                }
            }
            return titleText;
        }

        // Try og:title
        var ogTitle = doc.DocumentNode.SelectSingleNode("//meta[@property='og:title']");
        if (ogTitle != null)
            return HttpUtility.HtmlDecode(ogTitle.GetAttributeValue("content", string.Empty));

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
    /// Get a set of URLs that have been downloaded (from archive metadata in HTML files)
    /// </summary>
    private HashSet<string> GetDownloadedUrlSet()
    {
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Use the input directory where HTML files are stored (same as markdown conversion input)
        if (!Directory.Exists(_options.InputDirectory))
            return urls;

        foreach (var htmlFile in Directory.GetFiles(_options.InputDirectory, "*.html"))
        {
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
                    if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    {
                        urls.Add(uri.AbsolutePath);
                    }
                }
            }
            catch
            {
                // Skip files we can't read
            }
        }

        return urls;
    }

    /// <summary>
    /// Rewrite URL with fallback to Archive.org for broken local links
    /// </summary>
    private string RewriteUrlWithArchiveFallback(string url, Uri baseUri, string targetHost, HashSet<string> downloadedUrls)
    {
        // Skip empty, mailto, tel, javascript, and anchor links
        if (string.IsNullOrEmpty(url) ||
            url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith('#'))
        {
            return url;
        }

        // Remove Wayback Machine URL prefix if present
        var waybackMatch = Regex.Match(url, @"https?://web\.archive\.org/web/\d+[a-z_]*/(.+)");
        if (waybackMatch.Success)
        {
            url = waybackMatch.Groups[1].Value;
        }

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
    /// Convert archive-style paths to blog format
    /// e.g., /articles/nestedrepeaters.htm -> /blog/nestedrepeaters
    ///       /archive/2004/05/27/1054.aspx -> /blog/1054
    /// </summary>
    private static string ConvertToBlogUrl(string absolutePath)
    {
        // Extract the filename without extension
        var fileName = Path.GetFileNameWithoutExtension(absolutePath);

        if (string.IsNullOrEmpty(fileName) || fileName.Equals("index", StringComparison.OrdinalIgnoreCase))
        {
            // For index pages, use the parent directory name
            var segments = absolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 1)
            {
                fileName = segments[^2]; // Second to last segment
            }
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
        {
            return url;
        }

        // Remove Wayback Machine URL prefix if present
        var waybackMatch = Regex.Match(url, @"https?://web\.archive\.org/web/\d+[a-z_]*/(.+)");
        if (waybackMatch.Success)
        {
            url = waybackMatch.Groups[1].Value;
        }

        // Try to parse as absolute URL
        if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
        {
            // If it's from the same domain, convert to relative
            if (absoluteUri.Host.Equals(targetHost, StringComparison.OrdinalIgnoreCase) ||
                absoluteUri.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase))
            {
                return absoluteUri.PathAndQuery;
            }

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
        if (!string.IsNullOrEmpty(originalUrl))
        {
            Uri.TryCreate(originalUrl, UriKind.Absolute, out baseUri);
        }

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
                    {
                        absoluteUrl = resolvedUri.ToString();
                    }
                    else
                    {
                        continue;
                    }
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
                    try
                    {
                        var imageData = await _httpClient.GetByteArrayAsync(absoluteUrl, cancellationToken);
                        await File.WriteAllBytesAsync(localPath, imageData, cancellationToken);
                        imageInfo.Downloaded = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to download image: {Url}", absoluteUrl);
                    }
                }
                else
                {
                    imageInfo.Downloaded = true;
                }

                // Update the img src to the local path
                if (imageInfo.Downloaded)
                {
                    img.SetAttributeValue("src", markdownPath);
                }

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

        if (string.IsNullOrEmpty(fileName))
        {
            fileName = $"image_{Guid.NewGuid():N}.jpg";
        }

        // Sanitize filename
        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var c in invalidChars)
        {
            fileName = fileName.Replace(c, '_');
        }

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

        // Try common date meta tags
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
            "//*[@class='postfoot']"
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
                if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var date))
                {
                    return date;
                }
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
            if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var date))
            {
                return date;
            }
        }

        return null;
    }

    /// <summary>
    /// Parse date from "posted on Thursday, May 27, 2004 11:21 PM" format
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
            {
                if (DateTime.TryParseExact(dateStr, format,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var parsedDate))
                {
                    return parsedDate;
                }
            }

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
        {
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
                    if (!string.IsNullOrEmpty(lastSegment) && lastSegment != "index")
                    {
                        return SanitizeSlug(lastSegment);
                    }
                }
            }
            catch
            {
                // Fall through to title-based slug
            }
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
