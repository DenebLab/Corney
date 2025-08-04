using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
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

public class ConfigFileMonitorService : IConfigFileMonitor
{
    private readonly List<IDisposable> _cfd = [];

    private readonly Channel<FileSystemEventArgs> _channel = Channel.CreateUnbounded<FileSystemEventArgs>();

    private readonly CompositeDisposable _configDisposable = new();
    private readonly string _configFilePath;
    private readonly FileWatchHelpers _fileWatchHelpers;
    private readonly PerformanceMonitor _performanceMonitor;

    private readonly ILogger<ConfigFileMonitorService> _log;
    private readonly IMediator _mediator;
    private IObservable<FileSystemEventArgs> _configFileObservable;
    private int _counter;
    private CorneyConfig _lastConfig = new();
    private DateTime _lastReload = DateTime.MinValue;

    public ConfigFileMonitorService(ILogger<ConfigFileMonitorService> log, IMediator mediator,
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
        _log.LogDebug(LogMessages.ServiceStopping, "ConfigFileMonitorService disposed");
        _configDisposable.Dispose();
        ClearDisposable();
        
        // Complete the channel
        _channel.Writer.Complete();
    }

    private async Task ChannelConsumerProcess()
    {
        while (await _channel.Reader.WaitToReadAsync())
        {
            _log.Debug("New message in channel");
            if (_channel.Reader.TryRead(out var msg))
                try
                {
                    _log.Debug($"Read from channel; File: {msg.FullPath} - OK");
                    await ProcessOneConfigFileEvent(msg);
                }
                catch (Exception e)
                {
                    _log.Error(e.Message);
                    _log.Error(e.InnerException?.Message);
                    _log.Error(e);
                }
            else
                _log.Debug("Read from channel; FAIL");
        }
    }

    private void WriteToChannel(FileSystemEventArgs e)
    {
        _log.Debug(_channel.Writer.TryWrite(e)
            ? $"Write to channel: File: {e.FullPath} - OK"
            : $"Write to channel: File: {e.FullPath} - FAIL");
    }

    private void ClearDisposable()
    {
        _log.Debug("Begin ClearDisposable");
        //var index = 0;
        try
        {
            //_log.Debug("Make clone");
            var clone = _cfd.ToArray();
            //_log.Debug($"Clone lenght: {clone.Length}");
            //_log.Debug("Begin dispose");
            foreach (var disposable in clone)
                //index++;
                //  _log.Debug($"Dispose: {index}");
                disposable.Dispose();

            // _log.Debug("End dispose");
        }
        catch (Exception e)
        {
            _log.Error(e.Message);
            _log.Error(e.InnerException?.Message);
            _log.Error(e);
        }

        try
        {
            //_log.Debug("Clear _cfd");
            _cfd.Clear();
        }
        catch (Exception e)
        {
            _log.Error(e.Message);
            _log.Error(e.InnerException?.Message);
            _log.Error(e);
        }

        _log.Debug("End ClearDisposable");
    }

    public void Initialize()
    {
        using var timer = _performanceMonitor?.StartTiming($"{MetricCategories.FileMonitoring}.initialize");
        
        try
        {
            // Load initial config to get debounce settings
            _lastConfig = LoadConfigSafely(_configFilePath);
            var debounceInterval = TimeSpan.FromSeconds(_lastConfig.FileMonitoringDebounceSeconds);
            
            _configFileObservable = _fileWatchHelpers.CreateForFile(_configFilePath, debounceInterval);
            _configDisposable.Add(_configFileObservable.Subscribe(WriteToChannel));
            _ = MonitorFilesInConfig(_configFilePath);
            
            _log.LogInformation("ConfigFileMonitorService initialized with {DebounceMs}ms debounce", 
                debounceInterval.TotalMilliseconds);
                
            timer?.MarkSuccess();
        }
        catch (Exception ex)
        {
            timer?.MarkFailure();
            _log.LogError(ex, "Failed to initialize ConfigFileMonitorService");
            throw;
        }
    }


    private async Task ProcessOneConfigFileEvent(FileSystemEventArgs x)
    {
        _log.Debug(
            "ProcessOneConfigFileEvent; " +
            $"Counter: {_counter++}; " +
            $"Type: {x.ChangeType}; " +
            $"FullPath: {x.FullPath}; " +
            $"Name: {x.Name}");
        switch (x.ChangeType)
        {
            case WatcherChangeTypes.Created:
                break;
            case WatcherChangeTypes.Deleted:
                break;
            case WatcherChangeTypes.Changed:
                using (var timer = _performanceMonitor?.StartTiming($"{MetricCategories.FileMonitoring}.config_reload"))
                {
                    // Implement basic change detection to avoid unnecessary reloads
                    var newConfig = LoadConfigSafely(_configFilePath);
                    var configChanged = HasConfigurationChanged(newConfig);
                    
                    if (!configChanged && DateTime.UtcNow - _lastReload < TimeSpan.FromSeconds(1))
                    {
                        _log.LogDebug("Configuration file changed but content appears identical, skipping reload");
                        timer?.MarkSuccess();
                        break;
                    }
                    
                    _lastConfig = newConfig;
                    _lastReload = DateTime.UtcNow;
                    
                    var files = MonitorFilesInConfig(_configFilePath);
                    await _mediator.Publish(new CrontabFileIsChanged(files));
                    
                    timer?.MarkSuccess();
                }
                break;
            case WatcherChangeTypes.Renamed:
                break;
            case WatcherChangeTypes.All:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }


    private string[] MonitorFilesInConfig(string file)
    {
        using var timer = _performanceMonitor?.StartTiming($"{MetricCategories.FileMonitoring}.setup_monitoring");
        
        var list = new HashSet<string>();
        ClearDisposable();

        try
        {
            var config = LoadConfigSafely(file);
            var filesToObserve = config.CrontabFiles.Distinct().ToArray();
            var debounceInterval = TimeSpan.FromSeconds(config.FileMonitoringDebounceSeconds);
            
            _log.LogDebug("Files to monitor: {FileCount} with {DebounceMs}ms debounce", 
                filesToObserve.Length, debounceInterval.TotalMilliseconds);
            
            var cronFilesObservable = new List<IObservable<FileSystemEventArgs>>();
            
            foreach (var filesCrontabFile in filesToObserve)
            {
                _log.LogDebug("File to monitor: {FilePath}", filesCrontabFile);
                var cronFile = _fileWatchHelpers.CreateForFile(filesCrontabFile, debounceInterval);
                cronFilesObservable.Add(cronFile);
                list.Add(filesCrontabFile);
            }

            if (cronFilesObservable.Any())
            {
                var dis = cronFilesObservable.Merge().Subscribe(WriteToChannel);
                _cfd.Add(dis);
            }
            
            timer?.MarkSuccess();
            return list.ToArray();
        }
        catch (Exception ex)
        {
            timer?.MarkFailure();
            _log.LogError(ex, "Failed to setup file monitoring");
            return Array.Empty<string>();
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
            _log.LogWarning(ex, "Failed to load configuration from {ConfigPath}, using previous config", configPath);
            return _lastConfig ?? new CorneyConfig();
        }
    }
    
    private bool HasConfigurationChanged(CorneyConfig newConfig)
    {
        // Quick reference check
        if (ReferenceEquals(_lastConfig, newConfig))
            return false;

        // Compare key properties
        if (_lastConfig.FileMonitoringDebounceSeconds != newConfig.FileMonitoringDebounceSeconds)
            return true;
            
        if (_lastConfig.CheckIntervalSeconds != newConfig.CheckIntervalSeconds)
            return true;

        // Compare crontab files array
        if (_lastConfig.CrontabFiles?.Length != newConfig.CrontabFiles?.Length)
            return true;
            
        if (_lastConfig.CrontabFiles != null && newConfig.CrontabFiles != null)
        {
            for (int i = 0; i < newConfig.CrontabFiles.Length; i++)
            {
                if (_lastConfig.CrontabFiles[i] != newConfig.CrontabFiles[i])
                    return true;
            }
        }

        return false;
    }
}