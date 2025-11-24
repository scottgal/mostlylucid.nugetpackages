using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.LlmI18nAssistant.Models;
using Mostlylucid.LlmI18nAssistant.Services;

namespace Mostlylucid.LlmI18nAssistant.Cli.Commands;

public class TranslateCommandHandler
{
    private readonly IServiceProvider _services;

    public TranslateCommandHandler(IServiceProvider services)
    {
        _services = services;
    }

    public async Task<int> ExecuteAsync(
        FileInfo file,
        string source,
        string target,
        DirectoryInfo? output,
        bool consistency,
        string? format,
        CancellationToken cancellationToken)
    {
        if (!file.Exists)
        {
            Console.Error.WriteLine($"Error: File not found: {file.FullName}");
            return 1;
        }

        var targetLanguages = target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (targetLanguages.Length == 0)
        {
            Console.Error.WriteLine("Error: At least one target language is required");
            return 1;
        }

        Console.WriteLine($"Translating: {file.Name}");
        Console.WriteLine($"Source language: {source}");
        Console.WriteLine($"Target languages: {string.Join(", ", targetLanguages)}");
        Console.WriteLine($"Consistency mode: {(consistency ? "enabled" : "disabled")}");
        Console.WriteLine();

        try
        {
            using var scope = _services.CreateScope();
            var assistant = scope.ServiceProvider.GetRequiredService<ILlmI18nAssistant>();
            var parser = scope.ServiceProvider.GetRequiredService<IResourceFileParser>();

            // Check services
            var status = await assistant.CheckServicesAsync(cancellationToken);
            if (!status.OllamaAvailable)
            {
                Console.Error.WriteLine("Error: Ollama is not available. Please ensure Ollama is running.");
                return 1;
            }

            Console.WriteLine($"Ollama available with models: {string.Join(", ", status.OllamaModels.Take(3))}...");
            if (status.NmtAvailable)
                Console.WriteLine("NMT service available");
            Console.WriteLine();

            var options = new TranslationOptions
            {
                UseConsistencyMode = consistency,
                OnProgress = progress =>
                {
                    Console.Write(
                        $"\r[{progress.TargetLanguage}] {progress.CurrentIndex}/{progress.TotalCount}: {progress.CurrentKey}"
                            .PadRight(80));
                }
            };

            // Translate to each language
            var outputDir = output?.FullName ?? file.DirectoryName ?? ".";
            var baseName = Path.GetFileNameWithoutExtension(file.Name);
            var extension = GetOutputExtension(format, file.Extension);

            foreach (var targetLang in targetLanguages)
            {
                Console.WriteLine($"\nTranslating to {targetLang}...");

                var result = await assistant.TranslateResourceFileAsync(
                    file.FullName,
                    source,
                    targetLang,
                    options,
                    cancellationToken);

                if (!result.Success)
                {
                    Console.Error.WriteLine($"\nWarning: Translation had {result.Errors.Count} errors");
                    foreach (var error in result.Errors.Take(5))
                        Console.Error.WriteLine($"  - {error.Key}: {error.Message}");
                }

                // Write output file
                var outputPath = Path.Combine(outputDir, $"{baseName}.{targetLang}{extension}");
                await parser.WriteAsync(result, outputPath, cancellationToken);

                Console.WriteLine();
                Console.WriteLine($"Written: {outputPath}");
                Console.WriteLine($"  Translated: {result.Statistics.TranslatedCount}");
                Console.WriteLine($"  Skipped: {result.Statistics.SkippedCount}");
                Console.WriteLine($"  Failed: {result.Statistics.FailedCount}");
                Console.WriteLine($"  Duration: {result.Duration.TotalSeconds:F1}s");
            }

            Console.WriteLine();
            Console.WriteLine("Translation complete!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static string GetOutputExtension(string? format, string inputExtension)
    {
        return format?.ToLowerInvariant() switch
        {
            "resx" => ".resx",
            "json" => ".json",
            "properties" => ".properties",
            _ => inputExtension
        };
    }
}