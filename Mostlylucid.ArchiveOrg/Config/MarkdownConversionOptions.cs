namespace Mostlylucid.ArchiveOrg.Config;

public class MarkdownConversionOptions
{
    public const string SectionName = "MarkdownConversion";

    /// <summary>
    ///     Input directory containing HTML files to convert
    /// </summary>
    public string InputDirectory { get; set; } = "./archive-output";

    /// <summary>
    ///     Output directory for converted markdown files
    /// </summary>
    public string OutputDirectory { get; set; } = "./markdown-output";

    /// <summary>
    ///     CSS selector for the main content area to extract
    ///     e.g., "article", ".post-content", "#main-content"
    ///     If empty, attempts to auto-detect main content
    /// </summary>
    public string ContentSelector { get; set; } = string.Empty;

    /// <summary>
    ///     CSS selectors to remove from the content before conversion
    ///     e.g., navigation, ads, footers, sidebars
    /// </summary>
    public List<string> RemoveSelectors { get; set; } =
    [
        "nav",
        "header",
        "footer",
        ".sidebar",
        ".advertisement",
        ".ads",
        ".comments",
        ".social-share",
        "script",
        "style",
        "noscript",
        "iframe"
    ];

    /// <summary>
    ///     Whether to generate tags using LLM
    /// </summary>
    public bool GenerateTags { get; set; } = true;

    /// <summary>
    ///     Whether to extract/infer publish date from content
    /// </summary>
    public bool ExtractDates { get; set; } = true;

    /// <summary>
    ///     File extension pattern to process
    /// </summary>
    public string FilePattern { get; set; } = "*.html";

    /// <summary>
    ///     Whether to preserve images (download and update paths)
    /// </summary>
    public bool PreserveImages { get; set; } = true;

    /// <summary>
    ///     Directory for downloaded images (relative to markdown output)
    /// </summary>
    public string ImagesDirectory { get; set; } = "images";

    /// <summary>
    ///     CSS selector for extracting the publish date
    ///     e.g., ".postfoot", ".post-date", "time"
    /// </summary>
    public string DateSelector { get; set; } = string.Empty;

    /// <summary>
    ///     CSS selector for individual posts on multi-post pages (e.g., archive/index pages)
    ///     e.g., ".post", "article", ".blog-entry"
    ///     If set, pages with multiple matching elements will be split into separate markdown files
    /// </summary>
    public string PostSelector { get; set; } = string.Empty;

    /// <summary>
    ///     CSS selector for extracting the title within each post (when using PostSelector)
    ///     e.g., "h2", ".post-title", "h1"
    /// </summary>
    public string PostTitleSelector { get; set; } = "h2";

    /// <summary>
    ///     CSS selector for extracting the permalink/link within each post
    ///     e.g., "a.permalink", "h2 a", ".read-more"
    ///     Used to generate unique slugs for split posts
    /// </summary>
    public string PostLinkSelector { get; set; } = "h2 a";

    /// <summary>
    ///     URL patterns that indicate index/archive pages to skip (regex)
    ///     These pages aggregate existing content and shouldn't be converted
    ///     e.g., "/index", "/archive", "/page/", "/category/"
    /// </summary>
    public List<string> SkipUrlPatterns { get; set; } =
    [
        @"^https?://[^/]+/?$", // Root/homepage
        @"/index\.aspx?", // index pages
        @"/index\.html?",
        @"/default\.aspx?",
        @"/archive/?$", // archive listing
        @"/archives/?$",
        @"/page/\d+", // pagination
        @"/category/", // category listings
        @"/tag/", // tag listings
        @"/author/", // author listings
        @"\?page=", // query string pagination
        @"/feed/?$", // RSS feeds
        @"/rss/?$"
    ];

    /// <summary>
    ///     Minimum content length (characters) for a post to be considered valid
    ///     Helps filter out stubs and navigation-only pages
    /// </summary>
    public int MinContentLength { get; set; } = 200;

    /// <summary>
    ///     Optional: Only convert archives from this date (inclusive)
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    ///     Optional: Only convert archives up to this date (inclusive)
    /// </summary>
    public DateTime? EndDate { get; set; }
}