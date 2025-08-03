using MediatR;

namespace Corney.Features.Monitors.ReqRes
{
    public class CrontabFileIsChanged : INotification
    {
        public string[] CronFiles { get; }

        public CrontabFileIsChanged(string[] cronFiles)
        {
            CronFiles = cronFiles;
        }
    }
}