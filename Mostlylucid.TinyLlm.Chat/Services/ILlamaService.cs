using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mostlylucid.TinyLlm.Chat.Models;

namespace Mostlylucid.TinyLlm.Chat.Services;

/// <summary>
/// Service interface for interacting with LLama models
/// </summary>
public interface ILlamaService : IDisposable
{
    /// <summary>
    /// Loads a model from the specified path
    /// </summary>
    Task<bool> LoadModelAsync(ModelConfig config, CancellationToken ct = default);

    /// <summary>
    /// Checks if a model is currently loaded
    /// </summary>
    bool IsModelLoaded { get; }

    /// <summary>
    /// Generates a response based on the conversation history
    /// </summary>
    IAsyncEnumerable<string> GenerateResponseAsync(
        IEnumerable<ChatMessage> history,
        CancellationToken ct = default);

    /// <summary>
    /// Unloads the current model and frees resources
    /// </summary>
    void UnloadModel();

    /// <summary>
    /// Gets the current model configuration
    /// </summary>
    ModelConfig? CurrentConfig { get; }
}
