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
using Serilog;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.ChangeLog.ChangelogTasks;
using static Nuke.GitHub.ChangeLogExtensions;
using static Nuke.GitHub.GitHubTasks;
using Nuke.Common.ChangeLog;

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

    public static int Main()
    {
        return Execute<Build>(x => x.Compile);
    }

    [Parameter("Configuration to build - Default is 'Release'")]
    readonly Configuration Configuration = Configuration.Release;

    [Parameter("NuGet api key", Name = "opentk_nuget_api_key")]
    readonly string NugetApiKey = "";

    [Parameter("Github authentication token", Name = "opentk_github_token")]
    readonly string GitHubAuthToken = "";

    [Parameter("NuGet api url")]
    readonly string NugetApiUrl = "https://api.nuget.org/v3/index.json";

    //[Parameter("URL of the icon to be displayed for the package")]
    readonly string packageIconUrl = "https://raw.githubusercontent.com/opentk/opentk.net/docfx/assets/opentk.png";

    [Solution] readonly Solution Solution;
    // FIXME: Is there some way for us to push to the upstream repo instead?
    readonly GitRepository GitRepository = GitRepository.FromUrl("https://github.com/opentk/GLControl");

    static AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    static AbsolutePath ChangelogPath => RootDirectory / "RELEASE_NOTES.md";

    // Information based on the changelog
    private string releaseVersion;
    private string assemblyVersion;
    private string releaseNotes;

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            RootDirectory.GlobDirectories("OpenTK**/bin", "OpenTK**/obj").ForEach(p => p.DeleteDirectory());
            ArtifactsDirectory.CreateOrCleanDirectory();
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
            Log.Information("Reading changelog...");

            // Changelog.LatestVersion is taken from the end of the file, but ours is reversed.
            ReleaseNotes latest = ReadChangelog(ChangelogPath).ReleaseNotes[^1];//pick first in the file
            releaseVersion = latest.Version.ToNormalizedString(); // semver
            assemblyVersion = latest.Version.Version.ToString(); // strips suffix // TODO: technically this should be different for each prerelease too
            releaseNotes = string.Join(Environment.NewLine, latest.Notes);

            string channel = latest.Version.IsPrerelease ? "prerelease" : "stable";
            Log.Information($"Package version:\t{releaseVersion} ({channel})");
            Log.Information($"Assembly version:\t{assemblyVersion}");
            Log.Information($"Release notes:");
            Log.Information(releaseNotes);
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
        .DependsOn(Compile, VersionInfo)
        //.DependsOn(Compile)//TODO: Enable this in a CI scenario
        .Produces(ArtifactsDirectory / "*.nupkg")
        //.Requires(() => releaseNotes, () => releaseVersion)
        .Executes(() =>
        {
            string msbuildFormattedReleaseNotes = releaseNotes.EscapeStringPropertyForMsBuild();
            DotNetPack(s => s
                .SetProject(Solution.GetProject("OpenTK.WinForms"))
                .SetConfiguration(Configuration)
                // properties specific to this release:
                .SetVersion(releaseVersion)
                .SetAssemblyVersion(assemblyVersion)
                .SetPackageReleaseNotes(msbuildFormattedReleaseNotes)
                .SetCopyright($"Copyright (c) {DateTime.Now.Year} Team OpenTK")
                // these can't be set in the .csproj:
                // FIXME: Move over to <PackageIcon>?
                .SetPackageIconUrl(packageIconUrl)
                .SetIncludeSymbols(true) // this will produce 2 nupkgs, one with symbols and one with dll-only
                //.SetIncludeSource(true) // set this to include source in the symbols package
                // the rest of the properties are set once in the .csproj, such as project url, authors, ...
                // TODO: Enable these in a CI scenario:
                //.EnableNoRestore()
                //.EnableNoBuild()
                .SetOutputDirectory(ArtifactsDirectory));

        });

    /// <summary>
    /// Triggers all push targets at once after a clean.
    /// </summary>
    Target PublishRelease => _ => _
        .DependsOn(Clean)
        .DependsOn(Pack)
        .Requires(() => Configuration.Equals(Configuration.Release))
        .Triggers(PushNuget, PushGithub);

    Target PushNuget => _ => _
        .DependsOn(Pack)
        .Requires(() => NugetApiUrl)
        .Requires(() => NugetApiKey)
        .Requires(() => Configuration.Equals(Configuration.Release))
        .Executes(() =>
        {
            if (string.IsNullOrEmpty(NugetApiKey))
            {
                Log.Warning("No nuget api key set.");
            }
            else
            {
                DotNetNuGetPush(s => s
                .SetSource(NugetApiUrl)
                .SetApiKey(NugetApiKey)
                .EnableSkipDuplicate() //in case the artifacts folder was not cleaned
                .CombineWith(
                        ArtifactsDirectory.GlobFiles("*.symbols.nupkg").NotEmpty(), (cs, v) => cs
                        .SetTargetPath(v)));
            }
        });

    Target PushGithub => _ => _
        .DependsOn(Pack, VersionInfo)
        .Requires(() => GitHubAuthToken)
        //.Requires(() => releaseVersion, () => releaseNotes)
        .Requires(() => Configuration.Equals(Configuration.Release))
        .Executes(() =>
        {
            if (string.IsNullOrEmpty(GitHubAuthToken))
            {
                Log.Warning("No github auth token set.");
            }
            else
            {
                string releaseTag =$"v{releaseVersion}";
                var repositoryInfo = GetGitHubRepositoryInfo(GitRepository);
                AbsolutePath[] nuGetPackages = ArtifactsDirectory.GlobFiles("*.symbols.nupkg").NotEmpty().ToArray();

                //Note: if the release is already present, nothing happens
                GitHubTasks.PublishRelease(s => s
                    .SetToken(GitHubAuthToken)
                    .SetArtifactPaths(nuGetPackages.Select(s => s.ToString()).ToArray())
                    .SetTag(releaseTag)
                    .SetReleaseNotes(releaseNotes)
                    .SetCommitSha(GitRepository.Commit)
                    .SetRepositoryName(repositoryInfo.repositoryName)
                    .SetRepositoryOwner(repositoryInfo.gitHubOwner)).Wait();
            }
        });

}
