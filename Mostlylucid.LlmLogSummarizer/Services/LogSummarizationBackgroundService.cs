using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmLogSummarizer.Models;

namespace Mostlylucid.LlmLogSummarizer.Services;

/// <summary>
///     Background service that runs periodic log summarization.
/// </summary>
public class LogSummarizationBackgroundService : BackgroundService
{
    private readonly ILogger<LogSummarizationBackgroundService> _logger;
    private readonly LogSummarizerOptions _options;
    private readonly ILogSummarizationOrchestrator _orchestrator;

    public LogSummarizationBackgroundService(
        ILogSummarizationOrchestrator orchestrator,
        IOptions<LogSummarizerOptions> options,
        ILogger<LogSummarizationBackgroundService> logger)
    {
        _orchestrator = orchestrator;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Log summarization service is disabled");
            return;
        }

        _logger.LogInformation(
            "Log summarization service started. Interval: {Interval}, DailyRunTime: {DailyRunTime}",
            _options.SummarizationInterval,
            _options.DailyRunTime?.ToString(@"hh\:mm") ?? "not set");

        // Run on startup if configured
        if (_options.RunOnStartup) await RunSummarizationSafelyAsync(stoppingToken);

        // Main loop
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = CalculateNextRunDelay();

            _logger.LogDebug("Next summarization run in {Delay}", delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            await RunSummarizationSafelyAsync(stoppingToken);
        }

        _logger.LogInformation("Log summarization service stopped");
    }

    private async Task RunSummarizationSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting scheduled log summarization");
            var report = await _orchestrator.RunSummarizationAsync(cancellationToken);

            _logger.LogInformation(
                "Scheduled summarization completed: {Health} status, {Errors} errors in {Duration:F2}s",
                report.OverallHealth,
                report.ErrorCount,
                report.ProcessingStats.TotalDuration.TotalSeconds);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Summarization was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled log summarization failed");
        }
    }

    private TimeSpan CalculateNextRunDelay()
    {
        // If a specific daily run time is configured
        if (_options.DailyRunTime.HasValue)
        {
            var now = DateTime.Now;
            var targetTime = now.Date.Add(_options.DailyRunTime.Value);

            // If we've passed today's target time, schedule for tomorrow
            if (now.TimeOfDay > _options.DailyRunTime.Value) targetTime = targetTime.AddDays(1);

            return targetTime - now;
        }

        // Otherwise use the interval
        return _options.SummarizationInterval;
    }
}