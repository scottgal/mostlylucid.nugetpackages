using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.ArchiveOrg.Config;
using Mostlylucid.ArchiveOrg.Services;
using Polly;
using Polly.Extensions.Http;

namespace Mostlylucid.ArchiveOrg;

public static class ServiceExtensions
{
    /// <summary>
    /// Add all Archive.org downloader services
    /// </summary>
    public static IServiceCollection AddArchiveOrgServices(
        this IServiceCollection services,
        IConfiguration configuration,
        string? basePath = null)
    {
        // Use current directory if no base path specified
        basePath ??= Directory.GetCurrentDirectory();

        // Register configuration with resolved paths
        services.Configure<ArchiveOrgOptions>(options =>
        {
            configuration.GetSection(ArchiveOrgOptions.SectionName).Bind(options);
            // Resolve relative paths
            if (!Path.IsPathRooted(options.OutputDirectory))
            {
                options.OutputDirectory = Path.GetFullPath(Path.Combine(basePath, options.OutputDirectory));
            }
        });

        services.Configure<MarkdownConversionOptions>(options =>
        {
            configuration.GetSection(MarkdownConversionOptions.SectionName).Bind(options);
            // Resolve relative paths
            if (!Path.IsPathRooted(options.InputDirectory))
            {
                options.InputDirectory = Path.GetFullPath(Path.Combine(basePath, options.InputDirectory));
            }
            if (!Path.IsPathRooted(options.OutputDirectory))
            {
                options.OutputDirectory = Path.GetFullPath(Path.Combine(basePath, options.OutputDirectory));
            }
        });

        services.Configure<OllamaOptions>(configuration.GetSection(OllamaOptions.SectionName));

        // HTTP clients with retry policies
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

        services.AddHttpClient<ICdxApiClient, CdxApiClient>()
            .AddPolicyHandler(retryPolicy)
            .ConfigureHttpClient(client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent",
                    "Mostlylucid-ArchiveOrg-Downloader/1.0 (https://github.com/scottgal/mostlylucidweb)");
            });

        services.AddHttpClient<IArchiveDownloader, ArchiveDownloader>()
            .AddPolicyHandler(retryPolicy)
            .ConfigureHttpClient(client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent",
                    "Mostlylucid-ArchiveOrg-Downloader/1.0 (https://github.com/scottgal/mostlylucidweb)");
            });

        services.AddHttpClient<IOllamaTagGenerator, OllamaTagGenerator>();

        services.AddHttpClient<IHtmlToMarkdownConverter, HtmlToMarkdownConverter>()
            .AddPolicyHandler(retryPolicy);

        return services;
    }
}
