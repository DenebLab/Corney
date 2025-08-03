using System;
using System.Windows.Forms;
using Corney.Features.App;
using Corney.Features.Cron.ReqRes;
using Corney.Properties;
using MediatR;

namespace Corney;

public class CorneyContext : ApplicationContext
{
    private readonly IMediator _mediator;
    private readonly NotifyIcon _notifyIcon;
    private readonly CorneyRegistry _registry;

    public CorneyContext(CorneyRegistry registry, IMediator mediator)
    {
        _registry = registry;
        _mediator = mediator;
        var exitMenuItem = new ToolStripMenuItem("Exit", null, OnExit);

        _notifyIcon = new NotifyIcon
        {
            Icon = Resources.AppIco, // Upewnij się, że plik istnieje
            ContextMenuStrip = new ContextMenuStrip(),
            Text = $"Corney - {_registry.AppVersion.Sem}",
            Visible = true
        };

        _notifyIcon.ContextMenuStrip.Items.Add(exitMenuItem);
        _notifyIcon.DoubleClick += (s, e) => MessageBox.Show("App is running in tray.");
    }

    private async void OnExit(object? sender, EventArgs e)
    {
        await _mediator.Publish(new StopCorneyReq());
        _notifyIcon.Visible = false;
        Application.Exit();
    }
}