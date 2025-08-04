using Corney.Common.Logging;
using Corney.Core.Features.Cron.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Corney.Common.Resilience;

/// <summary>
/// Resilient process executor with circuit breaker and fallback mechanisms
/// </summary>
public class ResilientProcessExecutor
{
    private readonly CircuitBreaker _circuitBreaker;
    private readonly RetryPolicy _retryPolicy;
    private readonly ILogger<ResilientProcessExecutor> _logger;

    public ResilientProcessExecutor(
        CircuitBreaker circuitBreaker,
        RetryPolicy retryPolicy,
        ILogger<ResilientProcessExecutor> logger)
    {
        _circuitBreaker = circuitBreaker ?? throw new ArgumentNullException(nameof(circuitBreaker));
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Execute process with resilience and fallback mechanisms
    /// </summary>
    public async Task<ProcessExecutionResult> ExecuteAsync(
        ExecuteItem executeItem,
        CancellationToken cancellationToken = default)
    {
        var operationName = $"Process:{Path.GetFileName(executeItem.Program)}";

        try
        {
            // Try primary execution through circuit breaker
            return await _circuitBreaker.ExecuteAsync(
                async () => await ExecuteProcessWithRetry(executeItem, cancellationToken),
                operationName);
        }
        catch (CircuitBreakerOpenException)
        {
            // Circuit breaker is open, try fallback
            return await ExecuteFallback(executeItem, "CircuitBreakerOpen");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Process execution failed completely for {Program}", executeItem.Program);
            return await ExecuteFallback(executeItem, $"ExecutionFailed:{ex.GetType().Name}");
        }
    }

    /// <summary>
    /// Execute process synchronously with resilience
    /// </summary>
    public ProcessExecutionResult Execute(ExecuteItem executeItem)
    {
        var operationName = $"Process:{Path.GetFileName(executeItem.Program)}";

        try
        {
            // Try primary execution through circuit breaker
            return _circuitBreaker.Execute(
                () => ExecuteProcessWithRetrySync(executeItem),
                operationName);
        }
        catch (CircuitBreakerOpenException)
        {
            // Circuit breaker is open, try fallback
            return ExecuteFallbackSync(executeItem, "CircuitBreakerOpen");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Process execution failed completely for {Program}", executeItem.Program);
            return ExecuteFallbackSync(executeItem, $"ExecutionFailed:{ex.GetType().Name}");
        }
    }

    private async Task<ProcessExecutionResult> ExecuteProcessWithRetry(
        ExecuteItem executeItem,
        CancellationToken cancellationToken)
    {
        return await _retryPolicy.ExecuteAsync(
            async () => await ExecuteSingleProcess(executeItem, cancellationToken),
            $"ProcessExecution:{Path.GetFileName(executeItem.Program)}",
            IsRetryableProcessException,
            cancellationToken);
    }

    private ProcessExecutionResult ExecuteProcessWithRetrySync(ExecuteItem executeItem)
    {
        return _retryPolicy.Execute(
            () => ExecuteSingleProcessSync(executeItem),
            $"ProcessExecution:{Path.GetFileName(executeItem.Program)}",
            IsRetryableProcessException);
    }

    private async Task<ProcessExecutionResult> ExecuteSingleProcess(
        ExecuteItem executeItem,
        CancellationToken cancellationToken)
    {
        using var process = new Process();
        
        process.StartInfo.FileName = executeItem.Program;
        process.StartInfo.Arguments = string.Join(" ", executeItem.GetArgumentsArray());
        process.StartInfo.WorkingDirectory = executeItem.WorkingDirectory;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        var startTime = DateTime.UtcNow;
        
        _logger.LogDebug("Starting process: {Program} {Arguments} in {WorkingDirectory}",
            executeItem.Program, process.StartInfo.Arguments, executeItem.WorkingDirectory);

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process: {executeItem.Program}");
        }

        var processId = process.Id;
        _logger.LogDebug(LogMessages.ProcessStarted, LogMessages.ProcessStartedTemplate, processId, executeItem.Program);

        // Wait for process completion with timeout
        var timeout = TimeSpan.FromMinutes(5); // Default timeout
        try
        {
            await process.WaitForExitAsync(cancellationToken).WaitAsync(timeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Process {ProcessId} timed out after {Timeout}, killing process", processId, timeout);
            process.Kill();
            throw new TimeoutException($"Process execution timed out after {timeout}");
        }

        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        var result = new ProcessExecutionResult
        {
            ProcessId = processId,
            ExitCode = process.ExitCode,
            Duration = duration,
            StartTime = startTime,
            EndTime = endTime,
            StandardOutput = await process.StandardOutput.ReadToEndAsync(),
            StandardError = await process.StandardError.ReadToEndAsync(),
            Success = process.ExitCode == 0
        };

        if (result.Success)
        {
            _logger.LogInformation("Process {ProcessId} completed successfully in {Duration}ms", 
                processId, duration.TotalMilliseconds);
        }
        else
        {
            _logger.LogWarning("Process {ProcessId} failed with exit code {ExitCode}", processId, result.ExitCode);
        }

        return result;
    }

    private ProcessExecutionResult ExecuteSingleProcessSync(ExecuteItem executeItem)
    {
        using var process = new Process();
        
        process.StartInfo.FileName = executeItem.Program;
        process.StartInfo.Arguments = string.Join(" ", executeItem.GetArgumentsArray());
        process.StartInfo.WorkingDirectory = executeItem.WorkingDirectory;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        var startTime = DateTime.UtcNow;
        
        _logger.LogDebug("Starting process: {Program} {Arguments} in {WorkingDirectory}",
            executeItem.Program, process.StartInfo.Arguments, executeItem.WorkingDirectory);

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process: {executeItem.Program}");
        }

        var processId = process.Id;
        _logger.LogDebug(LogMessages.ProcessStarted, LogMessages.ProcessStartedTemplate, processId, executeItem.Program);

        // Wait for process completion with timeout
        var timeout = TimeSpan.FromMinutes(5); // Default timeout
        var completed = process.WaitForExit((int)timeout.TotalMilliseconds);

        if (!completed)
        {
            _logger.LogWarning("Process {ProcessId} timed out after {Timeout}, killing process", processId, timeout);
            process.Kill();
            throw new TimeoutException($"Process execution timed out after {timeout}");
        }

        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        var result = new ProcessExecutionResult
        {
            ProcessId = processId,
            ExitCode = process.ExitCode,
            Duration = duration,
            StartTime = startTime,
            EndTime = endTime,
            StandardOutput = process.StandardOutput.ReadToEnd(),
            StandardError = process.StandardError.ReadToEnd(),
            Success = process.ExitCode == 0
        };

        if (result.Success)
        {
            _logger.LogInformation("Process {ProcessId} completed successfully in {Duration}ms", 
                processId, duration.TotalMilliseconds);
        }
        else
        {
            _logger.LogWarning("Process {ProcessId} failed with exit code {ExitCode}", processId, result.ExitCode);
        }

        return result;
    }

    private async Task<ProcessExecutionResult> ExecuteFallback(ExecuteItem executeItem, string reason)
    {
        _logger.LogWarning(LogMessages.FallbackExecuted, LogMessages.FallbackExecutedTemplate, 
            $"Process:{Path.GetFileName(executeItem.Program)}", reason);

        // Fallback strategy: create a minimal result indicating failure
        await Task.Delay(100); // Simulate some work

        return new ProcessExecutionResult
        {
            ProcessId = 0,
            ExitCode = -1,
            Duration = TimeSpan.Zero,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow,
            StandardOutput = string.Empty,
            StandardError = $"Process execution failed due to: {reason}",
            Success = false,
            IsFallback = true,
            FallbackReason = reason
        };
    }

    private ProcessExecutionResult ExecuteFallbackSync(ExecuteItem executeItem, string reason)
    {
        _logger.LogWarning(LogMessages.FallbackExecuted, LogMessages.FallbackExecutedTemplate, 
            $"Process:{Path.GetFileName(executeItem.Program)}", reason);

        // Fallback strategy: create a minimal result indicating failure
        Thread.Sleep(100); // Simulate some work

        return new ProcessExecutionResult
        {
            ProcessId = 0,
            ExitCode = -1,
            Duration = TimeSpan.Zero,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow,
            StandardOutput = string.Empty,
            StandardError = $"Process execution failed due to: {reason}",
            Success = false,
            IsFallback = true,
            FallbackReason = reason
        };
    }

    private static bool IsRetryableProcessException(Exception ex)
    {
        return ex switch
        {
            FileNotFoundException => false,  // Don't retry if executable doesn't exist
            DirectoryNotFoundException => false,  // Don't retry if working directory doesn't exist
            UnauthorizedAccessException => false,  // Don't retry permission issues
            ArgumentException => false,  // Don't retry invalid arguments
            InvalidOperationException when ex.Message.Contains("Failed to start") => true,  // Retry start failures
            TimeoutException => false,  // Don't retry timeouts (process took too long)
            _ => true  // Retry other exceptions by default
        };
    }
}

/// <summary>
/// Result of process execution
/// </summary>
public class ProcessExecutionResult
{
    public int ProcessId { get; set; }
    public int ExitCode { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public bool Success { get; set; }
    public bool IsFallback { get; set; }
    public string FallbackReason { get; set; } = string.Empty;
}