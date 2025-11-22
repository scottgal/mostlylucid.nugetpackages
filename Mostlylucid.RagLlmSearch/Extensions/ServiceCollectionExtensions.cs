using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.RagLlmSearch.Configuration;
using Mostlylucid.RagLlmSearch.Conversation;
using Mostlylucid.RagLlmSearch.LlmServices;
using Mostlylucid.RagLlmSearch.Models;
using Mostlylucid.RagLlmSearch.Rag;
using Mostlylucid.RagLlmSearch.SearchProviders;
using Mostlylucid.RagLlmSearch.SignalR;

namespace Mostlylucid.RagLlmSearch.Extensions;

/// <summary>
/// Extension methods for configuring RAG LLM Search services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds RAG LLM Search services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Optional configuration action for RagLlmSearchOptions</param>
    /// <param name="configureSearchProviders">Optional configuration action for SearchProviderOptions</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddRagLlmSearch(
        this IServiceCollection services,
        Action<RagLlmSearchOptions>? configureOptions = null,
        Action<SearchProviderOptions>? configureSearchProviders = null)
    {
        // Configure options
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<RagLlmSearchOptions>(_ => { });
        }

        if (configureSearchProviders != null)
        {
            services.Configure(configureSearchProviders);
        }
        else
        {
            services.Configure<SearchProviderOptions>(_ => { });
        }

        // Register HttpClient for search providers
        services.AddHttpClient<DuckDuckGoSearchProvider>();
        services.AddHttpClient<BraveSearchProvider>();
        services.AddHttpClient<TavilySearchProvider>();
        services.AddHttpClient<SerpApiSearchProvider>();

        // Register search providers
        services.AddSingleton<ISearchProvider, DuckDuckGoSearchProvider>();
        services.AddSingleton<ISearchProvider, BraveSearchProvider>();
        services.AddSingleton<ISearchProvider, TavilySearchProvider>();
        services.AddSingleton<ISearchProvider, SerpApiSearchProvider>();
        services.AddSingleton<ISearchProviderFactory, SearchProviderFactory>();

        // Register core services
        services.AddSingleton<ILlmService, OllamaLlmService>();
        services.AddSingleton<IRagService, SqliteRagService>();
        services.AddSingleton<IConversationService, SqliteConversationService>();
        services.AddSingleton<IChatService, ChatService>();

        // Add SignalR
        services.AddSignalR();

        return services;
    }

    /// <summary>
    /// Maps the ChatHub SignalR endpoint
    /// </summary>
    /// <param name="endpoints">The endpoint route builder</param>
    /// <param name="pattern">The hub URL pattern (default: /chathub)</param>
    /// <returns>The hub endpoint convention builder</returns>
    public static HubEndpointConventionBuilder MapChatHub(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/chathub")
    {
        return endpoints.MapHub<ChatHub>(pattern);
    }

    /// <summary>
    /// Initializes the RAG and Conversation services (creates database tables)
    /// Call this during application startup
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static async Task<IApplicationBuilder> InitializeRagLlmSearchAsync(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();

        var ragService = scope.ServiceProvider.GetRequiredService<IRagService>();
        await ragService.InitializeAsync();

        var conversationService = scope.ServiceProvider.GetRequiredService<IConversationService>();
        await conversationService.InitializeAsync();

        return app;
    }
}
