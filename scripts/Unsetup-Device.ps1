<#
.SYNOPSIS
    SimulatorCashChanger の登録を解除するスクリプト。
#>

param (
    [string]$LogicalName = "VirtualCashChanger"
)

$ErrorActionPreference = "Continue"

Write-Host "登録解除を開始します..." -ForegroundColor Cyan

# 1. 論理名の削除
$deviceRegPath = "HKLM:\SOFTWARE\Microsoft\PointOfService\Configuration\CashChanger\$LogicalName"
if (Test-Path $deviceRegPath) {
    Remove-Item $deviceRegPath -Recurse -Force
    Write-Host "Removed Logical Name: $LogicalName" -ForegroundColor Green
}

# 2. 検索パスのクリーンアップ（任意）
# ※他のデバイスに影響を与える可能性があるため、ここでは自動削除は行いません。

Write-Host "解除完了。" -ForegroundColor Yellow
