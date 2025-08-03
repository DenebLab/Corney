using Microsoft.Extensions.Logging;

namespace Corney.Common.Logging;

/// <summary>
/// Structured logging message templates and event IDs for Corney application
/// </summary>
public static class LogMessages
{
    // CronService Event IDs (1000-1999)
    public static readonly EventId CronServiceStarted = new(1001, nameof(CronServiceStarted));
    public static readonly EventId CronServiceRestarted = new(1002, nameof(CronServiceRestarted));
    public static readonly EventId CronServiceStopped = new(1003, nameof(CronServiceStopped));
    public static readonly EventId CronExecutionStarted = new(1010, nameof(CronExecutionStarted));
    public static readonly EventId CronExecutionCompleted = new(1011, nameof(CronExecutionCompleted));
    public static readonly EventId CronNextScheduled = new(1020, nameof(CronNextScheduled));
    public static readonly EventId CronDefinitionsLoaded = new(1030, nameof(CronDefinitionsLoaded));
    public static readonly EventId CronTasksScheduled = new(1040, nameof(CronTasksScheduled));
    public static readonly EventId CronLockTimeout = new(1050, nameof(CronLockTimeout));
    public static readonly EventId CronGenerateNextError = new(1060, nameof(CronGenerateNextError));

    // Process Event IDs (2000-2999)
    public static readonly EventId ProcessStarted = new(2001, nameof(ProcessStarted));
    public static readonly EventId ProcessStartFailed = new(2002, nameof(ProcessStartFailed));
    public static readonly EventId ProcessError = new(2003, nameof(ProcessError));

    // File Operations Event IDs (3000-3999)
    public static readonly EventId FileReadRetry = new(3001, nameof(FileReadRetry));
    public static readonly EventId FileMonitorStarted = new(3010, nameof(FileMonitorStarted));
    public static readonly EventId FileChanged = new(3020, nameof(FileChanged));

    // Application Event IDs (4000-4999)
    public static readonly EventId ApplicationStarted = new(4001, nameof(ApplicationStarted));
    public static readonly EventId MinuteExecutionCompleted = new(4010, nameof(MinuteExecutionCompleted));
    public static readonly EventId NextExecutionScheduled = new(4020, nameof(NextExecutionScheduled));

    // CronService message templates
    public const string CronServiceStartedTemplate = "CronService started with version {AppVersion}";
    public const string CronServiceRestartedTemplate = "CronService restarted with version {AppVersion}";
    public const string CronServiceStoppedTemplate = "CronService stopped";
    
    public const string CronExecutionStartedTemplate = "Executing cron jobs {Marker} at {ExecutionTime} with {JobCount} items to run";
    public const string CronExecutionCompletedTemplate = "Cron execution completed {OldMarker}, next marker {NextMarker}";
    
    public const string CronNextScheduledTemplate = "Next cron execution scheduled {Marker} at {NextTime}";
    public const string CronDefinitionsLoadedTemplate = "Loaded cron definitions from files: {CronFiles}";
    public const string CronTasksScheduledTemplate = "Generated next cron tasks {Marker} at {NextTime} with {ItemCount} items";
    
    public const string CronLockTimeoutTemplate = "Failed to acquire {LockType} lock for {Operation} within timeout";
    public const string CronGenerateNextErrorTemplate = "Error generating next cron jobs on attempt {Attempt}: {ErrorMessage}";

    // Process message templates
    public const string ProcessStartedTemplate = "Process started with ID {ProcessId} for command {Program}";
    public const string ProcessStartFailedTemplate = "Failed to start process for command {Program}";
    public const string ProcessErrorTemplate = "Error starting process '{Program}': {ErrorMessage}";

    // File operations message templates
    public const string FileReadRetryTemplate = "File not available [{Attempt}/{MaxAttempts}] for path {FilePath}";
    public const string FileMonitorStartedTemplate = "Monitoring {FileCount} files for changes";
    public const string FileChangedTemplate = "File change detected: {FilePath}";

    // Application message templates
    public const string ApplicationStartedTemplate = "Application started: {ApplicationVersion}";
    public const string MinuteExecutionCompletedTemplate = "Minute execution completed at {ExecutionTime}";
    public const string NextExecutionScheduledTemplate = "Next execution scheduled in {DelaySeconds}s";
}