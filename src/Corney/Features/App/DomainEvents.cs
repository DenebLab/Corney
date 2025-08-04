using System;
using MediatR;

namespace Corney.Features.App;

public record MinuteExecutionNotification(DateTime ExecutionTime) : INotification;
public record AppStartingEvent : INotification;
public class StartCorneyReq : INotification
{
    public string[] CrontabFiles { get; }

    public StartCorneyReq(string[] crontabFiles)
    {
        CrontabFiles = crontabFiles;
    }
}

