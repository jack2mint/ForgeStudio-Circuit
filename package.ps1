$ErrorActionPreference = "Stop"
$Version = "0.1.0-dev"
.uild.ps1

$Iscc = Get-Command iscc.exe -ErrorAction SilentlyContinue
if (-not $Iscc) {
    Write-Warning "Inno Setup compiler is not installed or not on PATH. Portable ZIP will still be created."
} else {
    & $Iscc.Source .\installer\ForgeStudioCircuit.iss
}

$ZipDir = ".rtifacts\portable"
$ZipFile = ".rtifacts\ForgeStudioCircuit-v$Version-portable.zip"
if (Test-Path $ZipDir) { Remove-Item $ZipDir -Recurse -Force }
New-Item -ItemType Directory -Path $ZipDir | Out-Null
Copy-Item .rtifacts\publish\win-x64\* $ZipDir -Recurse -Force
Compress-Archive -Path "$ZipDir\*" -DestinationPath $ZipFile -Force
Write-Host "[ForgeStudio Circuit] Portable package: $ZipFile" -ForegroundColor Green
