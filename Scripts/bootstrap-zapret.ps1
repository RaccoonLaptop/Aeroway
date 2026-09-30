param(
    [Parameter(Mandatory = $true)]
    [string]$TargetDir
)

$ErrorActionPreference = 'Stop'

function Test-ZapretRoot {
    param([string]$Dir)
    return (Test-Path (Join-Path $Dir 'service.bat')) -and (Test-Path (Join-Path $Dir 'bin'))
}

function Get-ZapretPackageRoot {
    param([string]$Dir)
    if (Test-ZapretRoot $Dir) { return $Dir }
    foreach ($sub in Get-ChildItem -LiteralPath $Dir -Directory -ErrorAction SilentlyContinue) {
        if (Test-ZapretRoot $sub.FullName) { return $sub.FullName }
    }
    return $null
}

function Copy-BundledZapret {
    param(
        [string]$SourceDir,
        [string]$TargetDir
    )

    if (-not (Test-ZapretRoot $SourceDir)) { return $false }

    New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
    Copy-Item -Path (Join-Path $SourceDir '*') -Destination $TargetDir -Recurse -Force
    return (Test-ZapretRoot $TargetDir)
}

function Install-DefaultTargets {
    param([string]$TargetDir)

    $utilsDir = Join-Path $TargetDir 'utils'
    $targetsFile = Join-Path $utilsDir 'targets.txt'
    $bundledTargets = Join-Path $PSScriptRoot 'targets.txt'
    if (-not (Test-Path $targetsFile) -and (Test-Path $bundledTargets)) {
        New-Item -ItemType Directory -Path $utilsDir -Force | Out-Null
        Copy-Item -Path $bundledTargets -Destination $targetsFile -Force
        Write-Host "Installed default targets.txt for strategy tests."
    }
}

if (Test-ZapretRoot $TargetDir) {
    Write-Host "Zapret already installed at $TargetDir"
    exit 0
}

$bundledSource = Join-Path (Split-Path $PSScriptRoot -Parent) 'packaging\zapret'
$headers = @{ 'User-Agent' = 'Aeroway' }

try {
    Write-Host 'Fetching latest Flowseal release...'
    $release = Invoke-RestMethod -Uri 'https://api.github.com/repos/Flowseal/zapret-discord-youtube/releases/latest' -Headers $headers
    $asset = $release.assets | Where-Object { $_.name -like '*.zip' } | Select-Object -First 1
    if (-not $asset) {
        $asset = $release.assets | Where-Object { $_.name -like '*.rar' } | Select-Object -First 1
    }
    if (-not $asset) { throw 'Zip/RAR asset not found in latest release.' }

    $tmp = Join-Path $env:TEMP ("zapret-bootstrap-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $tmp -Force | Out-Null
    try {
        $archivePath = Join-Path $tmp $asset.name
        Write-Host "Downloading $($asset.name)..."
        Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $archivePath -Headers $headers

        $extractDir = Join-Path $tmp 'extract'
        New-Item -ItemType Directory -Path $extractDir -Force | Out-Null

        if ($asset.name -like '*.rar') {
            $sevenZip = @(
                "${env:ProgramFiles}\7-Zip\7z.exe",
                "${env:ProgramFiles(x86)}\7-Zip\7z.exe"
            ) | Where-Object { Test-Path $_ } | Select-Object -First 1
            if (-not $sevenZip) { throw '7-Zip is required to extract .rar release.' }
            & $sevenZip x $archivePath "-o$extractDir" -y | Out-Null
            if ($LASTEXITCODE -ne 0) { throw 'Failed to extract RAR archive.' }
        } else {
            Expand-Archive -Path $archivePath -DestinationPath $extractDir -Force
        }

        $packageRoot = Get-ZapretPackageRoot $extractDir
        if (-not $packageRoot) {
            throw 'Downloaded package is invalid (service.bat or bin missing).'
        }

        New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
        Copy-Item -Path (Join-Path $packageRoot '*') -Destination $TargetDir -Recurse -Force

        if (-not (Test-ZapretRoot $TargetDir)) {
            throw 'Downloaded package is invalid (service.bat or bin missing).'
        }

        Install-DefaultTargets -TargetDir $TargetDir
        Write-Host "Zapret installed to $TargetDir (version $($release.tag_name))"
    }
    finally {
        if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue }
    }
}
catch {
    if (Copy-BundledZapret -SourceDir $bundledSource -TargetDir $TargetDir) {
        Install-DefaultTargets -TargetDir $TargetDir
        Write-Host "Zapret installed from bundled package at $TargetDir (GitHub unavailable: $($_.Exception.Message))"
        exit 0
    }

    throw
}
