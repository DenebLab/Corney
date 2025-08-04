using System;
using System.IO;
using System.Threading.Tasks;
using Corney.Common.Io;
using Deneblab.Common.Host;
using Deneblab.Common.Logging;
using Microsoft.Extensions.Logging;

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

    public CorneyConfig CreateConfig(AppEnv env)
    {
        var configPath = ConfigPath(env);
        CorneyConfig config;
        
        if (File.Exists(configPath))
        {
            _log.Trace($"Using existing config file at {configPath}");
            config = Misc.ReadJson<CorneyConfig>(configPath);
        }
        else
        {
            _log.Trace($"Creating new config file at {configPath}");
            config = new CorneyConfig();
            Misc.WriteJson(configPath, config);
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
        {
            foreach (var warning in validationResult.Warnings)
            {
                _log.LogWarning("Configuration warning - {PropertyName}: {WarningMessage}", 
                    warning.PropertyName, warning.WarningMessage);
            }
        }

        return config;
    }

    public async Task<CorneyConfig> CreateConfigAsync(AppEnv env)
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
        {
            foreach (var warning in validationResult.Warnings)
            {
                _log.LogWarning("Configuration warning - {PropertyName}: {WarningMessage}", 
                    warning.PropertyName, warning.WarningMessage);
            }
        }

        return config;
    }

    private string ConfigPath(AppEnv env)
    {
        var configPath = Path.Combine(env.ConfigDir, "corney-config.json");
        return configPath;
    }

    public CorneyRegistry GetRegistry(AppEnv env, CorneyConfig config)
    {
        var configPath = ConfigPath(env);
        return new CorneyRegistry(env, configPath, config);
    }
}