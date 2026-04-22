
$path = "c:\Users\ITI202301003_User\source\repos\w-red\CashChangerSimulator.Core\src\Device.Virtual\StrykerOutput\2026-04-22.17-51-00\reports\mutation-report.json"
$json = Get-Content $path -Raw
$data = $json | ConvertFrom-Json
$files = $data.files
foreach ($fileKey in $files.PSObject.Properties.Name) {
    if ($fileKey -like "*DepositTracker.cs") {
        $file = $files.$fileKey
        $statuses = $file.mutants | Group-Object status | Select-Object Name, Count
        Write-Output "File: $fileKey"
        foreach ($s in $statuses) {
            Write-Output "  $($s.Name): $($s.Count)"
        }
    }
}
