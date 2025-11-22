using Microsoft.Extensions.Options;
using Mostlylucid.LlmPiiRedactor.Models;
using Mostlylucid.LlmPiiRedactor.Services;
using Serilog.Core;
using Serilog.Events;

namespace Mostlylucid.LlmPiiRedactor.Serilog;

/// <summary>
/// Serilog destructuring policy that redacts PII from structured log properties.
/// </summary>
public class PiiDestructuringPolicy : IDestructuringPolicy
{
    private readonly IPiiRedactionService _redactionService;
    private readonly PiiLoggingOptions _options;

    public PiiDestructuringPolicy(
        IPiiRedactionService redactionService,
        IOptions<PiiLoggingOptions> options)
    {
        _redactionService = redactionService;
        _options = options.Value;
    }

    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue? result)
    {
        result = null;

        if (!_options.Enabled || !_options.RedactStructuredProperties)
            return false;

        if (value is string stringValue)
        {
            var redacted = _redactionService.Redact(stringValue);
            if (redacted.ContainedPii)
            {
                result = new ScalarValue(redacted.RedactedText);
                return true;
            }
        }

        return false;
    }
}
