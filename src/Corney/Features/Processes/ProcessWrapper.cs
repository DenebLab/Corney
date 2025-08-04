using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Corney.Common.Logging;
using Corney.Common.Performance;
using Corney.Common.Resilience;
using Corney.Core.Features.Cron.Models;
using Deneblab.Common.Logging;
using Microsoft.Extensions.Logging;

namespace Corney.Features.Processes;

public class ProcessWrapper
{
    private readonly ILogger<ProcessWrapper> _log;
    private readonly ResilientProcessExecutor _resilientExecutor;
    private readonly PerformanceMonitor _performanceMonitor;

    public ProcessWrapper()
    {
    }

    public ProcessWrapper(ILogger<ProcessWrapper> log, PerformanceMonitor performanceMonitor = null)
    {
        _log = log;
        _performanceMonitor = performanceMonitor;
        
        // Create resilient process executor with circuit breaker and retry policies
        var circuitBreakerOptions = new CircuitBreakerOptions
        {
            FailureThreshold = 3,
            OpenDuration = TimeSpan.FromMinutes(2),
            HalfOpenMaxAttempts = 2
        };

        var retryPolicyOptions = new RetryPolicyOptions
        {
            MaxAttempts = 3,
            BaseDelay = TimeSpan.FromSeconds(1),
            BackoffMultiplier = 2.0,
            MaxDelay = TimeSpan.FromSeconds(10),
            UseJitter = true
        };

        // Using NullLoggerFactory since we don't have access to the factory
        var loggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
        
        var circuitBreaker = new CircuitBreaker(circuitBreakerOptions, 
            loggerFactory.CreateLogger<CircuitBreaker>());
        var retryPolicy = new RetryPolicy(retryPolicyOptions,
            loggerFactory.CreateLogger<RetryPolicy>());

        _resilientExecutor = new ResilientProcessExecutor(circuitBreaker, retryPolicy,
            loggerFactory.CreateLogger<ResilientProcessExecutor>());
    }

    public void Start(ExecuteItem item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        if (string.IsNullOrWhiteSpace(item.Program))
            throw new ArgumentException("Program cannot be null or empty", nameof(item));

        // Prepare ExecuteItem with working directory if not set
        var executeItem = PrepareExecuteItem(item);
        
        var operationName = $"{MetricCategories.ProcessExecution}.{Path.GetFileNameWithoutExtension(executeItem.Program)}";

        try
        {
            if (_resilientExecutor != null)
            {
                // Use resilient executor with circuit breaker and retry
                ProcessExecutionResult result;
                
                if (_performanceMonitor != null)
                {
                    result = _performanceMonitor.RecordExecution(operationName, () => _resilientExecutor.Execute(executeItem));
                }
                else
                {
                    result = _resilientExecutor.Execute(executeItem);
                }
                
                if (result.IsFallback)
                {
                    _log.LogWarning("Process execution used fallback: {FallbackReason}", result.FallbackReason);
                }
                else if (!result.Success)
                {
                    _log.LogWarning("Process {ProcessId} failed with exit code {ExitCode}", result.ProcessId, result.ExitCode);
                }
                
                // Record additional performance metrics
                _performanceMonitor?.RecordMetric(new PerformanceMetrics
                {
                    OperationName = $"{operationName}.detailed",
                    Duration = result.Duration,
                    Success = result.Success && !result.IsFallback,
                    AdditionalData = new()
                    {
                        ["ProcessId"] = result.ProcessId,
                        ["ExitCode"] = result.ExitCode,
                        ["IsFallback"] = result.IsFallback,
                        ["Program"] = executeItem.Program
                    }
                });
            }
            else
            {
                // Fallback to legacy execution if resilient executor not available
                if (_performanceMonitor != null)
                {
                    _performanceMonitor.RecordExecution(operationName, () => { ExecuteLegacy(executeItem); return true; });
                }
                else
                {
                    ExecuteLegacy(executeItem);
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to execute process {Program}", item.Program);
            throw;
        }
    }

    /// <summary>
    /// Async version of Start method
    /// </summary>
    public async Task StartAsync(ExecuteItem item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        if (string.IsNullOrWhiteSpace(item.Program))
            throw new ArgumentException("Program cannot be null or empty", nameof(item));

        var executeItem = PrepareExecuteItem(item);

        try
        {
            if (_resilientExecutor != null)
            {
                var result = await _resilientExecutor.ExecuteAsync(executeItem);
                
                if (result.IsFallback)
                {
                    _log.LogWarning("Process execution used fallback: {FallbackReason}", result.FallbackReason);
                }
                else if (!result.Success)
                {
                    _log.LogWarning("Process {ProcessId} failed with exit code {ExitCode}", result.ProcessId, result.ExitCode);
                }
            }
            else
            {
                // Fallback to legacy execution
                await Task.Run(() => ExecuteLegacy(executeItem));
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to execute process {Program}", item.Program);
            throw;
        }
    }

    private ExecuteItem PrepareExecuteItem(ExecuteItem item)
    {
        var executeItem = new ExecuteItem
        {
            Program = item.Program,
            Arguments = item.Arguments,
            ArgumentsArray = item.GetArgumentsArray(),
            WorkingDirectory = item.WorkingDirectory
        };

        // Set working directory if not specified
        if (string.IsNullOrEmpty(executeItem.WorkingDirectory) && Path.IsPathRooted(item.Program))
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(item.Program));
            if (!string.IsNullOrEmpty(directory))
            {
                executeItem.WorkingDirectory = directory;
            }
        }

        _log.LogDebug("Executing: {Program}", executeItem.Program);
        _log.LogDebug("Parameters: {Arguments}", string.Join(" ", executeItem.ArgumentsArray));
        _log.LogDebug("In directory: {WorkingDirectory}", executeItem.WorkingDirectory);

        return executeItem;
    }

    private void ExecuteLegacy(ExecuteItem executeItem)
    {
        using var process = new Process();
        
        process.StartInfo.FileName = executeItem.Program;
        process.StartInfo.Arguments = string.Join(" ", executeItem.GetArgumentsArray());
        process.StartInfo.WorkingDirectory = executeItem.WorkingDirectory;

        if (!process.Start())
        {
            _log.LogError(LogMessages.ProcessStartFailed, LogMessages.ProcessStartFailedTemplate, executeItem.Program);
            throw new InvalidOperationException($"Failed to start process: {executeItem.Program}");
        }

        _log.LogDebug(LogMessages.ProcessStarted, LogMessages.ProcessStartedTemplate, process.Id, executeItem.Program);
    }
}