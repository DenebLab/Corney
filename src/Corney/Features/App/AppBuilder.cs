using System;
using System.IO;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Corney.Common.Diagnostics;
using Corney.Common.Io;
using Corney.Common.Logging;
using Corney.Common.Performance;
using Corney.Core.Features.Cron.Service;
using Corney.Features.Cron;
using Corney.Features.Monitors;
using Corney.Features.Processes;
using Deneblab.Common.Host;
using Deneblab.Common.Logging;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZLogger;
using ZLogger.Providers;

namespace Corney.Features.App;

public class AppBuilder
{
    private readonly AppEnv _env;
    private readonly ILogger<AppBuilder> _log;

    public AppBuilder(AppEnv env, ILoggerFactory getLoggerFactory)
    {
        _env = env;
        _log = getLoggerFactory.CreateLogger<AppBuilder>();
    }

    public async Task MainLowLevel(string[] args)
    {
        try
        {
            var config = await CreateConfigAsync(_env);
            var registry = CreateRegistry(_env, config);
            var host = CreateHost(args, registry);
            await MainRegular(host);
        }
        catch (Exception e)
        {
            _log.Critical(e);
            _log.Critical(e.StackTrace);
            _log.Critical($"Env: {JsonSerializer.Serialize(_env,
                new JsonSerializerOptions { WriteIndented = true })}");
        }
    }

    private async Task MainRegular(IHost host)
    {
        var log = host.Services.GetRequiredService<ILogger<AppBuilder>>();
        var registry = host.Services.GetRequiredService<CorneyRegistry>();
        try
        {
            await Process(host, log, registry);
        }
        catch (Exception e)
        {
            log.Critical(e);
            log.Critical(e.StackTrace);
            log.Critical($"Env: {JsonSerializer.Serialize(_env,
                new JsonSerializerOptions { WriteIndented = true })}");
        }
    }

    private IHost CreateHost(string[] args, CorneyRegistry registry)
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
                services.AddSingleton<CorneyContext>();

                // Add config file menu services
                services.AddTransient<IFileOperationHelper, FileOperationHelper>();
                services.AddTransient<IConfigFileMenuService, ConfigFileMenuService>();

                // Add performance monitoring services
                services.AddPerformanceMonitoring(options =>
                {
                    options.ReportingInterval = TimeSpan.FromMinutes(15);
                    options.DashboardInterval = TimeSpan.FromHours(1);
                    options.EnableAutomaticReporting = true;
                });

                // Add development and diagnostics services
                services.AddSingleton<DevelopmentValidator>();

                // Enhanced logging with correlation context
                services.AddTransient(typeof(EnhancedLogger<>));

                services.AddMediatR(cfg => { cfg.RegisterServicesFromAssembly(typeof(AppBuilder).Assembly); });
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

                    if (registry.AppEnv.IsDev) o.CloseFileAfterEachWrite = true; // Close file after each write
                });
            })
            .Build();

        return host;
    }

    private async Task<CorneyConfig> CreateConfigAsync(AppEnv env)
    {
        var configPath = ConfigPath(env);
        CorneyConfig config;

        if (File.Exists(configPath))
        {
            _log.Trace($"Using existing config file at {configPath}");
            config = await Misc.ReadJsonAsync<CorneyConfig>(configPath);
        }
        else
        {
            _log.Trace($"Creating new config file at {configPath}");
            config = new CorneyConfig();
            if (Directory.Exists(env.ConfigDir) == false) Directory.CreateDirectory(env.ConfigDir);
            await Misc.WriteJsonAsync(configPath, config);
        }

        // Validate configuration
        var validationResult = ConfigurationValidator.ValidateConfiguration(config, _log);

        if (!validationResult.IsValid)
        {
            var errorMessage = validationResult.GetFormattedErrorMessage();
            _log.LogError("Configuration validation failed:{NewLine}{ErrorMessage}", Environment.NewLine, errorMessage);
            throw new InvalidOperationException($"Configuration validation failed:{Environment.NewLine}{errorMessage}");
        }

        // Log warnings if any
        if (validationResult.Warnings.Count > 0)
            foreach (var warning in validationResult.Warnings)
                _log.LogWarning("Configuration warning - {PropertyName}: {WarningMessage}",
                    warning.PropertyName, warning.WarningMessage);

        return config;
    }

    [SupportedOSPlatform("windows6.1")]
    private static async Task Process(IHost host, ILogger log, CorneyRegistry registry)
    {
        log.LogInformation(LogMessages.ApplicationStarted, LogMessages.ApplicationStartedTemplate,
            registry.AppEnv.AppVersion.FullName);
        log.ZLogInformation($"Mode: {registry.AppEnv.AppMode}; " +
                            $"Root: {registry.AppEnv.RootDir};");

        var mediator = host.Services.GetRequiredService<IMediator>();

        await mediator.Publish(new AppStartingEvent());
        await mediator.Publish(new StartCorneyReq(registry.CrontabFiles));

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var form = host.Services.GetRequiredService<CorneyContext>();
        Application.Run(form);
    }


    private string ConfigPath(AppEnv env)
    {
        var configPath = Path.Combine(env.ConfigDir, "corney-config.json");
        return configPath;
    }

    private CorneyRegistry CreateRegistry(AppEnv env, CorneyConfig config)
    {
        var configPath = ConfigPath(env);
        return new CorneyRegistry(env, configPath, config);
    }
}