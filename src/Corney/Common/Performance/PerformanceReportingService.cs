#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Corney.Common.Performance;

/// <summary>
///     Background service for periodic performance reporting
/// </summary>
public class PerformanceReportingService : IDisposable
{
    private readonly PerformanceDashboard _dashboard;
    private readonly Timer _dashboardTimer;
    private readonly ILogger<PerformanceReportingService> _logger;
    private readonly Timer _reportingTimer;
    private bool _disposed;

    public PerformanceReportingService(
        PerformanceDashboard dashboard,
        ILogger<PerformanceReportingService> logger,
        TimeSpan? reportingInterval = null,
        TimeSpan? dashboardInterval = null)
    {
        _dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var reportingInterval1 = reportingInterval ?? TimeSpan.FromMinutes(15);
        var dashboardInterval1 = dashboardInterval ?? TimeSpan.FromHours(1);

        // Start periodic reporting
        _reportingTimer = new Timer(GenerateReport, null, reportingInterval1, reportingInterval1);
        _dashboardTimer = new Timer(GenerateDashboard, null, dashboardInterval1, dashboardInterval1);

        _logger.LogInformation(
            "Performance reporting service started with intervals: Report {ReportInterval}, Dashboard {DashboardInterval}",
            reportingInterval1, dashboardInterval1);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _reportingTimer?.Dispose();
            _dashboardTimer?.Dispose();

            _logger.LogInformation("Performance reporting service disposed");
            _disposed = true;
        }
    }

    private async void GenerateReport(object? state)
    {
        try
        {
            await GeneratePerformanceReportAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating performance report");
        }
    }

    private async void GenerateDashboard(object? state)
    {
        try
        {
            await _dashboard.LogDashboardAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating performance dashboard");
        }
    }

    /// <summary>
    ///     Generate and log a performance report
    /// </summary>
    public async Task GeneratePerformanceReportAsync()
    {
        var alerts = _dashboard.GenerateAlerts();

        if (alerts.Count > 0)
        {
            _logger.LogWarning("Performance alerts detected: {AlertCount} issues found", alerts.Count);

            foreach (var alert in alerts)
            {
                var logLevel = alert.Severity switch
                {
                    AlertSeverity.Critical => LogLevel.Error,
                    AlertSeverity.Warning => LogLevel.Warning,
                    _ => LogLevel.Information
                };

                _logger.Log(logLevel, "Performance Alert [{Severity}] {Category}: {Message}",
                    alert.Severity, alert.Category, alert.Message);
            }
        }
        else
        {
            _logger.LogInformation("Performance check: No issues detected");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    ///     Force generation of dashboard report
    /// </summary>
    public async Task ForceDashboardReportAsync()
    {
        await _dashboard.LogDashboardAsync();
    }

    /// <summary>
    ///     Force generation of performance report
    /// </summary>
    public async Task ForcePerformanceReportAsync()
    {
        await GeneratePerformanceReportAsync();
    }
}