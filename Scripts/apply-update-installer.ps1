param(
    [Parameter(Mandatory)][string]$InstallerPath,
    [Parameter(Mandatory)][string]$TargetDir,
    [Parameter(Mandatory)][int]$ProcessId,
    [Parameter(Mandatory)][string]$ExePath,
    [string]$LogFile = "",
    [string]$StagingDir = ""
)

$ErrorActionPreference = "Stop"

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

function Get-InstallerErrorMessage([int]$ExitCode) {
    switch ($ExitCode) {
        5 { return "Installer was aborted (exit code 5)" }
        default { return "Installer exited with code $ExitCode" }
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
    Write-Log "Starting Aeroway..."
    Write-Log "Starting: $launch"
    Start-Process -FilePath $launch -WorkingDirectory $TargetDir
}

. (Join-Path $PSScriptRoot 'stop-bypass.ps1')

try {
    Write-Log "Installer update started"
    Write-Log "Installer: $InstallerPath"
    Write-Log "Target: $TargetDir"

    if (-not (Test-Path -LiteralPath $InstallerPath)) {
        throw "Installer not found: $InstallerPath"
    }

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

    $args = @(
        "/VERYSILENT",
        "/SUPPRESSMSGBOXES",
        "/NORESTART",
        "/CLOSEAPPLICATIONS",
        "/FORCECLOSEAPPLICATIONS",
        "/DIR=`"$TargetDir`""
    )
    Write-Log "Installing update..."
    Write-Log "Running: $InstallerPath $($args -join ' ')"
    $proc = Start-Process -FilePath $InstallerPath -ArgumentList $args -Wait -PassThru
    if ($proc.ExitCode -ne 0) {
        throw (Get-InstallerErrorMessage $proc.ExitCode)
    }

    if ($StagingDir -and (Test-Path -LiteralPath $StagingDir)) {
        Remove-Item -LiteralPath $StagingDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    Start-ZapretUi
    Write-Log "Installer update completed"
    exit 0
}
catch {
    Write-Log "ERROR: $($_.Exception.Message)"
    Start-ZapretUi
    exit 1
}
