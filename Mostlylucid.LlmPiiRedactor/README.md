# Mostlylucid.LlmPiiRedactor

A comprehensive PII (Personally Identifiable Information) detection and redaction library for ASP.NET Core applications.
Automatically detects and redacts sensitive data from logs, traces, error reports, and API responses.

## Features

- **Multi-Type PII Detection**: Emails, phone numbers, credit cards, SSNs, IP addresses, names, addresses, postcodes,
  bank accounts, API keys, and more
- **Configurable Redaction Styles**:
    - Full mask: `****************`
    - Partial mask: `jo****@gmail.com`, `****-****-****-1234`
    - Tokenized: `[EMAIL_001]` (consistent tokens for debugging)
    - Type labels: `[EMAIL]`, `[PHONE]`
    - Hashed: `[EMAIL:a3f2b1c4]`
    - Complete removal
- **ASP.NET Core Integration**:
    - Request/response body redaction middleware
    - Header and query string redaction
    - Exception filter for error pages
- **Logging Integration**:
    - ILogger wrapper for automatic log redaction
    - Serilog enricher and destructuring policy
- **Compliance Ready**: Pre-configured options for GDPR and PCI-DSS

## Installation

```bash
dotnet add package mostlylucid.llmpiiredactor
```

## Quick Start

### Basic Setup

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add PII redaction with default settings
builder.Services.AddPiiRedaction();

var app = builder.Build();

// Enable middleware for request/response redaction
app.UsePiiRedaction();

app.Run();
```

### Custom Configuration

```csharp
builder.Services.AddPiiRedaction(
    configureRedaction: options =>
    {
        options.DefaultStyle = RedactionStyle.PartialMask;
        options.DetectionTypes = PiiType.Email | PiiType.CreditCard | PiiType.PhoneNumber;
        options.MinConfidenceThreshold = 0.8;

        // Per-type style overrides
        options.StyleOverrides[PiiType.CreditCard] = RedactionStyle.PartialMask;
        options.StyleOverrides[PiiType.Email] = RedactionStyle.Tokenized;

        // Whitelist certain values
        options.Whitelist.Add("support@example.com");
        options.WhitelistedEmailDomains.Add("internal.company.com");
    },
    configureMiddleware: options =>
    {
        options.RedactRequestBody = true;
        options.RedactResponseBody = true;
        options.ExcludedPaths.Add("/api/internal/*");
    },
    configureLogging: options =>
    {
        options.RedactExceptions = true;
        options.RedactStackTraces = true;
    }
);
```

### Compliance-Focused Configurations

```csharp
// GDPR-compliant configuration
builder.Services.AddGdprCompliantRedaction();

// PCI-DSS compliant configuration
builder.Services.AddPciCompliantRedaction();
```

## Using the Redaction Service Directly

```csharp
public class MyService
{
    private readonly IPiiRedactionService _redactionService;

    public MyService(IPiiRedactionService redactionService)
    {
        _redactionService = redactionService;
    }

    public void ProcessData(string userInput)
    {
        // Check if data contains PII
        if (_redactionService.ContainsPii(userInput))
        {
            // Redact and get detailed results
            var result = _redactionService.Redact(userInput);

            Console.WriteLine($"Original: {result.OriginalText}");
            Console.WriteLine($"Redacted: {result.RedactedText}");
            Console.WriteLine($"Found {result.Matches.Count} PII instances");

            foreach (var match in result.Matches)
            {
                Console.WriteLine($"  - {match.Type}: {match.OriginalValue} -> {match.RedactedValue}");
            }
        }
    }
}
```

## Serilog Integration

```csharp
// Configure Serilog with PII redaction
var serviceProvider = builder.Services.BuildServiceProvider();

Log.Logger = new LoggerConfiguration()
    .RedactPii(serviceProvider)
    .WriteTo.Console()
    .CreateLogger();
```

## Exception Filter

```csharp
// Apply to specific controllers
[RedactPiiExceptions]
public class UserController : ControllerBase
{
    // PII in exception messages will be redacted
}

// Or apply globally
builder.Services.AddControllers(options =>
{
    options.Filters.Add<PiiExceptionFilter>();
});
```

## Supported PII Types

| Type          | Examples                            | Detection Method          |
|---------------|-------------------------------------|---------------------------|
| Email         | `john@example.com`                  | RFC 5322 regex            |
| Phone         | `+1-234-567-8901`, `(123) 456-7890` | Multi-format regex        |
| Credit Card   | `4111-1111-1111-1111`               | Luhn algorithm validation |
| SSN           | `123-45-6789`                       | US format with validation |
| IP Address    | `192.168.1.1`, `::1`                | IPv4/IPv6 parsing         |
| Name          | `John Smith`, `Dr. Jane Doe`        | Pattern + name database   |
| Address       | `123 Main Street, Apt 4`            | Street suffix detection   |
| Postcode      | `12345`, `SW1A 1AA`, `A1A 1A1`      | Multi-country formats     |
| Bank Account  | `GB82WEST12345698765432`            | IBAN mod-97 validation    |
| API Key       | `sk_live_...`, `AKIA...`            | Known prefix patterns     |
| NI Number     | `AB123456C`                         | UK format validation      |
| Date of Birth | `01/15/1990`                        | Context-aware detection   |
| Account ID    | `USER-12345`, UUIDs                 | Pattern + context         |

## Redaction Styles Examples

```
Original: Contact john.doe@example.com or call +1-555-123-4567

Full Mask:     Contact ********************* or call **************
Partial Mask:  Contact jo****@example.com or call ****-****-****-4567
Tokenized:     Contact [EMAIL_001] or call [PHONE_001]
Type Label:    Contact [EMAIL] or call [PHONE]
Hashed:        Contact [EMAIL:a3f2b1c4] or call [PHONE:7d8e9f0a]
Remove:        Contact  or call
```

## Statistics

```csharp
var stats = redactionService.GetStatistics();

Console.WriteLine($"Total scans: {stats.TotalScans}");
Console.WriteLine($"Total redactions: {stats.TotalRedactions}");

foreach (var (type, count) in stats.RedactionsByType)
{
    Console.WriteLine($"  {type}: {count}");
}
```

## License

Unlicense - See LICENSE file for details.
