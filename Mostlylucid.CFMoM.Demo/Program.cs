using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.CFMoM.Aggregation;
using Mostlylucid.CFMoM.ConsensusSpace;
using Mostlylucid.CFMoM.Constrainers;
using Mostlylucid.CFMoM.Demo.Constrainers;
using Mostlylucid.CFMoM.Demo.Embedding;
using Mostlylucid.CFMoM.Demo.Learning;
using Mostlylucid.CFMoM.Demo.Llm;
using Mostlylucid.CFMoM.Demo.Proposers;
using Mostlylucid.CFMoM.Orchestration;
using Mostlylucid.CFMoM.Proposers;
using Spectre.Console;

namespace Mostlylucid.CFMoM.Demo;

public static class Program
{
    public static async Task Main(string[] args)
    {
        // Setup services
        var services = new ServiceCollection();
        ConfigureServices(services);
        var provider = services.BuildServiceProvider();

        // Initialize embedding service (downloads model on first run)
        var embeddingService = provider.GetRequiredService<IEmbeddingService>();
        if (embeddingService is OnnxEmbeddingService onnxService)
        {
            await AnsiConsole.Status()
                .StartAsync("Initializing embedding model...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    await onnxService.InitializeAsync();
                });
        }

        // Check Ollama availability
        var ollamaClient = provider.GetRequiredService<IOllamaClient>();
        var ollamaAvailable = await ollamaClient.IsAvailableAsync();

        // Get learning store stats
        var learningStore = provider.GetRequiredService<ILearningStore>();
        var stats = await learningStore.GetStatsAsync();

        // Print header
        var headerPanel = new Panel(
            new Markup("[bold blue]CFMoM Prompt Router Demo[/]\n" +
                       $"Using Ollama: {(ollamaAvailable ? "[green]Connected[/]" : "[yellow]Fallback Mode[/]")}\n" +
                       $"Learning DB: [cyan]{stats.TotalDecisions}[/] entries, [cyan]{stats.TotalHits}[/] hits"))
            .Border(BoxBorder.Rounded)
            .Header("[bold]Multi-Tier LLM Architecture[/]");

        AnsiConsole.Write(headerPanel);

        // Show architecture
        var archTree = new Tree("[bold]LLM Tiers[/]")
            .Style("dim");
        archTree.AddNode("[yellow]Sentinel[/] (tinyllama) - Fast triage, ~1B params");
        archTree.AddNode("[green]Intent/Sentiment[/] (llama3.2:3b) - Wave 0 parallel analysis");
        archTree.AddNode("[blue]Safety Evaluator[/] (llama3.1:8b) - Deep analysis, bounded async");

        AnsiConsole.Write(archTree);
        AnsiConsole.WriteLine();

        // Get orchestrator
        var orchestrator = provider.GetRequiredService<CFMoMOrchestrator<PromptContext, PromptRoutingDecision>>();

        // Check for command-line argument
        if (args.Length > 0)
        {
            var input = string.Join(" ", args);
            AnsiConsole.MarkupLine($"[yellow]Processing:[/] {input}");
            await ProcessPromptAsync(orchestrator, learningStore, provider, input);
            return;
        }

        // Main interactive loop
        while (true)
        {
            var input = AnsiConsole.Ask<string>("[yellow]Enter prompt[/] (or 'quit'):");

            if (input.Equals("quit", StringComparison.OrdinalIgnoreCase))
                break;

            await ProcessPromptAsync(orchestrator, learningStore, provider, input);
            AnsiConsole.WriteLine();
        }

        // Cleanup
        (provider as IDisposable)?.Dispose();
    }

    private static async Task ProcessPromptAsync(
        CFMoMOrchestrator<PromptContext, PromptRoutingDecision> orchestrator,
        ILearningStore learningStore,
        IServiceProvider provider,
        string promptText)
    {
        var embeddingService = provider.GetRequiredService<IEmbeddingService>();

        // Create context with embedding
        var embedding = embeddingService.Embed(promptText);
        var context = new PromptContext
        {
            Prompt = promptText,
            Embedding = embedding
        };

        // Execute pipeline with live display
        AnsiConsole.Status()
            .Start("Processing...", ctx =>
            {
                ctx.Status = "Starting pipeline...";
            });

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await orchestrator.ExecuteAsync(context);
        stopwatch.Stop();

        // Display results
        DisplayResults(result, stopwatch.ElapsedMilliseconds);

        // Store learned decision if not from cache
        if (!result.Signals.Any(s => s.FactsSchemaId == "learned.v1"))
        {
            await StoreLearningAsync(learningStore, embeddingService, promptText, embedding, result);
        }
    }

    private static void DisplayResults(
        CFMoMResult<PromptRoutingDecision> result,
        long durationMs)
    {
        var learningMatch = result.Signals.FirstOrDefault(s => s.FactsSchemaId == "learned.v1");

        // Learning check section
        AnsiConsole.Write(new Rule("[bold]Learning Check[/]").RuleStyle("grey"));

        if (learningMatch != null)
        {
            var similarity = learningMatch.Metadata.GetValueOrDefault("similarity", 0.0);
            AnsiConsole.MarkupLine($"[green]LEARNED SOLUTION FOUND![/]");
            AnsiConsole.MarkupLine($"  Similarity: [cyan]{similarity:P0}[/]");
            AnsiConsole.MarkupLine($"  Skipped expensive LLM calls!");
        }
        else
        {
            AnsiConsole.MarkupLine("[dim]No similar prompt found[/]");
        }

        // Wave execution section
        AnsiConsole.Write(new Rule($"[bold]Waves ({result.WaveCount})[/]").RuleStyle("grey"));

        var table = new Table()
            .AddColumn("Proposer")
            .AddColumn("Status")
            .AddColumn("Signal");

        foreach (var proposer in result.CompletedProposers)
        {
            var signal = result.Signals.FirstOrDefault(s => s.SourceId == proposer);
            var signalInfo = signal != null
                ? $"conf: {signal.Confidence:P0}"
                : "-";

            table.AddRow(
                $"[green]{proposer}[/]",
                "[green]Done[/]",
                signalInfo);
        }

        foreach (var proposer in result.FailedProposers)
        {
            table.AddRow(
                $"[red]{proposer}[/]",
                "[red]Failed[/]",
                "-");
        }

        AnsiConsole.Write(table);

        // Aggregation section
        AnsiConsole.Write(new Rule("[bold]Aggregation[/]").RuleStyle("grey"));
        AnsiConsole.MarkupLine($"  Score:      [cyan]{result.Aggregation.Score:P0}[/]");
        AnsiConsole.MarkupLine($"  Confidence: [cyan]{result.Aggregation.Confidence:P0}[/]");
        AnsiConsole.MarkupLine($"  Band:       [cyan]{result.Aggregation.Band}[/]");

        // Decision section
        var decisionColor = result.Decision switch
        {
            PromptRoutingDecision.Allow => "green",
            PromptRoutingDecision.Block => "red",
            PromptRoutingDecision.Challenge => "yellow",
            _ => "blue"
        };

        AnsiConsole.Write(new Rule($"[bold {decisionColor}]Decision: {result.Decision}[/]").RuleStyle(decisionColor));

        // Get handler suggestion
        var handler = SuggestHandler(result);
        AnsiConsole.MarkupLine($"  Route:      [cyan]{handler}[/]");
        AnsiConsole.MarkupLine($"  Signals:    [dim]{result.Signals.Count}[/]");
        AnsiConsole.MarkupLine($"  Duration:   [dim]{durationMs}ms[/]");

        if (learningMatch != null)
        {
            AnsiConsole.MarkupLine($"  [dim italic](Learned - no LLM calls)[/]");
        }
    }

    private static string SuggestHandler(CFMoMResult<PromptRoutingDecision> result)
    {
        // Get intent from signals
        var intentSignal = result.Signals.FirstOrDefault(s => s.FactsSchemaId == "intent.v1");
        if (intentSignal != null)
        {
            var intentMetadata = intentSignal.Metadata.GetValueOrDefault("intent", "general") as string;
            return intentMetadata?.ToLowerInvariant() switch
            {
                "creative" => "CreativeWritingHandler",
                "coding" => "CodeAssistantHandler",
                "question" => "QuestionAnswerHandler",
                "command" => "CommandExecutorHandler",
                "chat" => "ConversationHandler",
                _ => "GeneralHandler"
            };
        }

        return "GeneralHandler";
    }

    private static async Task StoreLearningAsync(
        ILearningStore learningStore,
        IEmbeddingService embeddingService,
        string promptText,
        float[] embedding,
        CFMoMResult<PromptRoutingDecision> result)
    {
        var decision = new LearnedDecision
        {
            Id = Guid.NewGuid(),
            PromptText = promptText,
            PromptEmbedding = embedding,
            Decision = result.Decision.ToString(),
            Reason = $"Score: {result.Aggregation.Score:P0}, Confidence: {result.Aggregation.Confidence:P0}",
            Score = result.Aggregation.Score,
            Confidence = result.Aggregation.Confidence,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            HitCount = 0
        };

        // Extract facts from quick extractor
        var quickFacts = QuickFactExtractor.Extract(promptText);
        var facts = quickFacts.Select(kv => new LearnedFact
        {
            Id = Guid.NewGuid(),
            DecisionId = decision.Id,
            SchemaId = "quick",
            FactKey = kv.Key,
            FactValue = kv.Value,
            Confidence = 0.8,
            OccurrenceCount = 1
        });

        try
        {
            await learningStore.StoreDecisionAsync(decision, facts);
            AnsiConsole.MarkupLine("[dim]Decision saved to learning store[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red dim]Failed to save: {ex.Message}[/]");
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Ollama HTTP client
        services.AddHttpClient<IOllamaClient, OllamaClient>(client =>
        {
            client.BaseAddress = new Uri("http://localhost:11434/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        // Embedding service
        services.AddSingleton<IEmbeddingService, OnnxEmbeddingService>();

        // Learning store - try DuckDB first, fall back to in-memory
        services.AddSingleton<ILearningStore>(sp =>
        {
            var embeddingService = sp.GetRequiredService<IEmbeddingService>();
            try
            {
                var duckDbStore = new DuckDbLearningStore(embeddingService);
                // Test if DuckDB is actually available by getting stats
                _ = duckDbStore.GetStatsAsync().GetAwaiter().GetResult();
                return duckDbStore;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Learning] DuckDB unavailable: {ex.Message}");
                return new InMemoryLearningStore(embeddingService);
            }
        });

        // CFMoM components
        services.AddSingleton<IConsensusSpace, ConsensusSpace.ConsensusSpace>();
        services.AddSingleton<IAggregator, WeightedAggregator>();
        services.AddSingleton<IConstrainer<PromptContext, PromptRoutingDecision>, PromptRouterConstrainer>();

        // Proposers
        services.AddSingleton<IProposer<PromptContext>>(sp =>
            new LearningProposer(
                sp.GetRequiredService<ILearningStore>(),
                sp.GetRequiredService<IEmbeddingService>()));

        services.AddSingleton<IProposer<PromptContext>>(sp =>
            new SentinelProposer(sp.GetRequiredService<IOllamaClient>()));

        services.AddSingleton<IProposer<PromptContext>>(sp =>
            new IntentClassifierProposer(sp.GetRequiredService<IOllamaClient>()));

        services.AddSingleton<IProposer<PromptContext>>(sp =>
            new SentimentProposer(sp.GetRequiredService<IOllamaClient>()));

        services.AddSingleton<IProposer<PromptContext>>(sp =>
            new TopicProposer(sp.GetRequiredService<IOllamaClient>()));

        services.AddSingleton<IProposer<PromptContext>>(sp =>
            new SafetyEvaluatorProposer(sp.GetRequiredService<IOllamaClient>()));

        // Orchestrator
        services.AddSingleton(sp =>
        {
            var proposers = sp.GetServices<IProposer<PromptContext>>().ToList();
            var consensusSpace = sp.GetRequiredService<IConsensusSpace>();
            var aggregator = sp.GetRequiredService<IAggregator>();
            var constrainer = sp.GetRequiredService<IConstrainer<PromptContext, PromptRoutingDecision>>();

            var options = new CFMoMOptions
            {
                MaxWaves = 5,
                MaxParallelProposers = 4
            };

            return new CFMoMOrchestrator<PromptContext, PromptRoutingDecision>(
                proposers,
                consensusSpace,
                aggregator,
                constrainer,
                options);
        });
    }
}
