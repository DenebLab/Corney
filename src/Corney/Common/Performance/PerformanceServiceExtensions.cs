#nullable enable
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Corney.Common.Performance;

/// <summary>
/// Extension methods for registering performance monitoring services
/// </summary>
public static class PerformanceServiceExtensions
{
    /// <summary>
    /// Adds performance monitoring services to the dependency injection container
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Optional configuration for performance monitoring</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddPerformanceMonitoring(
        this IServiceCollection services,
        Action<PerformanceOptions>? configure = null)
    {
        // Register core performance monitoring services as singletons
        services.AddSingleton<PerformanceMonitor>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<PerformanceMonitor>>();
            return new PerformanceMonitor(logger);
        });

        services.AddSingleton<PerformanceDashboard>(serviceProvider =>
        {
            var performanceMonitor = serviceProvider.GetRequiredService<PerformanceMonitor>();
            var logger = serviceProvider.GetRequiredService<ILogger<PerformanceDashboard>>();
            return new PerformanceDashboard(performanceMonitor, logger);
        });

        services.AddSingleton<PerformanceReportingService>(serviceProvider =>
        {
            var dashboard = serviceProvider.GetRequiredService<PerformanceDashboard>();
            var logger = serviceProvider.GetRequiredService<ILogger<PerformanceReportingService>>();
            
            // Apply configuration if provided
            var options = new PerformanceOptions();
            configure?.Invoke(options);
            
            return new PerformanceReportingService(
                dashboard, 
                logger, 
                options.ReportingInterval, 
                options.DashboardInterval);
        });

        return services;
    }

    /// <summary>
    /// Adds basic performance monitoring without reporting services
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddBasicPerformanceMonitoring(this IServiceCollection services)
    {
        services.AddSingleton<PerformanceMonitor>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<PerformanceMonitor>>();
            return new PerformanceMonitor(logger);
        });

        return services;
    }
}

/// <summary>
/// Configuration options for performance monitoring
/// </summary>
public class PerformanceOptions
{
    /// <summary>
    /// Interval for performance reports (default: 15 minutes)
    /// </summary>
    public TimeSpan ReportingInterval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Interval for dashboard generation (default: 1 hour)
    /// </summary>
    public TimeSpan DashboardInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Enable automatic performance reporting
    /// </summary>
    public bool EnableAutomaticReporting { get; set; } = true;

    /// <summary>
    /// Maximum number of recent metrics to keep in memory
    /// </summary>
    public int MaxRecentMetrics { get; set; } = 1000;
}