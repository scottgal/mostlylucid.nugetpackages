using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mostlylucid.LlmI18nAssistant.Cli.Commands;
using Mostlylucid.LlmI18nAssistant.Extensions;

namespace Mostlylucid.LlmI18nAssistant.Cli;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("LLM-assisted localization tool for .resx and JSON resource files");

        // Add translate command
        var translateCommand = CreateTranslateCommand();
        rootCommand.AddCommand(translateCommand);

        // Add status command
        var statusCommand = CreateStatusCommand();
        rootCommand.AddCommand(statusCommand);

        // Add glossary command
        var glossaryCommand = CreateGlossaryCommand();
        rootCommand.AddCommand(glossaryCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static Command CreateTranslateCommand()
    {
        var fileArgument = new Argument<FileInfo>(
            "file",
            "Path to the resource file (.resx or .json)");

        var sourceOption = new Option<string>(
            ["--source", "-s"],
            () => "en",
            "Source language code");

        var targetOption = new Option<string>(
            ["--target", "-t"],
            "Target language codes (comma-separated)")
        {
            IsRequired = true
        };

        var outputOption = new Option<DirectoryInfo?>(
            ["--output", "-o"],
            "Output directory (default: same as input)");

        var glossaryOption = new Option<string?>(
            ["--glossary", "-g"],
            "Path to glossary file or directory");

        var consistencyOption = new Option<bool>(
            ["--consistency", "-c"],
            () => true,
            "Enable consistency mode");

        var formatOption = new Option<string?>(
            ["--format", "-f"],
            "Output format: resx, json, properties");

        var ollamaOption = new Option<string>(
            ["--ollama"],
            () => "http://localhost:11434",
            "Ollama endpoint");

        var modelOption = new Option<string>(
            ["--model"],
            () => "llama3.2:3b",
            "Ollama model name");

        var nmtOption = new Option<string?>(
            ["--nmt"],
            "NMT service endpoint");

        var verboseOption = new Option<bool>(
            ["--verbose", "-v"],
            "Verbose output");

        var command = new Command("translate", "Translate a resource file to one or more languages")
        {
            fileArgument,
            sourceOption,
            targetOption,
            outputOption,
            glossaryOption,
            consistencyOption,
            formatOption,
            ollamaOption,
            modelOption,
            nmtOption,
            verboseOption
        };

        command.SetHandler(async context =>
        {
            var file = context.ParseResult.GetValueForArgument(fileArgument);
            var source = context.ParseResult.GetValueForOption(sourceOption)!;
            var target = context.ParseResult.GetValueForOption(targetOption)!;
            var output = context.ParseResult.GetValueForOption(outputOption);
            var glossary = context.ParseResult.GetValueForOption(glossaryOption);
            var consistency = context.ParseResult.GetValueForOption(consistencyOption);
            var format = context.ParseResult.GetValueForOption(formatOption);
            var ollama = context.ParseResult.GetValueForOption(ollamaOption)!;
            var model = context.ParseResult.GetValueForOption(modelOption)!;
            var nmt = context.ParseResult.GetValueForOption(nmtOption);
            var verbose = context.ParseResult.GetValueForOption(verboseOption);

            var services = BuildServiceProvider(ollama, model, nmt, glossary, verbose);
            var handler = new TranslateCommandHandler(services);

            context.ExitCode = await handler.ExecuteAsync(
                file!, source, target, output, consistency, format, context.GetCancellationToken());
        });

        return command;
    }

    private static Command CreateStatusCommand()
    {
        var ollamaOption = new Option<string>(
            ["--ollama"],
            () => "http://localhost:11434",
            "Ollama endpoint");

        var nmtOption = new Option<string?>(
            ["--nmt"],
            "NMT service endpoint");

        var command = new Command("status", "Check the status of translation services")
        {
            ollamaOption,
            nmtOption
        };

        command.SetHandler(async context =>
        {
            var ollama = context.ParseResult.GetValueForOption(ollamaOption)!;
            var nmt = context.ParseResult.GetValueForOption(nmtOption);

            var services = BuildServiceProvider(ollama, "llama3.2:3b", nmt, null, false);
            var handler = new StatusCommandHandler(services);

            context.ExitCode = await handler.ExecuteAsync(context.GetCancellationToken());
        });

        return command;
    }

    private static Command CreateGlossaryCommand()
    {
        var initCommand = new Command("init", "Initialize a new glossary file");
        var pathArgument = new Argument<FileInfo>("path", "Path for the new glossary file");
        initCommand.AddArgument(pathArgument);
        initCommand.SetHandler(async context =>
        {
            var path = context.ParseResult.GetValueForArgument(pathArgument);
            var handler = new GlossaryCommandHandler();
            context.ExitCode = await handler.InitAsync(path!);
        });

        var importCommand = new Command("import", "Import translations to glossary");
        var glossaryPathArg = new Argument<FileInfo>("glossary", "Path to glossary file");
        var fromOption = new Option<FileInfo>("--from", "Source resource file") { IsRequired = true };
        var langOption = new Option<string>("--lang", "Language code of the translations") { IsRequired = true };
        importCommand.AddArgument(glossaryPathArg);
        importCommand.AddOption(fromOption);
        importCommand.AddOption(langOption);
        importCommand.SetHandler(async context =>
        {
            var glossary = context.ParseResult.GetValueForArgument(glossaryPathArg);
            var from = context.ParseResult.GetValueForOption(fromOption);
            var lang = context.ParseResult.GetValueForOption(langOption);
            var handler = new GlossaryCommandHandler();
            context.ExitCode = await handler.ImportAsync(glossary!, from!, lang!);
        });

        var command = new Command("glossary", "Manage glossary files")
        {
            initCommand,
            importCommand
        };

        return command;
    }

    private static IServiceProvider BuildServiceProvider(
        string ollamaEndpoint,
        string model,
        string? nmtEndpoint,
        string? glossaryPath,
        bool verbose)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("llmi18n.json", true)
            .AddJsonFile("appsettings.json", true)
            .Build();

        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Information);
        });

        // Add I18n services with overrides from command line
        services.AddLlmI18nAssistant(options =>
        {
            // Bind from configuration first
            configuration.GetSection("LlmI18nAssistant").Bind(options);

            // Override with command line options
            options.Ollama.Endpoint = ollamaEndpoint;
            options.Ollama.Model = model;

            if (!string.IsNullOrEmpty(nmtEndpoint))
            {
                options.Nmt.Enabled = true;
                options.Nmt.ServiceEndpoints = [nmtEndpoint];
            }

            if (!string.IsNullOrEmpty(glossaryPath))
                options.ConsistencyMode.GlossaryPath = glossaryPath;
        });

        return services.BuildServiceProvider();
    }
}