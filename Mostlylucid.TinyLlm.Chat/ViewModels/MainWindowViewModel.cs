using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mostlylucid.TinyLlm.Chat.Models;
using Mostlylucid.TinyLlm.Chat.Services;
using ReactiveUI;

namespace Mostlylucid.TinyLlm.Chat.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly ILlamaService _llamaService;
    private string _userInput = string.Empty;
    private string _modelPath = string.Empty;
    private bool _isModelLoaded;
    private bool _isGenerating;
    private string _statusMessage = "No model loaded";
    private CancellationTokenSource? _generationCts;

    public MainWindowViewModel()
    {
        _llamaService = new LlamaService();

        // Initialize commands
        LoadModelCommand = ReactiveCommand.CreateFromTask(LoadModelAsync,
            this.WhenAnyValue(x => x.ModelPath, path => !string.IsNullOrWhiteSpace(path) && !IsGenerating));

        SendMessageCommand = ReactiveCommand.CreateFromTask(SendMessageAsync,
            this.WhenAnyValue(
                x => x.UserInput,
                x => x.IsModelLoaded,
                x => x.IsGenerating,
                (input, loaded, generating) => !string.IsNullOrWhiteSpace(input) && loaded && !generating));

        CancelGenerationCommand = ReactiveCommand.Create(CancelGeneration,
            this.WhenAnyValue(x => x.IsGenerating));

        ClearChatCommand = ReactiveCommand.Create(ClearChat);

        // Set up property change notifications
        this.WhenAnyValue(x => x.IsModelLoaded)
            .Subscribe(loaded => StatusMessage = loaded
                ? "Model loaded and ready"
                : "No model loaded");
    }

    public ObservableCollection<ChatMessage> Messages { get; } = new();

    public string UserInput
    {
        get => _userInput;
        set => this.RaiseAndSetIfChanged(ref _userInput, value);
    }

    public string ModelPath
    {
        get => _modelPath;
        set => this.RaiseAndSetIfChanged(ref _modelPath, value);
    }

    public bool IsModelLoaded
    {
        get => _isModelLoaded;
        private set => this.RaiseAndSetIfChanged(ref _isModelLoaded, value);
    }

    public bool IsGenerating
    {
        get => _isGenerating;
        private set => this.RaiseAndSetIfChanged(ref _isGenerating, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public ReactiveCommand<Unit, Unit> LoadModelCommand { get; }
    public ReactiveCommand<Unit, Unit> SendMessageCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelGenerationCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearChatCommand { get; }

    private async Task LoadModelAsync()
    {
        StatusMessage = "Loading model...";

        var config = new ModelConfig
        {
            ModelPath = ModelPath,
            ContextSize = 2048,
            GpuLayerCount = 0, // CPU only by default
            Temperature = 0.7f,
            MaxTokens = 512
        };

        var success = await _llamaService.LoadModelAsync(config);

        if (success)
        {
            IsModelLoaded = true;
            StatusMessage = "Model loaded successfully!";

            // Add welcome message
            Messages.Add(new ChatMessage
            {
                Content = "Hello! I'm a tiny AI assistant running locally on your machine. How can I help you today?",
                IsUser = false
            });
        }
        else
        {
            IsModelLoaded = false;
            StatusMessage = "Failed to load model. Check the path and try again.";
        }
    }

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(UserInput) || !IsModelLoaded)
            return;

        // Add user message
        var userMessage = new ChatMessage
        {
            Content = UserInput,
            IsUser = true
        };
        Messages.Add(userMessage);

        // Clear input
        var input = UserInput;
        UserInput = string.Empty;

        // Start generation
        IsGenerating = true;
        StatusMessage = "Generating response...";
        _generationCts = new CancellationTokenSource();

        // Create assistant message placeholder
        var assistantMessage = new ChatMessage
        {
            Content = string.Empty,
            IsUser = false
        };
        Messages.Add(assistantMessage);

        try
        {
            var responseText = string.Empty;
            var messageIndex = Messages.Count - 1;

            await foreach (var token in _llamaService.GenerateResponseAsync(Messages.Take(Messages.Count - 1), _generationCts.Token))
            {
                responseText += token;

                // Update message in place
                Messages[messageIndex] = new ChatMessage
                {
                    Id = assistantMessage.Id,
                    Content = responseText,
                    IsUser = false,
                    Timestamp = assistantMessage.Timestamp
                };
            }

            StatusMessage = "Response complete";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Generation cancelled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";

            // Update the last message to show error
            if (Messages.Any() && !Messages.Last().IsUser)
            {
                var errorMessage = new ChatMessage
                {
                    Content = $"Error generating response: {ex.Message}",
                    IsUser = false
                };
                Messages[Messages.Count - 1] = errorMessage;
            }
        }
        finally
        {
            IsGenerating = false;
            _generationCts?.Dispose();
            _generationCts = null;
        }
    }

    private void CancelGeneration()
    {
        _generationCts?.Cancel();
    }

    private void ClearChat()
    {
        Messages.Clear();
        StatusMessage = IsModelLoaded ? "Chat cleared" : "No model loaded";
    }
}
