using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using Corney.Common.Io;
using Corney.Features.App;
using Corney.Features.Monitors.Helpers;
using Corney.Features.Monitors.ReqRes;
using Deneblab.Common.Logging;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Corney.Features.Monitors.Services;

public class ConfigFileMonitorService : IDisposable
{
    private readonly List<IDisposable> _cfd = new();

    private readonly Channel<FileSystemEventArgs> _channel = Channel.CreateUnbounded<FileSystemEventArgs>();


    private readonly string _configFilePath;

    private readonly ILogger<ConfigFileMonitorService> _log;
    private readonly IMediator _mediator;
    private IObservable<FileSystemEventArgs> _configFileObservable;
    private int _counter;

    public ConfigFileMonitorService(ILogger<ConfigFileMonitorService> log, IMediator mediator, CorneyRegistry registry)
    {
        _log = log;
        _mediator = mediator;
        _configFilePath = registry.ConfigFilePath;
        Registry = registry;
        Task.Factory.StartNew(ChannelConsumerProcess, TaskCreationOptions.LongRunning);
    }

    public CorneyRegistry Registry { get; }

    public void Dispose()
    {
        _log.Debug("Dispose");
       // _configDisposable.Dispose();

        ClearDisposable();
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
        //_configFileObservable = FileWatchHelpers.CreateForFile(_configFilePath);
        //_configDisposable.Add(_configFileObservable.Subscribe(WriteToChannel));
        _ = MonitorFilesInConfig(_configFilePath);
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

                var files = MonitorFilesInConfig(_configFilePath);
                await _mediator.Publish(new CrontabFileIsChanged(files));
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
        var list = new HashSet<string>();

        ClearDisposable();


        var config = Misc.ReadJson<CorneyConfig>(file);
        var filesToObserve = config.CrontabFiles.Distinct().ToArray();
        _log.Debug($"Files to monitor: {filesToObserve.Length}");
        var cronFilesObservable = new List<IObservable<FileSystemEventArgs>>();
        foreach (var filesCrontabFile in filesToObserve)
        {
            _log.Debug($"File to monitor: {filesCrontabFile}");
           // var cronFile = FileWatchHelpers.CreateForFile(filesCrontabFile);
           // cronFilesObservable.Add(cronFile);
            list.Add(filesCrontabFile);
        }


        //var dis = cronFilesObservable.Merge().Subscribe(WriteToChannel);
        //_cfd.Add(dis);
        return list.ToArray();
    }
}