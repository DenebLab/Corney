using System;
using System.Threading;
using System.Threading.Tasks;
using Corney.Common.Logging;
using Deneblab.Common.Logging;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Corney.Features.App;

public class MinuteBackgroundService : BackgroundService
{
    private readonly ILogger<MinuteBackgroundService> _log;
    private readonly IMediator _mediator;

    public MinuteBackgroundService(ILogger<MinuteBackgroundService> log, IMediator mediator)
    {
        _log = log;
        _mediator = mediator;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.Info("MinuteBackgroundService starting");

        while (!stoppingToken.IsCancellationRequested)
            try
            {
                var now = DateTime.Now;
                var nextMinute = now.AddSeconds(-now.Second).AddMilliseconds(-now.Millisecond).AddMinutes(1);
                var delay = nextMinute - now;

                _log.LogDebug(LogMessages.NextExecutionScheduled, LogMessages.NextExecutionScheduledTemplate,
                    delay.TotalSeconds);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _log.Info("MinuteBackgroundService stopping");
                    return;
                }

                if (!stoppingToken.IsCancellationRequested) await ExecuteMainTask();
            }
            catch (OperationCanceledException)
            {
                _log.Info("MinuteBackgroundService stopping");
                return;
            }
            catch (Exception ex)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    _log.Info("MinuteBackgroundService stopping due to error");
                    return;
                }

                _log.LogError(ex, "Error in MinuteBackgroundService");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _log.Info("MinuteBackgroundService stopping during recovery");
                    return;
                }
            }

        _log.Info("MinuteBackgroundService stopped");
    }

    private async Task ExecuteMainTask()
    {
        var executionTime = DateTime.Now;
        await _mediator.Publish(new MinuteExecutionNotification(executionTime));
    }
}