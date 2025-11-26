# TinyLlm Chat - Local AI Assistant

A cross-platform desktop chat application built with Avalonia UI that runs small language models locally using LLamaSharp.

## Features

- **Cross-Platform**: Runs on Windows, macOS, and Linux thanks to Avalonia UI
- **Local Inference**: All processing happens on your machine - no cloud APIs required
- **Streaming Responses**: See the AI's response token-by-token as it's generated
- **Clean MVVM Architecture**: Proper separation of concerns using ReactiveUI
- **Modern UI**: Fluent-style interface with chat bubbles
- **Low Resource Usage**: Designed to run small quantized models efficiently

## Requirements

- .NET 9.0 SDK
- A GGUF format language model (e.g., TinyLlama, Phi-2, or similar small models)
- At least 4GB of RAM (8GB+ recommended for larger models)
- Optional: CUDA-capable GPU for faster inference

## Getting Started

### 1. Get a Model

Download a small GGUF model. Good options include:

- **TinyLlama 1.1B** (Recommended for beginners): https://huggingface.co/TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF
- **Phi-2 2.7B**: https://huggingface.co/TheBloke/phi-2-GGUF
- **Mistral 7B** (Requires more RAM): https://huggingface.co/TheBloke/Mistral-7B-Instruct-v0.2-GGUF

Download a quantized version like `Q4_K_M` for a good balance of quality and performance.

### 2. Build and Run

```bash
cd Mostlylucid.TinyLlm.Chat
dotnet restore
dotnet build
dotnet run
```

### 3. Load Your Model

1. Enter the full path to your `.gguf` model file
2. Click "Load Model" and wait for it to initialize
3. Start chatting!

## Architecture

```
Mostlylucid.TinyLlm.Chat/
├── Models/              # Data models (ChatMessage, ModelConfig)
├── Services/            # Business logic (LlamaService)
├── ViewModels/          # MVVM ViewModels with ReactiveUI
├── Views/               # Avalonia XAML views
├── App.axaml            # Application entry point
└── Program.cs           # .NET entry point
```

## Configuration

The default model configuration is:

- **Context Size**: 2048 tokens
- **Temperature**: 0.7 (creativity level)
- **Max Tokens**: 512 per response
- **GPU Layers**: 0 (CPU only)

You can modify these in `MainWindowViewModel.cs` before loading a model.

## Performance Tips

- **Use Quantized Models**: Q4_K_M or Q4_0 offer the best speed/quality tradeoff
- **Enable GPU**: If you have a CUDA GPU, increase `GpuLayerCount` in the model config
- **Reduce Context Size**: Smaller context windows use less RAM and are faster
- **Choose Smaller Models**: TinyLlama (1.1B) is perfect for testing and quick responses

## Troubleshooting

**Model fails to load**
- Check the file path is correct
- Ensure the model is in GGUF format
- Try a smaller quantization (e.g., Q4_0 instead of Q8_0)

**Out of memory errors**
- Reduce the context size
- Use a more aggressively quantized model
- Close other applications

**Slow inference**
- Try enabling GPU layers if you have a compatible GPU
- Use a smaller model
- Reduce max tokens per response

## Future Enhancements

- Integration with the mostlyucid.llmbackend universal LLM/NMT backend
- WebAssembly support for browser-based inference
- Model download manager
- Conversation export/import
- Custom system prompts
- Multi-turn conversation optimization

## License

Part of the Mostlylucid blog project. See parent repository for license details.
