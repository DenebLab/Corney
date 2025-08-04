using System;
using System.Threading;

namespace Corney.Common.Diagnostics;

/// <summary>
/// Provides correlation context for tracing operations across the application.
/// This enables developers to correlate related log entries and debug complex workflows.
/// </summary>
public static class CorrelationContext
{
    private static readonly AsyncLocal<string> _correlationId = new();
    private static readonly AsyncLocal<string> _operationName = new();
    private static readonly AsyncLocal<DateTime> _operationStartTime = new();

    /// <summary>
    /// Gets or sets the correlation ID for the current operation context.
    /// This ID is automatically propagated through async operations.
    /// </summary>
    public static string CorrelationId
    {
        get => _correlationId.Value ?? string.Empty;
        set => _correlationId.Value = value;
    }

    /// <summary>
    /// Gets or sets the name of the current operation for debugging purposes.
    /// </summary>
    public static string OperationName
    {
        get => _operationName.Value ?? string.Empty;
        set => _operationName.Value = value;
    }

    /// <summary>
    /// Gets or sets the start time of the current operation.
    /// </summary>
    public static DateTime OperationStartTime
    {
        get => _operationStartTime.Value;
        set => _operationStartTime.Value = value;
    }

    /// <summary>
    /// Creates a new correlation ID for the current context.
    /// </summary>
    /// <returns>The newly generated correlation ID</returns>
    public static string NewCorrelationId()
    {
        var correlationId = Guid.NewGuid().ToString("N")[..8];
        CorrelationId = correlationId;
        return correlationId;
    }

    /// <summary>
    /// Starts a new operation context with correlation tracking.
    /// </summary>
    /// <param name="operationName">The name of the operation</param>
    /// <param name="correlationId">Optional correlation ID (generates new if not provided)</param>
    /// <returns>A disposable context that restores the previous correlation state</returns>
    public static IDisposable StartOperation(string operationName, string correlationId = null)
    {
        return new OperationContext(operationName, correlationId);
    }

    /// <summary>
    /// Gets the elapsed time since the current operation started.
    /// </summary>
    /// <returns>The elapsed time, or TimeSpan.Zero if no operation is active</returns>
    public static TimeSpan GetElapsedTime()
    {
        var startTime = OperationStartTime;
        return startTime == default ? TimeSpan.Zero : DateTime.UtcNow - startTime;
    }

    /// <summary>
    /// Checks if there is an active correlation context.
    /// </summary>
    /// <returns>True if there is an active correlation context</returns>
    public static bool HasActiveContext()
    {
        return !string.IsNullOrEmpty(CorrelationId);
    }

    /// <summary>
    /// Clears the current correlation context.
    /// </summary>
    public static void Clear()
    {
        _correlationId.Value = null;
        _operationName.Value = null;
        _operationStartTime.Value = default;
    }

    /// <summary>
    /// Gets a summary of the current correlation context for logging.
    /// </summary>
    /// <returns>A formatted string with correlation information</returns>
    public static string GetContextSummary()
    {
        if (!HasActiveContext())
            return "No active correlation context";

        var elapsed = GetElapsedTime();
        return $"[{CorrelationId}] {OperationName} (elapsed: {elapsed.TotalMilliseconds:F1}ms)";
    }

    /// <summary>
    /// Disposable context for managing correlation state.
    /// </summary>
    private class OperationContext : IDisposable
    {
        private readonly string _previousCorrelationId;
        private readonly string _previousOperationName;
        private readonly DateTime _previousStartTime;

        public OperationContext(string operationName, string correlationId = null)
        {
            // Save previous context
            _previousCorrelationId = CorrelationId;
            _previousOperationName = OperationName;
            _previousStartTime = OperationStartTime;

            // Set new context
            CorrelationId = correlationId ?? NewCorrelationId();
            OperationName = operationName;
            OperationStartTime = DateTime.UtcNow;
        }

        public void Dispose()
        {
            // Restore previous context
            _correlationId.Value = _previousCorrelationId;
            _operationName.Value = _previousOperationName;
            _operationStartTime.Value = _previousStartTime;
        }
    }
}

/// <summary>
/// Extension methods for easier correlation context usage.
/// </summary>
public static class CorrelationContextExtensions
{
    /// <summary>
    /// Executes an action within a correlation context.
    /// </summary>
    /// <param name="action">The action to execute</param>
    /// <param name="operationName">The name of the operation</param>
    /// <param name="correlationId">Optional correlation ID</param>
    public static void WithCorrelationContext(this Action action, string operationName, string correlationId = null)
    {
        using (CorrelationContext.StartOperation(operationName, correlationId))
        {
            action();
        }
    }

    /// <summary>
    /// Executes a function within a correlation context.
    /// </summary>
    /// <typeparam name="T">The return type</typeparam>
    /// <param name="func">The function to execute</param>
    /// <param name="operationName">The name of the operation</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <returns>The result of the function</returns>
    public static T WithCorrelationContext<T>(this Func<T> func, string operationName, string correlationId = null)
    {
        using (CorrelationContext.StartOperation(operationName, correlationId))
        {
            return func();
        }
    }
}