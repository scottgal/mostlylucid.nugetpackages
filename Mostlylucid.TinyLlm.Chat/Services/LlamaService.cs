using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LLama;
using LLama.Common;
using LLama.Sampling;
using Mostlylucid.TinyLlm.Chat.Models;

namespace Mostlylucid.TinyLlm.Chat.Services;

/// <summary>
/// Implementation of ILlamaService using LLamaSharp
/// </summary>
public class LlamaService : ILlamaService
{
    private LLamaWeights? _model;
    private LLamaContext? _context;
    private InteractiveExecutor? _executor;
    private ModelConfig? _currentConfig;

    public bool IsModelLoaded => _model != null && _context != null;
    public ModelConfig? CurrentConfig => _currentConfig;

    /// <summary>
    /// Loads a model from the specified configuration
    /// </summary>
    public async Task<bool> LoadModelAsync(ModelConfig config, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Clean up existing model if any
                UnloadModel();

                // Create model parameters
                var parameters = new ModelParams(config.ModelPath)
                {
                    ContextSize = config.ContextSize,
                    GpuLayerCount = config.GpuLayerCount
                };

                // Load the model
                _model = LLamaWeights.LoadFromFile(parameters);

                // Create context
                _context = _model.CreateContext(parameters);

                // Create executor for interactive sessions
                _executor = new InteractiveExecutor(_context);

                _currentConfig = config;

                return true;
            }
            catch (Exception)
            {
                UnloadModel();
                return false;
            }
        }, ct);
    }

    /// <summary>
    /// Generates a streaming response based on conversation history
    /// </summary>
    public async IAsyncEnumerable<string> GenerateResponseAsync(
        IEnumerable<ChatMessage> history,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!IsModelLoaded || _executor == null || _currentConfig == null)
        {
            yield return "Error: No model loaded. Please load a model first.";
            yield break;
        }

        // Build the prompt from conversation history
        var prompt = BuildPrompt(history);

        // Inference parameters with sampling pipeline
        var inferenceParams = new InferenceParams
        {
            SamplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = _currentConfig.Temperature,
                TopP = _currentConfig.TopP,
                RepeatPenalty = _currentConfig.RepeatPenalty
            },
            MaxTokens = _currentConfig.MaxTokens,
            AntiPrompts = new List<string> { "User:", "\nUser:" } // Stop generation on user prompt
        };

        // Stream tokens as they're generated
        await foreach (var token in _executor.InferAsync(prompt, inferenceParams, ct))
        {
            if (ct.IsCancellationRequested)
                break;

            yield return token;
        }
    }

    /// <summary>
    /// Builds a prompt from the conversation history
    /// Uses a simple format: "User: [message]\nAssistant: [response]\n"
    /// </summary>
    private string BuildPrompt(IEnumerable<ChatMessage> history)
    {
        var sb = new StringBuilder();

        // Add system prompt
        sb.AppendLine("You are a helpful AI assistant. You provide clear, concise answers to questions.");
        sb.AppendLine();

        // Add conversation history
        foreach (var message in history)
        {
            if (message.IsUser)
            {
                sb.AppendLine($"User: {message.Content}");
            }
            else
            {
                sb.AppendLine($"Assistant: {message.Content}");
            }
        }

        // Add the assistant prompt to start generation
        sb.Append("Assistant: ");

        return sb.ToString();
    }

    /// <summary>
    /// Unloads the model and frees resources
    /// </summary>
    public void UnloadModel()
    {
        _executor = null;
        _context?.Dispose();
        _context = null;
        _model?.Dispose();
        _model = null;
        _currentConfig = null;
    }

    public void Dispose()
    {
        UnloadModel();
    }
}
