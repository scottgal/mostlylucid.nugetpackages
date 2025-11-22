using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmPiiRedactor.Models;
using Mostlylucid.LlmPiiRedactor.Services;

namespace Mostlylucid.LlmPiiRedactor.Formatters;

/// <summary>
/// Logger provider that wraps other loggers to redact PII from log messages.
/// </summary>
public class RedactingLoggerProvider : ILoggerProvider
{
    private readonly ILoggerFactory _innerFactory;
    private readonly IPiiRedactionService _redactionService;
    private readonly PiiLoggingOptions _options;

    public RedactingLoggerProvider(
        ILoggerFactory innerFactory,
        IPiiRedactionService redactionService,
        IOptions<PiiLoggingOptions> options)
    {
        _innerFactory = innerFactory;
        _redactionService = redactionService;
        _options = options.Value;
    }

    public ILogger CreateLogger(string categoryName)
    {
        var innerLogger = _innerFactory.CreateLogger(categoryName);
        return new RedactingLogger(innerLogger, _redactionService, _options, categoryName);
    }

    public void Dispose()
    {
        // Inner factory is disposed by DI container
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Logger that redacts PII from log messages before forwarding to inner logger.
/// </summary>
public class RedactingLogger : ILogger
{
    private readonly ILogger _innerLogger;
    private readonly IPiiRedactionService _redactionService;
    private readonly PiiLoggingOptions _options;
    private readonly string _categoryName;

    public RedactingLogger(
        ILogger innerLogger,
        IPiiRedactionService redactionService,
        PiiLoggingOptions options,
        string categoryName)
    {
        _innerLogger = innerLogger;
        _redactionService = redactionService;
        _options = options;
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _innerLogger.BeginScope(state);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _innerLogger.IsEnabled(logLevel);
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        // Check if we should skip redaction for this category/level
        if (!ShouldRedact(logLevel))
        {
            _innerLogger.Log(logLevel, eventId, state, exception, formatter);
            return;
        }

        // Format the original message
        var originalMessage = formatter(state, exception);

        // Redact the message
        var redactedMessage = _redactionService.Redact(originalMessage).RedactedText;

        // Redact exception if configured
        var redactedException = exception;
        if (_options.RedactExceptions && exception != null)
        {
            redactedException = RedactException(exception);
        }

        // Log with redacted content
        _innerLogger.Log(
            logLevel,
            eventId,
            redactedMessage,
            redactedException,
            (msg, ex) => msg?.ToString() ?? string.Empty);
    }

    private bool ShouldRedact(LogLevel logLevel)
    {
        if (!_options.Enabled)
            return false;

        if (logLevel < _options.MinLogLevel)
            return false;

        if (_options.ExcludedCategories.Any(c =>
            _categoryName.StartsWith(c, StringComparison.OrdinalIgnoreCase)))
            return false;

        return true;
    }

    private Exception RedactException(Exception ex)
    {
        var redactedMessage = _redactionService.Redact(ex.Message).RedactedText;

        string? redactedStackTrace = null;
        if (_options.RedactStackTraces && ex.StackTrace != null)
        {
            redactedStackTrace = _redactionService.Redact(ex.StackTrace).RedactedText;
        }

        // Create a wrapper exception with redacted content
        return new RedactedException(redactedMessage, redactedStackTrace, ex.GetType().Name);
    }
}

/// <summary>
/// Exception wrapper that contains redacted content.
/// </summary>
public class RedactedException : Exception
{
    public string OriginalExceptionType { get; }
    public string? RedactedStackTrace { get; }

    public RedactedException(string message, string? redactedStackTrace, string originalType)
        : base(message)
    {
        OriginalExceptionType = originalType;
        RedactedStackTrace = redactedStackTrace;
    }

    public override string? StackTrace => RedactedStackTrace ?? base.StackTrace;

    public override string ToString()
    {
        return $"{OriginalExceptionType}: {Message}{(RedactedStackTrace != null ? $"\n{RedactedStackTrace}" : "")}";
    }
}
