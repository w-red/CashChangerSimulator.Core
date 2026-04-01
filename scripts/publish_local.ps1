# Pack and publish to local NuGet source
$repoRoot = Resolve-Path "$PSScriptRoot\.."


dotnet pack "$repoRoot\src\Core\CashChangerSimulator.Core.csproj" -c Release -o "C:\NuGetLocal" /p:Version=1.0.0-local
dotnet pack "$repoRoot\src\Device\CashChangerSimulator.Device.csproj" -c Release -o "C:\NuGetLocal" /p:Version=1.0.0-local
