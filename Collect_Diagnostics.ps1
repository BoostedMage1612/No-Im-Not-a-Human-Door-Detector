#requires -Version 5.1
param([string]$GamePath = '')
. (Join-Path $PSScriptRoot 'Common.ps1')
$stage = $null
try {
    $game = Resolve-Game $GamePath
    $stage = Join-Path $env:TEMP ('NINAH-Diagnostics-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stage | Out-Null
    foreach ($relative in @('UserData\DoorDetector\DoorDetector.log','MelonLoader\Latest.log')) {
        $file = Join-Path $game $relative
        if (Test-Path -LiteralPath $file) { Copy-Item -LiteralPath $file -Destination (Join-Path $stage ([IO.Path]::GetFileName($file))) }
    }
    # Game-specific Unity logs can contain the native crash information omitted
    # by the loader log. No separate save files or binary dumps are copied,
    # but Unity logs may themselves contain save data and local paths.
    $localLow = Join-Path (Split-Path -Parent $env:LOCALAPPDATA) 'LocalLow'
    $unityLogs = Join-Path $localLow 'Trioskaz\NoImNotAHuman'
    foreach ($name in @('Player.log','Player-prev.log')) {
        $file = Join-Path $unityLogs $name
        if (Test-Path -LiteralPath $file) { Copy-Item -LiteralPath $file -Destination (Join-Path $stage ('Unity-' + $name)) }
    }
    try {
        $events = @(Get-WinEvent -FilterHashtable @{
            LogName = 'Application'; StartTime = (Get-Date).AddHours(-2); Id = @(1000,1001,1026)
        } -ErrorAction Stop | Where-Object { $_.Message -match 'NoImNotAHuman' } | Select-Object -First 8 -Property @('TimeCreated','Id','ProviderName','Message'))
        if ($events.Count -gt 0) {
            $events | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $stage 'GameCrashEvents.json') -Encoding UTF8
        }
    } catch {
        'Game crash events were unavailable. Other diagnostics were still collected.' |
            Set-Content -LiteralPath (Join-Path $stage 'CrashEventsNote.txt') -Encoding UTF8
    }
    $hashes = @{}
    foreach ($relative in @('GameAssembly.dll','NoImNotAHuman_Data\il2cpp_data\Metadata\global-metadata.dat','Mods\DoorDetector.dll')) {
        $file = Join-Path $game $relative
        if (Test-Path -LiteralPath $file) { $hashes[$relative] = File-Hash $file }
    }
    $hashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stage 'BuildHashes.json') -Encoding UTF8
    $out = Join-Path $PSScriptRoot ('NINAH_Diagnostics_' + [DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss') + '_' + [guid]::NewGuid().ToString('N').Substring(0,6) + '.zip')
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $out
    Write-Host ('Created: ' + $out) -ForegroundColor Green
    Write-Host 'This contains game/loader logs, game-specific crash events and build hashes. Nothing was uploaded.'
} catch { Write-Host $_.Exception.Message -ForegroundColor Red; exit 1 }
finally { if ($stage -and (Test-Path -LiteralPath $stage)) { Remove-Item -LiteralPath $stage -Recurse -Force } }
