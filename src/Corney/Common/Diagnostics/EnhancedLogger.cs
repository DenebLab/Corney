using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Corney.Common.Diagnostics;

/// <summary>
/// Enhanced logger that automatically includes correlation context in log messages.
/// Provides developer-friendly logging with automatic context propagation.
/// </summary>
public class EnhancedLogger<T> : ILogger<T>
{
    private readonly ILogger<T> _innerLogger;

    public EnhancedLogger(ILogger<T> innerLogger)
    {
        _innerLogger = innerLogger ?? throw new ArgumentNullException(nameof(innerLogger));
    }

    public IDisposable BeginScope<TState>(TState state) => _innerLogger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => _innerLogger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        // Enhance the state with correlation context
        var enhancedState = EnhanceWithCorrelationContext(state, exception);
        _innerLogger.Log(logLevel, eventId, enhancedState, exception, (s, ex) => s.ToString());
    }

    /// <summary>
    /// Log a debug message with automatic correlation context.
    /// </summary>
    /// <param name="message">The log message template</param>
    /// <param name="args">Message template arguments</param>
    /// <param name="memberName">Automatically populated with calling member name</param>
    /// <param name="sourceFilePath">Automatically populated with source file path</param>
    /// <param name="sourceLineNumber">Automatically populated with source line number</param>
    public void LogDebugWithContext(
        string message,
        object[] args = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        if (!IsEnabled(LogLevel.Debug))
            return;

        var enhancedMessage = FormatWithContext(message, memberName, sourceFilePath, sourceLineNumber);
        _innerLogger.LogDebug(enhancedMessage, args ?? Array.Empty<object>());
    }

    /// <summary>
    /// Log an information message with automatic correlation context.
    /// </summary>
    /// <param name="message">The log message template</param>
    /// <param name="args">Message template arguments</param>
    /// <param name="memberName">Automatically populated with calling member name</param>
    /// <param name="sourceFilePath">Automatically populated with source file path</param>
    /// <param name="sourceLineNumber">Automatically populated with source line number</param>
    public void LogInformationWithContext(
        string message,
        object[] args = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        if (!IsEnabled(LogLevel.Information))
            return;

        var enhancedMessage = FormatWithContext(message, memberName, sourceFilePath, sourceLineNumber);
        _innerLogger.LogInformation(enhancedMessage, args ?? Array.Empty<object>());
    }

    /// <summary>
    /// Log a warning message with automatic correlation context.
    /// </summary>
    /// <param name="exception">The exception (optional)</param>
    /// <param name="message">The log message template</param>
    /// <param name="args">Message template arguments</param>
    /// <param name="memberName">Automatically populated with calling member name</param>
    /// <param name="sourceFilePath">Automatically populated with source file path</param>
    /// <param name="sourceLineNumber">Automatically populated with source line number</param>
    public void LogWarningWithContext(
        Exception exception,
        string message,
        object[] args = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        if (!IsEnabled(LogLevel.Warning))
            return;

        var enhancedMessage = FormatWithContext(message, memberName, sourceFilePath, sourceLineNumber);
        _innerLogger.LogWarning(exception, enhancedMessage, args ?? Array.Empty<object>());
    }

    /// <summary>
    /// Log a warning message with automatic correlation context.
    /// </summary>
    /// <param name="message">The log message template</param>
    /// <param name="args">Message template arguments</param>
    /// <param name="memberName">Automatically populated with calling member name</param>
    /// <param name="sourceFilePath">Automatically populated with source file path</param>
    /// <param name="sourceLineNumber">Automatically populated with source line number</param>
    public void LogWarningWithContext(
        string message,
        object[] args = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        LogWarningWithContext(null, message, args, memberName, sourceFilePath, sourceLineNumber);
    }

    /// <summary>
    /// Log an error message with automatic correlation context.
    /// </summary>
    /// <param name="exception">The exception</param>
    /// <param name="message">The log message template</param>
    /// <param name="args">Message template arguments</param>
    /// <param name="memberName">Automatically populated with calling member name</param>
    /// <param name="sourceFilePath">Automatically populated with source file path</param>
    /// <param name="sourceLineNumber">Automatically populated with source line number</param>
    public void LogErrorWithContext(
        Exception exception,
        string message,
        object[] args = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        if (!IsEnabled(LogLevel.Error))
            return;

        var enhancedMessage = FormatWithContext(message, memberName, sourceFilePath, sourceLineNumber);
        _innerLogger.LogError(exception, enhancedMessage, args ?? Array.Empty<object>());
    }

    /// <summary>
    /// Log a critical message with automatic correlation context.
    /// </summary>
    /// <param name="exception">The exception</param>
    /// <param name="message">The log message template</param>
    /// <param name="args">Message template arguments</param>
    /// <param name="memberName">Automatically populated with calling member name</param>
    /// <param name="sourceFilePath">Automatically populated with source file path</param>
    /// <param name="sourceLineNumber">Automatically populated with source line number</param>
    public void LogCriticalWithContext(
        Exception exception,
        string message,
        object[] args = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        if (!IsEnabled(LogLevel.Critical))
            return;

        var enhancedMessage = FormatWithContext(message, memberName, sourceFilePath, sourceLineNumber);
        _innerLogger.LogCritical(exception, enhancedMessage, args ?? Array.Empty<object>());
    }

    private string FormatWithContext(
        string message,
        string memberName,
        string sourceFilePath,
        int sourceLineNumber)
    {
        var contextInfo = new List<string>();

        // Add correlation context if available
        if (CorrelationContext.HasActiveContext())
        {
            contextInfo.Add($"CorrelationId: {CorrelationContext.CorrelationId}");
            if (!string.IsNullOrEmpty(CorrelationContext.OperationName))
            {
                contextInfo.Add($"Operation: {CorrelationContext.OperationName}");
            }
            var elapsed = CorrelationContext.GetElapsedTime();
            if (elapsed > TimeSpan.Zero)
            {
                contextInfo.Add($"Elapsed: {elapsed.TotalMilliseconds:F1}ms");
            }
        }

        // Add caller information for debugging
        var fileName = System.IO.Path.GetFileNameWithoutExtension(sourceFilePath);
        contextInfo.Add($"Source: {fileName}.{memberName}:{sourceLineNumber}");

        var contextString = string.Join(" | ", contextInfo);
        return $"[{contextString}] {message}";
    }

    private string EnhanceWithCorrelationContext<TState>(TState state, Exception exception)
    {
        var baseMessage = state?.ToString() ?? string.Empty;

        if (!CorrelationContext.HasActiveContext())
            return baseMessage;

        var correlationInfo = CorrelationContext.GetContextSummary();
        return $"{correlationInfo} {baseMessage}";
    }
}

/// <summary>
/// Extensions for enhanced logging functionality.
/// </summary>
public static class EnhancedLoggingExtensions
{
    /// <summary>
    /// Wraps an existing logger with enhanced correlation context functionality.
    /// </summary>
    /// <typeparam name="T">The logger category type</typeparam>
    /// <param name="logger">The logger to enhance</param>
    /// <returns>An enhanced logger with correlation context support</returns>
    public static EnhancedLogger<T> WithCorrelationContext<T>(this ILogger<T> logger)
    {
        return new EnhancedLogger<T>(logger);
    }

    /// <summary>
    /// Logs the start of an operation with correlation context.
    /// </summary>
    /// <param name="logger">The logger instance</param>
    /// <param name="operationName">The name of the operation</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <returns>A disposable context that logs completion when disposed</returns>
    public static IDisposable LogOperationStart<T>(this ILogger<T> logger, string operationName, string correlationId = null)
    {
        return new OperationLogger<T>(logger, operationName, correlationId);
    }

    private class OperationLogger<T> : IDisposable
    {
        private readonly ILogger<T> _logger;
        private readonly IDisposable _context;
        private readonly DateTime _startTime;

        public OperationLogger(ILogger<T> logger, string operationName, string correlationId)
        {
            _logger = logger;
            _startTime = DateTime.UtcNow;
            _context = CorrelationContext.StartOperation(operationName, correlationId);
            
            _logger.LogDebug("Operation started: {OperationName} [{CorrelationId}]", 
                operationName, CorrelationContext.CorrelationId);
        }

        public void Dispose()
        {
            var elapsed = DateTime.UtcNow - _startTime;
            _logger.LogDebug("Operation completed: {OperationName} [{CorrelationId}] in {ElapsedMs}ms",
                CorrelationContext.OperationName, CorrelationContext.CorrelationId, elapsed.TotalMilliseconds);
            
            _context?.Dispose();
        }
    }
}