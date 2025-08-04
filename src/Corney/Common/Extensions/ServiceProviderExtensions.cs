#nullable enable
using System;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for IServiceProvider
/// </summary>
public static class ServiceProviderExtensions
{
    /// <summary>
    /// Attempts to get a service from the service provider
    /// </summary>
    /// <typeparam name="T">The type of service to get</typeparam>
    /// <param name="serviceProvider">The service provider</param>
    /// <param name="service">The service instance if found</param>
    /// <returns>True if service was found, false otherwise</returns>
    public static bool TryGetService<T>(this IServiceProvider serviceProvider, out T? service)
        where T : class
    {
        try
        {
            service = serviceProvider.GetService<T>();
            return service != null;
        }
        catch
        {
            service = null;
            return false;
        }
    }
}