using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Corney.Common.Extensions;
using Corney.Features.App;
using Corney.Features.Cron;
using Corney.Features.Processes;
using Deneblab.Common.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Corney.Core.Features.Cron.Service;

public class CronService : ICronService
{
    private readonly object _balanceLock = new();
    private readonly object _balanceLock2 = new();
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
        _log.Info($"Start CronService: {_corneyRegistry.AppVersion.Sem}");
        InitWork(crontabFiles);
    }

    public void Restart(string[] cronFiles)
    {
        _log.Info($"Restart CronService: {_corneyRegistry.AppVersion.Sem}");
        var c = _nextSchedule as CompositeDisposable;
        c?.Dispose();


        _nextSchedule?.Dispose();
        InitWork(cronFiles);
    }

    public void Stop()
    {
        _log.Info("Stop CronService");
        _nextSchedule?.Dispose();
    }

    private void InitWork(string[] cronFiles)
    {
        var marker = Guid.NewGuid();
        CreateListDefinitions(cronFiles);
        //LogNextItemToRun();
        var start = DateTime.UtcNow;
        var startDown = start.RoundDown(TimeSpan.FromSeconds(60));
        var next = startDown.AddMinutes(1);
        _log.Info($"Cron will be processing items from; Local: {next.ToLocalTime()};");
        WriteTasksToLog();
        _log.Debug("*** New round  ***");
        _log.Debug($"InitWork; Marker: {marker}");
        GenerateNext(next, marker);
        ScheduleNext(next, marker);
    }


    private void WriteTasksToLog()
    {
        var list = new List<(string, DateTimeOffset)>();
        var from = DateTimeOffset.Now;
        var to = DateTimeOffset.Now.AddHours(24);
        const int showNumberJobs = 2;
        _log.Debug(
            $"Jobs (first {showNumberJobs} tasks every job from crontab file) Range; form: {from} to: {to}; ");

        try
        {
            _log.Debug("Collect definitions to list");

            var definitions = _cronDefinitions.Values.SelectMany(x => x).ToList();

            _log.Debug($"Iterate definitions. Itames number: {definitions.Count}");

            foreach (var cronDefinition in definitions)
            {
                _log.Debug($"Definition: {cronDefinition.ExecutePart}");
                var occurrence = cronDefinition.Expression.GetOccurrences(
                    from,
                    to, TimeZoneInfo.Local).ToList();

                foreach (var dateTimeOffset in occurrence.OrderBy(x => x).Take(showNumberJobs))
                    list.Add((cronDefinition.ExecutePart, dateTimeOffset));
            }
        }
        catch (Exception e)
        {
            _log.Error("Error in WriteTasksToLog");
            _log.Error(e.Message);
            _log.Error(e);
        }

        foreach (var valueTuple in list.OrderBy(x => x.Item2))
            _log.Debug($"{valueTuple.Item2} - {valueTuple.Item1}");
    }


    private void Execute(DateTime date, Guid marker)
    {
        _log.Debug(
            $"Execute; Marker: {marker}; Time: {date.ToLocalTime()}; The number items to run: {_itemsToRunOnNextMinute.Count}");
        lock (_balanceLock)
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
            _log.Debug("*** Next round  ***");
            _log.Debug($"Old marker: {marker}; Next marker: {nextMarker}");
            GenerateNext(next, nextMarker);
            ScheduleNext(next, nextMarker);
        }
    }

    private void ScheduleNext(DateTime next, Guid marker)
    {
        _log.Debug($"ScheduleNext; Marker: {marker}; Set next time: {next.ToLocalTime()};");
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
        _log.Debug("Start GenerateNext");
        var nextLocal = next.ToLocalTime();
        var counter = 0;
        const int max = 10;

        while (true)
        {
            counter++;
            if (counter == max) break;
            _log.Debug($"GenerateNext; Try: {counter}");
            try
            {
                var list = _cronDefinitions
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
                _itemsToRunOnNextMinute.Clear();
                _itemsToRunOnNextMinute.AddRange(list);
                break;
            }
            catch (Exception e)
            {
                _log.Error($"Problem with GenerateNext; Try:{counter}; Message: {e.Message}");
                _log.Error(e.Message);
            }
        }

        _log.Debug("GenerateNext; " +
                   $"Marker: {marker}; " +
                   $"Next time: {nextLocal}; " +
                   $"Items to run on next: {_itemsToRunOnNextMinute.Count}");
    }

    private void CreateListDefinitions(string[] cronFiles)
    {
        try
        {
            _log.Debug($"CreateListDefinitions part 1; Files: {string.Join(" ", cronFiles)}");
            lock (_balanceLock2)
            {
                _log.Debug($"CreateListDefinitions part 2; Files: {string.Join(" ", cronFiles)}");
                _cronDefinitions.Clear();
                foreach (var crontabFile in cronFiles)
                {
                    var crontabFileParser = _serviceProvider.GetRequiredService<CrontabFileParser>();
                    var list = crontabFileParser.Read(crontabFile);
                    _cronDefinitions.Add(crontabFile, list);
                }
            }
        }
        catch (Exception e)
        {
            _log.Error("CreateListDefinitions error");
            _log.Error(e.Message);
            _log.Error(e);
        }
    }
}

public interface ICronService
{
    void Start(string[] crontabFiles);
    void Restart(string[] crontabFiles);
    void Stop();
}