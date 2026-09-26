# Deletes %LOCALAPPDATA%\ZapretUI after the app has moved to Aeroway.
# WinDivert64.sys stays locked until the driver service is removed (admin), same as service.bat "Remove Services".
param(
    [Parameter(Mandatory)][string]$LegacyPath,
    [int]$WaitPid = 0,
    [switch]$Elevated
)

$ErrorActionPreference = 'SilentlyContinue'
$legacy = [System.IO.Path]::GetFullPath($LegacyPath).TrimEnd('\')
if ([System.IO.Path]::GetFileName($legacy) -ne 'ZapretUI') { exit 1 }

function Test-IsAdmin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Stop-Locked([string]$dir) {
    $prefix = $dir + '\'
    Get-CimInstance Win32_Process | Where-Object {
        $_.ExecutablePath -and $_.ExecutablePath.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)
    } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
}

function Remove-LegacyDrivers([string]$dir) {
    foreach ($name in @('zapret', 'WinDivert', 'WinDivert14')) {
        $svc = Get-CimInstance Win32_Service -Filter "Name='$name'"
        if (-not $svc) { continue }
        $path = [string]$svc.PathName
        if (-not $path) { continue }
        if ($path.IndexOf($dir, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) { continue }
        sc.exe stop $name | Out-Null
        Start-Sleep -Milliseconds 500
        sc.exe delete $name | Out-Null
    }
    Start-Sleep -Seconds 1
}

function Remove-TreeForce([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return }
    cmd.exe /c "attrib -r -s -h `"$path\*`" /s /d" | Out-Null
    cmd.exe /c "rd /s /q `"$path`"" | Out-Null
}

function Test-NeedsDriverRemoval([string]$dir) {
    if (Test-Path -LiteralPath (Join-Path $dir 'zapret\bin\WinDivert64.sys')) { return $true }
    foreach ($name in @('zapret', 'WinDivert', 'WinDivert14')) {
        $svc = Get-CimInstance Win32_Service -Filter "Name='$name'"
        if ($svc -and [string]$svc.PathName -and ([string]$svc.PathName).IndexOf($dir, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $true
        }
    }
    return $false
}

if ($WaitPid -gt 0) {
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Process -Id $WaitPid -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 200
    }
}

$needsDriver = Test-NeedsDriverRemoval $legacy
if ($needsDriver -and -not (Test-IsAdmin) -and -not $Elevated) {
    $arg = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$PSCommandPath`" -LegacyPath `"$legacy`" -WaitPid 0 -Elevated"
    try {
        Start-Process -FilePath 'powershell.exe' -ArgumentList $arg -Verb RunAs -WindowStyle Hidden
        exit 0
    }
    catch {
        # User declined the prompt. The folder delete below still removes everything that is not locked.
    }
}

for ($i = 0; $i -lt 15; $i++) {
    if (-not (Test-Path -LiteralPath $legacy)) { break }
    Stop-Locked $legacy
    if ($needsDriver) { Remove-LegacyDrivers $legacy }
    Remove-TreeForce $legacy
    if (-not (Test-Path -LiteralPath $legacy)) { break }
    Start-Sleep -Seconds 2
}

Get-ChildItem -LiteralPath $env:TEMP -Force -ErrorAction SilentlyContinue | Where-Object {
    $_.Name -like 'ZapretUI-update-*' -or $_.Name -like 'ZapretUI-backup-*' -or $_.Name -eq 'ZapretUI-install-payload' -or $_.Name -eq 'ZapretUI-update.log'
} | ForEach-Object { Remove-TreeForce $_.FullName }

if (Test-Path -LiteralPath $legacy) {
    $runOnce = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\RunOnce'
    if (-not (Test-Path $runOnce)) { New-Item -Path $runOnce -Force | Out-Null }
    New-ItemProperty -Path $runOnce -Name 'AerowayRemoveZapretUI' -Value "cmd /c rd /s /q `"$legacy`"" -PropertyType String -Force | Out-Null
}
