using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.CFMoM.Aggregation;
using Mostlylucid.CFMoM.ConsensusSpace;
using Mostlylucid.CFMoM.Constrainers;
using Mostlylucid.CFMoM.Orchestration;

namespace Mostlylucid.CFMoM.Extensions;

/// <summary>
///     Extension methods for registering CFMoM services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Add CFMoM framework with default components.
    /// </summary>
    /// <typeparam name="TContext">The context type for proposers.</typeparam>
    /// <typeparam name="TDecision">The decision type from constrainer.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional options configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCFMoM<TContext, TDecision>(
        this IServiceCollection services,
        Action<CFMoMOptions>? configureOptions = null)
    {
        // Configure options
        var options = new CFMoMOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton(options);

        // Register core components
        services.AddScoped<IConsensusSpace, ConsensusSpace.ConsensusSpace>();
        services.AddSingleton<IAggregator, WeightedAggregator>();

        // Register orchestrator (scoped for per-request use)
        services.AddScoped<CFMoMOrchestrator<TContext, TDecision>>();

        return services;
    }

    /// <summary>
    ///     Add CFMoM framework with CommonDecision as the decision type.
    ///     Uses ThresholdConstrainer by default.
    /// </summary>
    /// <typeparam name="TContext">The context type for proposers.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional options configuration.</param>
    /// <param name="configureThresholds">Optional threshold configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCFMoMWithThresholds<TContext>(
        this IServiceCollection services,
        Action<CFMoMOptions>? configureOptions = null,
        Action<ThresholdConstrainerOptions>? configureThresholds = null)
    {
        // Configure options
        var options = new CFMoMOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton(options);

        // Configure threshold options
        var thresholdOptions = new ThresholdConstrainerOptions();
        configureThresholds?.Invoke(thresholdOptions);

        // Register core components
        services.AddScoped<IConsensusSpace, ConsensusSpace.ConsensusSpace>();
        services.AddSingleton<IAggregator, WeightedAggregator>();
        services.AddSingleton<IConstrainer<TContext, CommonDecision>>(_ =>
            new ThresholdConstrainer<TContext>(thresholdOptions));

        // Register orchestrator
        services.AddScoped<CFMoMOrchestrator<TContext, CommonDecision>>();

        return services;
    }

    /// <summary>
    ///     Add a custom constrainer.
    /// </summary>
    public static IServiceCollection AddCFMoMConstrainer<TContext, TDecision, TConstrainer>(
        this IServiceCollection services)
        where TConstrainer : class, IConstrainer<TContext, TDecision>
    {
        services.AddSingleton<IConstrainer<TContext, TDecision>, TConstrainer>();
        return services;
    }

    /// <summary>
    ///     Add a proposer implementation.
    /// </summary>
    public static IServiceCollection AddCFMoMProposer<TContext, TProposer>(
        this IServiceCollection services)
        where TProposer : class, Proposers.IProposer<TContext>
    {
        services.AddSingleton<Proposers.IProposer<TContext>, TProposer>();
        return services;
    }

    /// <summary>
    ///     Add a custom aggregator.
    /// </summary>
    public static IServiceCollection AddCFMoMAggregator<TAggregator>(
        this IServiceCollection services)
        where TAggregator : class, IAggregator
    {
        services.AddSingleton<IAggregator, TAggregator>();
        return services;
    }

    /// <summary>
    ///     Configure the weighted aggregator with custom options.
    /// </summary>
    public static IServiceCollection AddCFMoMWeightedAggregator(
        this IServiceCollection services,
        Action<WeightedAggregatorOptions> configure)
    {
        var options = new WeightedAggregatorOptions();
        configure(options);
        services.AddSingleton<IAggregator>(_ => new WeightedAggregator(options));
        return services;
    }

    /// <summary>
    ///     Configure the consensus space with custom options.
    /// </summary>
    public static IServiceCollection ConfigureCFMoMConsensusSpace(
        this IServiceCollection services,
        Action<ConsensusSpaceOptions> configure)
    {
        var options = new ConsensusSpaceOptions();
        configure(options);
        services.AddSingleton(options);
        return services;
    }
}
