using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Corney.Features.App;
using Deneblab.Common.Host;
using Microsoft.Extensions.Logging;

namespace Corney;

internal class App
{
    private static readonly DLabApp _dLabApp = DLabHost.CreateDLabAppBuilder()
        .CreateAppEnv(o => { o.PreferredAppsModes = [AppMode.Syrup, AppMode.Dev, AppMode.ProcessPathDir]; })
        .CreateLogManager(x => x.SetMinimumLevel(LogLevel.Debug))
        .AddDeveloperLogProvider(c =>
        {
            var env = DLabHost.CurrentAppEnv();
            c.FilePath = Path.Combine(env.LogDir, $"{env.AppNameSlug}.lowlevel.txt");
        })
        .Build();

    private static readonly AppEnv _appEnv = _dLabApp.GetAppEnv();



    /// <summary>
    ///     The main entry point for the application.
    /// </summary>
    [STAThread]
    [SupportedOSPlatform("windows6.1")]
    public static async Task Main(string[] args)
    {
        const string mutexName = "Global\\Corney_SingleInstance";
        using var mutex = new Mutex(true, mutexName, out var createdNew);
        if (!createdNew) return;
        var ab = new AppBuilder(_appEnv, _dLabApp.GetLoggerFactory());
        await ab.MainLowLevel(args);
    }
}