using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Corney.Common.Performance;
using Corney.Common.Resilience;
using Cronos;
using Deneblab.Common.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Corney.Features.Cron;

public class CrontabFileParser
{
    private static readonly char[] _delimiterChars = { ' ', '\t' };
    private readonly ILogger<CrontabFileParser> _log;
    private readonly ResilientFileReader _resilientFileReader;
    private readonly PerformanceMonitor _performanceMonitor;

    public CrontabFileParser(ILogger<CrontabFileParser> log, IServiceProvider serviceProvider)
    {
        _log = log;
        // Create resilient file reader with aggressive retry for critical crontab files
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var resilientLogger = loggerFactory.CreateLogger<ResilientFileReader>();
        _resilientFileReader = ResilientFileReaderFactory.CreateAggressive(resilientLogger);
        
        // Get performance monitor if available
        serviceProvider.TryGetService(out _performanceMonitor);
    }

    public List<CronDefinition> Read(string crontabFile)
    {
        if (_performanceMonitor != null)
        {
            return _performanceMonitor.RecordExecution($"{MetricCategories.FileRead}.crontab", () => ReadInternal(crontabFile));
        }
        
        return ReadInternal(crontabFile);
    }

    private List<CronDefinition> ReadInternal(string crontabFile)
    {
        var l = new List<CronDefinition>();
        var s = Strings(crontabFile);

        var source = s.Where(x => !string.IsNullOrEmpty(x))
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrEmpty(x) && !x.StartsWith("#"))
            .ToArray();

        var m =
            $"Valid line in config: {crontabFile}{Environment.NewLine}{string.Join(Environment.NewLine, source)}";
        _log.Debug(m);

        var index = 1;
        foreach (var s1 in source)
        {
            try
            {
                if (string.IsNullOrEmpty(s1) || s1.StartsWith("#")) continue;
                var arr = s1.Split(_delimiterChars, 6, StringSplitOptions.RemoveEmptyEntries);
                var executePart = arr[5];
                var cronPart = string.Join(" ", arr.Take(5));
                var expression = CronExpression.Parse(cronPart, CronFormat.Standard);
                var next = expression.GetNextOccurrence(DateTime.UtcNow);
                var r = new CronDefinition(cronPart, executePart, expression, next);
                WriteToLog(s1, r);
                l.Add(r);
            }
            catch (Exception e)
            {
                _log.Error(e, $"Problem with file: {crontabFile}; Line number: {index}; Line text: '{s1}'");
                _log.Error(e);
            }

            index++;
        }

        return l;
    }

    public async Task<List<CronDefinition>> ReadAsync(string crontabFile)
    {
        if (_performanceMonitor != null)
        {
            return await _performanceMonitor.RecordExecutionAsync($"{MetricCategories.FileRead}.crontab_async", () => ReadAsyncInternal(crontabFile));
        }
        
        return await ReadAsyncInternal(crontabFile);
    }

    private async Task<List<CronDefinition>> ReadAsyncInternal(string crontabFile)
    {
        var l = new List<CronDefinition>();
        var s = await StringsAsync(crontabFile);

        var source = s.Where(x => !string.IsNullOrEmpty(x))
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrEmpty(x) && !x.StartsWith("#"))
            .ToArray();

        var m =
            $"Valid line in config: {crontabFile}{Environment.NewLine}{string.Join(Environment.NewLine, source)}";
        _log.Debug(m);

        var index = 1;
        foreach (var s1 in source)
        {
            try
            {
                if (string.IsNullOrEmpty(s1) || s1.StartsWith("#")) continue;
                var arr = s1.Split(_delimiterChars, 6, StringSplitOptions.RemoveEmptyEntries);
                var executePart = arr[5];
                var cronPart = string.Join(" ", arr.Take(5));
                var expression = CronExpression.Parse(cronPart, CronFormat.Standard);
                var next = expression.GetNextOccurrence(DateTime.UtcNow);
                var r = new CronDefinition(cronPart, executePart, expression, next);
                WriteToLog(s1, r);
                l.Add(r);
            }
            catch (Exception e)
            {
                _log.Error(e, $"Problem with file: {crontabFile}; Line number: {index}; Line text: '{s1}'");
                _log.Error(e);
            }

            index++;
        }

        return l;
    }

    private string[] Strings(string crontabFile)
    {
        try
        {
            return _resilientFileReader.ReadAllLines(crontabFile);
        }
        catch (RetryExhaustedException ex)
        {
            _log.LogError(ex, "Failed to read crontab file {FilePath} after all retry attempts", crontabFile);
            return new string[] { };
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unexpected error reading crontab file {FilePath}", crontabFile);
            return new string[] { };
        }
    }

    private async Task<string[]> StringsAsync(string crontabFile)
    {
        try
        {
            return await _resilientFileReader.ReadAllLinesAsync(crontabFile);
        }
        catch (RetryExhaustedException ex)
        {
            _log.LogError(ex, "Failed to read crontab file {FilePath} after all retry attempts", crontabFile);
            return new string[] { };
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unexpected error reading crontab file {FilePath}", crontabFile);
            return new string[] { };
        }
    }

    private void WriteToLog(string definition, CronDefinition cronDefinition)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Parse result:");
        sb.AppendLine($"Line: {definition.Replace('\t', ' ').Replace("  ", " ")}");
        sb.AppendLine($"Cron part: {cronDefinition.CronPart}");
        sb.Append($"Next: {cronDefinition.Next?.ToString("yyyy-MM-ddTHH:mm:ssZ")}");
        _log.Debug(sb.ToString());
    }
}