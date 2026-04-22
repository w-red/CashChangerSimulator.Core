$path = 'C:\Users\ITI202301003_User\source\repos\w-red\CashChangerSimulator.Core\src\Device.Virtual\StrykerOutput\2026-04-22.17-51-00\reports\mutation-report.json'
$json = Get-Content $path -Raw | ConvertFrom-Json
$targetFile = "DepositController.cs"

$results = foreach ($file in $json.files.PSObject.Properties) {
    if ($file.Name -like "*$targetFile") {
        $file.Value.mutants | Where-Object { $_.status -eq 'Survived' } | ForEach-Object {
            [PSCustomObject]@{
                ID = $_.id
                Mutator = $_.mutatorName
                Line = $_.location.start.line
                Replacement = $_.replacement
            }
        }
    }
}

if ($results) {
    $results | Format-Table -AutoSize
} else {
    Write-Host "No survived mutants found for $targetFile"
}
