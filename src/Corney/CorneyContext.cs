using System;
using System.Windows.Forms;
using Corney.Features.App;
using Corney.Properties;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Corney;

public class CorneyContext : ApplicationContext
{
    private readonly IMediator _mediator;
    private readonly NotifyIcon _notifyIcon;

    public CorneyContext(ILogger<CorneyContext> log, CorneyRegistry registry, IMediator mediator)
    {
        _mediator = mediator;
        var exitMenuItem = new ToolStripMenuItem("Exit", null, OnExit);
        var aboutMenuItem = new ToolStripMenuItem($"Corney - {registry.AppVersion.Sem}");

        _notifyIcon = new NotifyIcon
        {
            Icon = Resources.AppIco, // Upewnij się, że plik istnieje
            ContextMenuStrip = new ContextMenuStrip(),
            Text = $@"Corney - {registry.AppVersion.Sem}",
            Visible = true
        };

        _notifyIcon.ContextMenuStrip.Items.Add(aboutMenuItem);
        _notifyIcon.ContextMenuStrip.Items.Add(exitMenuItem);
        _notifyIcon.DoubleClick += (s, e) => MessageBox.Show("App is running in tray.");
    }

    private  void OnExit(object sender, EventArgs e)
    {

        _notifyIcon.Visible = false;
        Application.Exit();
    }
}