# Build Aeroway-Dev.zip: a self-contained Debug portable folder.
# Debug defines DEBUG, so AppBuild.IsDevBuild is true (DEV badge and strategy picker).
# The Release installer stays Aeroway-Setup.exe and is built by build-installer.ps1.
$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$OutDir = Join-Path $Root "build\dev-portable"
$DistDir = Join-Path $Root "dist"
$ZipPath = Join-Path $DistDir "Aeroway-Dev.zip"

[xml]$csproj = Get-Content (Join-Path $Root "Aeroway.csproj")
$version = $csproj.Project.PropertyGroup.Version | Select-Object -First 1
if (-not $version) { $version = "1.0.0" }

Write-Host "Building Aeroway-Dev v$version (self-contained Debug portable)..." -ForegroundColor Cyan

if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }

Push-Location $Root
# DebugType/DebugSymbols only drop PDBs. The Debug configuration still defines DEBUG.
dotnet publish (Join-Path $Root "Aeroway.csproj") -c Debug -r win-x64 --self-contained true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $OutDir
if ($LASTEXITCODE -ne 0) { Pop-Location; exit 1 }
Pop-Location

$exe = Join-Path $OutDir "Aeroway-Dev.exe"
if (-not (Test-Path -LiteralPath $exe)) {
    Write-Error "Expected Debug publish output Aeroway-Dev.exe in $OutDir"
    exit 1
}

Get-ChildItem -LiteralPath $OutDir -Recurse -Filter *.pdb -File -ErrorAction SilentlyContinue |
    Remove-Item -Force

# Do not ship update.json inside the portable folder (it would shadow GitHub checks).
$staleManifest = Join-Path $OutDir "update.json"
if (Test-Path $staleManifest) { Remove-Item $staleManifest -Force }

Add-Type -AssemblyName System.IO.Compression.FileSystem
# Two-argument CreateFromDirectory puts the folder contents at the archive root.
[System.IO.Compression.ZipFile]::CreateFromDirectory($OutDir, $ZipPath)

$opened = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
try {
    $entry = $opened.Entries | Where-Object { $_.FullName -eq "Aeroway-Dev.exe" } | Select-Object -First 1
    if (-not $entry) {
        throw "Aeroway-Dev.zip does not contain Aeroway-Dev.exe at the archive root."
    }
    if ($entry.Length -le 0) {
        throw "Aeroway-Dev.exe inside Aeroway-Dev.zip is empty."
    }
}
finally {
    $opened.Dispose()
}

Write-Host ""
Write-Host "Done: $ZipPath" -ForegroundColor Green
Write-Host "Unzip and run Aeroway-Dev.exe for the DEV build (strategy picker)." -ForegroundColor Yellow
