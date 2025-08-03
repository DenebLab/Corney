using MediatR;

namespace Corney.Features.Monitors;

public class CrontabFileIsChanged : INotification
{
    public CrontabFileIsChanged(string[] cronFiles)
    {
        CronFiles = cronFiles;
    }

    public string[] CronFiles { get; }
}