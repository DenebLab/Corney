using System.Threading;
using System.Threading.Tasks;
using Corney.Features.Cron.ReqRes;
using Corney.Features.Cron.Service;
using Corney.Features.Monitors.ReqRes;
using MediatR;

namespace Corney.Features.Cron.Handlers
{
    public class CronHandlers : INotificationHandler<CrontabFileIsChanged>, INotificationHandler<StartCorneyReq>
    {
        private readonly ICronService _cronService;

        public CronHandlers(ICronService cronService)
        {
            _cronService = cronService;
        }

        public Task Handle(CrontabFileIsChanged notification, CancellationToken cancellationToken)
        {
            _cronService.Restart(notification.CronFiles);
            return Task.CompletedTask;

        }


        public Task Handle(StartCorneyReq notification, CancellationToken cancellationToken)
        {
            _cronService.Start(notification.CrontabFiles);
            return Task.CompletedTask;
        }
    }
}
