using System.CommandLine;

namespace NuGetHelper.Commands;

public interface ICommandDefinition
{
    public Command Get();
}
