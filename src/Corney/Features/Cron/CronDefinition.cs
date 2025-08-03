using System;
using Cronos;

namespace Corney.Features.Cron;

public class CronDefinition
{
    public CronDefinition(string cronPart, string executePart, CronExpression expression, DateTime? next)
    {
        CronPart = cronPart;
        ExecutePart = executePart;
        Expression = expression;
        Next = next;
    }

    public string CronPart { get; set; }
    public string ExecutePart { get; set; }
    public CronExpression Expression { get; }
    public DateTime? Next { get; }
}