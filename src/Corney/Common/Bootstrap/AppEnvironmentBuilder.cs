using System;
using System.IO;
using System.Reflection;

namespace Corney.Common.Bootstrap
{
    public class AppEnvironmentBuilder
    {
        private const string _DEV_DIR = "dev";
        private const string _GIT_DIR = ".git";
        private const string _DEV_FILE = "dev.json";
        private const string _LOG_DIR = "log";
        private const string _CONFIG_DIR = "config";
        private const string _GLOBAL_CONFIG_DIR = "global-config";
        private const string _SYRUP_DIR = ".syrup";
        private const string _WORK_DIR = "work-dir";
        private const string _GLOBAL_DIR = "global-dir";
        private static readonly AppEnvironmentBuilder _instance = new AppEnvironmentBuilder();

        private static AppEnvironment _appEnvironmentValue;
        private static readonly object _padlock = new object();

        static AppEnvironmentBuilder()
        {
        }

        private AppEnvironmentBuilder()
        {
        }

        // ReSharper disable once ConvertToAutoProperty
        public static AppEnvironmentBuilder Instance => _instance;


        public AppEnvironment GetAppEnvironment()
        {
            if (_appEnvironmentValue == null)
                lock (_padlock)
                {
                    var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                    if (_appEnvironmentValue != null) return _appEnvironmentValue;
                    _appEnvironmentValue = GetTemporaryRegistryImpl(asm);
                }

            return _appEnvironmentValue;
        }

        private AppEnvironment GetTemporaryRegistryImpl(Assembly asm)
        {
            var res = new AppEnvironment();


            // main 
            res.AssemblyFilePath = new Uri(asm.CodeBase ?? string.Empty).LocalPath;
            res.AssemblyFileDir = Path.GetDirectoryName(res.AssemblyFilePath);
            res.ExeFilePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            res.ExeFileDir =  Path.GetDirectoryName(res.ExeFilePath);
            res.CommandLineArgs = Environment.GetCommandLineArgs();
            res.SyrupDir = FindDir(res.ExeFileDir, _SYRUP_DIR);

            // dev
            res.DevDir = FindDir(res.ExeFileDir, _DEV_DIR);
            var dv = GetDevSettings(res.DevDir);
            res.IsDeveloperMode = IsDeveloperMode(res.DevDir, dv);

            // root
            res.RootDir = GetRoot(res.ExeFileDir, res.DevDir, res.SyrupDir, dv);
            res.CurrentDir = CurrentDir(dv, res.RootDir);

            // important dev dirs
            res.GitDir = FindDir(res.ExeFileDir, _GIT_DIR);
            res.RepositoryRootDir = GetRepositoryRoot(res.GitDir);


            // others
            res.AssemblyName = asm.GetName().Name;
            res.AppNameSlug = res.AssemblyName.GenerateSlug2();
            res.WorkDir = Path.Combine(res.RootDir, _WORK_DIR);
            res.GlobalDir = GetGlobalDir(res.RootDir);

            res.AppVersion = AppVersionBuilder.Init(asm);
            res.MachineName = Environment.MachineName;
            res.Is64BitProcess = Environment.Is64BitProcess;
            res.ProcessorArchitecture = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
            res.LogDir = GetSimpleDir(res.RootDir, _LOG_DIR, res.IsDeveloperMode);
            res.ConfigDir = GetSimpleDir(res.RootDir, _CONFIG_DIR, res.IsDeveloperMode);
            res.UserDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            res.LocalApplicationData = res.IsDeveloperMode
                ? res.RootDir
                : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            res.LocalPrograms = Path.Combine(res.LocalApplicationData, "Programs");

            res.NodeId = IdentHelper.GetNodeKey(res.ExeFileDir, res.MachineName, res.AppVersion?.SemVer ?? res.AppVersion?.AssemblyVersion ?? string.Empty);
#if DEBUG
            res.IsDebug = true;
#else
            res.IsDebug = false;
#endif

            return res;
        }

        private string GetGlobalDir(string resRootDir)
        {
            var home = Environment.GetEnvironmentVariable("USERPROFILE");
            var droboxDir = Path.Combine(home, "Dropbox");
            if (File.Exists(droboxDir)) return droboxDir;
            var rootGlobalDir = Path.Combine(resRootDir, _GLOBAL_DIR);
            return rootGlobalDir;
        }

        private string GetExtensionDir(string path)
        {
            return path == null ? null : Path.GetDirectoryName(path);
        }

        private string GetGlobalConfigDir(string defaultGlobalConfig, string configDir, bool isDeveloperMode)
        {
            var defaultDir = defaultGlobalConfig.Replace("__UserName__", Environment.UserName);
            var globalConfigDir = Path.Combine(configDir, _GLOBAL_CONFIG_DIR);
            if (isDeveloperMode)
            {
                Helpers.CreateDirIfNotExist(globalConfigDir);
                return globalConfigDir;
            }

            return defaultDir;
        }

        private string CurrentDir(DeveloperConfig dv, string rootDir)
        {
            if (dv.DevMode) return rootDir;

            return Directory.GetCurrentDirectory();
        }


        private static string GetSimpleDir(string rootDir, string dirName, bool useShort)
        {
            return useShort ? Path.Combine(rootDir, dirName) : Path.Combine(rootDir, dirName, Environment.MachineName);
        }

        private static string GetDir(string rootDir, string dirName)
        {
            return Path.Combine(rootDir, dirName);
        }


        private static bool IsDeveloperMode(string devDir, DeveloperConfig dv)
        {
            return Directory.Exists(devDir) && dv.DevMode;
        }

        private static string GetRepositoryRoot(string gitDir)
        {
            var parentGit = string.IsNullOrEmpty(gitDir)
                ? string.Empty
                : new DirectoryInfo(gitDir)?.Parent?.FullName;

            return parentGit;
        }


        private string GetRoot(string appDir, string devDir, string syrupDir, DeveloperConfig dv)
        {
            if (dv.DevMode)
            {
                var devAppDir = Path.Combine(devDir, dv.DevSubdir);
                Helpers.CreateDirIfNotExist(devAppDir);
                if (Directory.Exists(devAppDir)) return devAppDir;
            }

            if (string.IsNullOrEmpty(syrupDir)) return appDir;
            var parent = new DirectoryInfo(syrupDir).Parent?.FullName;
            return string.IsNullOrEmpty(parent) == false ? parent : appDir;
        }


        private static DeveloperConfig GetDevSettings(string devDir)
        {
            var conf = new DeveloperConfig();
            try
            {
                if (!Directory.Exists(devDir)) return conf;
                var devConfig = Path.Combine(devDir, _DEV_FILE);
                if (!File.Exists(devConfig)) return conf;
                var json = File.ReadAllText(devConfig);
                conf = System.Text.Json.JsonSerializer.Deserialize<DeveloperConfig>(json);
            }
            catch (Exception)
            {
                // ignore
            }

            return conf;
        }


        private static string FindDir(string startPath, string dirToFind)
        {
            var di = new DirectoryInfo(startPath);
            while (true)
            {
                var path = Path.Combine(di.FullName, dirToFind);
                if (Directory.Exists(path))
                    return path;

                if (di.Parent == null) return null;
                di = di.Parent;
            }
        }


        internal class DevSettings
        {
            public bool IsDeveloperMode { get; set; }
            public DeveloperConfig DeveloperConfig { get; set; }
        }
    }
}