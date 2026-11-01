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
$IssFile = Join-Path $ProjectDir "installer\Aeroway.iss"

[xml]$csproj = Get-Content (Join-Path $ProjectDir "Aeroway.csproj")
$version = $csproj.Project.PropertyGroup.Version | Select-Object -First 1
if (-not $version) { $version = "1.0.0" }

Write-Host "Building Aeroway v$version (self-contained installer)..." -ForegroundColor Cyan

& (Join-Path $ProjectDir "build-icon.ps1")
& (Join-Path $ProjectDir "build-installer-art.ps1")

if (Test-Path $StagingDir) { Remove-Item $StagingDir -Recurse -Force }
New-Item -ItemType Directory -Path $StagingDir -Force | Out-Null
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

Push-Location $ProjectDir
# Skip the csproj copy of packaging/zapret into the publish folder.
# The block below places that same tree at staging/zapret, which is what the
# installed app runs. Copying both would ship cygwin, winws, WinDivert, and the
# ipset backup twice inside Aeroway-Setup.exe.
dotnet publish -c Release -r win-x64 --self-contained true `
    -p:PublishReadyToRun=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:AerowaySkipBundledZapretCopy=true `
    -o $StagingDir
if ($LASTEXITCODE -ne 0) { Pop-Location; exit 1 }
Pop-Location

# Compatibility copy. Shortcuts and the updater from already-installed builds
# may still launch ZapretUI.exe. InstallDirMigration removes the copy once
# the running process is Aeroway.exe.
Copy-Item -LiteralPath (Join-Path $StagingDir "Aeroway.exe") -Destination (Join-Path $StagingDir "ZapretUI.exe") -Force

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

# Манифест обновлений (только Setup.exe на GitHub).
# AppSelfUpdateService.DefaultManifestUrl reads the repo-root file from main.
# packaging/update.json is not loaded by the app and is not copied into the install;
# keep that committed copy identical so the two files cannot drift.
$manifest = @{
    version = $version
    installerUrl = "https://github.com/RaccoonLaptop/Aeroway/releases/download/v$version/Aeroway-Setup.exe"
} | ConvertTo-Json -Depth 3
$manifestPath = Join-Path $ProjectDir "update.json"
Set-Content -Path $manifestPath -Value $manifest -Encoding UTF8
Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $Packaging "update.json") -Force

# Do not ship update.json inside the installed app (it would shadow GitHub checks).
$staleManifest = Join-Path $StagingDir "update.json"
if (Test-Path $staleManifest) { Remove-Item $staleManifest -Force }

Write-Host ""
Write-Host "Done: $setupExe" -ForegroundColor Green
Write-Host "Give users Aeroway-Setup.exe - one file, no .NET install." -ForegroundColor Yellow
