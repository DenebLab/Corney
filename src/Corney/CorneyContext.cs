using Corney.Features.App;
using Corney.Properties;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Principal;
using System.Windows.Forms;

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
        var isAdminMenuItem = new ToolStripMenuItem($"Is Running As Administrator: {PrivilegeHelper.IsRunAsAdministrator()}");

        _notifyIcon = new NotifyIcon
        {
            Icon = Resources.AppIco, // Upewnij się, że plik istnieje
            ContextMenuStrip = new ContextMenuStrip(),
            Text = $@"Corney - {registry.AppVersion.Sem}",
            Visible = true
        };

        _notifyIcon.ContextMenuStrip.Items.Add(aboutMenuItem);
        _notifyIcon.ContextMenuStrip.Items.Add(isAdminMenuItem);
        _notifyIcon.ContextMenuStrip.Items.Add(exitMenuItem);
        _notifyIcon.DoubleClick += (s, e) => MessageBox.Show("App is running in tray.");
    }

    private  void OnExit(object sender, EventArgs e)
    {

        _notifyIcon.Visible = false;
        Application.Exit();
    }
}
public static class PrivilegeHelper
{
    public static bool IsRunAsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}