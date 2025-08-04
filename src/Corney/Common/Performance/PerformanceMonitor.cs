#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Corney.Common.Logging;
using Microsoft.Extensions.Logging;

namespace Corney.Common.Performance;

/// <summary>
/// Central performance monitoring service
/// </summary>
public class PerformanceMonitor : IDisposable
{
    private readonly ILogger<PerformanceMonitor> _logger;
    private readonly ConcurrentDictionary<string, PerformanceCounter> _counters = new();
    private readonly ConcurrentQueue<PerformanceMetrics> _recentMetrics = new();
    private readonly Timer _reportingTimer;
    private readonly Timer _resourceMonitorTimer;
    private readonly int _maxRecentMetrics = 1000;
    private readonly TimeSpan _reportingInterval = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _resourceMonitorInterval = TimeSpan.FromMinutes(1);
    private bool _disposed = false;

    public PerformanceMonitor(ILogger<PerformanceMonitor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Start periodic reporting
        _reportingTimer = new Timer(ReportPerformanceMetrics, null, _reportingInterval, _reportingInterval);
        _resourceMonitorTimer = new Timer(MonitorSystemResources, null, _resourceMonitorInterval, _resourceMonitorInterval);
        
        _logger.LogInformation(LogMessages.PerformanceMonitorStarted, LogMessages.PerformanceMonitorStartedTemplate, _reportingInterval);
    }

    /// <summary>
    /// Record a performance metric
    /// </summary>
    public void RecordMetric(PerformanceMetrics metric)
    {
        if (_disposed) return;

        // Add to recent metrics queue
        _recentMetrics.Enqueue(metric);
        
        // Trim queue if too large
        while (_recentMetrics.Count > _maxRecentMetrics)
        {
            _recentMetrics.TryDequeue(out _);
        }

        // Update counter
        var counter = _counters.GetOrAdd(metric.OperationName, name => new PerformanceCounter(name));
        counter.RecordExecution(metric.Duration, metric.Success);

        // Log detailed metrics for critical operations
        if (ShouldLogDetailedMetric(metric))
        {
            _logger.LogDebug("Performance metric recorded: {OperationName} completed in {Duration}ms, Success: {Success}, Memory: {MemoryMB}MB",
                metric.OperationName, metric.Duration.TotalMilliseconds, metric.Success, metric.MemoryUsed / 1024 / 1024);
        }
    }

    /// <summary>
    /// Start timing an operation
    /// </summary>
    public ExecutionTimer StartTiming(string operationName)
    {
        return new ExecutionTimer((duration, success) =>
        {
            var metric = new PerformanceMetrics
            {
                OperationName = operationName,
                Duration = duration,
                Success = success,
                MemoryUsed = GC.GetTotalMemory(false),
                ThreadCount = Process.GetCurrentProcess().Threads.Count
            };
            
            RecordMetric(metric);
        });
    }

    /// <summary>
    /// Record an execution with automatic timing
    /// </summary>
    public T RecordExecution<T>(string operationName, Func<T> operation)
    {
        using var timer = StartTiming(operationName);
        try
        {
            var result = operation();
            timer.MarkSuccess();
            return result;
        }
        catch
        {
            timer.MarkFailure();
            throw;
        }
    }

    /// <summary>
    /// Record an async execution with automatic timing
    /// </summary>
    public async Task<T> RecordExecutionAsync<T>(string operationName, Func<Task<T>> operation)
    {
        using var timer = StartTiming(operationName);
        try
        {
            var result = await operation();
            timer.MarkSuccess();
            return result;
        }
        catch
        {
            timer.MarkFailure();
            throw;
        }
    }

    /// <summary>
    /// Get performance counter by name
    /// </summary>
    public PerformanceCounter? GetCounter(string name)
    {
        return _counters.TryGetValue(name, out var counter) ? counter : null;
    }

    /// <summary>
    /// Get all performance counters
    /// </summary>
    public IReadOnlyDictionary<string, PerformanceCounter> GetAllCounters()
    {
        return _counters.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Get recent metrics
    /// </summary>
    public IEnumerable<PerformanceMetrics> GetRecentMetrics(int count = 100)
    {
        return _recentMetrics.TakeLast(Math.Min(count, _recentMetrics.Count));
    }

    /// <summary>
    /// Generate performance report
    /// </summary>
    public PerformanceReport GenerateReport()
    {
        var counters = _counters.Values.Select(c => c.GetSnapshot()).ToList();
        var systemMetrics = SystemResourceMetrics.GetCurrent();
        var recentMetrics = GetRecentMetrics(50).ToList();

        return new PerformanceReport
        {
            Timestamp = DateTime.UtcNow,
            Counters = counters,
            SystemMetrics = systemMetrics,
            RecentMetrics = recentMetrics,
            TotalOperations = counters.Sum(c => c.TotalExecutions),
            AverageSuccessRate = counters.Count > 0 ? counters.Average(c => c.SuccessRate) : 0.0
        };
    }

    /// <summary>
    /// Reset all performance counters
    /// </summary>
    public void ResetCounters()
    {
        foreach (var counter in _counters.Values)
        {
            counter.Reset();
        }
        
        _logger.LogInformation("Performance counters reset");
    }

    private bool ShouldLogDetailedMetric(PerformanceMetrics metric)
    {
        // Log if execution took longer than threshold
        if (metric.Duration.TotalSeconds > 5)
            return true;

        // Log if operation failed
        if (!metric.Success)
            return true;

        // Log critical operations
        return metric.OperationName.Contains("cron", StringComparison.OrdinalIgnoreCase) ||
               metric.OperationName.Contains("process", StringComparison.OrdinalIgnoreCase);
    }

    private void ReportPerformanceMetrics(object? state)
    {
        try
        {
            var report = GenerateReport();
            
            _logger.LogInformation(LogMessages.PerformanceReport, LogMessages.PerformanceReportTemplate,
                report.TotalOperations, report.AverageSuccessRate, 
                report.SystemMetrics.WorkingSet / 1024 / 1024, // MB
                report.SystemMetrics.ThreadCount);

            // Log top 5 slowest operations
            var slowestOperations = report.Counters
                .Where(c => c.TotalExecutions > 0)
                .OrderByDescending(c => c.AverageDuration.TotalMilliseconds)
                .Take(5)
                .ToList();

            if (slowestOperations.Any())
            {
                _logger.LogInformation("Top 5 slowest operations:");
                foreach (var op in slowestOperations)
                {
                    _logger.LogInformation("  • {OperationName}: {AverageMs}ms avg ({Executions} executions, {SuccessRate:F1}% success)",
                        op.Name, op.AverageDuration.TotalMilliseconds, op.TotalExecutions, op.SuccessRate);
                }
            }

            // Check for performance issues
            DetectPerformanceIssues(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating performance report");
        }
    }

    private void MonitorSystemResources(object? state)
    {
        try
        {
            var metrics = SystemResourceMetrics.GetCurrent();
            
            // Log system resource usage
            _logger.LogDebug("System resources: Memory {MemoryMB}MB, Threads {ThreadCount}, Handles {HandleCount}",
                metrics.WorkingSet / 1024 / 1024, metrics.ThreadCount, metrics.HandleCount);

            // Check for resource issues
            CheckResourceThresholds(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring system resources");
        }
    }

    private void DetectPerformanceIssues(PerformanceReport report)
    {
        // Check for high failure rates
        var highFailureOperations = report.Counters
            .Where(c => c.TotalExecutions >= 10 && c.SuccessRate < 90)
            .ToList();

        foreach (var op in highFailureOperations)
        {
            _logger.LogWarning("High failure rate detected: {OperationName} has {SuccessRate:F1}% success rate ({FailedCount}/{TotalCount})",
                op.Name, op.SuccessRate, op.FailedExecutions, op.TotalExecutions);
        }

        // Check for slow operations
        var slowOperations = report.Counters
            .Where(c => c.TotalExecutions >= 5 && c.AverageDuration.TotalSeconds > 10)
            .ToList();

        foreach (var op in slowOperations)
        {
            _logger.LogWarning("Slow operation detected: {OperationName} averages {AverageSeconds:F2}s per execution",
                op.Name, op.AverageDuration.TotalSeconds);
        }
    }

    private void CheckResourceThresholds(SystemResourceMetrics metrics)
    {
        // Check memory usage (warn if over 500MB)
        if (metrics.WorkingSet > 500 * 1024 * 1024)
        {
            _logger.LogWarning("High memory usage: {MemoryMB}MB working set", metrics.WorkingSet / 1024 / 1024);
        }

        // Check thread count (warn if over 50)
        if (metrics.ThreadCount > 50)
        {
            _logger.LogWarning("High thread count: {ThreadCount} threads", metrics.ThreadCount);
        }

        // Check handle count (warn if over 1000)
        if (metrics.HandleCount > 1000)
        {
            _logger.LogWarning("High handle count: {HandleCount} handles", metrics.HandleCount);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _reportingTimer?.Dispose();
            _resourceMonitorTimer?.Dispose();
            
            _logger.LogInformation("Performance monitor disposed");
            _disposed = true;
        }
    }
}

/// <summary>
/// Comprehensive performance report
/// </summary>
public class PerformanceReport
{
    public DateTime Timestamp { get; set; }
    public List<PerformanceCounterSnapshot> Counters { get; set; } = new();
    public SystemResourceMetrics SystemMetrics { get; set; } = new();
    public List<PerformanceMetrics> RecentMetrics { get; set; } = new();
    public long TotalOperations { get; set; }
    public double AverageSuccessRate { get; set; }
}