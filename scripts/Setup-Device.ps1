<#
.SYNOPSIS
    SimulatorCashChanger を POS for .NET システムに登録するスクリプト。
.DESCRIPTION
    1. POS for .NET の Service Object 検索パスにビルド済みの DLL フォルダを追加します。
    2. 論理名 (Logical Name) を登録し、サービスオブジェクト名にマッピングします。
#>

param (
    [string]$LogicalName = "VirtualCashChanger",
    [string]$SoName = "SimulatorCashChanger"
)

$ErrorActionPreference = "Stop"

# リポジトリのルートパスを取得（スクリプトの場所から推測）
$repoRoot = Resolve-Path "$PSScriptRoot\.."

# DLL の場所を特定（net10.0 を優先、なければ net9.0）
$dllDir = Join-Path $repoRoot "src\Device\bin\Debug\net10.0"
if (!(Test-Path $dllDir)) {
    $dllDir = Join-Path $repoRoot "src\Device\bin\Debug\net9.0"
}

if (!(Test-Path $dllDir)) {
    Write-Error "ビルド済みの DLL が見つかりません。先に dotnet build を実行してください。"
    return
}

Write-Host "レジストリ登録を開始します..." -ForegroundColor Cyan

# 1. POS for .NET 設定キーの作成
$regPath = "HKLM:\SOFTWARE\Microsoft\PointOfService\Configuration"
if (!(Test-Path $regPath)) {
    New-Item $regPath -Force | Out-Null
    Write-Host "Created: $regPath"
}

# 2. 検索パス (ControlConfigs) の更新
$configsValue = Get-ItemProperty $regPath -Name "ControlConfigs" -ErrorAction SilentlyContinue
$currentPaths = if ($configsValue.ControlConfigs) { [string[]]$configsValue.ControlConfigs } else { @() }

if ($currentPaths -notcontains $dllDir) {
    Set-ItemProperty $regPath -Name "ControlConfigs" -Value ($currentPaths + $dllDir)
    Write-Host "Added to search path: $dllDir" -ForegroundColor Green
} else {
    Write-Host "Search path already contains: $dllDir" -ForegroundColor Gray
}

# 3. 論理名 (Logical Name) の登録
$deviceRegPath = "HKLM:\SOFTWARE\Microsoft\PointOfService\Configuration\CashChanger\$LogicalName"
if (!(Test-Path $deviceRegPath)) {
    New-Item $deviceRegPath -Force | Out-Null
    Write-Host "Created Logical Name Key: $deviceRegPath"
}

Set-ItemProperty $deviceRegPath -Name "Implementation" -Value $SoName
Write-Host "Registered Logical Name '$LogicalName' -> '$SoName'" -ForegroundColor Green

Write-Host "`n完了しました。管理者権限の PowerShell で実行してください。" -ForegroundColor Yellow
