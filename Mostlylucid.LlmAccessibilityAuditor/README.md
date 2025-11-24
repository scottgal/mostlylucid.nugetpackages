# Mostlylucid.LlmAccessibilityAuditor

LLM-powered HTML accessibility auditor that analyzes rendered HTML and identifies accessibility issues including missing
ARIA labels, bad heading hierarchy, suspicious contrast issues, and click targets without visible text.

## Features

- **Rule-based Analysis**: Fast HTML parsing to detect common accessibility issues
- **LLM-powered Analysis**: Uses Ollama to find subtle issues and provide intelligent suggestions
- **ASP.NET Middleware**: Automatically audit HTML responses in development
- **Diagnostic Dashboard**: Web UI to view audit history and statistics
- **Inline Widget**: Floating widget showing issues directly on the page
- **TagHelper**: Razor TagHelper for custom accessibility warning display
- **Machine-readable Reports**: JSON API for integration with CI/CD pipelines

## Detected Issues

| Category       | Issues Detected                                             |
|----------------|-------------------------------------------------------------|
| **ARIA**       | Missing/empty aria-label, broken aria-labelledby references |
| **Images**     | Missing alt text, empty alt on informative images           |
| **Headings**   | Skipped levels, multiple h1, bad hierarchy                  |
| **Forms**      | Inputs without labels, buttons without accessible names     |
| **Links**      | Links without accessible text content                       |
| **Landmarks**  | Missing main, nav landmarks                                 |
| **Tables**     | Data tables without headers                                 |
| **Contrast**   | Suspicious low-contrast CSS classes                         |
| **Navigation** | Missing skip links                                          |

## Installation

```bash
dotnet add package Mostlylucid.LlmAccessibilityAuditor
```

## Quick Start

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add accessibility auditor
builder.Services.AddAccessibilityAuditor(options =>
{
    options.Enabled = true;
    options.OnlyInDevelopment = true;  // Only run in development
    options.EnableLlmAnalysis = true;   // Use Ollama for AI analysis
    options.EnableInlineReport = true;  // Show floating widget on pages

    // Ollama configuration
    options.Ollama.Endpoint = "http://localhost:11434";
    options.Ollama.Model = "llama3.2:3b";
});

var app = builder.Build();

// Use the middleware
app.UseAccessibilityAudit();

// Map diagnostic endpoints
app.MapAccessibilityDiagnostics();

app.Run();
```

## Configuration Options

```csharp
builder.Services.AddAccessibilityAuditor(options =>
{
    // General
    options.Enabled = true;
    options.OnlyInDevelopment = true;
    options.EnableHeader = "X-Accessibility-Audit";  // Header to enable in prod

    // LLM
    options.EnableLlmAnalysis = true;
    options.Ollama.Endpoint = "http://localhost:11434";
    options.Ollama.Model = "llama3.2:3b";
    options.Ollama.TimeoutSeconds = 60;

    // Filtering
    options.ExcludePaths = new() { "/api/*", "/_*", "/health" };
    options.ContentTypesToAudit = new() { "text/html" };
    options.MaxHtmlSizeBytes = 1024 * 1024;  // 1MB

    // Reporting
    options.EnableInlineReport = true;
    options.EnableDiagnosticEndpoint = true;
    options.DiagnosticEndpointPath = "/_accessibility";
    options.StoreAuditHistory = true;
    options.MaxHistoryCount = 50;

    // Severity filtering
    options.MinimumSeverity = IssueSeverity.Info;
});
```

## API Usage

### Audit HTML Programmatically

```csharp
public class MyController : Controller
{
    private readonly IAccessibilityAuditor _auditor;

    public MyController(IAccessibilityAuditor auditor)
    {
        _auditor = auditor;
    }

    public async Task<IActionResult> AuditPage(string html)
    {
        // Full audit with LLM
        var report = await _auditor.AuditAsync(html, "https://example.com/page");

        // Quick audit (rule-based only)
        var quickResult = await _auditor.QuickAuditAsync(html);

        return Ok(report);
    }
}
```

### Diagnostic Endpoints

| Endpoint                              | Description           |
|---------------------------------------|-----------------------|
| `GET /_accessibility`                 | Dashboard UI          |
| `GET /_accessibility/report/{id}`     | Single report UI      |
| `GET /_accessibility/api/reports`     | Recent reports (JSON) |
| `GET /_accessibility/api/report/{id}` | Single report (JSON)  |
| `GET /_accessibility/api/stats`       | Aggregate statistics  |
| `GET /_accessibility/health`          | Service health check  |
| `POST /_accessibility/clear`          | Clear audit history   |

## TagHelper Usage

```html
@addTagHelper *, Mostlylucid.LlmAccessibilityAuditor

<!-- Inline display -->
<accessibility-warnings inline="true" min-severity="Serious" max-issues="10" />

<!-- Floating widget -->
<accessibility-warnings />
```

## Enabling in Production

By default, the auditor only runs in development. To enable in production:

1. Set `OnlyInDevelopment = false`, or
2. Send the enable header with requests:

```http
GET /your-page HTTP/1.1
X-Accessibility-Audit: true
```

## Report Structure

```json
{
  "reportId": "abc123",
  "pageUrl": "https://example.com/page",
  "pageTitle": "Example Page",
  "auditedAt": "2024-01-15T10:30:00Z",
  "auditDurationMs": 1234,
  "overallScore": 75,
  "llmAnalysisPerformed": true,
  "llmModel": "llama3.2:3b",
  "humanSummary": "Found 5 accessibility issues...",
  "summary": {
    "totalIssues": 5,
    "criticalCount": 1,
    "seriousCount": 2,
    "moderateCount": 1,
    "minorCount": 1
  },
  "issues": [
    {
      "type": "MissingAltText",
      "severity": "Critical",
      "description": "Image is missing alt attribute",
      "element": "<img src=\"photo.jpg\">",
      "selector": "body > main > img",
      "suggestedFix": "Add alt attribute describing the image",
      "wcagReference": "1.1.1",
      "wcagLevel": "A",
      "source": "HtmlParser"
    }
  ]
}
```

## WCAG References

Issues include WCAG guideline references where applicable:

- **1.1.1** - Non-text Content (images)
- **1.3.1** - Info and Relationships (headings, forms, tables)
- **1.4.3** - Contrast (Minimum)
- **2.4.1** - Bypass Blocks (skip links)
- **2.4.2** - Page Titled
- **2.4.4** - Link Purpose
- **2.4.6** - Headings and Labels
- **3.1.1** - Language of Page
- **4.1.2** - Name, Role, Value (buttons, forms)

## Requirements

- .NET 8.0 or .NET 9.0
- Ollama running locally (for LLM analysis)
    - Install: https://ollama.ai
    - Pull model: `ollama pull llama3.2:3b`

## License

Unlicense - See LICENSE file
