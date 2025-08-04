using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MediatR;
using System;

namespace Corney.Features.Monitors;

/// <summary>
/// Factory for creating configuration monitors with different optimization levels
/// </summary>
public static class ConfigMonitorFactory
{
    public enum MonitorType
    {
        /// <summary>
        /// Standard monitor with basic functionality
        /// </summary>
        Standard,
        
        /// <summary>
        /// Optimized monitor with differential reloading, configurable debouncing, and performance tracking
        /// </summary>
        Optimized
    }

    /// <summary>
    /// Create a configuration monitor based on the specified type
    /// </summary>
    public static IConfigFileMonitor CreateMonitor(
        MonitorType type,
        IServiceProvider serviceProvider,
        ILogger logger = null)
    {
        return type switch
        {
            MonitorType.Standard => serviceProvider.GetRequiredService<ConfigFileMonitorService>(),
            MonitorType.Optimized => serviceProvider.GetRequiredService<OptimizedConfigFileMonitor>(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown monitor type")
        };
    }

    /// <summary>
    /// Register configuration monitor services in the DI container
    /// </summary>
    public static IServiceCollection AddConfigurationMonitoring(
        this IServiceCollection services,
        MonitorType defaultType = MonitorType.Optimized)
    {
        // Register both implementations
        services.AddSingleton<ConfigFileMonitorService>();
        services.AddSingleton<OptimizedConfigFileMonitor>();
        
        // Register the interface with the default implementation
        services.AddSingleton<IConfigFileMonitor>(provider => 
            CreateMonitor(defaultType, provider));
        
        return services;
    }
}

/// <summary>
/// Common interface for configuration file monitors
/// </summary>
public interface IConfigFileMonitor : IDisposable
{
    /// <summary>
    /// Initialize the monitoring system
    /// </summary>
    void Initialize();
    
    /// <summary>
    /// Registry containing configuration paths
    /// </summary>
    Features.App.CorneyRegistry Registry { get; }
}