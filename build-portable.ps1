# Build portable ZapretUI into ZapretUI-Program folder + update package
$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$OutDir = Join-Path $Root "build\portable"
$UpdateDir = Join-Path $Root "build\update"
$Packaging = Join-Path $PSScriptRoot "packaging"
$ZipName = "ZapretUI-Program.zip"

# Read version from csproj
[xml]$csproj = Get-Content (Join-Path $PSScriptRoot "ZapretUI.csproj")
$version = $csproj.Project.PropertyGroup.Version | Select-Object -First 1
if (-not $version) { $version = "1.0.0" }

Write-Host "Building ZapretUI v$version..." -ForegroundColor Cyan
Push-Location $PSScriptRoot
dotnet publish -c Release -r win-x64 --self-contained false -o $OutDir
if ($LASTEXITCODE -ne 0) { Pop-Location; exit 1 }
Pop-Location

Get-ChildItem $Packaging -File | Where-Object { $_.Name -ne 'update.json' } | ForEach-Object {
    Copy-Item $_.FullName $OutDir -Force
}

# Create update package folder
New-Item -ItemType Directory -Path $UpdateDir -Force | Out-Null
if (Test-Path (Join-Path $UpdateDir $ZipName)) { Remove-Item (Join-Path $UpdateDir $ZipName) -Force }
Compress-Archive -Path "$OutDir\*" -DestinationPath (Join-Path $UpdateDir $ZipName) -Force

$manifest = @{
    version = $version
    packageFile = $ZipName
    downloadUrl = ""
} | ConvertTo-Json -Depth 3
Set-Content -Path (Join-Path $UpdateDir "update.json") -Value $manifest -Encoding UTF8

Write-Host "Done: $OutDir" -ForegroundColor Green
Write-Host "Update package: $UpdateDir\$ZipName" -ForegroundColor Green
Write-Host "Run: Aeroway.exe" -ForegroundColor Yellow
