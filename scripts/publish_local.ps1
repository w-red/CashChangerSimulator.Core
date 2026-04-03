# 全プロジェクトをローカル NuGet ソースにパックして発行するスクリプト

param (
    [string]$OutputDir = "C:\NuGetLocal",
    [string]$Version = "1.0.0-local"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path "$PSScriptRoot\.."

if (!(Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    Write-Host "Created output directory: $OutputDir" -ForegroundColor Gray
}

# パック対象のプロジェクト一覧
$projects = @(
    "src\Core\CashChangerSimulator.Core.csproj",
    "src\Device\CashChangerSimulator.Device.csproj",
    "src\Device.Virtual\CashChangerSimulator.Device.Virtual.csproj",
    "src\Device.PosForDotNet\CashChangerSimulator.Device.PosForDotNet.csproj"
)

Write-Host "Local NuGet Packing (Version: $Version)..." -ForegroundColor Cyan

foreach ($projPath in $projects) {
    $fullPath = Join-Path $repoRoot $projPath
    if (Test-Path $fullPath) {
        Write-Host "Packing: $projPath" -ForegroundColor Green
        dotnet pack "$fullPath" -c Release -o "$OutputDir" /p:Version=$Version --no-restore
    } else {
        Write-Warning "Project not found: $projPath"
    }
}

Write-Host "`n完了しました。出力先: $OutputDir" -ForegroundColor Yellow
