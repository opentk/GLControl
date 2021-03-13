using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using Nuke.GitHub;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.ChangeLog.ChangelogTasks;
using static Nuke.GitHub.ChangeLogExtensions;
using static Nuke.GitHub.GitHubTasks;

[CheckBuildProjectConfigurations]
[ShutdownDotNetAfterServerBuild]
//TODO: configure CI
//[GitHubActions("ci",
//    GitHubActionsImage.WindowsLatest,
//    AutoGenerate = true,
//    //OnPushBranches = new[] { "master" },
//    OnPullRequestBranches = new[] { "master" },
//    InvokedTargets = new[] { nameof(GitHubActions) },
//    ImportSecrets = new[] { "token" })]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("NuGet api key")]
    readonly string NugetApiKey;

    [Parameter("Github authentication token")]
    readonly string GitHubAuthToken;

    [Parameter("NuGet api url")]
    readonly string NugetApiUrl = "https://api.nuget.org/v3/index.json";


    //[Parameter("URL of the icon to be displayed for the package")]
    readonly string packageIconUrl = "https://raw.githubusercontent.com/opentk/opentk.net/docfx/assets/opentk.png";

    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;

    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    AbsolutePath ChangelogPath => RootDirectory / "RELEASE_NOTES.md";

    //informations based on the changelog
    private string releaseVersion;
    private string assemblyVersion;
    private string releaseNotes;

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            RootDirectory.GlobDirectories("OpenTK**/bin", "OpenTK**/obj").ForEach(DeleteDirectory);
            EnsureCleanDirectory(ArtifactsDirectory);
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target VersionInfo => _ => _
        .Unlisted()
        .Executes(() =>
        {
            Logger.Normal("Reading changelog...");

            //Changelog.LatestVersion is taken from the end of the file, but ours is reversed.
            var latest = ReadChangelog(ChangelogPath).ReleaseNotes.Last();//pick first in the file
            releaseVersion = latest.Version.ToNormalizedString();//semver
            assemblyVersion = latest.Version.Version.ToString();//strips suffix //TODO: technically this should be different for each prerelease too
            releaseNotes = string.Join(Environment.NewLine, latest.Notes);

            string channel = latest.Version.IsPrerelease ? "prerelease" : "stable";
            Logger.Info($"Package version:\t{releaseVersion} ({channel})");
            Logger.Info($"Assembly version:\t{assemblyVersion}");
            Logger.Info($"Release notes:");
            Logger.Normal(releaseNotes);
        });

    Target Compile => _ => _
        .DependsOn(Restore, VersionInfo)
        //.Requires(() => releaseVersion, () => assemblyVersion)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetVersion(releaseVersion)
                .SetAssemblyVersion(assemblyVersion)
                .EnableNoRestore());
        });

    Target Pack => _ => _
        .DependsOn(VersionInfo)
        //.DependsOn(Compile)//TODO: Enable this in a CI scenario
        .Produces(ArtifactsDirectory / "*.nupkg")
        //.Requires(() => releaseNotes, () => releaseVersion)
        .Executes(() =>
        {
            var msbuildFormattedReleaseNotes = releaseNotes.EscapeStringPropertyForMsBuild();
            DotNetPack(s => s
                .SetProject(Solution.GetProject("OpenTK.WinForms"))
                .SetConfiguration(Configuration)
                //properties specific to this release:
                .SetVersion(releaseVersion)
                .SetAssemblyVersion(assemblyVersion)
                .SetPackageReleaseNotes(msbuildFormattedReleaseNotes)
                .SetCopyright($"Copyright (c) {DateTime.Now.Year} Team OpenTK")
                //these can't be set in the .csproj:
                .SetPackageIconUrl(packageIconUrl)
                .SetIncludeSymbols(true)//this will produce 2 nupkgs, one with symbols and one with dll-only
                //.SetIncludeSource(true)//set this to include source in the symbols package
                //the rest of the properties are set once in the .csproj, such as project url, authors, ...
                //TODO: Enable these in a CI scenario:
                //.EnableNoRestore()
                //.EnableNoBuild()
                .SetOutputDirectory(ArtifactsDirectory));

        });
    
    /// <summary>
    /// Triggers all push targets at once after a clean.
    /// </summary>
    Target PublishRelease => _ => _
        .DependsOn(Clean, Pack)
        .Requires(() => Configuration.Equals(Configuration.Release))
        .Triggers(PushNuget, PushGithub);

    Target PushNuget => _ => _
        .DependsOn(Pack)
        .Requires(() => NugetApiUrl)
        .Requires(() => NugetApiKey)
        .Requires(() => Configuration.Equals(Configuration.Release))
        .Executes(() =>
        {
            DotNetNuGetPush(s => s
                .SetSource(NugetApiUrl)
                .SetApiKey(NugetApiKey)
                .EnableSkipDuplicate()//in case the artifacts folder was no cleaned
                .CombineWith(
                    ArtifactsDirectory.GlobFiles("*.symbols.nupkg").NotEmpty(), (cs, v) => cs
                        .SetTargetPath(v)));
        });

    Target PushGithub => _ => _
    .DependsOn(Pack, VersionInfo)
    .Requires(() => GitHubAuthToken)
    //.Requires(() => releaseVersion, () => releaseNotes)
    .Requires(() => Configuration.Equals(Configuration.Release))
    .Executes(() =>
    {
        var releaseTag =$"v{releaseVersion}";
        var repositoryInfo = GetGitHubRepositoryInfo(GitRepository);
        var nuGetPackages = GlobFiles(ArtifactsDirectory, "*.symbols.nupkg").NotEmpty().ToArray();

        //Note: if the release is already present, nothing happens
        GitHubTasks.PublishRelease(s => s
            .SetToken(GitHubAuthToken)
            .SetArtifactPaths(nuGetPackages)
            .SetTag(releaseTag)
            .SetReleaseNotes(releaseNotes)
            .SetCommitSha(GitRepository.Commit)
            .SetRepositoryName(repositoryInfo.repositoryName)
            .SetRepositoryOwner(repositoryInfo.gitHubOwner)).Wait();

    });

}
