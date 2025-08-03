using MediatR;

namespace Corney.Features.Cron.ReqRes
{
    public class StartCorneyReq : INotification
    {
        public string[] CrontabFiles { get; }

        public StartCorneyReq(string[] crontabFiles)
        {
            CrontabFiles = crontabFiles;
        }
    }
}