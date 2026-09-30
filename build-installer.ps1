# Build Aeroway-Setup.exe (self-contained, no .NET install required)
param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release"
)
$ErrorActionPreference = "Stop"
if ($Configuration -ne "Release") {
    Write-Error "Inno Setup is not built from Configuration=$Configuration."
    exit 1
}
$ProjectDir = $PSScriptRoot
$StagingDir = Join-Path $ProjectDir "build\staging"
$DistDir = Join-Path $ProjectDir "dist"
$Packaging = Join-Path $ProjectDir "packaging"
$IssFile = Join-Path $ProjectDir "installer\ZapretUI.iss"

[xml]$csproj = Get-Content (Join-Path $ProjectDir "ZapretUI.csproj")
$version = $csproj.Project.PropertyGroup.Version | Select-Object -First 1
if (-not $version) { $version = "1.0.0" }

Write-Host "Building Aeroway v$version (self-contained installer)..." -ForegroundColor Cyan

& (Join-Path $ProjectDir "build-icon.ps1")
& (Join-Path $ProjectDir "build-installer-art.ps1")

if (Test-Path $StagingDir) { Remove-Item $StagingDir -Recurse -Force }
New-Item -ItemType Directory -Path $StagingDir -Force | Out-Null
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

Push-Location $ProjectDir
dotnet publish -c Release -r win-x64 --self-contained true `
    -p:PublishReadyToRun=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $StagingDir
if ($LASTEXITCODE -ne 0) { Pop-Location; exit 1 }
Pop-Location

Copy-Item -LiteralPath (Join-Path $StagingDir "ZapretUI.exe") -Destination (Join-Path $StagingDir "Aeroway.exe") -Force

$bundledZapret = Join-Path $Packaging "zapret"
$stagingZapret = Join-Path $StagingDir "zapret"
if (-not (Test-Path (Join-Path $bundledZapret "service.bat")) -or -not (Test-Path (Join-Path $bundledZapret "bin"))) {
    Write-Error "Bundled zapret is missing or incomplete. Expected service.bat and bin/ in packaging/zapret"
}
Write-Host "Deploying bundled Flowseal to staging/zapret..." -ForegroundColor Cyan
if (Test-Path $stagingZapret) { Remove-Item $stagingZapret -Recurse -Force }
Copy-Item -Path $bundledZapret -Destination $stagingZapret -Recurse -Force

$isccCandidates = @(
    (Join-Path $ProjectDir "tools\innosetup\ISCC.exe"),
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Host "Inno Setup 6 not found. Installing via winget..." -ForegroundColor Yellow
    winget install --id JRSoftware.InnoSetup -e --accept-package-agreements --accept-source-agreements
    $iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if (-not $iscc) {
    Write-Error "ISCC.exe not found. Install Inno Setup 6: https://jrsoftware.org/isinfo.php"
}

$stagingAbs = (Resolve-Path $StagingDir).Path
$distAbs = (Resolve-Path $DistDir).Path

& $iscc "/DAppVersion=$version" "/DConfiguration=Release" "/DSourceDir=$stagingAbs" "/DOutputDir=$distAbs" $IssFile
if ($LASTEXITCODE -ne 0) { exit 1 }

$setupExe = Join-Path $DistDir "Aeroway-Setup.exe"

# Манифест обновлений (только Setup.exe на GitHub)
$manifest = @{
    version = $version
    installerUrl = "https://github.com/RaccoonLaptop/Aeroway/releases/download/v$version/Aeroway-Setup.exe"
} | ConvertTo-Json -Depth 3
Set-Content -Path (Join-Path $ProjectDir "update.json") -Value $manifest -Encoding UTF8

# Do not ship update.json inside the installed app (it would shadow GitHub checks).
$staleManifest = Join-Path $StagingDir "update.json"
if (Test-Path $staleManifest) { Remove-Item $staleManifest -Force }

Write-Host ""
Write-Host "Done: $setupExe" -ForegroundColor Green
Write-Host "Give users Aeroway-Setup.exe - one file, no .NET install." -ForegroundColor Yellow
