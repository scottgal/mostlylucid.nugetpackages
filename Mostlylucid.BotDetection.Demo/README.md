# Bot Detection Demo

This is a demo application showcasing the Mostlylucid.BotDetection library.

## Features

- **User-Agent Detection**: Identifies bots based on known patterns in User-Agent strings
- **Header Analysis**: Detects missing or suspicious HTTP headers
- **IP Detection**: Identifies traffic from datacenters and cloud providers
- **Behavioral Analysis**: Monitors request rates and patterns
- **LLM Detection** (Optional): Uses a small language model (Ollama) for advanced analysis with learning capabilities

## Running the Demo

1. **Basic Usage**:
   ```bash
   dotnet run
   ```

2. **With LLM Detection** (requires Ollama):
   ```bash
   # First, start Ollama with a small model
   ollama pull qwen2.5:1.5b
   ollama serve

   # Then run the demo with LLM enabled
   # Edit appsettings.json and set "EnableLlmDetection": true
   dotnet run
   ```

## Testing

### Test with Different User-Agents

```bash
# Test as a real browser
curl http://localhost:5000/ -H "User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36" -H "Accept-Language: en-US,en;q=0.9" -H "Accept: text/html,application/xhtml+xml"

# Test as curl (will be detected as bot)
curl http://localhost:5000/

# Test as Python (will be detected as bot)
curl http://localhost:5000/ -H "User-Agent: python-requests/2.28.0"

# Test as Googlebot (will be recognized as verified bot)
curl http://localhost:5000/ -H "User-Agent: Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)"

# Test with missing User-Agent (high bot score)
curl http://localhost:5000/ -H "User-Agent:"
```

### API Endpoints

- `GET /` - Main endpoint with bot detection info
- `GET /api/bot-check` - Detailed bot detection results
- `GET /api/stats` - Bot detection statistics
- `GET /api/test-bot` - Testing instructions

### View Statistics

```bash
curl http://localhost:5000/api/stats
```

## Configuration

Edit `appsettings.json` to customize bot detection:

```json
{
  "BotDetection": {
    "BotThreshold": 0.7,                    // Confidence threshold (0.0-1.0)
    "EnableUserAgentDetection": true,       // Enable User-Agent analysis
    "EnableHeaderAnalysis": true,           // Enable header analysis
    "EnableIpDetection": true,              // Enable IP-based detection
    "EnableBehavioralAnalysis": true,       // Enable behavioral patterns
    "EnableLlmDetection": false,            // Enable AI-powered detection
    "OllamaEndpoint": "http://localhost:11434",
    "OllamaModel": "qwen2.5:1.5b",         // Small 1.5B parameter model
    "LlmTimeoutMs": 2000,
    "MaxRequestsPerMinute": 60,
    "CacheDurationSeconds": 300
  }
}
```

## LLM Learning Feature

When LLM detection is enabled, the system automatically learns new bot patterns:

- Detected patterns are saved to `learned_bot_patterns.json`
- The file is updated in real-time as new patterns are discovered
- Patterns include: signature, bot type, confidence, occurrence count
- Useful for building custom bot detection rules over time

Example learned pattern:

```json
{
  "Pattern": "missing Accept-Language header",
  "BotType": "Scraper",
  "Confidence": 0.85,
  "FirstSeen": "2025-01-15T10:30:00Z",
  "LastSeen": "2025-01-15T14:20:00Z",
  "OccurrenceCount": 47,
  "ExampleRequest": "User-Agent: CustomBot/1.0..."
}
```

## Understanding Results

The detection result includes:

- `isBot`: Boolean indicating if request is classified as bot
- `confidence`: Confidence score (0.0 to 1.0)
- `botType`: Type of bot detected (Scraper, SearchEngine, etc.)
- `botName`: Name of identified bot (e.g., "Googlebot")
- `processingTime`: Detection time in milliseconds
- `reasons`: List of detection reasons with confidence impact

## Performance

- Typical detection time: 1-5ms without LLM
- With LLM: 50-200ms (depends on model size and system)
- Results are cached for 5 minutes by default
- Minimal overhead for normal traffic
