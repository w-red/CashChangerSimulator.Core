param (
    [Parameter(Mandatory = $true)]
    [string]$Path,

    [Parameter(Mandatory = $false)]
    [string]$Filter = "*.cs",

    [Parameter(Mandatory = $false)]
    [switch]$Recurse
)

$targetPath = (Resolve-Path $Path).Path
Write-Host "Target Path: $targetPath"

if (Test-Path $targetPath -PathType Container) {
    $files = Get-ChildItem -Path $targetPath -Filter $Filter -Recurse:$Recurse -File
} else {
    $files = Get-Item -Path $targetPath
}

# Use [char] codes to avoid encoding issues with Japanese literals in the script file
$fullWidthOpen = [char]0xFF08
$fullWidthClose = [char]0xFF09
$halfWidthOpen = '('
$halfWidthClose = ')'

foreach ($file in $files) {
    $filePath = $file.FullName
    Write-Host "Processing: $filePath"
    
    try {
        # Read file content as UTF-8
        $content = [System.IO.File]::ReadAllText($filePath, [System.Text.Encoding]::UTF8)
        
        # Replace full-width parentheses with half-width ones
        $newContent = $content.Replace($fullWidthOpen, $halfWidthOpen).Replace($fullWidthClose, $halfWidthClose)
        
        if ($content -ne $newContent) {
            # Save if changed, using UTF-8 without BOM
            $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
            [System.IO.File]::WriteAllText($filePath, $newContent, $utf8NoBom)
            Write-Host "  Updated." -ForegroundColor Green
        } else {
            Write-Host "  No changes." -ForegroundColor Gray
        }
    } catch {
        # Use ${} to avoid ambiguity with the following colon
        Write-Warning "Failed to process ${filePath}: $($_.Exception.Message)"
    }
}
