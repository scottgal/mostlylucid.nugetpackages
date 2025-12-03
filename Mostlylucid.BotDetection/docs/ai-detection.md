# AI Detection

Two AI providers are available for advanced bot classification.

## ONNX (Compact, Fast, Offline)

Lightweight ML-based detection that runs entirely locally without external dependencies. Good for common bot patterns, runs in 1-10ms per request.

### Configuration

```json
{
  "BotDetection": {
    "EnableLlmDetection": true,
    "AiDetection": {
      "Provider": "Onnx",
      "TimeoutMs": 1000,
      "Onnx": {
        "ModelPath": "",
        "AutoDownloadModel": true,
        "ModelDownloadUrl": "https://example.com/bot_classifier.onnx",
        "UseGpu": false,
        "EnableHeuristicFallback": true
      }
    }
  }
}
```

### Features

- **No external server** - Runs in-process with no network calls
- **Minimal resources** - ~10-50MB RAM depending on model
- **CPU-optimized** - GPU optional via CUDA (set `UseGpu: true`)
- **Heuristic fallback** - When no model is available, uses built-in feature weights

### How It Works

Extracts 12 features from each request (user-agent length, headers, cookies, bot keywords) and runs binary classification. Falls back to heuristic weights (sigmoid over learned feature weights) when no ONNX model file is present.

### Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `ModelPath` | `models/bot_classifier.onnx` | Path to ONNX model file |
| `AutoDownloadModel` | `true` | Download model if not present |
| `ModelDownloadUrl` | `""` | URL to download model from |
| `UseGpu` | `false` | Enable CUDA GPU acceleration |
| `EnableHeuristicFallback` | `true` | Use heuristic weights when no model |

## Ollama (Full LLM, Best Quality)

Uses a local LLM (via Ollama) to analyze request patterns with full reasoning capabilities. More accurate for sophisticated bots but adds 50-500ms latency.

### Setup

1. Install [Ollama](https://ollama.ai/) and pull a model:
   ```bash
   ollama pull gemma3:1b
   ```

2. Configure in appsettings.json:
   ```json
   {
     "BotDetection": {
       "EnableLlmDetection": true,
       "AiDetection": {
         "Provider": "Ollama",
         "TimeoutMs": 2000,
         "Ollama": {
           "Endpoint": "http://localhost:11434",
           "Model": "gemma3:1b"
         }
       }
     }
   }
   ```

### Custom Prompt

Customize the LLM prompt (use `{REQUEST_INFO}` placeholder):

```json
{
  "BotDetection": {
    "AiDetection": {
      "Ollama": {
        "CustomPrompt": "Analyze this HTTP request and classify as bot or human.\n\n{REQUEST_INFO}\n\nReturn JSON: {\"isBot\":bool,\"confidence\":0.0-1.0,\"reasoning\":\"...\",\"botType\":\"...\",\"pattern\":\"...\"}"
      }
    }
  }
}
```

### Recommended Models

| Model | Size | Context | Speed | Notes |
|-------|------|---------|-------|-------|
| `gemma3:1b` | 1B | 8K | Fast | Default, good balance |
| `tinyllama` | 1.1B | 2K | Very fast | Basic classification |
| `qwen2.5:1.5b` | 1.5B | 32K | Fast | Better reasoning |
| `phi3:mini` | 3.8B | 4K | Moderate | Microsoft's small model |

## Comparison: ONNX vs Ollama

| Feature | ONNX | Ollama |
|---------|------|--------|
| **Latency** | 1-10ms | 50-500ms |
| **Quality** | Good (pattern-based) | Best (full reasoning) |
| **Resources** | ~10-50MB RAM | ~1-4GB RAM |
| **Dependencies** | None (in-process) | Ollama server |
| **Offline** | Yes | Yes |
| **GPU** | Optional CUDA | Optional |

**Recommendation:** Start with ONNX for most use cases. Use Ollama when you need to catch sophisticated bots that evade pattern matching.

## Fail-Safe Behavior

If AI detection is unavailable or times out, detection continues with heuristics only. No errors are thrown - failures are logged and gracefully handled.
