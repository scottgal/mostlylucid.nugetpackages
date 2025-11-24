using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.LlmI18nAssistant.Services;

namespace Mostlylucid.LlmI18nAssistant.Cli.Commands;

public class StatusCommandHandler
{
    private readonly IServiceProvider _services;

    public StatusCommandHandler(IServiceProvider services)
    {
        _services = services;
    }

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Checking translation services...");
        Console.WriteLine();

        try
        {
            using var scope = _services.CreateScope();
            var assistant = scope.ServiceProvider.GetRequiredService<ILlmI18nAssistant>();

            var status = await assistant.CheckServicesAsync(cancellationToken);

            // Ollama status
            Console.Write("Ollama LLM:     ");
            if (status.OllamaAvailable)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Available");
                Console.ResetColor();
                Console.WriteLine($"  Models: {string.Join(", ", status.OllamaModels.Take(5))}");
                if (status.OllamaModels.Count > 5)
                    Console.WriteLine($"  ... and {status.OllamaModels.Count - 5} more");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Unavailable");
                Console.ResetColor();
            }

            // NMT status
            Console.Write("NMT Service:    ");
            if (status.NmtAvailable)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Available");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Unavailable (optional)");
                Console.ResetColor();
            }

            // Embedding status
            Console.Write("Embeddings:     ");
            if (status.EmbeddingAvailable)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Available");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Unavailable (consistency mode disabled)");
                Console.ResetColor();
            }

            Console.WriteLine();

            if (status.AllServicesAvailable)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("All required services are available. Ready to translate!");
                Console.ResetColor();
                return 0;
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Some required services are unavailable.");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("To start Ollama:");
            Console.WriteLine("  ollama serve");
            Console.WriteLine();
            Console.WriteLine("Recommended models for translation:");
            Console.WriteLine("  ollama pull mannix/llamax3-8b-alpaca");
            Console.WriteLine("  ollama pull llama3.2:3b");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}