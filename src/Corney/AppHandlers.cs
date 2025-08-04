using System.Threading;
using System.Threading.Tasks;
using Corney.Features.App;
using Corney.Features.Monitors;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Corney;

public class AppHandlers : INotificationHandler<AppStartingEvent>
{
    private readonly ConfigFileMonitorService _configFileMonitorService;
    private readonly ILogger<AppHandlers> _log;
    private readonly CorneyRegistry _registry;
    
    public AppHandlers(ILogger<AppHandlers> log, CorneyRegistry registry,
        ConfigFileMonitorService configFileMonitorService)
    {
        _log = log;
        _registry = registry;
        _configFileMonitorService = configFileMonitorService;
    }


    public Task Handle(AppStartingEvent notification, CancellationToken cancellationToken)
    {
        _configFileMonitorService.Initialize();
        return Task.CompletedTask;
    }
}