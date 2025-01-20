using System.Collections.Concurrent;
using System.CommandLine;
using System.Diagnostics;
using System.Text;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace NuGetHelper.Commands;

public class CleanCommand : ICommandDefinition
{
    public Command Get()
    {
        var command = new Command(
            "clean",
            "Remove unused PackageVersion definitions by using `dotnet nuget why` to audit usages"
        );

        var dryRunOption = new Option<bool>(
            ["--dry-run", "-d"],
            "Only print the unused PackageVersion definitions"
        );

        command.AddOption(dryRunOption);

        command.SetHandler(async (dryRun) => await Clean(dryRun), dryRunOption);

        return command;
    }

    private async Task Clean(bool dryRun)
    {
        var solutionFile = Directory.EnumerateFiles(".", "*.sln").SingleOrDefault();

        if (solutionFile is null)
        {
            Console.WriteLine("No solution file found in the current directory");
            return;
        }

        var directoryPackageProps = Path.Combine(
            Path.GetDirectoryName(solutionFile)!,
            "Directory.Packages.props"
        );

        if (!File.Exists(directoryPackageProps))
        {
            Console.WriteLine("No Directory.Packages.props file found in the solution directory");
            return;
        }

        var project = new Project(directoryPackageProps);

        var packageVersions = project
            .Items.Where(i => i.ItemType == "PackageVersion")
            .Select(i => i.EvaluatedInclude)
            .ToList();

        var toRemove = new ConcurrentBag<string>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
        await Parallel.ForEachAsync(
            packageVersions,
            options,
            async (package, ct) =>
            {
                using var process = new Process();

                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"nuget why {solutionFile} {package}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                process.EnableRaisingEvents = true;

                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync(ct);
                var error = await process.StandardError.ReadToEndAsync(ct);

                await process.WaitForExitAsync(ct);

                if (!string.IsNullOrWhiteSpace(error))
                {
                    Console.WriteLine(
                        $"Error running `{process.StartInfo.FileName} {process.StartInfo.Arguments}`"
                    );
                    Console.WriteLine(error);
                    return;
                }

                var hasDependency = output.Contains(
                    $" has the following dependency graph(s) for '{package}':"
                );

                if (hasDependency)
                {
                    /*
                    Console.WriteLine($"Package '{package}' is used");
                    */
                }
                else
                {
                    toRemove.Add(package);
                }
            }
        );

        foreach (var package in toRemove)
        {
            Console.WriteLine($"Package '{package}' is not used");
            if (!dryRun)
            {
                project.RemoveItem(project.Items.First(i => i.EvaluatedInclude == package));
            }
        }

        if (!dryRun)
        {
            project.Save();
        }
    }
}
