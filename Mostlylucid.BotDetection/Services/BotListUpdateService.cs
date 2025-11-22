using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mostlylucid.BotDetection.Data;

namespace Mostlylucid.BotDetection.Services;

/// <summary>
///     Background service that automatically updates bot detection lists daily
/// </summary>
public class BotListUpdateService(
    IBotListDatabase database,
    ILogger<BotListUpdateService> logger)
    : BackgroundService
{
    private readonly IBotListDatabase _database = database;
    private readonly ILogger<BotListUpdateService> _logger = logger;
    private readonly TimeSpan _updateInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Bot list update service started");

        // Initialize database on startup
        try
        {
            await _database.InitializeAsync(stoppingToken);
            _logger.LogInformation("Bot detection database initialized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize bot detection database");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Check if update is needed
                var lastUpdate = await _database.GetLastUpdateTimeAsync("bot_patterns", stoppingToken);

                if (lastUpdate == null || DateTime.UtcNow - lastUpdate.Value >= _updateInterval)
                {
                    _logger.LogInformation("Starting bot list update");
                    await _database.UpdateListsAsync(stoppingToken);
                    _logger.LogInformation("Bot list update completed");
                }
                else
                {
                    var nextUpdate = lastUpdate.Value.Add(_updateInterval) - DateTime.UtcNow;
                    _logger.LogDebug("Bot lists are up to date. Next update in {Hours:F1} hours",
                        nextUpdate.TotalHours);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bot list update");
            }

            // Wait for next check (check every hour, update every 24 hours)
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }

        _logger.LogInformation("Bot list update service stopped");
    }
}