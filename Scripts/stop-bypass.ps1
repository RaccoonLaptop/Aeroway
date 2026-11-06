function Stop-BypassForUpdate {
    param(
        [scriptblock]$LogAction = { param($Message) }
    )

    & $LogAction "Stopping bypass (winws / WinDivert)..."

    Get-Process -Name 'winws' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    $deadline = (Get-Date).AddSeconds(8)
    while ((Get-Process -Name 'winws' -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 200
    }

    foreach ($name in @('WinDivert', 'WinDivert14', 'Monkey')) {
        $svc = Get-Service -Name $name -ErrorAction SilentlyContinue
        if (-not $svc -or $svc.Status -ne 'Running') { continue }

        sc.exe stop $name 2>$null | Out-Null
        $waitDeadline = (Get-Date).AddSeconds(5)
        while ((Get-Date) -lt $waitDeadline) {
            $current = Get-Service -Name $name -ErrorAction SilentlyContinue
            if (-not $current -or $current.Status -ne 'Running') { break }
            Start-Sleep -Milliseconds 150
        }
    }

    $zapret = Get-Service -Name 'zapret' -ErrorAction SilentlyContinue
    if ($zapret -and $zapret.Status -eq 'Running') {
        Stop-Service -Name 'zapret' -Force -ErrorAction SilentlyContinue
    }

    Start-Sleep -Milliseconds 400
    & $LogAction "Bypass stopped"
}
