using System;
using System.Drawing;
using System.Security.Principal;
using System.Windows.Forms;
using Corney.Features.App;
using Corney.Properties;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Corney;

public class CorneyContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;

    public CorneyContext(ILogger<CorneyContext> log, CorneyRegistry registry, IMediator mediator,
        IConfigFileMenuService configFileMenuService)
    {
        var exitMenuItem = new ToolStripMenuItem("Exit", null, OnExit);
        var aboutMenuItem = new ToolStripMenuItem($"Corney - {registry.AppVersion.Sem}");
        var isAdminMenuItem =
            new ToolStripMenuItem($"Is Running As Administrator: {PrivilegeHelper.IsRunAsAdministrator()}");


        var icon = PrivilegeHelper.IsRunAsAdministrator()
            ? Resources.ResourceManager.GetObject("clock_red") as Icon
            : Resources.ResourceManager.GetObject("clock_green") as Icon;

        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            ContextMenuStrip = new ContextMenuStrip(),
            Text = $@"Corney - {registry.AppVersion.Sem}",
            Visible = true
        };

        _notifyIcon.ContextMenuStrip.Items.Add(aboutMenuItem);
        _notifyIcon.ContextMenuStrip.Items.Add(isAdminMenuItem);
        _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        _notifyIcon.ContextMenuStrip.Items.Add(configFileMenuService.CreateConfigFilesMenu(registry));
        _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        _notifyIcon.ContextMenuStrip.Items.Add(exitMenuItem);
        _notifyIcon.DoubleClick += (s, e) => MessageBox.Show("App is running in tray.");
    }

    private void OnExit(object sender, EventArgs e)
    {
        _notifyIcon.Visible = false;
        Application.Exit();
    }
}

public static class PrivilegeHelper
{
    public static bool IsRunAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}