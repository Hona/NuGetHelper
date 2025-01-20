# NuGetHelper
Tooling to use NuGet long term suck. NuGet is awesome. This CLI tool tries to patch some of the gaps.

[![NuGet Version](https://img.shields.io/nuget/v/Hona.NuGetHelper)](https://www.nuget.org/packages/Hona.NuGetHelper)


## What can this do?

```pwsh
$ nuget-helper --help

...

Commands:
  clean    Remove unused PackageVersion definitions by using `dotnet nuget why` to audit usages
  upgrade  Upgrade all NuGet packages (csproj or CPM), including transitive pinned versions

```

## Installation

```pwsh
dotnet tool install -g Hona.NuGetHelper
```

## Example commands

### Help

```pwsh
nuget-helper --help
```

### Upgrade all packages using ./Directory.Packages.props

```pwsh
nuget-helper upgrade
```

### Upgrade all packages for a specific project and allow pre-release versions

```pwsh
nuget-helper upgrade --path .\NuGetHelper\NuGetHelper.csproj --stable false
```

### Clean unused PackageVersion definitions

```pwsh
nuget-helper clean
```

### Print unused PackageVersion definitions

```pwsh
nuget-helper clean --dry-run
```
