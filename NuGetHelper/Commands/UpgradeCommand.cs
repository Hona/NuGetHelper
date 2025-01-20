using System.Collections.Concurrent;
using System.CommandLine;
using System.Runtime.InteropServices;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGetHelper.Commands;

public class UpgradeCommand : ICommandDefinition
{
    public Command Get()
    {
        var upgradeCommand = new Command(
            "upgrade",
            "Upgrade all NuGet packages (csproj or CPM), including transitive pinned versions"
        );

        var pathOption = new Option<string>(
            "--path",
            () => "./Directory.Packages.props",
            "Path to project or Directory.Packages.props file"
        );
        var suppressOption = new Option<bool>("--suppress", () => true);
        var stableOption = new Option<bool>("--stable", () => true);
        var dryRunOption = new Option<bool>("--dry-run", () => false);
        var parallelOption = new Option<int>("--parallel", () => 5);

        upgradeCommand.AddOption(pathOption);
        upgradeCommand.AddOption(suppressOption);
        upgradeCommand.AddOption(stableOption);
        upgradeCommand.AddOption(dryRunOption);
        upgradeCommand.AddOption(parallelOption);

        upgradeCommand.SetHandler(
            async (path, suppress, stable, dryRun, parallel) =>
                await HandleUpgrade(path, suppress, stable, dryRun, parallel),
            pathOption,
            suppressOption,
            stableOption,
            dryRunOption,
            parallelOption
        );

        return upgradeCommand;
    }

    private async Task HandleUpgrade(
        string path,
        bool suppress,
        bool stable,
        bool dryRun,
        int parallel
    )
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new ArgumentException("Path to project file is required.");

        var project = new Project(path);
        var packageReferences = GetPackageReferences(project);

        var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
        var resource = await repository.GetResourceAsync<FindPackageByIdResource>();

        var updates = await ProcessPackagesAsync(
            packageReferences,
            resource,
            stable,
            suppress,
            dryRun,
            parallel
        );

        if (!dryRun)
            UpdateProjectFile(project, updates);

        Console.WriteLine("Done");
    }

    private IEnumerable<(string Package, string Version)> GetPackageReferences(Project project)
    {
        var centralVersions = project
            .Items.Where(item => item.ItemType == "PackageVersion")
            .Select(item =>
                (Package: item.EvaluatedInclude, Version: item.GetMetadataValue("Version"))
            );

        var packageReferences = project
            .Items.Where(item => item.ItemType == "PackageReference")
            .Select(item =>
                (Package: item.EvaluatedInclude, Version: item.GetMetadataValue("Version"))
            );

        return centralVersions.Concat(packageReferences).Distinct();
    }

    private void UpdateProjectFile(
        Project project,
        IEnumerable<(
            string Package,
            NuGetVersion CurrentVersion,
            NuGetVersion UpdateVersion
        )> updates
    )
    {
        foreach (var (package, _, updateVersion) in updates)
        {
            var centralVersion = project.Items.FirstOrDefault(i =>
                i.ItemType == "PackageVersion" && i.EvaluatedInclude == package
            );

            if (centralVersion != null)
            {
                centralVersion.SetMetadataValue("Version", updateVersion.ToString());
                continue;
            }

            var packageReference = project.Items.First(i =>
                i.ItemType == "PackageReference" && i.EvaluatedInclude == package
            );
            packageReference.SetMetadataValue("Version", updateVersion.ToString());
        }

        project.Save();
    }

    private async Task<
        IEnumerable<(string Package, NuGetVersion CurrentVersion, NuGetVersion UpdateVersion)>
    > ProcessPackagesAsync(
        IEnumerable<(string Package, string Version)> packages,
        FindPackageByIdResource resource,
        bool stable,
        bool suppress,
        bool dryRun,
        int maxParallel = 5
    )
    {
        var updates = new ConcurrentBag<(string, NuGetVersion, NuGetVersion)>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = maxParallel };

        await Parallel.ForEachAsync(
            packages,
            options,
            async (package, ct) =>
            {
                if (!NuGetVersion.TryParse(package.Version, out var version))
                {
                    Console.WriteLine($"Could not parse version for {package.Package}");
                    return;
                }

                var versions = await resource.GetAllVersionsAsync(
                    package.Package,
                    NullSourceCacheContext.Instance,
                    NullLogger.Instance,
                    ct
                );

                var latest = versions.Where(x => !x.IsPrerelease || !stable).Max();

                if (latest == null)
                {
                    Console.WriteLine($"Could not find latest version for {package.Package}");
                    return;
                }

                if (version < latest)
                {
                    if (dryRun)
                    {
                        Console.WriteLine($"Update {package.Package} from {version} to {latest}");
                    }
                    else
                    {
                        updates.Add((package.Package, version, latest));
                        Console.WriteLine($"Updating {package.Package} from {version} to {latest}");
                    }
                }
                else if (!suppress)
                {
                    Console.WriteLine($"{package.Package} is up to date");
                }
            }
        );

        return updates;
    }
}
