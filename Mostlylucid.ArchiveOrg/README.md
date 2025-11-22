# Mostlylucid.ArchiveOrg

A command-line tool for downloading archived web pages from Archive.org's Wayback Machine and converting them to clean Markdown files. Useful for recovering blog content, migrating websites, or archiving content from historical snapshots.

## Features

- **CDX API Integration**: Query Archive.org's CDX API to find all archived snapshots of a website
- **Intelligent Filtering**: Filter by date range, URL patterns, MIME types, and HTTP status codes
- **Rate Limiting**: Respectful rate limiting to comply with Archive.org's usage policies
- **HTML Cleanup**: Automatically removes Wayback Machine artifacts (toolbars, scripts, URL rewrites)
- **Markdown Conversion**: Converts downloaded HTML to clean GitHub-Flavored Markdown using ReverseMarkdown
- **Content Extraction**: Intelligently extracts main content from blog posts, removing navigation, sidebars, and ads
- **Image Preservation**: Optionally downloads and preserves images locally
- **AI-Powered Tagging**: Optional integration with Ollama for automatic tag/category generation
- **Metadata Extraction**: Extracts publish dates, titles, and generates SEO-friendly slugs

## Installation

```bash
# Clone and build
cd Mostlylucid.ArchiveOrg
dotnet build
```

## Usage

```bash
# Download archived pages from Archive.org
dotnet run -- download

# Convert downloaded HTML to Markdown
dotnet run -- convert

# Run full pipeline (download + convert)
dotnet run -- full

# Show help
dotnet run -- help
```

### Command-Line Overrides

```bash
# Override configuration via command line
dotnet run -- download --ArchiveOrg:TargetUrl=https://myblog.com --ArchiveOrg:EndDate=2023-12-31
```

## Configuration

Configure via `appsettings.json`:

```json
{
  "ArchiveOrg": {
    "TargetUrl": "https://example.com",
    "StartDate": null,
    "EndDate": "2024-01-01",
    "OutputDirectory": "./archive-output",
    "RateLimitMs": 5000,
    "MaxConcurrentDownloads": 1,
    "UniqueUrlsOnly": true,
    "IncludePatterns": [],
    "ExcludePatterns": [
      ".*\\.js$",
      ".*\\.css$",
      ".*\\.png$",
      ".*/wp-admin/.*"
    ],
    "MimeTypes": ["text/html"],
    "StatusCodes": [200]
  },
  "MarkdownConversion": {
    "InputDirectory": "./archive-output",
    "OutputDirectory": "./markdown-output",
    "ContentSelector": "article",
    "RemoveSelectors": [
      "nav", "header", "footer", ".sidebar",
      ".comments", ".social-share", "script", "style"
    ],
    "GenerateTags": true,
    "ExtractDates": true,
    "PreserveImages": true,
    "ImagesDirectory": "images"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "llama3.2",
    "Temperature": 0.3,
    "MaxTags": 5,
    "TimeoutSeconds": 60,
    "Enabled": true
  }
}
```

### Configuration Options

#### ArchiveOrg Section

| Option | Description | Default |
|--------|-------------|---------|
| `TargetUrl` | Base URL of the website to download | Required |
| `StartDate` | Only download snapshots from this date (null = all) | `null` |
| `EndDate` | Only download snapshots until this date | `null` |
| `OutputDirectory` | Directory for downloaded HTML files | `./archive-output` |
| `RateLimitMs` | Milliseconds between requests | `5000` |
| `MaxConcurrentDownloads` | Maximum parallel downloads | `1` |
| `UniqueUrlsOnly` | Only download latest snapshot per URL | `true` |
| `IncludePatterns` | Regex patterns for URLs to include | `[]` |
| `ExcludePatterns` | Regex patterns for URLs to exclude | `[]` |
| `MimeTypes` | MIME types to download | `["text/html"]` |
| `StatusCodes` | HTTP status codes to include | `[200]` |

#### MarkdownConversion Section

| Option | Description | Default |
|--------|-------------|---------|
| `InputDirectory` | Directory containing HTML files | `./archive-output` |
| `OutputDirectory` | Directory for Markdown output | `./markdown-output` |
| `ContentSelector` | CSS selector for main content | Auto-detect |
| `RemoveSelectors` | Elements to remove before conversion | Common selectors |
| `GenerateTags` | Use Ollama to generate tags | `true` |
| `ExtractDates` | Extract publish dates from HTML | `true` |
| `PreserveImages` | Download and preserve images | `true` |
| `ImagesDirectory` | Subdirectory for images | `images` |

#### Ollama Section (Optional)

| Option | Description | Default |
|--------|-------------|---------|
| `BaseUrl` | Ollama API endpoint | `http://localhost:11434` |
| `Model` | LLM model for tag generation | `llama3.2` |
| `Temperature` | Model temperature | `0.3` |
| `MaxTags` | Maximum tags to generate | `5` |
| `Enabled` | Enable AI tagging | `true` |

## How It Works

### 1. Download Phase

1. Queries Archive.org's CDX API to get all archived snapshots
2. Filters results by date range, URL patterns, and MIME types
3. Downloads each HTML file from the Wayback Machine's raw URL
4. Cleans Wayback Machine artifacts (toolbar, scripts, URL rewrites)
5. Saves HTML with metadata comments

### 2. Conversion Phase

1. Loads downloaded HTML files
2. Extracts main content using configurable selectors
3. Removes unwanted elements (nav, ads, scripts)
4. Converts to GitHub-Flavored Markdown
5. Extracts metadata (title, date, slug)
6. Optionally generates tags using Ollama
7. Downloads and preserves images locally
8. Outputs Markdown files with YAML frontmatter

## Output Format

Generated Markdown files include YAML frontmatter:

```markdown
---
title: "My Blog Post Title"
slug: "my-blog-post-title"
date: 2023-06-15
categories:
  - Technology
  - Programming
originalUrl: "https://example.com/my-blog-post"
archiveDate: 2023-06-20T14:30:00
---

# My Blog Post Title

Content here...
```

## Dependencies

- **HtmlAgilityPack**: HTML parsing and manipulation
- **ReverseMarkdown**: HTML to Markdown conversion
- **Polly**: HTTP retry policies for resilient downloads
- **Serilog**: Structured logging
- **Microsoft.Extensions.Hosting**: Dependency injection and configuration

## Service Integration

The services can be used programmatically in other applications:

```csharp
services.AddArchiveOrgServices(configuration);

// Then inject and use:
var downloader = serviceProvider.GetRequiredService<IArchiveDownloader>();
var results = await downloader.DownloadAllAsync(progress);

var converter = serviceProvider.GetRequiredService<IHtmlToMarkdownConverter>();
var articles = await converter.ConvertAllAsync(progress);
```

## License

MIT License - See LICENSE file for details.
