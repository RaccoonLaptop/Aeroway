param(
    [Parameter(Mandatory)][string]$SourceDir,
    [Parameter(Mandatory)][string]$TargetDir,
    [Parameter(Mandatory)][int]$ProcessId,
    [Parameter(Mandatory)][string]$ExePath,
    [string]$LogFile = "",
    [string]$StagingDir = ""
)

$ErrorActionPreference = "Stop"

# Пользовательские данные — не удалять при обновлении
$PreserveNames = @('zapret', 'settings.json')

function Write-Log($Message) {
    if (-not $LogFile) { return }
    $line = "[$(Get-Date -Format 'HH:mm:ss')] $Message"
    for ($try = 0; $try -lt 10; $try++) {
        try {
            $stream = [System.IO.File]::Open(
                $LogFile,
                [System.IO.FileMode]::Append,
                [System.IO.FileAccess]::Write,
                [System.IO.FileShare]::ReadWrite)
            try {
                $bytes = [System.Text.Encoding]::UTF8.GetBytes("$line`r`n")
                $stream.Write($bytes, 0, $bytes.Length)
            }
            finally {
                $stream.Dispose()
            }
            return
        }
        catch {
            Start-Sleep -Milliseconds 100
        }
    }
}

function Remove-DirContentsExcept {
    param([string]$Dir, [string[]]$KeepNames)
    if (-not (Test-Path -LiteralPath $Dir)) { return }
    Get-ChildItem -LiteralPath $Dir -Force | ForEach-Object {
        if ($KeepNames -contains $_.Name) {
            Write-Log "Preserve: $($_.Name)"
            return
        }
        Write-Log "Remove: $($_.Name)"
        Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction Stop
    }
}

function Copy-Tree {
    param([string]$From, [string]$To, [string[]]$SkipNames)
    New-Item -ItemType Directory -Path $To -Force | Out-Null
    Get-ChildItem -LiteralPath $From -Force | ForEach-Object {
        if ($SkipNames -contains $_.Name) {
            Write-Log "Skip copy: $($_.Name)"
            return
        }
        $dest = Join-Path $To $_.Name
        Copy-Item -LiteralPath $_.FullName -Destination $dest -Recurse -Force
    }
}

function Stop-ZapretUiProcess {
    Write-Log "Closing Aeroway so files can be replaced..."
    Get-Process -Name 'ZapretUI','Aeroway' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    $deadline = (Get-Date).AddSeconds(15)
    while ((Get-Process -Name 'ZapretUI','Aeroway' -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 200
    }
}

function Start-ZapretUi {
    $launch = Join-Path $TargetDir "Aeroway.exe"
    if (-not (Test-Path -LiteralPath $launch)) {
        $launch = Join-Path $TargetDir "ZapretUI.exe"
    }
    if (-not (Test-Path -LiteralPath $launch)) { return }
    Write-Log "Запуск Aeroway..."
    Write-Log "Starting: $launch"
    Start-Process -FilePath $launch -WorkingDirectory $TargetDir
}

. (Join-Path $PSScriptRoot 'stop-bypass.ps1')

try {
    Write-Log "Updater started (full replace)"
    Write-Log "Source: $SourceDir"
    Write-Log "Target: $TargetDir"

    if ($ProcessId -gt 0) {
        Write-Log "Waiting for application to close (PID $ProcessId)..."
        $waited = 0
        while ($waited -lt 120) {
            if (-not (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)) { break }
            Start-Sleep -Milliseconds 500
            $waited++
        }
        Start-Sleep -Seconds 2
    }

    Stop-BypassForUpdate -LogAction { param($Message) Write-Log $Message }
    Stop-ZapretUiProcess

    if (-not (Test-Path -LiteralPath $SourceDir)) {
        throw "Source folder not found: $SourceDir"
    }

    $backupDir = Join-Path $env:TEMP ("ZapretUI-backup-" + [guid]::NewGuid().ToString("N"))
    Write-Log "Backup program files: $backupDir"
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null

    if (Test-Path -LiteralPath $TargetDir) {
        Get-ChildItem -LiteralPath $TargetDir -Force | ForEach-Object {
            if ($PreserveNames -contains $_.Name) { return }
            Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $backupDir $_.Name) -Recurse -Force
        }
    }

    New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null

    $copied = $false
    for ($i = 1; $i -le 20; $i++) {
        try {
            Write-Log "Установка обновления (попытка $i)..."
            Remove-DirContentsExcept -Dir $TargetDir -KeepNames $PreserveNames
            Copy-Tree -From $SourceDir -To $TargetDir -SkipNames @('zapret')
            $copied = $true
            break
        }
        catch {
            Write-Log "Replace failed: $($_.Exception.Message)"
            Start-Sleep -Seconds 1
        }
    }

    if (-not $copied) {
        Write-Log "Restore from backup..."
        Remove-DirContentsExcept -Dir $TargetDir -KeepNames $PreserveNames
        if (Test-Path -LiteralPath $backupDir) {
            Copy-Tree -From $backupDir -To $TargetDir -SkipNames @()
        }
        throw "Failed to apply update after multiple attempts"
    }

    $required = @(
        (Join-Path $TargetDir "Aeroway.exe"),
        (Join-Path $TargetDir "Assets\BatchZapret.xshd")
    )
    foreach ($f in $required) {
        if (-not (Test-Path -LiteralPath $f)) {
            Write-Log "WARNING: missing after update: $f"
        }
    }

    Write-Log "Update applied successfully (full replace)"
    Remove-Item -LiteralPath $backupDir -Recurse -Force -ErrorAction SilentlyContinue

    if ($StagingDir -and (Test-Path -LiteralPath $StagingDir)) {
        Write-Log "Remove staging: $StagingDir"
        Remove-Item -LiteralPath $StagingDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    if ($SourceDir -match 'ZapretUI-install-payload') {
        $payloadRoot = Split-Path $SourceDir -Parent
        if (Test-Path -LiteralPath $payloadRoot) {
            Write-Log "Remove install payload: $payloadRoot"
            Remove-Item -LiteralPath $payloadRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    Start-ZapretUi
    exit 0
}
catch {
    Write-Log "ERROR: $($_.Exception.Message)"
    Start-ZapretUi
    exit 1
}
