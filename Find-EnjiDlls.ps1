# Finds Gc*.dll for enjiCAD / GstarCAD-style installs
$names = @("GcMgd.dll", "GcDbMgd.dll", "GcCoreMgd.dll")
$roots = @(
    ${env:ProgramFiles},
    ${env:ProgramFiles(x86)},
    "$env:USERPROFILE\Desktop"
) | Where-Object { $_ -and (Test-Path $_) }

Write-Host "Searching for enjiCAD managed DLLs..."
$hits = @()
foreach ($root in $roots) {
    foreach ($name in $names) {
        Get-ChildItem -Path $root -Filter $name -Recurse -ErrorAction SilentlyContinue |
            ForEach-Object { $hits += $_.FullName }
    }
}

$hits = $hits | Sort-Object -Unique
if (-not $hits) {
    Write-Host "NOT FOUND. Install enjiCAD or point to GRX SDK."
    exit 1
}

Write-Host "`nFound:"
$hits | ForEach-Object { Write-Host "  $_" }

$dir = Split-Path ($hits | Where-Object { $_ -like "*GcMgd.dll" } | Select-Object -First 1) -Parent
if ($dir) {
    Write-Host "`nSuggested build:"
    Write-Host "  `$env:ENJICAD_DIR = '$dir'"
    Write-Host "  dotnet build -c Release"
}
