using System.IO;
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
        if (File.Exists(configPath))
        {
            _log.Trace($"Using existing config file at {configPath}");
            var config = Misc.ReadJson<CorneyConfig>(configPath);
            return config;
        }
        else
        {
            _log.Trace($"Creating new config file at {configPath}");
            var config = new CorneyConfig();
            Misc.WriteJson(configPath, config);
            return config;
        }
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