namespace Corney.Common.Bootstrap
{
    public class AppVersion
    {
        public AppVersion(string assemblyVersion, string assemblyFileVersion, string assemblyName,
            string appNameSimple)
        {
            AssemblyVersion = assemblyVersion;
            AssemblyFileVersion = assemblyFileVersion;
            AssemblyName = assemblyName;
            AppNameSimple = appNameSimple;
        }

        public AppVersion(string assemblyVersion, string assemblyFileVersion, string assemblyName,
            string assemblyProductVersion, string semVer, string buildCounter,
            string branch, string dateTime, string env, string sha, string commitsCounter, string appNameSimple)
        {
            AssemblyVersion = assemblyVersion;
            AssemblyFileVersion = assemblyFileVersion;
            AssemblyName = assemblyName;
            AssemblyProductVersion = assemblyProductVersion;
            SemVer = semVer;
            BuildCounter = buildCounter;
            Branch = branch;
            DateTime = dateTime;
            Env = env;
            Sha = sha;
            CommitsCounter = commitsCounter;
            AppNameSimple = appNameSimple;
        }

        public string AssemblyVersion { get; }
        public string AssemblyFileVersion { get; }
        public string AssemblyName { get; set; }
        public string AssemblyProductVersion { get; }
        public string SemVer { get; }
        public string BuildCounter { get; }
        public string Branch { get; }
        public string DateTime { get; }
        public string Env { get; }
        public string Sha { get; }
        public string CommitsCounter { get; }
        public string AppNameSimple { get; }

        public string MainVersion => string.IsNullOrEmpty(SemVer) ? AssemblyFileVersion : SemVer;
        public string FullName => $"{AssemblyName} {MainVersion}";

        public string FullInfo => string.IsNullOrEmpty(AssemblyProductVersion)
            ? FullName
            : $"{AssemblyName} {AssemblyProductVersion}";

        public override string ToString()
        {
            return MainVersion;
        }
    }
}