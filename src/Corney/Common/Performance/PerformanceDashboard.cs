#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Corney.Common.Performance;

/// <summary>
/// Performance dashboard that generates formatted reports and alerts
/// </summary>
public class PerformanceDashboard
{
    private readonly PerformanceMonitor _performanceMonitor;
    private readonly ILogger<PerformanceDashboard> _logger;

    public PerformanceDashboard(PerformanceMonitor performanceMonitor, ILogger<PerformanceDashboard> logger)
    {
        _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generate a comprehensive performance dashboard report
    /// </summary>
    public Task<string> GenerateDashboardAsync()
    {
        var report = _performanceMonitor.GenerateReport();
        var dashboard = new StringBuilder();

        dashboard.AppendLine("╔════════════════════════════════════════════════════════════════╗");
        dashboard.AppendLine("║                    CORNEY PERFORMANCE DASHBOARD                ║");
        dashboard.AppendLine("╚════════════════════════════════════════════════════════════════╝");
        dashboard.AppendLine();

        // System Overview
        AppendSystemOverview(dashboard, report);
        dashboard.AppendLine();

        // Operation Performance
        AppendOperationPerformance(dashboard, report);
        dashboard.AppendLine();

        // System Resources
        AppendSystemResources(dashboard, report);
        dashboard.AppendLine();

        // Recent Activity
        AppendRecentActivity(dashboard, report);
        dashboard.AppendLine();

        // Performance Alerts
        AppendPerformanceAlerts(dashboard, report);

        return Task.FromResult(dashboard.ToString());
    }

    /// <summary>
    /// Log performance dashboard to structured logging
    /// </summary>
    public async Task LogDashboardAsync()
    {
        try
        {
            var dashboardContent = await GenerateDashboardAsync();
            _logger.LogInformation("Performance Dashboard:{NewLine}{Dashboard}", Environment.NewLine, dashboardContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate performance dashboard");
        }
    }

    /// <summary>
    /// Generate alerts for performance issues
    /// </summary>
    public List<PerformanceAlert> GenerateAlerts()
    {
        var report = _performanceMonitor.GenerateReport();
        var alerts = new List<PerformanceAlert>();

        // Check for slow operations
        foreach (var counter in report.Counters.Where(c => c.TotalExecutions >= 5))
        {
            if (counter.AverageDuration.TotalSeconds > 10)
            {
                alerts.Add(new PerformanceAlert
                {
                    Severity = AlertSeverity.Warning,
                    Category = "SlowOperation",
                    Message = $"Operation '{counter.Name}' averages {counter.AverageDuration.TotalSeconds:F2}s per execution",
                    Value = counter.AverageDuration.TotalSeconds,
                    Threshold = 10.0
                });
            }
        }

        // Check for high failure rates
        foreach (var counter in report.Counters.Where(c => c.TotalExecutions >= 10))
        {
            if (counter.SuccessRate < 90)
            {
                alerts.Add(new PerformanceAlert
                {
                    Severity = AlertSeverity.Critical,
                    Category = "HighFailureRate",
                    Message = $"Operation '{counter.Name}' has {counter.SuccessRate:F1}% success rate",
                    Value = counter.SuccessRate,
                    Threshold = 90.0
                });
            }
        }

        // Check system resources
        if (report.SystemMetrics.WorkingSet > 500 * 1024 * 1024) // 500MB
        {
            alerts.Add(new PerformanceAlert
            {
                Severity = AlertSeverity.Warning,
                Category = "HighMemoryUsage",
                Message = $"High memory usage: {report.SystemMetrics.WorkingSet / 1024 / 1024}MB working set",
                Value = report.SystemMetrics.WorkingSet / 1024.0 / 1024.0,
                Threshold = 500.0
            });
        }

        if (report.SystemMetrics.ThreadCount > 50)
        {
            alerts.Add(new PerformanceAlert
            {
                Severity = AlertSeverity.Warning,
                Category = "HighThreadCount",
                Message = $"High thread count: {report.SystemMetrics.ThreadCount} threads",
                Value = report.SystemMetrics.ThreadCount,
                Threshold = 50.0
            });
        }

        return alerts;
    }

    private void AppendSystemOverview(StringBuilder dashboard, PerformanceReport report)
    {
        dashboard.AppendLine("📊 SYSTEM OVERVIEW");
        dashboard.AppendLine("├─────────────────────────────────────────────────────────────────");
        dashboard.AppendLine($"│ Report Time:        {report.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        dashboard.AppendLine($"│ Total Operations:   {report.TotalOperations:N0}");
        dashboard.AppendLine($"│ Success Rate:       {report.AverageSuccessRate:F1}%");
        dashboard.AppendLine($"│ Active Counters:    {report.Counters.Count}");
        dashboard.AppendLine($"│ Memory Usage:       {report.SystemMetrics.WorkingSet / 1024 / 1024:N0} MB");
        dashboard.AppendLine($"│ Thread Count:       {report.SystemMetrics.ThreadCount}");
    }

    private void AppendOperationPerformance(StringBuilder dashboard, PerformanceReport report)
    {
        dashboard.AppendLine("⚡ OPERATION PERFORMANCE");
        dashboard.AppendLine("├─────────────────────────────────────────────────────────────────");

        var topOperations = report.Counters
            .Where(c => c.TotalExecutions > 0)
            .OrderByDescending(c => c.TotalExecutions)
            .Take(10)
            .ToList();

        if (topOperations.Any())
        {
            dashboard.AppendLine("│ Operation Name                    │ Count │ Avg Time │ Success │");
            dashboard.AppendLine("├───────────────────────────────────┼───────┼──────────┼─────────┤");

            foreach (var op in topOperations)
            {
                var name = op.Name.Length > 33 ? op.Name.Substring(0, 30) + "..." : op.Name;
                dashboard.AppendLine($"│ {name,-33} │ {op.TotalExecutions,5} │ {op.AverageDuration.TotalMilliseconds,6:F0}ms │ {op.SuccessRate,5:F1}% │");
            }
        }
        else
        {
            dashboard.AppendLine("│ No operations recorded yet");
        }
    }

    private void AppendSystemResources(StringBuilder dashboard, PerformanceReport report)
    {
        dashboard.AppendLine("🖥️  SYSTEM RESOURCES");
        dashboard.AppendLine("├─────────────────────────────────────────────────────────────────");
        dashboard.AppendLine($"│ Working Set:        {report.SystemMetrics.WorkingSet / 1024 / 1024:N0} MB");
        dashboard.AppendLine($"│ Private Memory:     {report.SystemMetrics.PrivateMemorySize / 1024 / 1024:N0} MB");
        dashboard.AppendLine($"│ Virtual Memory:     {report.SystemMetrics.VirtualMemorySize / 1024 / 1024:N0} MB");
        dashboard.AppendLine($"│ Thread Count:       {report.SystemMetrics.ThreadCount}");
        dashboard.AppendLine($"│ Handle Count:       {report.SystemMetrics.HandleCount}");
        dashboard.AppendLine($"│ CPU Time:           {report.SystemMetrics.TotalProcessorTime.TotalSeconds:F1}s");
    }

    private void AppendRecentActivity(StringBuilder dashboard, PerformanceReport report)
    {
        dashboard.AppendLine("📈 RECENT ACTIVITY (Last 20 operations)");
        dashboard.AppendLine("├─────────────────────────────────────────────────────────────────");

        var recentMetrics = report.RecentMetrics.TakeLast(20).ToList();
        
        if (recentMetrics.Any())
        {
            dashboard.AppendLine("│ Time     │ Operation                     │ Duration │ Status │");
            dashboard.AppendLine("├──────────┼───────────────────────────────┼──────────┼────────┤");

            foreach (var metric in recentMetrics)
            {
                var time = metric.Timestamp.ToString("HH:mm:ss");
                var operation = metric.OperationName.Length > 29 ? 
                    metric.OperationName.Substring(0, 26) + "..." : metric.OperationName;
                var status = metric.Success ? "✓ OK" : "✗ FAIL";
                
                dashboard.AppendLine($"│ {time,-8} │ {operation,-29} │ {metric.Duration.TotalMilliseconds,6:F0}ms │ {status,-6} │");
            }
        }
        else
        {
            dashboard.AppendLine("│ No recent activity");
        }
    }

    private void AppendPerformanceAlerts(StringBuilder dashboard, PerformanceReport report)
    {
        dashboard.AppendLine("🚨 PERFORMANCE ALERTS");
        dashboard.AppendLine("├─────────────────────────────────────────────────────────────────");

        var alerts = GenerateAlerts();
        
        if (alerts.Any())
        {
            var criticalAlerts = alerts.Where(a => a.Severity == AlertSeverity.Critical).ToList();
            var warningAlerts = alerts.Where(a => a.Severity == AlertSeverity.Warning).ToList();

            if (criticalAlerts.Any())
            {
                dashboard.AppendLine("│ 🔴 CRITICAL ALERTS:");
                foreach (var alert in criticalAlerts)
                {
                    dashboard.AppendLine($"│   • {alert.Message}");
                }
            }

            if (warningAlerts.Any())
            {
                dashboard.AppendLine("│ 🟡 WARNING ALERTS:");
                foreach (var alert in warningAlerts)
                {
                    dashboard.AppendLine($"│   • {alert.Message}");
                }
            }
        }
        else
        {
            dashboard.AppendLine("│ ✅ No performance issues detected");
        }
    }
}

/// <summary>
/// Performance alert information
/// </summary>
public class PerformanceAlert
{
    public AlertSeverity Severity { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public double Value { get; set; }
    public double Threshold { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Alert severity levels
/// </summary>
public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}