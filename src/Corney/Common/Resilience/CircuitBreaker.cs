using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Corney.Common.Resilience;

/// <summary>
/// Circuit breaker configuration options
/// </summary>
public class CircuitBreakerOptions
{
    public int FailureThreshold { get; set; } = 5;
    public TimeSpan OpenDuration { get; set; } = TimeSpan.FromMinutes(1);
    public int HalfOpenMaxAttempts { get; set; } = 3;
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromMinutes(2);
}

/// <summary>
/// Circuit breaker states
/// </summary>
public enum CircuitBreakerState
{
    Closed,   // Normal operation
    Open,     // Failing fast
    HalfOpen  // Testing if service has recovered
}

/// <summary>
/// Circuit breaker implementation for process execution resilience
/// </summary>
public class CircuitBreaker
{
    private readonly CircuitBreakerOptions _options;
    private readonly ILogger<CircuitBreaker> _logger;
    private readonly object _lock = new();
    
    private CircuitBreakerState _state = CircuitBreakerState.Closed;
    private int _failureCount = 0;
    private int _halfOpenAttempts = 0;
    private DateTime _lastFailureTime = DateTime.MinValue;
    private DateTime _openedTime = DateTime.MinValue;

    public CircuitBreakerState State => _state;
    public int FailureCount => _failureCount;

    public CircuitBreaker(CircuitBreakerOptions options, ILogger<CircuitBreaker> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Execute operation through circuit breaker
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, string operationName)
    {
        if (!CanExecute())
        {
            _logger.LogWarning("Circuit breaker is OPEN for {OperationName}, failing fast", operationName);
            throw new CircuitBreakerOpenException(operationName, _state);
        }

        try
        {
            var result = await operation();
            OnSuccess(operationName);
            return result;
        }
        catch (Exception ex)
        {
            OnFailure(operationName, ex);
            throw;
        }
    }

    /// <summary>
    /// Execute operation through circuit breaker (synchronous)
    /// </summary>
    public T Execute<T>(Func<T> operation, string operationName)
    {
        if (!CanExecute())
        {
            _logger.LogWarning("Circuit breaker is OPEN for {OperationName}, failing fast", operationName);
            throw new CircuitBreakerOpenException(operationName, _state);
        }

        try
        {
            var result = operation();
            OnSuccess(operationName);
            return result;
        }
        catch (Exception ex)
        {
            OnFailure(operationName, ex);
            throw;
        }
    }

    private bool CanExecute()
    {
        lock (_lock)
        {
            switch (_state)
            {
                case CircuitBreakerState.Closed:
                    return true;

                case CircuitBreakerState.Open:
                    if (DateTime.UtcNow - _openedTime >= _options.OpenDuration)
                    {
                        _state = CircuitBreakerState.HalfOpen;
                        _halfOpenAttempts = 0;
                        _logger.LogInformation("Circuit breaker transitioning to HALF-OPEN state");
                        return true;
                    }
                    return false;

                case CircuitBreakerState.HalfOpen:
                    return _halfOpenAttempts < _options.HalfOpenMaxAttempts;

                default:
                    return false;
            }
        }
    }

    private void OnSuccess(string operationName)
    {
        lock (_lock)
        {
            switch (_state)
            {
                case CircuitBreakerState.Closed:
                    // Reset failure count on success
                    if (_failureCount > 0)
                    {
                        _logger.LogDebug("Circuit breaker: Operation {OperationName} succeeded, resetting failure count", operationName);
                        _failureCount = 0;
                    }
                    break;

                case CircuitBreakerState.HalfOpen:
                    _halfOpenAttempts++;
                    if (_halfOpenAttempts >= _options.HalfOpenMaxAttempts)
                    {
                        _state = CircuitBreakerState.Closed;
                        _failureCount = 0;
                        _logger.LogInformation("Circuit breaker: Operation {OperationName} recovered, transitioning to CLOSED state", operationName);
                    }
                    break;
            }
        }
    }

    private void OnFailure(string operationName, Exception ex)
    {
        lock (_lock)
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

            _logger.LogWarning(ex, "Circuit breaker: Operation {OperationName} failed, failure count: {FailureCount}", 
                operationName, _failureCount);

            switch (_state)
            {
                case CircuitBreakerState.Closed:
                    if (_failureCount >= _options.FailureThreshold)
                    {
                        _state = CircuitBreakerState.Open;
                        _openedTime = DateTime.UtcNow;
                        _logger.LogError("Circuit breaker: Threshold reached for {OperationName}, transitioning to OPEN state for {Duration}", 
                            operationName, _options.OpenDuration);
                    }
                    break;

                case CircuitBreakerState.HalfOpen:
                    _state = CircuitBreakerState.Open;
                    _openedTime = DateTime.UtcNow;
                    _logger.LogWarning("Circuit breaker: Operation {OperationName} failed in HALF-OPEN state, returning to OPEN state", operationName);
                    break;
            }
        }
    }

    /// <summary>
    /// Manually reset circuit breaker to closed state
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _state = CircuitBreakerState.Closed;
            _failureCount = 0;
            _halfOpenAttempts = 0;
            _logger.LogInformation("Circuit breaker manually reset to CLOSED state");
        }
    }

    /// <summary>
    /// Get current circuit breaker status
    /// </summary>
    public CircuitBreakerStatus GetStatus()
    {
        lock (_lock)
        {
            return new CircuitBreakerStatus
            {
                State = _state,
                FailureCount = _failureCount,
                LastFailureTime = _lastFailureTime,
                OpenedTime = _openedTime,
                HalfOpenAttempts = _halfOpenAttempts
            };
        }
    }
}

/// <summary>
/// Circuit breaker status information
/// </summary>
public class CircuitBreakerStatus
{
    public CircuitBreakerState State { get; set; }
    public int FailureCount { get; set; }
    public DateTime LastFailureTime { get; set; }
    public DateTime OpenedTime { get; set; }
    public int HalfOpenAttempts { get; set; }
}

/// <summary>
/// Exception thrown when circuit breaker is open
/// </summary>
public class CircuitBreakerOpenException : Exception
{
    public string OperationName { get; }
    public CircuitBreakerState State { get; }

    public CircuitBreakerOpenException(string operationName, CircuitBreakerState state)
        : base($"Circuit breaker is {state} for operation '{operationName}'")
    {
        OperationName = operationName;
        State = state;
    }
}