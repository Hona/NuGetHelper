dotnet tool uninstall -g Hona.NuGetHelper
dotnet pack -c Release -o ./nupkg
dotnet tool install -g --prerelease --add-source "./nupkg/" Hona.NuGetHelper