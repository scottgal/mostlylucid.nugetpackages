using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmPiiRedactor.Models;
using Mostlylucid.LlmPiiRedactor.Services;

namespace Mostlylucid.LlmPiiRedactor.Filters;

/// <summary>
///     Exception filter that redacts PII from error responses and exception details.
/// </summary>
public class PiiExceptionFilter : IExceptionFilter
{
    private readonly ILogger<PiiExceptionFilter> _logger;
    private readonly PiiLoggingOptions _options;
    private readonly IPiiRedactionService _redactionService;

    public PiiExceptionFilter(
        IPiiRedactionService redactionService,
        IOptions<PiiLoggingOptions> options,
        ILogger<PiiExceptionFilter> logger)
    {
        _redactionService = redactionService;
        _options = options.Value;
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        if (!_options.Enabled || !_options.RedactExceptions)
            return;

        var exception = context.Exception;
        var redactedMessage = _redactionService.Redact(exception.Message).RedactedText;

        string? redactedStackTrace = null;
        if (_options.RedactStackTraces && exception.StackTrace != null)
            redactedStackTrace = _redactionService.Redact(exception.StackTrace).RedactedText;

        _logger.LogError("Exception occurred: {Message}", redactedMessage);

        // Create a ProblemDetails response with redacted information
        var problemDetails = new ProblemDetails
        {
            Status = 500,
            Title = "An error occurred",
            Detail = redactedMessage,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1"
        };

        // Add redacted stack trace in development (handled by caller)
        if (redactedStackTrace != null) problemDetails.Extensions["stackTrace"] = redactedStackTrace;

        context.Result = new ObjectResult(problemDetails)
        {
            StatusCode = 500
        };

        context.ExceptionHandled = true;
    }
}

/// <summary>
///     Attribute to enable PII redaction on exception responses for a controller or action.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RedactPiiExceptionsAttribute : TypeFilterAttribute
{
    public RedactPiiExceptionsAttribute() : base(typeof(PiiExceptionFilter))
    {
    }
}