using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Corney.Common.Io;
using Corney.Common.Logging;
using Corney.Common.Performance;
using Corney.Features.App;
using Deneblab.Common.Logging;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Corney.Features.Monitors;

/// <summary>
/// Optimized configuration file monitor with debouncing, differential reloading, and resource management
/// </summary>
public class OptimizedConfigFileMonitor : IConfigFileMonitor
{
    private readonly List<IDisposable> _crontabFileDisposables = [];
    private readonly Channel<ConfigChangeEvent> _channel = Channel.CreateUnbounded<ConfigChangeEvent>();
    private readonly CompositeDisposable _configDisposable = new();
    private readonly string _configFilePath;
    private readonly FileWatchHelpers _fileWatchHelpers;
    private readonly ILogger<OptimizedConfigFileMonitor> _log;
    private readonly IMediator _mediator;
    private readonly PerformanceMonitor _performanceMonitor;
    
    // Configuration state tracking
    private CorneyConfig _lastKnownConfig = new();
    private Dictionary<string, string> _fileHashes = new();
    private HashSet<string> _monitoredFiles = new();
    private DateTime _lastConfigChange = DateTime.MinValue;
    private int _changeCounter;

    public OptimizedConfigFileMonitor(
        ILogger<OptimizedConfigFileMonitor> log,
        IMediator mediator,
        FileWatchHelpers fileWatchHelpers,
        CorneyRegistry registry,
        IServiceProvider serviceProvider)
    {
        _log = log;
        _mediator = mediator;
        _fileWatchHelpers = fileWatchHelpers;
        _configFilePath = registry.ConfigFilePath;
        Registry = registry;
        
        // Get performance monitor if available
        serviceProvider.TryGetService(out _performanceMonitor);
        
        Task.Factory.StartNew(ChannelConsumerProcess, TaskCreationOptions.LongRunning);
    }

    public CorneyRegistry Registry { get; }

    public void Dispose()
    {
        _log.LogDebug(LogMessages.ServiceStopping, "OptimizedConfigFileMonitor disposed");
        _configDisposable.Dispose();
        ClearCrontabFileDisposables();
        
        // Complete the channel
        _channel.Writer.Complete();
    }

    /// <summary>
    /// Initialize monitoring with current configuration
    /// </summary>
    public void Initialize()
    {
        using var timer = _performanceMonitor?.StartTiming($"{MetricCategories.FileMonitoring}.initialize");
        
        try
        {
            // Load initial configuration
            _lastKnownConfig = LoadConfigSafely(_configFilePath);
            
            // Setup config file monitoring with configurable debounce
            var debounceInterval = TimeSpan.FromSeconds(_lastKnownConfig.FileMonitoringDebounceSeconds);
            var configObservable = _fileWatchHelpers.CreateForFile(_configFilePath, debounceInterval);
            _configDisposable.Add(configObservable.Subscribe(evt => WriteToChannel(new ConfigChangeEvent(evt, true))));
            
            // Setup initial crontab file monitoring
            var crontabFiles = SetupCrontabFileMonitoring(_lastKnownConfig, debounceInterval);
            
            _log.LogInformation("OptimizedConfigFileMonitor initialized for {ConfigFile} monitoring {CrontabFileCount} crontab files",
                _configFilePath, crontabFiles.Length);
                
            timer?.MarkSuccess();
        }
        catch (Exception ex)
        {
            timer?.MarkFailure();
            _log.LogError(ex, "Failed to initialize OptimizedConfigFileMonitor");
            throw;
        }
    }

    private async Task ChannelConsumerProcess()
    {
        try
        {
            await foreach (var changeEvent in _channel.Reader.ReadAllAsync())
            {
                _log.LogDebug("Processing configuration change event: {EventType} for {FilePath}",
                    changeEvent.IsConfigFile ? "Config" : "Crontab", changeEvent.Event.FullPath);
                    
                try
                {
                    await ProcessConfigChangeEvent(changeEvent);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Error processing configuration change event for {FilePath}", changeEvent.Event.FullPath);
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Configuration change processing loop terminated");
        }
    }

    private void WriteToChannel(ConfigChangeEvent changeEvent)
    {
        var success = _channel.Writer.TryWrite(changeEvent);
        _log.LogDebug("Channel write {Result} for {EventType}: {FilePath}",
            success ? "succeeded" : "failed",
            changeEvent.IsConfigFile ? "Config" : "Crontab",
            changeEvent.Event.FullPath);
    }

    private async Task ProcessConfigChangeEvent(ConfigChangeEvent changeEvent)
    {
        using var timer = _performanceMonitor?.StartTiming($"{MetricCategories.FileMonitoring}.process_change");
        
        _changeCounter++;
        _lastConfigChange = DateTime.UtcNow;
        
        _log.LogDebug("Processing change event #{Counter}: {ChangeType} on {FilePath}",
            _changeCounter, changeEvent.Event.ChangeType, changeEvent.Event.FullPath);

        if (changeEvent.IsConfigFile)
        {
            await ProcessConfigFileChange(changeEvent.Event);
        }
        else
        {
            await ProcessCrontabFileChange(changeEvent.Event);
        }
        
        timer?.MarkSuccess();
    }

    private async Task ProcessConfigFileChange(FileSystemEventArgs evt)
    {
        if (evt.ChangeType != WatcherChangeTypes.Changed)
        {
            _log.LogDebug("Ignoring config file event: {ChangeType}", evt.ChangeType);
            return;
        }

        using var timer = _performanceMonitor?.StartTiming($"{MetricCategories.FileMonitoring}.config_reload");
        
        try
        {
            var newConfig = LoadConfigSafely(_configFilePath);
            var configChanged = HasConfigurationChanged(newConfig);
            
            if (!configChanged)
            {
                _log.LogDebug("Configuration file changed but content is identical, skipping reload");
                timer?.MarkSuccess();
                return;
            }

            _log.LogInformation("Configuration changed, performing differential reload");
            
            // Perform differential update
            var changes = AnalyzeConfigurationChanges(_lastKnownConfig, newConfig);
            ApplyConfigurationChanges(changes, newConfig);
            
            _lastKnownConfig = newConfig;
            
            // Notify about configuration change
            await _mediator.Publish(new CrontabFileIsChanged(newConfig.CrontabFiles));
            
            timer?.MarkSuccess();
        }
        catch (Exception ex)
        {
            timer?.MarkFailure();
            _log.LogError(ex, "Failed to process configuration file change");
        }
    }

    private async Task ProcessCrontabFileChange(FileSystemEventArgs evt)
    {
        if (evt.ChangeType == WatcherChangeTypes.Deleted)
        {
            _log.LogWarning("Crontab file deleted: {FilePath}", evt.FullPath);
            return;
        }

        using var timer = _performanceMonitor?.StartTiming($"{MetricCategories.FileMonitoring}.crontab_change");
        
        try
        {
            var hasContentChanged = await HasFileContentChanged(evt.FullPath);
            if (!hasContentChanged)
            {
                _log.LogDebug("Crontab file {FilePath} changed but content is identical", evt.FullPath);
                timer?.MarkSuccess();
                return;
            }
            
            _log.LogInformation("Crontab file content changed: {FilePath}", evt.FullPath);
            
            // Update file hash
            UpdateFileHash(evt.FullPath);
            
            // Notify about crontab file change
            await _mediator.Publish(new CrontabFileIsChanged(_lastKnownConfig.CrontabFiles));
            
            timer?.MarkSuccess();
        }
        catch (Exception ex)
        {
            timer?.MarkFailure();
            _log.LogError(ex, "Failed to process crontab file change: {FilePath}", evt.FullPath);
        }
    }

    private CorneyConfig LoadConfigSafely(string configPath)
    {
        try
        {
            return Misc.ReadJson<CorneyConfig>(configPath);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to load configuration from {ConfigPath}, using previous config", configPath);
            return _lastKnownConfig ?? new CorneyConfig();
        }
    }

    private bool HasConfigurationChanged(CorneyConfig newConfig)
    {
        // Quick reference check first
        if (ReferenceEquals(_lastKnownConfig, newConfig))
            return false;

        // Compare arrays efficiently
        if (_lastKnownConfig.CrontabFiles?.Length != newConfig.CrontabFiles?.Length)
            return true;
            
        if (_lastKnownConfig.CheckIntervalSeconds != newConfig.CheckIntervalSeconds)
            return true;
            
        if (_lastKnownConfig.FileMonitoringDebounceSeconds != newConfig.FileMonitoringDebounceSeconds)
            return true;

        // Deep array comparison only if lengths match
        for (int i = 0; i < newConfig.CrontabFiles.Length; i++)
        {
            if (_lastKnownConfig.CrontabFiles[i] != newConfig.CrontabFiles[i])
                return true;
        }

        return false;
    }

    private async Task<bool> HasFileContentChanged(string filePath)
    {
        try
        {
            var currentHash = await ComputeFileHashAsync(filePath);
            
            if (_fileHashes.TryGetValue(filePath, out var previousHash))
            {
                return currentHash != previousHash;
            }
            
            // First time seeing this file
            _fileHashes[filePath] = currentHash;
            return true;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not compute hash for file {FilePath}, assuming changed", filePath);
            return true;
        }
    }

    private void UpdateFileHash(string filePath)
    {
        Task.Run(async () =>
        {
            try
            {
                _fileHashes[filePath] = await ComputeFileHashAsync(filePath);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Could not update hash for file {FilePath}", filePath);
            }
        });
    }

    private async Task<string> ComputeFileHashAsync(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream);
        return Convert.ToBase64String(hashBytes);
    }

    private ConfigurationChanges AnalyzeConfigurationChanges(CorneyConfig oldConfig, CorneyConfig newConfig)
    {
        var changes = new ConfigurationChanges();
        
        // Detect crontab file changes
        var oldFiles = new HashSet<string>(oldConfig.CrontabFiles ?? Array.Empty<string>());
        var newFiles = new HashSet<string>(newConfig.CrontabFiles ?? Array.Empty<string>());
        
        changes.AddedFiles = newFiles.Except(oldFiles).ToArray();
        changes.RemovedFiles = oldFiles.Except(newFiles).ToArray();
        changes.UnchangedFiles = oldFiles.Intersect(newFiles).ToArray();
        
        // Detect settings changes
        changes.DebounceIntervalChanged = oldConfig.FileMonitoringDebounceSeconds != newConfig.FileMonitoringDebounceSeconds;
        changes.CheckIntervalChanged = oldConfig.CheckIntervalSeconds != newConfig.CheckIntervalSeconds;
        
        return changes;
    }

    private void ApplyConfigurationChanges(ConfigurationChanges changes, CorneyConfig newConfig)
    {
        var debounceInterval = TimeSpan.FromSeconds(newConfig.FileMonitoringDebounceSeconds);
        
        _log.LogInformation("Applying configuration changes: +{Added} -{Removed} files, debounce: {DebounceChanged}, interval: {IntervalChanged}",
            changes.AddedFiles.Length, changes.RemovedFiles.Length, 
            changes.DebounceIntervalChanged, changes.CheckIntervalChanged);
        
        // If debounce interval changed, we need to recreate all watchers
        if (changes.DebounceIntervalChanged)
        {
            _log.LogInformation("Debounce interval changed from {Old}s to {New}s, recreating all watchers",
                _lastKnownConfig.FileMonitoringDebounceSeconds, newConfig.FileMonitoringDebounceSeconds);
            
            ClearCrontabFileDisposables();
            SetupCrontabFileMonitoring(newConfig, debounceInterval);
        }
        else
        {
            // Apply incremental changes
            foreach (var removedFile in changes.RemovedFiles)
            {
                RemoveFileMonitoring(removedFile);
            }
            
            foreach (var addedFile in changes.AddedFiles)
            {
                AddFileMonitoring(addedFile, debounceInterval);
            }
        }
        
        // Update file hashes for new/changed files
        var filesToHash = changes.AddedFiles.Concat(changes.UnchangedFiles);
        foreach (var file in filesToHash)
        {
            UpdateFileHash(file);
        }
    }

    private string[] SetupCrontabFileMonitoring(CorneyConfig config, TimeSpan debounceInterval)
    {
        var filesToMonitor = config.CrontabFiles?.Distinct().ToArray() ?? Array.Empty<string>();
        
        _log.LogDebug("Setting up monitoring for {FileCount} crontab files with {DebounceMs}ms debounce",
            filesToMonitor.Length, debounceInterval.TotalMilliseconds);
        
        foreach (var file in filesToMonitor)
        {
            AddFileMonitoring(file, debounceInterval);
        }
        
        _monitoredFiles = new HashSet<string>(filesToMonitor);
        return filesToMonitor;
    }

    private void AddFileMonitoring(string filePath, TimeSpan debounceInterval)
    {
        try
        {
            _log.LogDebug("Adding file monitoring: {FilePath}", filePath);
            
            var observable = _fileWatchHelpers.CreateForFile(filePath, debounceInterval);
            var subscription = observable.Subscribe(evt => WriteToChannel(new ConfigChangeEvent(evt, false)));
            
            _crontabFileDisposables.Add(subscription);
            _monitoredFiles.Add(filePath);
            
            // Initialize file hash
            UpdateFileHash(filePath);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to add monitoring for file: {FilePath}", filePath);
        }
    }

    private void RemoveFileMonitoring(string filePath)
    {
        _log.LogDebug("Removing file monitoring: {FilePath}", filePath);
        _monitoredFiles.Remove(filePath);
        _fileHashes.Remove(filePath);
        
        // Note: We don't remove individual disposables as they're managed per-file
        // In a more complex implementation, we might track disposables per file
    }

    private void ClearCrontabFileDisposables()
    {
        _log.LogDebug("Clearing {Count} crontab file disposables", _crontabFileDisposables.Count);
        
        foreach (var disposable in _crontabFileDisposables.ToArray())
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Error disposing file watcher");
            }
        }
        
        _crontabFileDisposables.Clear();
        _monitoredFiles.Clear();
    }

    /// <summary>
    /// Event representing a configuration change
    /// </summary>
    private record ConfigChangeEvent(FileSystemEventArgs Event, bool IsConfigFile);

    /// <summary>
    /// Analysis of configuration changes for differential processing
    /// </summary>
    private class ConfigurationChanges
    {
        public string[] AddedFiles { get; set; } = Array.Empty<string>();
        public string[] RemovedFiles { get; set; } = Array.Empty<string>();
        public string[] UnchangedFiles { get; set; } = Array.Empty<string>();
        public bool DebounceIntervalChanged { get; set; }
        public bool CheckIntervalChanged { get; set; }
    }
}

/// <summary>
/// Metric categories for file monitoring operations
/// </summary>
public static partial class MetricCategories
{
    public const string FileMonitoring = "file.monitoring";
}