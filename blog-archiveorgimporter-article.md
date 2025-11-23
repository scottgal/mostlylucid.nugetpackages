# Resurrecting My Old Blog with Archive.org and a Lot of C#

<!--category-- Imported, .NET, Archive.org -->
<datetime class="hidden">2025-11-24T12:00</datetime>

So you might have noticed hundreds of "new" blog posts appearing recently. Well, they're not new at all - they're OLD. Like, 2004 old. I finally built a tool to rescue my content from the digital graveyard that was my old blog at mostlylucid.co.uk.

## Why Archive.org is Absolutely Brilliant

Before I dive into the technical stuff, I have to give a massive shoutout to the [Internet Archive](https://archive.org/) and their Wayback Machine. This non-profit organization has been quietly archiving the web since 1996, preserving billions of web pages that would otherwise be lost forever.

Think about that for a second. Every blog post you wrote in 2005, every GeoCities page, every MySpace profile - there's a decent chance it's still accessible through Archive.org. They're essentially running a museum of the entire internet, funded by donations and grants.

When my old hosting provider disappeared (along with my backups because I was SMART like that), I thought all that content was gone forever. Turns out the Wayback Machine had been diligently snapshotting my site for years. Archive.org quite literally saved 6+ years of my blogging history.

If you've never donated to them, consider doing so. They're preserving our collective digital history.

## The Problem: My Blog Was a Mess

Here's the thing about running a blog from 2004 to 2010 - web technologies changed A LOT during that time. And apparently, I changed my blogging setup at least three times:

1. **Early Days (2004)**: Some homebrew ASP.NET thing with content wrapped in `<div class="post">` inside a `<form>` element (because everything was a form back then)
2. **Middle Period**: Different template structure, dates in different places
3. **Later Years**: Yet another restructure with slightly different selectors

This meant any extraction tool needed to be flexible enough to handle multiple HTML structures. A "one size fits all" scraper wasn't going to cut it.

## Enter the ArchiveOrgImporter

I built [ArchiveOrgImporter](https://github.com/scottgal/mostlylucid.nugetpackages/tree/main/Mostlylucid.ArchiveOrg) to solve this very specific problem. It's a .NET 9.0 console application that:

1. **Respects Archive.org's usage limits** - They're a non-profit running on donations, so hammering their servers would be a terrible thing to do
2. **Downloads archived pages between configurable dates**
3. **Extracts blog content from multiple HTML structures**
4. **Generates clean Markdown** in my current blog format
5. **Uses Ollama to generate useful tags** - because why not throw some LLM magic at it?

### How It Works

The tool follows a pipeline architecture with three main phases:

```
Archive.org CDX API → Download HTML → Convert to Markdown → Generate Tags → Output Files
```

#### Phase 1: Querying the Archive

The tool uses Archive.org's [CDX API](https://github.com/internetarchive/wayback/tree/master/wayback-cdx-server) to find all archived snapshots of my blog. This API returns a list of captured URLs with timestamps, MIME types, and HTTP status codes.

```csharp
// The CDX query builds a URL like this:
// https://web.archive.org/cdx/search/cdx?url=mostlylucid.co.uk/posts/&output=json&collapse=urlkey
```

The `collapse=urlkey` parameter is clever - it returns only the latest snapshot for each unique URL, which dramatically reduces the number of duplicates you need to deal with.

I also use regex patterns to filter URLs. My old posts followed the pattern `/posts/[number].aspx`, so:

```json
{
  "IncludePatterns": ["/posts/\\d+\\.aspx$"]
}
```

This way I only grab actual blog posts, not the archive pages, category pages, or RSS feeds.

#### Phase 2: Being a Good Citizen with Rate Limiting

Archive.org is a public service. They don't have the infrastructure budget of Google or Amazon. So the downloader is deliberately conservative:

```csharp
// Default: 5 seconds between requests, single-threaded downloads
"RateLimitMs": 5000,
"MaxConcurrentDownloads": 1
```

Yes, this means downloading hundreds of posts takes a while. But it's the right thing to do. The tool also handles 429 (rate limit) responses gracefully with exponential backoff.

There's also some cleanup needed for downloaded files. Archive.org adds toolbar scripts and rewrites URLs in the captured HTML. The downloader strips all that out:

```csharp
// Removes Wayback toolbar, playback scripts, and archive URL prefixes
```

#### Phase 3: The Content Extraction Nightmare

This is where things get interesting. Remember I mentioned my blog changed structure three times? Here's how I handled it.

The primary extraction uses a CSS selector:

```json
{
  "ContentSelector": "div.post"
}
```

But here's a fun quirk - in some versions of my old site, the `div.post` was INSIDE a `<form>` element. The code has to extract content BEFORE removing unwanted elements, otherwise removing `<form>` tags would nuke the actual blog content:

```csharp
// IMPORTANT: Extract content FIRST before removing elements
// (because form removal would destroy div.post which is inside the form)
```

The tool also has fallback selectors if the primary one fails:

1. `article`, `main`
2. `#content`, `#main-content`, `#PostBody`
3. `.post-content`, `.entry-content`, `.article-content`
4. Finally, just the `<body>` if all else fails

Then there's a long list of elements to strip out AFTER content extraction:

```json
{
  "RemoveSelectors": [
    "nav", "header", "footer", ".sidebar", ".advertisement",
    ".comments", ".social-share", ".related-posts", "script",
    "style", "noscript", "iframe", "#commentform", ".postNav"
  ]
}
```

#### Phase 4: Markdown Generation

Once we have clean HTML content, [ReverseMarkdown](https://github.com/mysticmind/reversemarkdown-net) handles the heavy lifting of converting it to GitHub-flavored Markdown.

But the output needed some post-processing. Old HTML often had weird indentation that would confuse Markdown parsers (indented lines become code blocks!). So there's cleanup:

```csharp
// Removes leading whitespace from non-code lines
// Preserves code block formatting (respects ``` fences)
// Removes excessive blank lines (max 2 consecutive)
```

The final output includes my blog's frontmatter format:

```markdown
# Article Title

<datetime class="hidden">2004-05-27T23:21</datetime>
<!--category-- mostlylucidcouk, Imported, SomeCategory -->

Article content here...
```

#### Phase 5: LLM-Powered Tag Generation

Now for the fun part. Old blog posts often had no tags at all, or tags that didn't make sense for my current site structure. So I integrated Ollama to analyze content and generate relevant tags.

The configuration is straightforward:

```json
{
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "gemma3:4b",
    "Temperature": 0.3,
    "MaxTags": 5,
    "Enabled": true
  }
}
```

I use `gemma3:4b` as it's fast and small enough to run locally without much fuss. The low temperature (0.3) keeps the output consistent - we don't want creative hallucinations in our tags.

##### Handling Longer Posts

Here's a practical limitation: LLMs have context windows, and pumping an entire blog post into them for tag generation is wasteful. The tool truncates content to 3000 characters:

```csharp
var truncatedContent = content.Length > 3000
    ? content[..3000] + "..."
    : content;
```

For tag generation, the first 3000 characters usually contain enough context to understand what the post is about. The prompt instructs the model to focus on tech-specific categories:

```
Generate up to 5 tags, short (1-3 words), focusing on:
.NET, C#, ASP.NET, JavaScript, Docker, Database, API, Security, DevOps, Cloud
```

The response parsing is defensive - if Ollama returns garbage or times out, we just get an empty tag list rather than crashing the entire pipeline.

### Date Extraction: A Multi-Strategy Approach

Finding the original publication date is surprisingly tricky. My old blog stored dates in different places over the years. The tool tries multiple strategies:

1. **Configured selector** (e.g., `.postfoot`)
2. **Meta tags**: `article:published_time`, `DC.date.issued`
3. **HTML5 `<time>` elements**
4. **Common date classes**: `.date`, `.post-date`, `.entry-date`
5. **Regex patterns** on raw HTML (ISO format, slash-separated, etc.)
6. **Fallback**: The archive snapshot date itself

One particularly annoying format was my old blog template's "posted on Thursday, May 27, 2004 11:21 PM" string. There's specific parsing for that.

### The Pipeline Orchestration

The coolest architectural bit is how download and conversion run in parallel using .NET Channels:

```csharp
var channel = Channel.CreateBounded<string>(10);

// Producer: downloads and writes file paths to channel
// Consumer: reads file paths and converts to markdown
```

This means conversion starts as soon as the first file downloads, rather than waiting for all downloads to complete. The bounded capacity (10 items) provides backpressure - if conversion falls behind, downloading slows down to match.

## Limitations and Gotchas

Let's be honest about what this tool can't do:

1. **It's very specific to MY blog** - The selectors, patterns, and date extraction are all tuned for mostlylucid.co.uk. You'd need to customize everything for a different site.

2. **Archive.org doesn't have everything** - Some pages weren't archived, or were archived with broken CSS/images.

3. **Images are best-effort** - The tool tries to download images from the Wayback Machine, but many are simply gone forever.

4. **Dead links everywhere** - External links from 2004 point to sites that no longer exist. I'm working on a separate solution for this (automatic Archive.org link substitution in a middleware - blog post coming soon!).

5. **LLM tags are imperfect** - The Ollama-generated tags are usually sensible, but occasionally miss the mark. Manual review is recommended.

6. **CDX API truncation** - For sites with thousands of pages, the CDX API may return truncated results. The code handles this gracefully but you might miss some pages.

## Running It

If you want to adapt this for your own site (it'll need customization), the commands are:

```bash
# Full pipeline (download + convert)
dotnet run -- full

# Just download HTML from Archive.org
dotnet run -- download

# Just convert existing HTML files to Markdown
dotnet run -- convert
```

The tool supports graceful shutdown (Ctrl+C) and can resume from where it left off since files are cached locally.

## The Results

After running this on my old blog, I recovered posts dating back to [January 1st, 2004](/blog/365). Reading through them is a fascinating trip through time. Web development discussions from before jQuery existed. Posts about technologies that are now completely obsolete. And some embarrassing opinions I held as a younger developer.

You can find all the imported posts using the [Imported](/blog/category/Imported) category tag.

## What's Next

The imported content has a LOT of dead links - that's just the nature of 20-year-old web content. I'm working on a middleware solution that will:

1. Detect 404s for old URLs
2. Automatically find the closest Archive.org snapshot
3. Redirect or display the archived version

That'll be another blog post once it's working.

## Wrapping Up

Building this tool was a weekend project that turned into something genuinely useful. If you've lost content from an old blog, there's a decent chance Archive.org has it. And if you're comfortable with C#, the techniques here (CDX API querying, HTML content extraction, Markdown generation, LLM tagging) could be adapted for your own recovery project.

The code is at [github.com/scottgal/mostlylucid.nugetpackages](https://github.com/scottgal/mostlylucid.nugetpackages/tree/main/Mostlylucid.ArchiveOrg). It's not a polished library - it's a purpose-built tool for my specific situation - but it might give you ideas for your own archival adventures.

And seriously, go donate to Archive.org. They're doing important work.
