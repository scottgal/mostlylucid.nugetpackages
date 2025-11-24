using Mostlylucid.LlmPiiRedactor.Services;
using Serilog.Core;
using Serilog.Events;

namespace Mostlylucid.LlmPiiRedactor.Serilog;

/// <summary>
///     Serilog enricher that adds PII detection information to log events.
/// </summary>
public class PiiRedactingEnricher : ILogEventEnricher
{
    private readonly IPiiRedactionService _redactionService;

    public PiiRedactingEnricher(IPiiRedactionService redactionService)
    {
        _redactionService = redactionService;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // Check if the rendered message contains PII
        var message = logEvent.RenderMessage();
        var containsPii = _redactionService.ContainsPii(message);

        var piiProperty = propertyFactory.CreateProperty("ContainsPii", containsPii);
        logEvent.AddPropertyIfAbsent(piiProperty);
    }
}