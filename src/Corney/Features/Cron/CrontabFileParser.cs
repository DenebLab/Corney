using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cronos;
using Deneblab.Common.Logging;
using Microsoft.Extensions.Logging;

namespace Corney.Features.Cron;

public class CrontabFileParser
{
    private static readonly char[] _delimiterChars = { ' ', '\t' };
    private readonly ILogger<CrontabFileParser> _log;

    public CrontabFileParser(ILogger<CrontabFileParser> log)
    {
        _log = log;
    }

    public List<CronDefinition> Read(string crontabFile)
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
        var counter = 0;
        const int max = 10;
        while (counter <= max)
        {
            counter++;
            try
            {
                return File.ReadAllLines(crontabFile);
            }
            catch (Exception)
            {
                _log.Warn($"SEE-1269; File not available [{counter}/{max}]; Path: {crontabFile}");
            }

            Thread.Sleep(300);
        }

        return new string[] { };
    }

    private async Task<string[]> StringsAsync(string crontabFile)
    {
        var counter = 0;
        const int max = 10;
        while (counter <= max)
        {
            counter++;
            try
            {
                return await File.ReadAllLinesAsync(crontabFile);
            }
            catch (Exception)
            {
                _log.Warn($"SEE-1269; File not available [{counter}/{max}]; Path: {crontabFile}");
            }

            await Task.Delay(300);
        }

        return new string[] { };
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