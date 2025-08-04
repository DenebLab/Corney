#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Corney.Common.Performance;

/// <summary>
/// Core performance metrics data structures
/// </summary>
public class PerformanceMetrics
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string OperationName { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string? ErrorType { get; set; }
    public long MemoryUsed { get; set; }
    public int ThreadCount { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

/// <summary>
/// Performance counter for tracking execution metrics
/// </summary>
public class PerformanceCounter
{
    private readonly string _name;
    private long _totalExecutions;
    private long _successfulExecutions;
    private long _failedExecutions;
    private long _totalDurationTicks;
    private long _minDurationTicks = long.MaxValue;
    private long _maxDurationTicks = long.MinValue;
    private readonly object _lockObject = new();

    public PerformanceCounter(string name)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public string Name => _name;
    public long TotalExecutions => _totalExecutions;
    public long SuccessfulExecutions => _successfulExecutions;
    public long FailedExecutions => _failedExecutions;
    public double SuccessRate => TotalExecutions == 0 ? 0.0 : (double)SuccessfulExecutions / TotalExecutions * 100;
    
    public TimeSpan AverageDuration => TotalExecutions == 0 
        ? TimeSpan.Zero 
        : new TimeSpan(_totalDurationTicks / TotalExecutions);
    
    public TimeSpan MinDuration => _minDurationTicks == long.MaxValue 
        ? TimeSpan.Zero 
        : new TimeSpan(_minDurationTicks);
    
    public TimeSpan MaxDuration => _maxDurationTicks == long.MinValue 
        ? TimeSpan.Zero 
        : new TimeSpan(_maxDurationTicks);

    public void RecordExecution(TimeSpan duration, bool success)
    {
        lock (_lockObject)
        {
            Interlocked.Increment(ref _totalExecutions);
            
            if (success)
                Interlocked.Increment(ref _successfulExecutions);
            else
                Interlocked.Increment(ref _failedExecutions);

            var durationTicks = duration.Ticks;
            Interlocked.Add(ref _totalDurationTicks, durationTicks);

            if (durationTicks < _minDurationTicks)
                _minDurationTicks = durationTicks;
            
            if (durationTicks > _maxDurationTicks)
                _maxDurationTicks = durationTicks;
        }
    }

    public void Reset()
    {
        lock (_lockObject)
        {
            _totalExecutions = 0;
            _successfulExecutions = 0;
            _failedExecutions = 0;
            _totalDurationTicks = 0;
            _minDurationTicks = long.MaxValue;
            _maxDurationTicks = long.MinValue;
        }
    }

    public PerformanceCounterSnapshot GetSnapshot()
    {
        lock (_lockObject)
        {
            return new PerformanceCounterSnapshot
            {
                Name = _name,
                TotalExecutions = _totalExecutions,
                SuccessfulExecutions = _successfulExecutions,
                FailedExecutions = _failedExecutions,
                SuccessRate = SuccessRate,
                AverageDuration = AverageDuration,
                MinDuration = MinDuration,
                MaxDuration = MaxDuration,
                Timestamp = DateTime.UtcNow
            };
        }
    }
}

/// <summary>
/// Snapshot of performance counter state
/// </summary>
public class PerformanceCounterSnapshot
{
    public string Name { get; set; } = string.Empty;
    public long TotalExecutions { get; set; }
    public long SuccessfulExecutions { get; set; }
    public long FailedExecutions { get; set; }
    public double SuccessRate { get; set; }
    public TimeSpan AverageDuration { get; set; }
    public TimeSpan MinDuration { get; set; }
    public TimeSpan MaxDuration { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Execution timing helper using IDisposable pattern
/// </summary>
public class ExecutionTimer : IDisposable
{
    private readonly Stopwatch _stopwatch;
    private readonly Action<TimeSpan, bool> _onComplete;
    private bool _success = true;
    private bool _disposed = false;

    public ExecutionTimer(Action<TimeSpan, bool> onComplete)
    {
        _onComplete = onComplete ?? throw new ArgumentNullException(nameof(onComplete));
        _stopwatch = Stopwatch.StartNew();
    }

    public void MarkFailure()
    {
        _success = false;
    }

    public void MarkSuccess()
    {
        _success = true;
    }

    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public void Dispose()
    {
        if (!_disposed)
        {
            _stopwatch.Stop();
            _onComplete(_stopwatch.Elapsed, _success);
            _disposed = true;
        }
    }
}

/// <summary>
/// System resource usage metrics
/// </summary>
public class SystemResourceMetrics
{
    public long WorkingSet { get; set; }
    public long PrivateMemorySize { get; set; }
    public long VirtualMemorySize { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
    public TimeSpan TotalProcessorTime { get; set; }
    public TimeSpan UserProcessorTime { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static SystemResourceMetrics GetCurrent()
    {
        using var process = Process.GetCurrentProcess();
        
        return new SystemResourceMetrics
        {
            WorkingSet = process.WorkingSet64,
            PrivateMemorySize = process.PrivateMemorySize64,
            VirtualMemorySize = process.VirtualMemorySize64,
            ThreadCount = process.Threads.Count,
            HandleCount = process.HandleCount,
            TotalProcessorTime = process.TotalProcessorTime,
            UserProcessorTime = process.UserProcessorTime,
            Timestamp = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Performance metric categories
/// </summary>
public static class MetricCategories
{
    public const string CronExecution = "cron.execution";
    public const string FileRead = "file.read";
    public const string ProcessExecution = "process.execution";
    public const string ConfigurationLoad = "config.load";
    public const string HealthCheck = "health.check";
    public const string SystemResource = "system.resource";
}