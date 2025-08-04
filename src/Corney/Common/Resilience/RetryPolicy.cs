using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Corney.Common.Resilience;

/// <summary>
/// Retry policy configuration for different operation types
/// </summary>
public class RetryPolicyOptions
{
    public int MaxAttempts { get; set; } = 3;
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMilliseconds(100);
    public double BackoffMultiplier { get; set; } = 2.0;
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
    public bool UseJitter { get; set; } = true;
}

/// <summary>
/// Exponential backoff retry policy implementation
/// </summary>
public class RetryPolicy
{
    private readonly RetryPolicyOptions _options;
    private readonly ILogger<RetryPolicy> _logger;
    private readonly Random _random = new();

    public RetryPolicy(RetryPolicyOptions options, ILogger<RetryPolicy> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Execute operation with retry policy
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        Func<Exception, bool> shouldRetry = null,
        CancellationToken cancellationToken = default)
    {
        var attempt = 0;
        Exception lastException = null;

        while (attempt < _options.MaxAttempts)
        {
            attempt++;
            
            try
            {
                _logger.LogDebug("Executing {OperationName}, attempt {Attempt}/{MaxAttempts}", 
                    operationName, attempt, _options.MaxAttempts);
                
                var result = await operation();
                
                if (attempt > 1)
                {
                    _logger.LogInformation("Operation {OperationName} succeeded on attempt {Attempt}", 
                        operationName, attempt);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                lastException = ex;
                
                if (shouldRetry != null && !shouldRetry(ex))
                {
                    _logger.LogError(ex, "Operation {OperationName} failed with non-retryable exception on attempt {Attempt}", 
                        operationName, attempt);
                    throw;
                }

                if (attempt >= _options.MaxAttempts)
                {
                    _logger.LogError(ex, "Operation {OperationName} failed after {MaxAttempts} attempts", 
                        operationName, _options.MaxAttempts);
                    break;
                }

                var delay = CalculateDelay(attempt);
                
                _logger.LogWarning(ex, "Operation {OperationName} failed on attempt {Attempt}, retrying in {Delay}ms", 
                    operationName, attempt, delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new RetryExhaustedException(operationName, _options.MaxAttempts, lastException);
    }

    /// <summary>
    /// Execute operation with retry policy (synchronous)
    /// </summary>
    public T Execute<T>(
        Func<T> operation,
        string operationName,
        Func<Exception, bool> shouldRetry = null)
    {
        var attempt = 0;
        Exception lastException = null;

        while (attempt < _options.MaxAttempts)
        {
            attempt++;
            
            try
            {
                _logger.LogDebug("Executing {OperationName}, attempt {Attempt}/{MaxAttempts}", 
                    operationName, attempt, _options.MaxAttempts);
                
                var result = operation();
                
                if (attempt > 1)
                {
                    _logger.LogInformation("Operation {OperationName} succeeded on attempt {Attempt}", 
                        operationName, attempt);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                lastException = ex;
                
                if (shouldRetry != null && !shouldRetry(ex))
                {
                    _logger.LogError(ex, "Operation {OperationName} failed with non-retryable exception on attempt {Attempt}", 
                        operationName, attempt);
                    throw;
                }

                if (attempt >= _options.MaxAttempts)
                {
                    _logger.LogError(ex, "Operation {OperationName} failed after {MaxAttempts} attempts", 
                        operationName, _options.MaxAttempts);
                    break;
                }

                var delay = CalculateDelay(attempt);
                
                _logger.LogWarning(ex, "Operation {OperationName} failed on attempt {Attempt}, retrying in {Delay}ms", 
                    operationName, attempt, delay.TotalMilliseconds);

                Thread.Sleep(delay);
            }
        }

        throw new RetryExhaustedException(operationName, _options.MaxAttempts, lastException);
    }

    private TimeSpan CalculateDelay(int attempt)
    {
        var delay = TimeSpan.FromMilliseconds(
            _options.BaseDelay.TotalMilliseconds * Math.Pow(_options.BackoffMultiplier, attempt - 1));

        if (delay > _options.MaxDelay)
            delay = _options.MaxDelay;

        if (_options.UseJitter)
        {
            // Add ±25% jitter to prevent thundering herd
            var jitterFactor = 0.75 + (_random.NextDouble() * 0.5);
            delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * jitterFactor);
        }

        return delay;
    }
}

/// <summary>
/// Exception thrown when retry policy is exhausted
/// </summary>
public class RetryExhaustedException : Exception
{
    public string OperationName { get; }
    public int MaxAttempts { get; }

    public RetryExhaustedException(string operationName, int maxAttempts, Exception innerException)
        : base($"Operation '{operationName}' failed after {maxAttempts} attempts", innerException)
    {
        OperationName = operationName;
        MaxAttempts = maxAttempts;
    }
}