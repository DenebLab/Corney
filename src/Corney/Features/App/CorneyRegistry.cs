using Deneblab.Common.Host;

namespace Corney.Features.App;

public class CorneyRegistry
{
    public CorneyRegistry(AppEnv env, string configPath, CorneyConfig config)
    {
        ConfigFilePath = configPath;
        CrontabFiles = config.CrontabFiles;
        AppVersion = env.AppVersion;
        AppEnv = env;
    }

    public AppEnv AppEnv { get; set; }
    public AppEnvVersion AppVersion { get; }
    public string ConfigFilePath { get; }
    public string[] CrontabFiles { get; }
}