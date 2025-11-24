# Mostlylucid.LLMContentModeration

Local LLM-powered content moderation for ASP.NET Core applications. All processing happens on your server - no data ever
leaves your infrastructure.

## Features

- **Content Classification**: Toxicity, abuse, spam, self-harm, NSFW detection
- **PII Detection**: Email, phone, address, IBAN, credit card numbers
- **Three Modes**:
    - `DetectOnly` - Flag content, log findings, allow through
    - `Block` - Reject flagged content with error response
    - `MaskAndAllow` - Redact PII/flagged content and allow through
- **Per-Route Policies**: Configure different moderation levels per controller/action
- **Request/Response Interception**: Works on both incoming and outgoing content
- **Privacy-First**: All processing via local Ollama - no external API calls

## Installation

```bash
dotnet add package Mostlylucid.LLMContentModeration
```

## Prerequisites

- [Ollama](https://ollama.ai/) running locally with a capable model (e.g., `llama3.2:3b`)

## Quick Start

```csharp
// Program.cs
builder.Services.AddLLMContentModeration(options =>
{
    options.Ollama.Endpoint = "http://localhost:11434";
    options.Ollama.Model = "llama3.2:3b";
    options.DefaultMode = ModerationMode.Block;
    options.EnableForComments = true; // Default: true
});

var app = builder.Build();
app.UseContentModeration();
```

## Configuration

```json
{
  "LLMContentModeration": {
    "Enabled": true,
    "DefaultMode": "Block",
    "EnableForComments": true,
    "Ollama": {
      "Endpoint": "http://localhost:11434",
      "Model": "llama3.2:3b",
      "Temperature": 0.1,
      "TimeoutSeconds": 30
    },
    "ContentClassification": {
      "EnableToxicity": true,
      "EnableSpam": true,
      "EnableSelfHarm": true,
      "EnableNsfw": true,
      "ToxicityThreshold": 0.7,
      "SpamThreshold": 0.8
    },
    "PiiDetection": {
      "Enabled": true,
      "DetectEmail": true,
      "DetectPhone": true,
      "DetectAddress": true,
      "DetectIban": true,
      "DetectCreditCard": true,
      "MaskCharacter": "*"
    }
  }
}
```

## Per-Route Policies

```csharp
[ModerationPolicy(ModerationMode.Block, EnablePii = true, EnableToxicity = true)]
public class CommentsController : Controller
{
    [ModerationPolicy(ModerationMode.MaskAndAllow)] // Override at action level
    public IActionResult Create([FromBody] CommentDto comment) { }
}
```

## Direct Service Usage

```csharp
public class MyService
{
    private readonly IContentModerationService _moderation;

    public async Task<bool> ValidateComment(string content)
    {
        var result = await _moderation.ModerateAsync(content);

        if (result.IsFlagged)
        {
            // Handle flagged content
            foreach (var flag in result.Flags)
                Console.WriteLine($"{flag.Category}: {flag.Confidence:P}");
        }

        return !result.IsBlocked;
    }
}
```

## License

Public Domain (Unlicense)
