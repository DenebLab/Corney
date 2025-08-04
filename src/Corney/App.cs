using Corney.Common.Logging;
using Corney.Common.Performance;
using Corney.Core.Features.Cron.Service;
using Corney.Features.App;
using Corney.Features.Cron;
using Corney.Features.Monitors;
using Corney.Features.Processes;
using Deneblab.Common.Host;
using Deneblab.Common.Logging;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZLogger.Providers;

namespace Corney;

internal static class App
{
    private static readonly DLabApp _dLabApp = DLabHost.CreateDLabAppBuilder()
        .CreateAppEnv()
        .CreateAppEnv(o => { o.PreferredAppsModes = [AppMode.Syrup, AppMode.Dev, AppMode.CurrentUserAppDir]; })
        .CreateLogManager(x => x.SetMinimumLevel(LogLevel.Debug))
#if DEBUG
        .AddDeveloperLogProvider(c =>
        {
            var env = DLabHost.CurrentAppEnv();
            c.FilePath = Path.Combine(env.LogDir, $"{env.AppNameSlug}.lowlevel.txt");
        })
#endif

        .Build();

    private static readonly AppEnv _appEnv = _dLabApp.GetAppEnv();
    private static readonly ILogger _log = _dLabApp.GetCurrentClassLogger();
    private static readonly ILoggerFactory _loggerFactory = _dLabApp.GetLoggerFactory();


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
        
        CorneyRegistry registry = null;
        try
        {
            var ab = new AppBuilder(_appEnv, _dLabApp.GetLoggerFactory());
            var config = await ab.CreateConfigAsync(_appEnv);
            registry = ab.GetRegistry(_appEnv, config);
            await MainAsyncNew(args, registry);
        }
        catch (Exception e)
        {
            _log.Error(e);
            _log.Error(e.Message);
            _log.Error(e?.InnerException);
            _log.Error(e?.InnerException?.Message);
            _log.Error(e?.StackTrace);
            _log.Error($"Env: {JsonSerializer.Serialize(registry,
                new JsonSerializerOptions { WriteIndented = true })}");
            //  MessageBox.Show(e.Message, "Critical ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        
        // Release mutex on the same thread that acquired it
        // Using statement will call Dispose() which handles proper cleanup
    }

    [SupportedOSPlatform("windows6.1")]
    public static async Task MainAsyncNew(string[] args, CorneyRegistry registry)
    {
        var host = Host.CreateDefaultBuilder(args)
            // Optionally, register additional services such as your WinForms main form.
            .ConfigureServices(services =>
            {
                services.AddSingleton(registry);
                services.AddTransient<ICronService, CronService>();
                services.AddTransient<ProcessWrapper>();
                services.AddHostedService<MinuteBackgroundService>();
                services.AddSingleton<ConfigFileMonitorService>();
                services.AddSingleton<FileWatchHelpers>();
                services.AddSingleton<CrontabFileParser>();

                // Add performance monitoring services
                services.AddPerformanceMonitoring(options =>
                {
                    options.ReportingInterval = TimeSpan.FromMinutes(15);
                    options.DashboardInterval = TimeSpan.FromHours(1);
                    options.EnableAutomaticReporting = true;
                });

                services.AddMediatR(cfg => { cfg.RegisterServicesFromAssembly(typeof(App).Assembly); });
            })
            .ConfigureLogging(l =>
            {
                l.ClearProviders();
                l.SetMinimumLevel(LogLevel.Debug);
                l.AddZLoggerArchivingRollingFile(o =>
                {
                    o.FilePathSelector = (dt, sequenceNumber) =>
                        Path.Combine(registry.AppEnv.LogDir,
                            $"{registry.AppEnv.AppNameSlug}.{dt:yyyy-MM-dd}.{sequenceNumber:000}.txt");
                    o.RollingInterval = RollingInterval.Day;
                    o.MaxArchiveFilesPerLog = 7;
                    o.ArchiveInactiveFiles = true; // Archive inactive files
                    // o.DebugLoggerFactory = _loggerFactory;

                    //o.EnableFileDeletionRecovery = true; // Enable recovery of deleted files
                    //o.AllowExternalFileDeletion = true; // Allow external deletion of log files
                    //o.AllowExternalFileReading = true;

                    if (_appEnv.IsDev) o.CloseFileAfterEachWrite = true; // Close file after each write
                });

                l.AddFilter("Microsoft.AspNetCore", level => level >= LogLevel.Warning);
                l.AddFilter("Microsoft.WebTools.BrowserLink", level => level >= LogLevel.Warning);
            })
            .Build();

        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();


        _log.LogInformation(LogMessages.ApplicationStarted, LogMessages.ApplicationStartedTemplate, registry.AppEnv.AppVersion.FullName);

        var mediator = host.Services.GetRequiredService<IMediator>();

        // Any configuration checks, initializations should be handled by this event
        // Most important start code is in the AppHandlers class. 
        // You may extend it as you wont. 
        await mediator.Publish(new AppStartingEvent());

        // And after 
        await mediator.Publish(new AppStartedEvent());

        await mediator.Publish(new StartCorneyReq(registry.CrontabFiles));


        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new CorneyContext(registry, mediator));
    }
}