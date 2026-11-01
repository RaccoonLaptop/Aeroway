# Commits the manifests produced by build-installer.ps1 onto main.
# Call this only after Aeroway-Setup.exe is already on the GitHub Release.
param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"
# git writes progress to stderr. Keep that from becoming a terminating error on PowerShell 7.
if (Get-Variable -Name PSNativeCommandUseErrorActionPreference -Scope Global -ErrorAction SilentlyContinue) {
    $Global:PSNativeCommandUseErrorActionPreference = $false
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must look like 1.2.3 (got '$Version')."
}

$expectedUrl = "https://github.com/RaccoonLaptop/Aeroway/releases/download/v$Version/Aeroway-Setup.exe"

function Read-UpdateManifest {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Missing manifest: $Path"
    }

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    if ($text.Length -gt 0 -and [int][char]$text[0] -eq 0xFEFF) {
        $text = $text.Substring(1)
    }

    $manifest = $text | ConvertFrom-Json
    $manifestVersion = [string]$manifest.version
    $installerUrl = [string]$manifest.installerUrl
    if ($manifestVersion.Trim() -ne $Version) {
        throw "$Path version '$manifestVersion' does not match $Version."
    }
    if ($installerUrl.Trim() -ne $expectedUrl) {
        throw "$Path installerUrl '$installerUrl' does not match $expectedUrl."
    }

    return $text
}

function Write-RepoManifest {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    $normalized = $Text.Replace("`r`n", "`n").Replace("`r", "`n")
    if (-not $normalized.EndsWith("`n")) {
        $normalized += "`n"
    }

    $encoding = New-Object System.Text.UTF8Encoding $true
    [System.IO.File]::WriteAllText($Destination, $normalized, $encoding)
}

function Get-ManifestVersion {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    if ($text.Length -gt 0 -and [int][char]$text[0] -eq 0xFEFF) {
        $text = $text.Substring(1)
    }

    return ([string](($text | ConvertFrom-Json).version)).Trim()
}

$repoRoot = (& git rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repoRoot)) {
    throw "git rev-parse failed"
}
Set-Location -LiteralPath $repoRoot

$rootOnTag = Join-Path $repoRoot "update.json"
$packagingOnTag = Join-Path $repoRoot "packaging\update.json"
$rootText = [string](Read-UpdateManifest -Path $rootOnTag)
$packagingText = [string](Read-UpdateManifest -Path $packagingOnTag)
$rootNormalized = $rootText.Replace("`r`n", "`n").Replace("`r", "`n").Trim()
$packagingNormalized = $packagingText.Replace("`r`n", "`n").Replace("`r", "`n").Trim()
if ($rootNormalized -ne $packagingNormalized) {
    throw "update.json and packaging/update.json differ after build-installer.ps1."
}

# Drop installer output and regenerated icons before switching to main.
& git reset --hard HEAD
if ($LASTEXITCODE -ne 0) { throw "git reset failed" }

& git fetch origin main
if ($LASTEXITCODE -ne 0) { throw "git fetch origin main failed" }

& git checkout -B manifest-sync origin/main
if ($LASTEXITCODE -ne 0) { throw "git checkout origin/main failed" }

$currentVersion = Get-ManifestVersion -Path (Join-Path $repoRoot "update.json")
if ($currentVersion -and ([version]$Version -lt [version]$currentVersion)) {
    Write-Host "main/update.json is $currentVersion, which is newer than $Version. Leaving both manifests unchanged."
    exit 0
}

Write-RepoManifest -Text $rootText -Destination (Join-Path $repoRoot "update.json")
Write-RepoManifest -Text $packagingText -Destination (Join-Path $repoRoot "packaging\update.json")

& git -c core.autocrlf=false add -- update.json packaging/update.json
if ($LASTEXITCODE -ne 0) { throw "git add failed" }

& git -c core.autocrlf=false diff --cached --quiet
$diffExit = $LASTEXITCODE
if ($diffExit -eq 0) {
    Write-Host "main already has update.json and packaging/update.json for $Version."
    exit 0
}
if ($diffExit -ne 1) {
    throw "git diff failed with exit code $diffExit"
}

Write-Host "Committing manifests:"
& git -c core.quotepath=false status --short -- update.json packaging/update.json

& git -c user.name="github-actions[bot]" -c user.email="41898282+github-actions[bot]@users.noreply.github.com" -c core.autocrlf=false commit -m "Point Aeroway updates at the $Version installer."
if ($LASTEXITCODE -ne 0) { throw "git commit failed" }

& git push origin HEAD:main
if ($LASTEXITCODE -eq 0) {
    Write-Host "Pushed update.json and packaging/update.json to main for $Version."
    exit 0
}

Write-Host "Push rejected. Fetching main and rebasing the manifest commit."
& git fetch origin main
if ($LASTEXITCODE -ne 0) { throw "git fetch origin main failed" }

& git rebase origin/main
if ($LASTEXITCODE -ne 0) {
    throw "Could not rebase the update.json commit onto main. The release asset is already uploaded, so AppSelfUpdateService can still see v$Version from the GitHub releases API. Commit update.json and packaging/update.json to main so the raw manifest matches."
}

& git push origin HEAD:main
if ($LASTEXITCODE -ne 0) {
    throw "Could not push update.json to main. Allow GitHub Actions to write to main (Settings, Actions, General, Workflow permissions, Read and write). The release asset is already uploaded."
}

Write-Host "Pushed update.json and packaging/update.json to main for $Version."
exit 0
