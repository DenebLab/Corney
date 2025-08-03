using Deneblab.AbcVersion;
using Helpers;
using Helpers.Syrup;
using Nuke.Common;
using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.NuGet;
using Nuke.Common.Utilities.Collections;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Helpers.Azure;
using Nuke.Common.Tooling;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

class Build : NukeBuild
{
    /// Support plugins are available for:
    /// - JetBrains ReSharper        https://nuke.build/resharper
    /// - JetBrains Rider            https://nuke.build/rider
    /// - Microsoft VisualStudio     https://nuke.build/visualstudio
    /// - Microsoft VSCode           https://nuke.build/vscode
    readonly DateTime BuildDate = DateTime.UtcNow;

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;
    [Parameter] readonly string AzureDevOpsToken = Environment.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN");

    readonly bool IsAzureDevOps = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AGENT_NAME")) == false;
    [Solution] readonly Solution Solution;


    ProductInfo ProductInfo => new()
    {
        Company = "Deneblab",
        Copyright = $"Deneblab - {DateTime.UtcNow.Year}"
    };

    Project CorneyWinProject => Solution.GetProject("Corney").NotNull();

    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath TmpBuild => TemporaryDirectory / "w";

    AbsolutePath ArtifactsDir => RootDirectory / ".nuke" / "artifacts";

    AbcVersion AbcVersion => AbcVersionFactory.CreateOneBuilder()
        .SetDateTime(BuildDate)
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

    Target ConfigureAzureDevOps => _ => _
        .DependsOn(Information)
        .OnlyWhenStatic(() => IsAzureDevOps)
        .Executes(() =>
        {
            Log.Information($"Set version to AzureDevOps: {AbcVersion.SemVersion}");
            // https://github.com/microsoft/azure-pipelines-tasks/blob/master/docs/authoring/commands.md
            Log.Information($"##vso[build.updatebuildnumber]{AbcVersion.SemVersion}");
        });

    Target Clean => _ => _
        .DependsOn(ConfigureAzureDevOps)
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

    Target PublishAzureDevOpsArtifacts => _ => _
     
        .Produces(ArtifactsDir / "*.nupkg")
        .OnlyWhenStatic(() => IsAzureDevOps)
        .Executes(() =>
        {
            var globFiles = ArtifactsDir.GlobFiles(ArtifactsDir, "*.nupkg");
            Log.Information($"Artifact {globFiles.Count}");
            globFiles.ForEach(x => Log.Information($"Artifact file: {x}"));
            var serverPublishArtifact = Environment.GetEnvironmentVariable("BUILD_ARTIFACTSTAGINGDIRECTORY");
            Log.Information($"Artifact publish dir: {serverPublishArtifact}");
            AzurePipelines.Instance.UploadArtifacts("AntiPiracyTools", "AntiPiracyTools", ArtifactsDir);
        });

    Target PushNuGetToAzureArtifacts => _ => _

        .OnlyWhenStatic(() => IsAzureDevOps)
        .Executes(() =>
        {
            var packages = ArtifactsDir.GlobFiles("*.nupkg");

            foreach (var package in packages)
                DotNetNuGetPush(s => s
                    .SetTargetPath(package)
                    .SetSource(
                        "https://pkgs.dev.azure.com/antipiracypl/AntiPiracyTools/_packaging/AntiPiracyTools/nuget/v3/index.json")
                    .SetApiKey(AzureDevOpsToken)
                    .EnableSkipDuplicate()
                    .EnableNoSymbols()
                );
        });

    Target Publish => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            var p = CorneyWinProject;
            if (p == null) return;


            Log.Information($"Build; Project file: {p.Name}");
            var outDir = TmpBuild / p.Name / "build";
            outDir.CreateOrCleanDirectory();

            DotNetPublish(o => o
                .SetProject(p.Path)
                .EnableNoRestore()
                .SetConfiguration(Configuration)
                .SetOutput(outDir)
                .SetVersion(AbcVersion.SemVersion)
                .SetFileVersion(AbcVersion.SemVersion)
                .SetAssemblyVersion(AbcVersion.SemVersion)
                .SetInformationalVersion(AbcVersion.InformationalVersion)
            );
        });


    Target PublishAzureDevOpsStorage => _ => _
        .OnlyWhenStatic(() => IsAzureDevOps)
        .DependsOn(Syrup)
        .Executes(async () =>
        {
            void LogFiles(string title, List<ReleaseInfo> filesToShow)
            {
                Log.Information($"{title}: {filesToShow.Count}");
                foreach (var l in filesToShow)
                    Log.Information($"Name: {l.Name}; Date: {l.ReleaseDate}; Url: {l.FileUrl}");
            }

            var p = CorneyWinProject;
            if (p == null)
                throw new KeyNotFoundException($"Project: '{p}' not found in projects list");
            var syrupDir = TmpBuild / p.Name / "syrup";
            var blobName = GetAzureStorageBlobName();
            var storageConnectionString = Environment.GetEnvironmentVariable("azureStorageConnectionStringKey1");
            Log.Debug($"Build; azureStorageConnectionStringKey1: {storageConnectionString}");
            var files = Directory.GetFiles(syrupDir).ToList();
            foreach (var f in files)
            {
                Log.Information($"File to publish: {f}");
            }

            var client = AzureSyrupTools.Create(storageConnectionString, blobName);
            await client.UploadFiles(files);
            var list = await client.GetSyrupFiles();
            var fileToRemove = list.OrderByDescending(x => x.ReleaseDate).Skip(15).ToList();
            LogFiles("Files to remove", fileToRemove);
            await client.RemoveSyrupFiles(fileToRemove);
            var newList = await client.GetSyrupFiles();
            await client.CreateSyrupFilesList(newList);
            LogFiles("Files in container", newList);
        });

    Target Syrup => _ => _
    .DependsOn(Publish)
    .Executes(() =>

    {
        var p = CorneyWinProject;
        if (p == null) return;



        // dirs
        var slimBuildDir = TmpBuild / p.Name / "slim-build";
        var syrupDir = TmpBuild / p.Name / "syrup";
        var syrupBuildDir = TmpBuild / p.Name / "syrup-build";
        var srcBuild = SourceDirectory / "build";
        var srcSyrup = srcBuild / "syrup" / "scripts";
        var mainDir = syrupBuildDir / "main";
        var appDir = mainDir / p.Name;
        var othersDir = syrupBuildDir / "others";
        var robeOtherUpdaterDir = othersDir / "RobeNovaUpdater";



        // create dirs
        syrupDir.CreateOrCleanDirectory();
        robeOtherUpdaterDir.CreateOrCleanDirectory();



        // main directory
        slimBuildDir.Copy(appDir);

        // scripts
        srcSyrup.CopyToDirectory(syrupBuildDir / "_syrup", ExistsPolicy.MergeAndOverwrite);

        // nuget definition
        var srcNugetFile = srcBuild / "syrup" / "spec" / "nuget.nuspec";
        var dstNugetFile = syrupBuildDir / $"{p.Name}.nuspec";
        srcNugetFile.Copy(dstNugetFile);

        // set version
        var text = System.IO.File.ReadAllText(srcNugetFile);
        var r = text.Replace("{Version}", AbcVersion.SemVersion);
        System.IO.File.WriteAllText(dstNugetFile, r, Encoding.UTF8);

        DataChangeHelper.FixDate(slimBuildDir);

        Log.Information($"Make nuget; Src: {slimBuildDir}; Dst: {syrupDir}");


        NuGetTasks.NuGetPack(o => o
            .SetOutputDirectory(syrupDir)
            .SetProcessWorkingDirectory(syrupBuildDir)
            .SetNoPackageAnalysis(true)
        );


        var nugetFiles = syrupDir.GlobFiles("*.nupkg");

        foreach (var file in nugetFiles)
            SyrupTools.MakeSyrupFile(
                file,
                BuildDate,
                AbcVersion.SemVersion,
                AbcVersion.GitBranch,
                p.Name);
    });
    Target PublishRobeNova => _ => _
        .DependsOn(Information, Syrup, PublishAzureDevOpsStorage, PublishAzureDevOpsArtifacts,
            PushNuGetToAzureArtifacts);

    public static int Main() => Execute<Build>(x => x.PublishRobeNova);

    string GetAzureStorageBlobName()
    {
        var branch = AbcVersion.GitBranch;
        var blobName = "application-robe-nova-develop";
        switch (branch)
        {
            case "production":
                blobName = "application-robe-nova";
                break;
            case "develop":
                blobName = "application-robe-nova-develop";
                break;
        }

        Log.Information($"Branch: {branch}; Blob name: {blobName}");
        return blobName;
    }
}