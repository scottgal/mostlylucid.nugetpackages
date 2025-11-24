# Mostlylucid.LlmI18nAssistant

LLM-assisted localization helper for short-string app resources. Complements `LlmSlideTranslator` – while that handles
long documents, this is designed for UI copy and resource files.

## Features

- **Resource File Support**: Translate `.resx` (XML) and JSON resource files
- **Key Stability**: Only transforms values, keys remain unchanged
- **LLM + NMT Combo**: Uses local LLM with optional NMT baseline for quality translations
- **Consistency Mode**: RAG over existing translations and glossary to keep terminology aligned
- **Format Preservation**: Maintains placeholders (`{0}`, `{{name}}`), HTML tags, and special formatting
- **CLI Tool**: `dotnet tool` for offline resource generation
- **API Endpoint**: Minimal API for on-demand translation

## Installation

```bash
dotnet add package Mostlylucid.LlmI18nAssistant
```

## Quick Start

### 1. Register Services

```csharp
builder.Services.AddLlmI18nAssistant(builder.Configuration);
```

### 2. Configure in appsettings.json

```json
{
  "LlmI18nAssistant": {
    "Ollama": {
      "Endpoint": "http://localhost:11434",
      "Model": "mannix/llamax3-8b-alpaca",
      "Temperature": 0.3,
      "MaxTokens": 512,
      "TimeoutSeconds": 60
    },
    "Nmt": {
      "Enabled": true,
      "ServiceEndpoints": ["http://localhost:24080"],
      "UseAsBaseline": true
    },
    "Embedding": {
      "Provider": "Ollama",
      "OllamaModel": "nomic-embed-text:latest",
      "Dimension": 768
    },
    "ConsistencyMode": {
      "Enabled": true,
      "GlossaryPath": "./glossaries",
      "MinRelevance": 0.6,
      "MaxContextItems": 5
    },
    "ValueTransformation": {
      "PreserveFormatStrings": true,
      "PreserveHtmlTags": true,
      "SkipPatterns": []
    },
    "DataPath": "./data",
    "VectorStoreProvider": "File"
  }
}
```

### 3. Translate Resource Files

```csharp
var result = await i18nAssistant.TranslateResourceFileAsync(
    filePath: "Resources/Strings.resx",
    sourceLanguage: "en",
    targetLanguages: ["de", "fr", "es"],
    options: new TranslationOptions
    {
        UseConsistencyMode = true,
        PreserveFormatStrings = true
    });

// Save translated files
foreach (var translation in result.Translations)
{
    await translation.SaveAsync($"Resources/Strings.{translation.Language}.resx");
}
```

### 4. Translate Single Strings (API)

```csharp
var translation = await i18nAssistant.TranslateStringAsync(
    text: "Welcome to our application!",
    sourceLanguage: "en",
    targetLanguage: "de",
    context: "Button text on home page");
// Result: "Willkommen in unserer Anwendung!"
```

## Consistency Mode

When enabled, the assistant uses RAG (Retrieval-Augmented Generation) to maintain terminology consistency:

1. **Glossary Support**: Load existing glossaries with approved translations
2. **Translation Memory**: Previous translations are stored with embeddings
3. **Context Retrieval**: Similar terms from glossary/memory are provided to the LLM

```csharp
// Load glossary
await consistencyService.LoadGlossaryAsync("./glossaries/tech-terms.json");

// Translations will now use glossary for consistency
var result = await i18nAssistant.TranslateResourceFileAsync(...);
```

## CLI Tool

```bash
# Install globally
dotnet tool install -g Mostlylucid.LlmI18nAssistant.Cli

# Translate resource file
llm-i18n translate Resources/Strings.resx --source en --target de,fr,es

# With consistency mode
llm-i18n translate Resources/Strings.resx --source en --target de --glossary ./glossaries

# Output as JSON
llm-i18n translate Resources/Strings.json --source en --target de --format json
```

## API Endpoints

```csharp
// POST /i18n/translate
app.MapPost("/i18n/translate", async (TranslateRequest request, ILlmI18nAssistant assistant) =>
{
    var result = await assistant.TranslateResourceAsync(request);
    return Results.Ok(result);
});

// POST /i18n/translate/string
app.MapPost("/i18n/translate/string", async (TranslateStringRequest request, ILlmI18nAssistant assistant) =>
{
    var translation = await assistant.TranslateStringAsync(
        request.Text,
        request.SourceLanguage,
        request.TargetLanguage,
        request.Context);
    return Results.Ok(new { Translation = translation });
});
```

## License

Unlicense - Public Domain
