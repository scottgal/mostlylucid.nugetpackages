# Mostlylucid.LlmI18nAssistant.Cli

Command-line tool for LLM-assisted localization of .resx and JSON resource files.

## Installation

```bash
# Install globally
dotnet tool install -g Mostlylucid.LlmI18nAssistant.Cli

# Or install locally
dotnet tool install Mostlylucid.LlmI18nAssistant.Cli
```

## Usage

### Translate Resource Files

```bash
# Translate to a single language
llm-i18n translate Resources/Strings.resx --source en --target de

# Translate to multiple languages
llm-i18n translate Resources/Strings.resx --source en --target de,fr,es,it

# With output directory
llm-i18n translate Resources/Strings.resx --source en --target de --output ./localized

# With consistency mode (use glossary)
llm-i18n translate Resources/Strings.resx --source en --target de --glossary ./glossaries

# Translate JSON resources
llm-i18n translate resources/en.json --source en --target de --format json
```

### Options

```
-s, --source <lang>        Source language code (default: en)
-t, --target <langs>       Target language codes (comma-separated)
-o, --output <path>        Output directory (default: same as input)
-g, --glossary <path>      Path to glossary file or directory
-c, --consistency          Enable consistency mode (RAG over translations)
-f, --format <format>      Output format: resx, json, properties (default: same as input)
    --ollama <endpoint>    Ollama endpoint (default: http://localhost:11434)
    --model <name>         Ollama model name (default: llama3.2:3b)
    --nmt <endpoint>       NMT service endpoint
    --verbose              Verbose output
```

### Check Service Status

```bash
llm-i18n status
```

### Initialize Glossary

```bash
# Create empty glossary file
llm-i18n glossary init ./glossaries/tech-terms.json

# Import existing translations to glossary
llm-i18n glossary import ./glossaries/tech-terms.json --from Resources/Strings.resx --lang de
```

## Configuration

Create `llmi18n.json` in your project root:

```json
{
  "LlmI18nAssistant": {
    "Ollama": {
      "Endpoint": "http://localhost:11434",
      "Model": "mannix/llamax3-8b-alpaca",
      "Temperature": 0.3
    },
    "Nmt": {
      "Enabled": true,
      "ServiceEndpoints": ["http://localhost:24080"],
      "UseAsBaseline": true
    },
    "ConsistencyMode": {
      "Enabled": true,
      "GlossaryPath": "./glossaries"
    }
  }
}
```

## Examples

### Basic Translation

```bash
# Input: Resources/Strings.resx
llm-i18n translate Resources/Strings.resx -s en -t de

# Output: Resources/Strings.de.resx
```

### Multi-Language Translation

```bash
# Translate to German, French, Spanish
llm-i18n translate Resources/Strings.resx -s en -t de,fr,es -o ./localized

# Output:
# ./localized/Strings.de.resx
# ./localized/Strings.fr.resx
# ./localized/Strings.es.resx
```

### With Glossary

```bash
# First, create glossary with approved terms
echo '{
  "name": "Tech Terms",
  "sourceLanguage": "en",
  "entries": [
    {
      "sourceTerm": "Dashboard",
      "translations": { "de": "Übersicht", "fr": "Tableau de bord" }
    }
  ]
}' > glossary.json

# Then translate with glossary
llm-i18n translate app.resx -s en -t de -g glossary.json
```

## License

Unlicense - Public Domain
