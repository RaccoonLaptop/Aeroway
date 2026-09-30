param(
    [Parameter(Mandatory)][string]$Action,
    [string]$Extra = "",
    [string]$Root = "",
    [string]$LogFile = ""
)

$ErrorActionPreference = "Continue"

if (-not $Root) {
    $Root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    if (-not (Test-Path (Join-Path $Root "service.bat"))) {
        $Root = Split-Path $PSScriptRoot -Parent
        while ($Root -and -not (Test-Path (Join-Path $Root "service.bat"))) {
            $Root = Split-Path $Root -Parent
        }
    }
}

$BinPath = Join-Path $Root "bin\"
$ListsPath = Join-Path $Root "lists\"

function Write-Color($Text, $Color = "White") {
    Write-Output "{COLOR:$Color}$Text"
    if ($LogFile) {
        try { Add-Content -LiteralPath $LogFile -Value $Text -Encoding UTF8 } catch { }
    }
}

function Test-GameFilterPortRange {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    $compact = $Value -replace '\s', ''
    if (-not $compact) { return $false }
    foreach ($item in ($compact -split ',')) {
        if ($item -notmatch '^[1-9]\d*(?:-[1-9]\d*)?$') { return $false }
        $parts = $item -split '-', 2
        $start = [int]$parts[0]
        $end = if ($parts.Count -gt 1) { [int]$parts[1] } else { $start }
        if ($start -lt 1 -or $end -lt 1 -or $start -gt 65535 -or $end -gt 65535 -or $start -gt $end) {
            return $false
        }
        if ($parts[0].Length -gt 5 -or ($parts.Count -gt 1 -and $parts[1].Length -gt 5)) {
            return $false
        }
    }
    return $true
}

function Get-GameFilterVars {
    $flag = Join-Path $Root "utils\game_filter.enabled"
    $mode = "disabled"
    $tcp = "1024-65535"
    $udp = "1024-65535"
    if (Test-Path $flag) {
        foreach ($raw in Get-Content $flag) {
            $line = $raw.Trim()
            if (-not $line) { continue }
            $eq = $line.IndexOf('=')
            if ($eq -ge 0) {
                $key = $line.Substring(0, $eq).Trim().ToLower()
                $val = $line.Substring($eq + 1).Trim()
            } else {
                $key = $line.ToLower()
                $val = ""
            }
            if ($key -eq "mode" -and $val) { $mode = $val.ToLower() }
            elseif ($key -eq "all" -and -not $val) { $mode = "all" }
            elseif ($key -eq "udp") {
                if ($val) { if (Test-GameFilterPortRange $val) { $udp = ($val -replace '\s', '') } }
                else { $mode = "udp" }
            }
            elseif ($key -eq "tcp") {
                if ($val) { if (Test-GameFilterPortRange $val) { $tcp = ($val -replace '\s', '') } }
                else { $mode = "tcp" }
            }
        }
    }

    switch ($mode) {
        "all" { return @{ GameFilter = $tcp; GameFilterTCP = $tcp; GameFilterUDP = $udp } }
        "tcp" { return @{ GameFilter = $tcp; GameFilterTCP = $tcp; GameFilterUDP = "12" } }
        "udp" { return @{ GameFilter = $udp; GameFilterTCP = "12"; GameFilterUDP = $udp } }
        default { return @{ GameFilter = "12"; GameFilterTCP = "12"; GameFilterUDP = "12" } }
    }
}

function Parse-StrategyArgs {
    param([string]$BatFile)
    $gf = Get-GameFilterVars
    $argsWithValue = @("sni", "host", "altorder")
    $lines = Get-Content -LiteralPath $BatFile
    $capture = $false
    $mergeargs = 0
    $result = ""
    $quote = '"'

    foreach ($rawLine in $lines) {
        $line = $rawLine -replace '!', 'EXCL_MARK'

        if ($line -match [regex]::Escape('%BIN%winws.exe')) {
            $capture = $true
            $line = $line -replace '.*["'']?%BIN%winws\.exe["'']?\s*', ''
        }
        elseif ($line -match 'winws\.exe') {
            $capture = $true
            $line = $line -replace '.*winws\.exe["'']?\s*', ''
        }
        elseif (-not $capture) {
            continue
        }

        if ([string]::IsNullOrWhiteSpace($line)) { continue }

        $tokens = $line -split '\s+' | Where-Object { $_ -and $_ -ne '^' }
        foreach ($arg in $tokens) {
            if ($arg.StartsWith("--") -and $mergeargs -ne 0) { $mergeargs = 0 }

            $a = $arg
            $a = $a -replace '%GameFilterTCP%', $gf.GameFilterTCP
            $a = $a -replace '%GameFilterUDP%', $gf.GameFilterUDP
            $a = $a -replace '%GameFilter%', $gf.GameFilter
            $a = $a -replace '%BIN%', $BinPath
            $a = $a -replace '%LISTS%', $ListsPath

            if ($a.StartsWith($quote) -and $a.EndsWith($quote) -and $a.Length -ge 2) {
                $a = $a.Substring(1, $a.Length - 2)
                if ($a -match ':') { $a = "$quote$a$quote" }
                elseif ($a.StartsWith('@')) { $a = "$quote$($Root)\$($a.Substring(1))$quote" }
                else { $a = "$quote$a$quote" }
            }

            if ($mergeargs -eq 1) { $result += ",$a" }
            elseif ($mergeargs -eq 3) { $result += "=$a"; $mergeargs = 1 }
            else { $result += " $a" }

            if ($a.StartsWith("--")) { $mergeargs = 2 }
            elseif ($mergeargs -ge 1) {
                if ($mergeargs -eq 2) { $mergeargs = 1 }
                foreach ($v in $argsWithValue) {
                    if ($arg -eq $v) { $mergeargs = 3; break }
                }
            }
        }
    }
    return ($result -replace 'EXCL_MARK', '!').Trim()
}

function Stop-WinwsAndWinDivert {
    param([switch]$DeleteWinDivert)

    Get-Process winws -ErrorAction SilentlyContinue | Stop-Process -Force
    $stopDeadline = (Get-Date).AddSeconds(5)
    while ((Get-Process winws -ErrorAction SilentlyContinue) -and (Get-Date) -lt $stopDeadline) {
        Start-Sleep -Milliseconds 200
    }

    foreach ($n in @("WinDivert", "WinDivert14")) {
        $s = Get-Service -Name $n -ErrorAction SilentlyContinue
        if ($s -and $s.Status -eq 'Running') {
            sc.exe stop $n 2>$null | Out-Null
            $waitDeadline = (Get-Date).AddSeconds(3)
            while ($true) {
                $svc = Get-Service -Name $n -ErrorAction SilentlyContinue
                if (-not $svc -or $svc.Status -ne 'Running') { break }
                if ((Get-Date) -ge $waitDeadline) { break }
                Start-Sleep -Milliseconds 150
            }
        }
        if ($DeleteWinDivert) {
            sc.exe delete $n 2>$null | Out-Null
        }
    }

    Start-Sleep -Milliseconds 200
}

switch ($Action) {
    "CheckStatus" {
        $svc = Get-Service -Name "zapret" -ErrorAction SilentlyContinue
        if ($svc) {
            $strat = (Get-ItemProperty "HKLM:\System\CurrentControlSet\Services\zapret" -Name "zapret-discord-youtube" -ErrorAction SilentlyContinue).'zapret-discord-youtube'
            if ($strat) { Write-Color "Service strategy: $strat" Cyan }
            Write-Color "zapret service: $($svc.Status)" $(if ($svc.Status -eq 'Running') { 'Green' } else { 'Yellow' })
        } else {
            Write-Color "zapret service: NOT installed" Yellow
        }

        foreach ($name in @("WinDivert")) {
            $s = Get-Service -Name $name -ErrorAction SilentlyContinue
            if ($s) { Write-Color "$name service: $($s.Status)" $(if ($s.Status -eq 'Running') { 'Green' } else { 'Yellow' }) }
            else { Write-Color "$name service: NOT installed" Yellow }
        }

        $sys = Get-ChildItem $BinPath -Filter "*.sys" -ErrorAction SilentlyContinue
        if (-not $sys) { Write-Color "WinDivert64.sys NOT found!" Red }

        $winws = Get-Process winws -ErrorAction SilentlyContinue
        if ($winws) { Write-Color "Bypass (winws.exe) is RUNNING" Green }
        else { Write-Color "Bypass (winws.exe) is NOT running" Red }
    }

    "InstallService" {
        if (-not $Extra) { Write-Color "Strategy file not specified" Red; exit 1 }
        $bat = Join-Path $Root $Extra
        if (-not (Test-Path $bat)) { Write-Color "File not found: $bat" Red; exit 1 }

        Push-Location $Root
        foreach ($prep in @("status_zapret", "load_user_lists", "load_game_filter")) {
            Start-Process -FilePath "cmd.exe" -ArgumentList "/c call service.bat $prep" `
                -WorkingDirectory $Root -WindowStyle Hidden -Wait | Out-Null
        }
        Pop-Location

        $parsed = Parse-StrategyArgs -BatFile $bat
        if (-not $parsed) { Write-Color "Could not parse winws arguments from $Extra" Red; exit 1 }

        netsh interface tcp set global timestamps=enabled | Out-Null
        sc.exe stop zapret 2>$null | Out-Null
        sc.exe delete zapret 2>$null | Out-Null

        $binPath = Join-Path $BinPath "winws.exe"
        if (-not (Test-Path $binPath)) { Write-Color "winws.exe not found: $binPath" Red; exit 1 }

        $create = sc.exe create zapret binPath= "`"$binPath`" $parsed" DisplayName= "zapret" start= auto 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Color "sc create failed: $create" Red
            exit 1
        }
        sc.exe description zapret "Zapret DPI bypass software" | Out-Null
        $start = sc.exe start zapret 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Color "sc start failed: $start" Red
            exit 1
        }

        $name = [System.IO.Path]::GetFileNameWithoutExtension($Extra)
        Set-ItemProperty "HKLM:\System\CurrentControlSet\Services\zapret" -Name "zapret-discord-youtube" -Value $name -Type String -Force
        Write-Color "Service installed: $Extra" Green
    }

    "RemoveServices" {
        $svc = Get-Service zapret -ErrorAction SilentlyContinue
        if ($svc) {
            Stop-Service zapret -Force -ErrorAction SilentlyContinue
            sc.exe delete zapret | Out-Null
            Write-Color "zapret service removed" Green
        } else { Write-Color "zapret service not installed" Yellow }

        Stop-WinwsAndWinDivert -DeleteWinDivert
        Write-Color "WinDivert services cleaned" Green
    }

    "RunDiagnostics" {
        Write-Color "[OK] Zapret is installed in: '$Root'" Green

        $bfe = Get-Service BFE -ErrorAction SilentlyContinue
        if ($bfe -and $bfe.Status -eq 'Running') { Write-Color "[OK] Base Filtering Engine check passed" Green }
        else { Write-Color "[X] Base Filtering Engine is not running. This service is required for zapret to work" Red }

        $proxy = Get-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings" -ErrorAction SilentlyContinue
        if ($proxy.ProxyEnable -eq 1) {
            Write-Color "[?] System proxy is enabled: $($proxy.ProxyServer)" Yellow
            Write-Color "[?] Make sure it's valid or disable it if you don't use a proxy" Yellow
        } else {
            Write-Color "[OK] Proxy check passed" Green
        }

        $ts = netsh interface tcp show global 2>$null
        if ($ts -match "timestamps" -and $ts -match "enabled") {
            Write-Color "[OK] TCP timestamps check passed" Green
        } else {
            netsh interface tcp set global timestamps=enabled | Out-Null
            if ($LASTEXITCODE -eq 0) { Write-Color "[OK] TCP timestamps successfully enabled" Green }
            else { Write-Color "[X] Failed to enable TCP timestamps" Red }
        }

        if (Get-Process AdguardSvc -ErrorAction SilentlyContinue) {
            Write-Color "[X] Adguard process found. Adguard may cause problems with Discord" Red
            Write-Color "[X] https://github.com/Flowseal/zapret-discord-youtube/issues/417" Red
        } else {
            Write-Color "[OK] Adguard check passed" Green
        }

        $killers = Get-Service | Where-Object { $_.Name -like "*Killer*" -or $_.DisplayName -like "*Killer*" }
        if ($killers) {
            Write-Color "[X] Killer services found. Killer conflicts with zapret" Red
            Write-Color "[X] https://github.com/Flowseal/zapret-discord-youtube/issues/2512#issuecomment-2821119513" Red
        } else {
            Write-Color "[OK] Killer check passed" Green
        }

        $intel = Get-Service | Where-Object {
            ($_.Name -like "*Intel*" -or $_.DisplayName -like "*Intel*") -and
            ($_.Name -like "*Connectivity*" -or $_.DisplayName -like "*Connectivity*") -and
            ($_.Name -like "*Network*" -or $_.DisplayName -like "*Network*")
        }
        if ($intel) {
            Write-Color "[X] Intel Connectivity Network Service found. It conflicts with zapret" Red
            Write-Color "[X] https://github.com/ValdikSS/GoodbyeDPI/issues/541#issuecomment-2661670982" Red
        } else {
            Write-Color "[OK] Intel Connectivity check passed" Green
        }

        $checkpoint = Get-Service | Where-Object {
            $_.Name -like "*TracSrvWrapper*" -or $_.DisplayName -like "*TracSrvWrapper*" -or
            $_.Name -like "*EPWD*" -or $_.DisplayName -like "*EPWD*"
        }
        if ($checkpoint) {
            Write-Color "[X] Check Point services found. Check Point conflicts with zapret" Red
            Write-Color "[X] Try to uninstall Check Point" Red
        } else {
            Write-Color "[OK] Check Point check passed" Green
        }

        $smartByte = Get-Service | Where-Object { $_.Name -like "*SmartByte*" -or $_.DisplayName -like "*SmartByte*" }
        if ($smartByte) {
            Write-Color "[X] SmartByte services found. SmartByte conflicts with zapret" Red
            Write-Color "[X] Try to uninstall or disable SmartByte through services.msc" Red
        } else {
            Write-Color "[OK] SmartByte check passed" Green
        }

        if ($Root -match '[\u0430-\u044f\u0410-\u042f\u0451\u0401]') {
            Write-Color "[?] The path where Zapret is installed contains Cyrillic characters" Yellow
            Write-Color "[?] If bypass doesn't work, try to move Zapret to another directory, for example in C:\zapret" Yellow
        } else {
            Write-Color "[OK] Cyrillic path check passed" Green
        }

        if ($env:OneDrive -and $Root.StartsWith($env:OneDrive, [StringComparison]::OrdinalIgnoreCase)) {
            Write-Color "[X] Zapret is installed in a OneDrive folder" Red
            Write-Color "[X] If bypass doesn't work, try to move Zapret to another directory, for example in C:\zapret" Red
        } else {
            Write-Color "[OK] OneDrive check passed" Green
        }

        $sys = Get-ChildItem $BinPath -Filter "*.sys" -ErrorAction SilentlyContinue
        if (-not $sys) { Write-Color "[X] WinDivert64.sys file NOT found." Red }
        else { Write-Color "[OK] WinDivert driver present" Green }

        $vpnServices = Get-Service | Where-Object { $_.Name -like "*VPN*" -or $_.DisplayName -like "*VPN*" }
        if ($vpnServices) {
            $names = ($vpnServices | ForEach-Object { $_.DisplayName }) -join ', '
            Write-Color "[?] VPN services found: $names. Some VPNs can conflict with zapret" Yellow
            Write-Color "[?] Make sure that all VPNs are disabled" Yellow
        } else {
            Write-Color "[OK] VPN check passed" Green
        }

        $isWin11OrNewer = [Environment]::OSVersion.Version.Build -ge 22000
        try {
            $dohCount = Get-ChildItem -Recurse -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\Dnscache\InterfaceSpecificParameters\' -ErrorAction SilentlyContinue |
                Get-ItemProperty -ErrorAction SilentlyContinue |
                Where-Object { $_.DohFlags -gt 0 } |
                Measure-Object |
                Select-Object -ExpandProperty Count
            if ($dohCount -gt 0) {
                Write-Color "[OK] Secure DNS check passed" Green
            } elseif ($isWin11OrNewer) {
                Write-Color "[?] Make sure you have configured secure DNS in a browser with some non-default DNS service provider," Yellow
                Write-Color "[?] If you use Windows 11 you can configure encrypted DNS in the Settings to hide this warning" Yellow
            }
        } catch {
            if ($isWin11OrNewer) {
                Write-Color "[?] Make sure you have configured secure DNS in a browser with some non-default DNS service provider," Yellow
                Write-Color "[?] If you use Windows 11 you can configure encrypted DNS in the Settings to hide this warning" Yellow
            }
        }

        $hostsFile = "$env:SystemRoot\System32\drivers\etc\hosts"
        if (Test-Path $hostsFile) {
            $hostsContent = Get-Content $hostsFile -ErrorAction SilentlyContinue
            $ytHosts = $hostsContent | Where-Object { $_ -match '(?i)youtube\.com|youtu\.be' }
            if ($ytHosts) {
                Write-Color "[?] Your hosts file contains entries for youtube.com or youtu.be. This may cause problems with YouTube access" Yellow
            }
        }

        $winwsRunning = [bool](Get-Process winws -ErrorAction SilentlyContinue)
        $windivert = Get-Service WinDivert -ErrorAction SilentlyContinue
        $windivertActive = $windivert -and ($windivert.Status -in @('Running', 'StopPending'))
        if (-not $winwsRunning -and $windivertActive) {
            Write-Color "[?] winws.exe is not running but WinDivert service is active. Attempting to delete WinDivert..." Yellow
            sc.exe stop WinDivert 2>$null | Out-Null
            sc.exe delete WinDivert 2>$null | Out-Null
            $windivertAfter = Get-Service WinDivert -ErrorAction SilentlyContinue
            if ($windivertAfter) {
                Write-Color "[X] Failed to delete WinDivert. Checking for conflicting services..." Red
                if (Get-Service GoodbyeDPI -ErrorAction SilentlyContinue) {
                    Write-Color "[?] Found conflicting service: GoodbyeDPI. Stopping and removing..." Yellow
                    Stop-Service GoodbyeDPI -Force -ErrorAction SilentlyContinue
                    sc.exe delete GoodbyeDPI 2>$null | Out-Null
                    sc.exe stop WinDivert 2>$null | Out-Null
                    sc.exe delete WinDivert 2>$null | Out-Null
                    if (-not (Get-Service WinDivert -ErrorAction SilentlyContinue)) {
                        Write-Color "[OK] WinDivert successfully deleted after removing conflicting services" Green
                    } else {
                        Write-Color "[X] WinDivert still cannot be deleted. Check manually if any other bypass is using WinDivert." Red
                    }
                } else {
                    Write-Color "[X] No conflicting services found. Check manually if any other bypass is using WinDivert." Red
                }
            } else {
                Write-Color "[OK] WinDivert successfully removed" Green
            }
        }

        $conflicts = @("GoodbyeDPI", "discordfix_zapret", "winws1", "winws2") | Where-Object {
            Get-Service $_ -ErrorAction SilentlyContinue
        }
        if ($conflicts) {
            Write-Color "[X] Conflicting bypass services found: $($conflicts -join ', ')" Red
            foreach ($c in $conflicts) {
                Write-Color "  Stopping and removing service: $c" Yellow
                Stop-Service $c -Force -ErrorAction SilentlyContinue
                sc.exe delete $c | Out-Null
                if ($LASTEXITCODE -eq 0) { Write-Color "[OK] Successfully removed service: $c" Green }
                else { Write-Color "[X] Failed to remove service: $c" Red }
            }
            foreach ($n in @("WinDivert", "WinDivert14")) {
                sc.exe stop $n 2>$null | Out-Null
                sc.exe delete $n 2>$null | Out-Null
            }
        } else {
            Write-Color "[OK] No conflicting bypass services" Green
        }

        if ($winwsRunning) { Write-Color "[OK] Bypass (winws.exe) is RUNNING" Green }
        else { Write-Color "[?] Bypass (winws.exe) is NOT running" Yellow }

        Write-Color "Diagnostics complete" Cyan
    }

    "UpdateHosts" {
        $hostsUrls = @(
            "https://raw.githubusercontent.com/Flowseal/zapret-discord-youtube/refs/heads/main/.service/hosts",
            "https://raw.githubusercontent.com/Flowseal/zapret-discord-youtube/main/.service/hosts"
        )
        $tempFile = Join-Path $env:TEMP "zapret_hosts.txt"
        $hostsFile = "$env:SystemRoot\System32\drivers\etc\hosts"
        $downloaded = $false
        foreach ($hostsUrl in $hostsUrls) {
            try {
                Invoke-WebRequest -Uri $hostsUrl -OutFile $tempFile -UseBasicParsing -TimeoutSec 15 -Headers @{ 'User-Agent' = 'Aeroway' }
                if (Test-Path $tempFile) {
                    $downloaded = $true
                    break
                }
            } catch { }
        }
        if (-not $downloaded) {
            $fallbacks = @(
                (Join-Path $Root ".service\hosts"),
                (Join-Path (Split-Path $PSScriptRoot -Parent) "packaging\zapret\.service\hosts")
            )
            foreach ($fallback in $fallbacks) {
                if (Test-Path $fallback) {
                    Copy-Item -Path $fallback -Destination $tempFile -Force
                    $downloaded = $true
                    Write-Color "Using local hosts fallback: $fallback" Yellow
                    break
                }
            }
        }
        if (-not $downloaded) {
            Write-Color "Failed to download hosts file and no local fallback is available" Red
            exit 1
        }
        $lines = Get-Content $tempFile | ForEach-Object { $_.Trim() } | Where-Object { $_ -and -not $_.StartsWith('#') }
        if (-not $lines -or $lines.Count -lt 1) {
            Write-Color "Hosts reference file is empty" Red
            exit 1
        }
        $first = $lines[0]; $last = $lines[-1]
        $hostsContent = Get-Content $hostsFile -ErrorAction SilentlyContinue | ForEach-Object { $_.Trim() }
        $needs = ($hostsContent -notcontains $first) -or ($hostsContent -notcontains $last)
        if ($needs) {
            Write-Color "Hosts file needs update. Opening files..." Yellow
            Start-Process notepad $tempFile
            Start-Process explorer "/select,$hostsFile"
        } else {
            Write-Color "Hosts file is up to date" Green
            Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
        }
    }

    "StartStrategy" {
        $quick = $false
        $batName = $Extra
        if ($Extra -match '^quick\|(.+)$') {
            $quick = $true
            $batName = $Matches[1]
        }

        if (-not $batName) { Write-Color "Strategy file not specified" Red; exit 1 }
        $bat = Join-Path $Root $batName
        if (-not (Test-Path $bat)) { Write-Color "File not found: $bat" Red; exit 1 }

        Stop-WinwsAndWinDivert

        Push-Location $Root
        $prepSteps = if ($quick) {
            @("load_game_filter", "load_user_lists")
        } else {
            @("status_zapret", "check_updates", "load_game_filter", "load_user_lists")
        }
        foreach ($prep in $prepSteps) {
            Start-Process -FilePath "cmd.exe" -ArgumentList "/c call service.bat $prep" `
                -WorkingDirectory $Root -WindowStyle Hidden -Wait | Out-Null
        }
        Pop-Location

        $parsed = Parse-StrategyArgs -BatFile $bat
        if (-not $parsed) { Write-Color "Could not parse winws arguments from $batName" Red; exit 1 }

        $winwsPath = Join-Path $BinPath "winws.exe"
        if (-not (Test-Path $winwsPath)) { Write-Color "winws.exe not found" Red; exit 1 }

        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = $winwsPath
        $psi.Arguments = $parsed
        $psi.WorkingDirectory = $BinPath.TrimEnd('\')
        $psi.UseShellExecute = $false
        $psi.CreateNoWindow = $true
        $psi.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
        $proc = [System.Diagnostics.Process]::Start($psi)
        if (-not $proc) { Write-Color "Failed to start winws.exe" Red; exit 1 }

        $initialWait = if ($quick) { 300 } else { 900 }
        $stableSeconds = if ($quick) { 1 } else { 3 }
        Start-Sleep -Milliseconds $initialWait
        $running = Get-Process winws -ErrorAction SilentlyContinue
        if (-not $running) {
            Write-Color "winws.exe завершился сразу. Запустите Aeroway от имени администратора." Red
            exit 1
        }

        $stableDeadline = (Get-Date).AddSeconds($stableSeconds)
        while ((Get-Date) -lt $stableDeadline) {
            Start-Sleep -Milliseconds 200
            if (-not (Get-Process winws -ErrorAction SilentlyContinue)) {
                Write-Color "winws.exe завершился сразу. Запустите Aeroway от имени администратора." Red
                exit 1
            }
        }

        $name = [System.IO.Path]::GetFileNameWithoutExtension($batName)
        Write-Color "Strategy started (hidden): $name" Green
    }

    "RunTests" {
        $testScript = Join-Path $Root "utils\test zapret.ps1"
        if (-not (Test-Path $testScript)) { Write-Color "Test script not found" Red; exit 1 }
        & $testScript
    }

    default {
        Write-Color "Unknown action: $Action" Red
        exit 1
    }
}
