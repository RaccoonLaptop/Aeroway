# Stop Zapret / WinDivert before uninstall (requires administrator)
$ErrorActionPreference = 'SilentlyContinue'

Get-Process -Name 'winws' -ErrorAction SilentlyContinue | Stop-Process -Force

$zapret = Get-Service -Name 'zapret' -ErrorAction SilentlyContinue
if ($zapret) {
    Stop-Service -Name 'zapret' -Force -ErrorAction SilentlyContinue
    sc.exe delete zapret | Out-Null
}

foreach ($name in @('WinDivert', 'WinDivert14', 'Monkey')) {
    sc.exe stop $name 2>$null | Out-Null
    sc.exe delete $name 2>$null | Out-Null
}

Start-Sleep -Seconds 1
Write-Host 'Zapret / WinDivert stopped.'
