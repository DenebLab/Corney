using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Corney.Common.Extensions;
using Corney.Common.Logging;
using Corney.Features.App;
using Corney.Features.Cron;
using Corney.Features.Processes;
using Deneblab.Common.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Corney.Core.Features.Cron.Service;

public class CronService : ICronService, IDisposable
{
    private readonly ReaderWriterLockSlim _stateLock = new(LockRecursionPolicy.SupportsRecursion);
    private readonly CorneyRegistry _corneyRegistry;

    private readonly Dictionary<string, List<CronDefinition>> _cronDefinitions = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<CronDefinition> _itemsToRunOnNextMinute = new();

    private readonly ILogger<CronService> _log;
    private readonly IServiceProvider _serviceProvider;
    private IDisposable _nextSchedule;

    public CronService(ILogger<CronService> log, IServiceProvider serviceProvider, CorneyRegistry corneyRegistry)
    {
        _log = log;
        _serviceProvider = serviceProvider;
        _corneyRegistry = corneyRegistry;
    }

    public void Start(string[] crontabFiles)
    {
        _log.LogInformation(LogMessages.CronServiceStarted, LogMessages.CronServiceStartedTemplate, _corneyRegistry.AppVersion.Sem);
        InitWork(crontabFiles);
    }

    public void Restart(string[] cronFiles)
    {
        _log.LogInformation(LogMessages.CronServiceRestarted, LogMessages.CronServiceRestartedTemplate, _corneyRegistry.AppVersion.Sem);
        var c = _nextSchedule as CompositeDisposable;
        c?.Dispose();


        _nextSchedule?.Dispose();
        InitWork(cronFiles);
    }

    public void Stop()
    {
        _log.LogInformation(LogMessages.CronServiceStopped, LogMessages.CronServiceStoppedTemplate);
        _nextSchedule?.Dispose();
    }

    public void Dispose()
    {
        _log.LogDebug("Disposing CronService");
        _nextSchedule?.Dispose();
        _stateLock?.Dispose();
    }

    private void InitWork(string[] cronFiles)
    {
        var marker = Guid.NewGuid();
        CreateListDefinitions(cronFiles);
        //LogNextItemToRun();
        var start = DateTime.UtcNow;
        var startDown = start.RoundDown(TimeSpan.FromSeconds(60));
        var next = startDown.AddMinutes(1);
        _log.LogInformation("Cron will be processing items from local time {NextExecutionTime}", next.ToLocalTime());
        WriteTasksToLog();
        _log.Debug("*** New round  ***");
        _log.LogDebug("InitWork started with marker {Marker}", marker);
        GenerateNext(next, marker);
        ScheduleNext(next, marker);
    }


    private void WriteTasksToLog()
    {
        var list = new List<(string, DateTimeOffset)>();
        var from = DateTimeOffset.Now;
        var to = DateTimeOffset.Now.AddHours(24);
        const int showNumberJobs = 2;
        _log.LogDebug(
            "Displaying first {JobCount} tasks from each crontab file in range {FromTime} to {ToTime}", 
            showNumberJobs, from, to);

        try
        {
            _log.LogDebug("Collecting definitions to list");

            var definitions = _cronDefinitions.Values.SelectMany(x => x).ToList();

            _log.LogDebug("Iterating {DefinitionCount} cron definitions", definitions.Count);

            foreach (var cronDefinition in definitions)
            {
                _log.LogDebug("Processing definition: {ExecutePart}", cronDefinition.ExecutePart);
                var occurrence = cronDefinition.Expression.GetOccurrences(
                    from,
                    to, TimeZoneInfo.Local).ToList();

                foreach (var dateTimeOffset in occurrence.OrderBy(x => x).Take(showNumberJobs))
                    list.Add((cronDefinition.ExecutePart, dateTimeOffset));
            }
        }
        catch (Exception e)
        {
            _log.LogError(e, "Error occurred in WriteTasksToLog: {ErrorMessage}", e.Message);
        }

        foreach (var valueTuple in list.OrderBy(x => x.Item2))
            _log.LogDebug("Scheduled: {ScheduledTime} - {Command}", valueTuple.Item2, valueTuple.Item1);
    }


    private void Execute(DateTime date, Guid marker)
    {
        _log.LogDebug(LogMessages.CronExecutionStarted, LogMessages.CronExecutionStartedTemplate, 
            marker, date.ToLocalTime(), _itemsToRunOnNextMinute.Count);
        
        if (!_stateLock.TryEnterWriteLock(TimeSpan.FromSeconds(30)))
        {
            _log.LogError(LogMessages.CronLockTimeout, LogMessages.CronLockTimeoutTemplate, "write", "Execute");
            return;
        }
        
        try
        {
            if (_itemsToRunOnNextMinute.Any())
            {
                foreach (var cronDefinition in _itemsToRunOnNextMinute)
                {
                    var processWrapper = _serviceProvider.GetRequiredService<ProcessWrapper>();
                    var t1 = Pharse.Tokenize2(cronDefinition.ExecutePart);
                    processWrapper.Start(t1);
                }

                _itemsToRunOnNextMinute.Clear();
                //LogNextItemToRun();
            }

            var next = date.AddMinutes(1);
            var nextMarker = Guid.NewGuid();
            _log.LogDebug("Starting next cron execution round");
            _log.LogDebug(LogMessages.CronExecutionCompleted, LogMessages.CronExecutionCompletedTemplate, marker, nextMarker);
            GenerateNext(next, nextMarker);
            ScheduleNext(next, nextMarker);
        }
        finally
        {
            _stateLock.ExitWriteLock();
        }
    }

    private void ScheduleNext(DateTime next, Guid marker)
    {
        _log.LogDebug(LogMessages.CronNextScheduled, LogMessages.CronNextScheduledTemplate, marker, next.ToLocalTime());
        var dateTimeOffset = new DateTimeOffset(next);
        if (_nextSchedule != null)
        {
            var c = _nextSchedule as CompositeDisposable;
            c?.Dispose();
        }


        _nextSchedule = Observable
            .Timer(dateTimeOffset, Scheduler.CurrentThread)
            .Timestamp()
            .FirstAsync()
            .Subscribe(x =>
            {
                Task.Run(() => { Execute(next, marker); });
                //Execute(next);
            });
    }

    private void GenerateNext(DateTime next, Guid marker)
    {
        // Dates in cron file are in "local time". We should convert next to local time. 
        _log.LogDebug("Starting GenerateNext operation");
        var nextLocal = next.ToLocalTime();
        var counter = 0;
        const int max = 10;

        while (true)
        {
            counter++;
            if (counter == max) break;
            _log.LogDebug("GenerateNext attempt {Attempt}", counter);
            try
            {
                // Read operation - use read lock for accessing _cronDefinitions
                if (!_stateLock.TryEnterReadLock(TimeSpan.FromSeconds(10)))
                {
                    _log.LogError(LogMessages.CronLockTimeout, LogMessages.CronLockTimeoutTemplate, "read", "GenerateNext");
                    break;
                }

                List<CronDefinition> list;
                try
                {
                    list = _cronDefinitions
                        .SelectMany(x => x.Value)
                        .Where(x =>
                        {
                            // ReSharper disable once CommentTypo
                            // Read more: https://github.com/HangfireIO/Cronos#working-with-time-zones
                            var n1 = x.Expression.GetNextOccurrence(DateTimeOffset.Now, TimeZoneInfo.Local);
                            var nextLocalTime = n1?.DateTime;
                            return nextLocalTime == nextLocal;
                        })
                        .Distinct()
                        .ToList();
                }
                finally
                {
                    _stateLock.ExitReadLock();
                }

                _itemsToRunOnNextMinute.Clear();
                _itemsToRunOnNextMinute.AddRange(list);
                break;
            }
            catch (Exception e)
            {
                _log.LogError(LogMessages.CronGenerateNextError, LogMessages.CronGenerateNextErrorTemplate, counter, e.Message);
                _log.Error(e.Message);
            }
        }

        _log.LogDebug(LogMessages.CronTasksScheduled, LogMessages.CronTasksScheduledTemplate, 
            marker, nextLocal, _itemsToRunOnNextMinute.Count);
    }

    private void CreateListDefinitions(string[] cronFiles)
    {
        try
        {
            _log.LogDebug("CreateListDefinitions starting for files: {CronFiles}", string.Join(", ", cronFiles));
            
            if (!_stateLock.TryEnterWriteLock(TimeSpan.FromSeconds(30)))
            {
                _log.LogError(LogMessages.CronLockTimeout, LogMessages.CronLockTimeoutTemplate, "write", "CreateListDefinitions");
                return;
            }
            
            try
            {
                _log.LogDebug(LogMessages.CronDefinitionsLoaded, LogMessages.CronDefinitionsLoadedTemplate, string.Join(", ", cronFiles));
                _cronDefinitions.Clear();
                foreach (var crontabFile in cronFiles)
                {
                    var crontabFileParser = _serviceProvider.GetRequiredService<CrontabFileParser>();
                    var list = crontabFileParser.Read(crontabFile);
                    _cronDefinitions.Add(crontabFile, list);
                }
            }
            finally
            {
                _stateLock.ExitWriteLock();
            }
        }
        catch (Exception e)
        {
            _log.LogError(e, "Error in CreateListDefinitions operation: {ErrorMessage}", e.Message);
        }
    }
}

public interface ICronService
{
    void Start(string[] crontabFiles);
    void Restart(string[] crontabFiles);
    void Stop();
}