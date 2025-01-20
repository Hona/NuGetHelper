using System.CommandLine;
using System.Reflection;
using Microsoft.Build.Locator;
using NuGetHelper.Commands;

if (!MSBuildLocator.IsRegistered)
    MSBuildLocator.RegisterDefaults();

var rootCommand = new RootCommand("NuGet helper tool");

var commandDefinition = typeof(ICommandDefinition);
var commands = Assembly
    .GetExecutingAssembly()
    .GetTypes()
    .Where(x =>
        commandDefinition.IsAssignableFrom(x) && x is { IsInterface: false, IsAbstract: false }
    );

foreach (var commandType in commands)
{
    var command = (ICommandDefinition?)Activator.CreateInstance(commandType);

    if (command is null)
    {
        throw new InvalidOperationException($"Failed to create instance of {commandType.Name}");
    }

    rootCommand.AddCommand(command.Get());
}

return await rootCommand.InvokeAsync(args);
