#requires -Version 5.1
param([string]$GamePath = '')
. (Join-Path $PSScriptRoot 'Common.ps1')
try {
    Assert-GameClosed
    $game = Resolve-Game $GamePath
    $data = Join-Path $game 'UserData\DoorDetector'
    $manifest = Join-Path $data 'install.json'
    if (-not (Test-Path -LiteralPath $manifest)) { throw 'No Door Detector installation record found in this game folder.' }
    $record = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
    $mod = Join-Path $game 'Mods\DoorDetector.dll'
    if (Test-Path -LiteralPath $mod) {
        if ((File-Hash $mod) -ne $record.modHash) { throw 'DoorDetector.dll has changed since installation; it was left untouched.' }
        Remove-Item -LiteralPath $mod -Force
    }
    if ($record.backup -and (Test-Path -LiteralPath $record.backup)) { Copy-Item -LiteralPath $record.backup -Destination $mod -Force }
    $otherMods = @()
    foreach ($sub in @('Mods','Plugins')) {
        $dir = Join-Path $game $sub
        if (Test-Path -LiteralPath $dir) { $otherMods += @(Get-ChildItem -LiteralPath $dir -Filter '*.dll' -File -Recurse) }
    }
    $bootstrap = Join-Path $game 'version.dll'
    if ($record.createdBootstrap -and $otherMods.Count -eq 0 -and (Test-Path -LiteralPath $bootstrap)) {
        if ((File-Hash $bootstrap) -eq $record.bootstrapHash) { Remove-Item -LiteralPath $bootstrap -Force }
    }
    Remove-Item -LiteralPath $manifest -Force
    Write-Host 'Door Detector uninstalled. Game saves and reservation history were kept.' -ForegroundColor Green
    Write-Host 'Loader cache files remain; its bootstrap is kept only if other mods need it or it predates this installation.'
} catch { Write-Host $_.Exception.Message -ForegroundColor Red; exit 1 }
