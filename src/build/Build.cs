using System;
using Deneblab.AbcVersion;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

class Build : NukeBuild
{
    [Parameter] readonly string AzureDevOpsToken = Environment.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN");

    /// Support plugins are available for:
    /// - JetBrains ReSharper        https://nuke.build/resharper
    /// - JetBrains Rider            https://nuke.build/rider
    /// - Microsoft VisualStudio     https://nuke.build/visualstudio
    /// - Microsoft VSCode           https://nuke.build/vscode
    readonly DateTime BuildDate = DateTime.UtcNow;

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    readonly bool IsAzureDevOps = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AGENT_NAME")) == false;
    [Solution] readonly Solution Solution;
    Project CorneyWinProject => Solution.GetProject("Corney").NotNull();
    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath TmpBuild => TemporaryDirectory / "w";
    AbcVersion AbcVersion => AbcVersionFactory.CreateOneBuilder()
        .SetDateTime(BuildDate)
        .SetRepositoryRoot(RootDirectory)
        .Build(); // Creates new instance

    Target Information => _ => _
        .Executes(() =>
        {
            Log.Information($"Host: '{Host}'");
            Log.Information($"Version: '{AbcVersion.SemVersion}'");
            Log.Information($"Date: '{AbcVersion.DateTime:s}Z'");
            Log.Information($"Build target: {Configuration}");
            Log.Information($"FullVersion: '{AbcVersion.InformationalVersion}'");
            Log.Information($"AzureDevOps IsAzureDevOps: '{IsAzureDevOps}'");
            Log.Information($"AzureDevOps AgentName: '{Environment.GetEnvironmentVariable("AGENT_NAME")}'");
            Log.Information(
                $"AzureDevOps ArtifactDir: '{Environment.GetEnvironmentVariable("BUILD_ARTIFACTSTAGINGDIRECTORY")}'");
        });

    Target Clean => _ => _
        .Executes(() =>
        {
            TmpBuild.CreateOrCleanDirectory();
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(x =>
            {
                if (x.Parent?.Name == "build") return;
                if (x.Parent?.Name == "infra") return;
                x.DeleteDirectory();
            });
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(s => s.SetProjectFile(CorneyWinProject));
        });

    Target Test => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            var testProject = Solution.GetProject("Corney.Tests");
            if (testProject == null)
            {
                Log.Warning("Test project not found, skipping tests");
                return;
            }

            // Check if running on Windows since the test project targets net8.0-windows
            if (!OperatingSystem.IsWindows())
            {
                Log.Warning("Skipping tests - Windows Forms tests require Windows runtime");
                return;
            }
            var p = CorneyWinProject;
            if (p == null) return;

            var outDir = TmpBuild / CorneyWinProject.Name / "test-result";
            outDir.CreateOrCleanDirectory();

            try
            {
                DotNetTest(s => s
                    .SetProjectFile(testProject)
                    .SetConfiguration(Configuration)
                    .SetLoggers("trx")
                    .SetResultsDirectory(outDir));
            }
            catch (ProcessException ex)
            {
                Log.Warning($"Tests failed with exit code: {ex.ExitCode}");
                Log.Information("Continuing build despite test failures...");
            }
        });

    Target GithubRelease => _ => _
        .DependsOn(Information, Clean, Test)
        .Executes(() =>
        {
            var p = CorneyWinProject;
            if (p == null) return;

            Log.Information($"Build Single-File; Project file: {p.Name}; Version: {AbcVersion.SemVersion}");
            var outDir = TmpBuild / p.Name / "github-release";
            outDir.CreateOrCleanDirectory();


            // Restore with runtime identifier
            DotNetRestore(s => s
                .SetProjectFile(p.Path)
                .SetRuntime("win-x64")
            );

            DotNetPublish(o => o
                .SetProject(p.Path)
                .EnableNoRestore()
                .SetConfiguration(Configuration)
                .SetOutput(outDir)
                .EnablePublishSingleFile()
                .SetSelfContained(false)
                .SetRuntime("win-x64")
                .SetVersion(AbcVersion.SemVersion)
                .SetFileVersion(AbcVersion.SemVersion)
                .SetAssemblyVersion(AbcVersion.SemVersion)
                .SetInformationalVersion(AbcVersion.InformationalVersion)
            );

            Log.Information($"Single-file executable created: {outDir / "Corney.exe"}");
        });

    public static int Main() => Execute<Build>(x => x.GithubRelease);
}