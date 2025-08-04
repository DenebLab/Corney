using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Corney.Common.Resilience;
using Microsoft.Extensions.Logging;

namespace Corney.Common.Health;

/// <summary>
///     Health check status
/// </summary>
public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}

/// <summary>
///     Health check result
/// </summary>
public class HealthCheckResult
{
    public string ComponentName { get; set; } = string.Empty;
    public HealthStatus Status { get; set; }
    public string Description { get; set; } = string.Empty;
    public Exception Exception { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime CheckTime { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();

    public static HealthCheckResult Healthy(string componentName, string description = null, TimeSpan? duration = null)
    {
        return new HealthCheckResult
        {
            ComponentName = componentName,
            Status = HealthStatus.Healthy,
            Description = description ?? "Component is healthy",
            Duration = duration ?? TimeSpan.Zero,
            CheckTime = DateTime.UtcNow
        };
    }

    public static HealthCheckResult Degraded(string componentName, string description, TimeSpan? duration = null)
    {
        return new HealthCheckResult
        {
            ComponentName = componentName,
            Status = HealthStatus.Degraded,
            Description = description,
            Duration = duration ?? TimeSpan.Zero,
            CheckTime = DateTime.UtcNow
        };
    }

    public static HealthCheckResult Unhealthy(string componentName, string description, Exception exception = null,
        TimeSpan? duration = null)
    {
        return new HealthCheckResult
        {
            ComponentName = componentName,
            Status = HealthStatus.Unhealthy,
            Description = description,
            Exception = exception,
            Duration = duration ?? TimeSpan.Zero,
            CheckTime = DateTime.UtcNow
        };
    }
}

/// <summary>
///     Health check interface
/// </summary>
public interface IHealthCheck
{
    string Name { get; }
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
///     File system health check
/// </summary>
public class FileSystemHealthCheck : IHealthCheck
{
    private readonly string[] _criticalPaths;
    private readonly ILogger<FileSystemHealthCheck> _logger;

    public FileSystemHealthCheck(string[] criticalPaths, ILogger<FileSystemHealthCheck> logger)
    {
        _criticalPaths = criticalPaths ?? throw new ArgumentNullException(nameof(criticalPaths));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "FileSystem";

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var missingPaths = new List<string>();
            var inaccessiblePaths = new List<string>();

            foreach (var path in _criticalPaths)
            {
                await Task.Yield(); // Make this actually async

                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    missingPaths.Add(path);
                    continue;
                }

                try
                {
                    // Test read access
                    if (File.Exists(path))
                    {
                        using var stream = File.OpenRead(path);
                        // Just opening is enough to test access
                    }
                    else if (Directory.Exists(path))
                    {
                        Directory.GetFiles(path);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    inaccessiblePaths.Add(path);
                }
            }

            var duration = DateTime.UtcNow - startTime;
            var data = new Dictionary<string, object>
            {
                ["TotalPaths"] = _criticalPaths.Length,
                ["MissingPaths"] = missingPaths,
                ["InaccessiblePaths"] = inaccessiblePaths
            };

            if (missingPaths.Any())
                return new HealthCheckResult
                {
                    ComponentName = Name,
                    Status = HealthStatus.Unhealthy,
                    Description = $"Critical paths missing: {string.Join(", ", missingPaths)}",
                    Duration = duration,
                    CheckTime = DateTime.UtcNow,
                    Data = data
                };

            if (inaccessiblePaths.Any())
                return new HealthCheckResult
                {
                    ComponentName = Name,
                    Status = HealthStatus.Degraded,
                    Description = $"Paths inaccessible: {string.Join(", ", inaccessiblePaths)}",
                    Duration = duration,
                    CheckTime = DateTime.UtcNow,
                    Data = data
                };

            return new HealthCheckResult
            {
                ComponentName = Name,
                Status = HealthStatus.Healthy,
                Description = "All critical paths accessible",
                Duration = duration,
                CheckTime = DateTime.UtcNow,
                Data = data
            };
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "FileSystem health check failed");

            return HealthCheckResult.Unhealthy(Name,
                "FileSystem health check encountered an error", ex, duration);
        }
    }
}

/// <summary>
///     Circuit breaker health check
/// </summary>
public class CircuitBreakerHealthCheck : IHealthCheck
{
    private readonly CircuitBreaker _circuitBreaker;
    private readonly ILogger<CircuitBreakerHealthCheck> _logger;

    public CircuitBreakerHealthCheck(CircuitBreaker circuitBreaker, ILogger<CircuitBreakerHealthCheck> logger)
    {
        _circuitBreaker = circuitBreaker ?? throw new ArgumentNullException(nameof(circuitBreaker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "CircuitBreaker";

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield(); // Make this actually async

        var startTime = DateTime.UtcNow;

        try
        {
            var status = _circuitBreaker.GetStatus();
            var duration = DateTime.UtcNow - startTime;

            var data = new Dictionary<string, object>
            {
                ["State"] = status.State.ToString(),
                ["FailureCount"] = status.FailureCount,
                ["LastFailureTime"] = status.LastFailureTime,
                ["HalfOpenAttempts"] = status.HalfOpenAttempts
            };

            return status.State switch
            {
                CircuitBreakerState.Closed => new HealthCheckResult
                {
                    ComponentName = Name,
                    Status = HealthStatus.Healthy,
                    Description = "Circuit breaker is closed and healthy",
                    Duration = duration,
                    CheckTime = DateTime.UtcNow,
                    Data = data
                },
                CircuitBreakerState.HalfOpen => new HealthCheckResult
                {
                    ComponentName = Name,
                    Status = HealthStatus.Degraded,
                    Description = "Circuit breaker is half-open, testing recovery",
                    Duration = duration,
                    CheckTime = DateTime.UtcNow,
                    Data = data
                },
                CircuitBreakerState.Open => new HealthCheckResult
                {
                    ComponentName = Name,
                    Status = HealthStatus.Unhealthy,
                    Description = $"Circuit breaker is open with {status.FailureCount} failures",
                    Duration = duration,
                    CheckTime = DateTime.UtcNow,
                    Data = data
                },
                _ => HealthCheckResult.Unhealthy(Name, "Unknown circuit breaker state", null, duration)
            };
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "CircuitBreaker health check failed");

            return HealthCheckResult.Unhealthy(Name,
                "CircuitBreaker health check encountered an error", ex, duration);
        }
    }
}

/// <summary>
///     Health check service to coordinate all health checks
/// </summary>
public class HealthCheckService
{
    private readonly List<IHealthCheck> _healthChecks = new();
    private readonly ILogger<HealthCheckService> _logger;

    public HealthCheckService(ILogger<HealthCheckService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void RegisterHealthCheck(IHealthCheck healthCheck)
    {
        if (healthCheck == null) throw new ArgumentNullException(nameof(healthCheck));

        _healthChecks.Add(healthCheck);
        _logger.LogDebug("Registered health check: {HealthCheckName}", healthCheck.Name);
    }

    public async Task<HealthCheckResult[]> CheckAllAsync(CancellationToken cancellationToken = default)
    {
        var tasks = _healthChecks.Select(async healthCheck =>
        {
            try
            {
                return await healthCheck.CheckHealthAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check {HealthCheckName} failed", healthCheck.Name);
                return HealthCheckResult.Unhealthy(healthCheck.Name,
                    $"Health check failed with exception: {ex.Message}", ex);
            }
        });

        var results = await Task.WhenAll(tasks);

        var unhealthyCount = results.Count(r => r.Status == HealthStatus.Unhealthy);
        var degradedCount = results.Count(r => r.Status == HealthStatus.Degraded);

        if (unhealthyCount > 0)
            _logger.LogWarning("Health check completed: {UnhealthyCount} unhealthy, {DegradedCount} degraded",
                unhealthyCount, degradedCount);
        else if (degradedCount > 0)
            _logger.LogInformation("Health check completed: {DegradedCount} degraded components", degradedCount);
        else
            _logger.LogDebug("Health check completed: All components healthy");

        return results;
    }

    public async Task<HealthStatus> GetOverallHealthAsync(CancellationToken cancellationToken = default)
    {
        var results = await CheckAllAsync(cancellationToken);

        if (results.Any(r => r.Status == HealthStatus.Unhealthy))
            return HealthStatus.Unhealthy;

        if (results.Any(r => r.Status == HealthStatus.Degraded))
            return HealthStatus.Degraded;

        return HealthStatus.Healthy;
    }
}