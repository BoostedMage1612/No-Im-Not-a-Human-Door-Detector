#requires -Version 5.1
param([string]$GamePath = '')
. (Join-Path $PSScriptRoot 'Common.ps1')
$stage = $null
$createdLoader = $false
$createdBootstrap = $false
$createdRuntime = $false
$modWritten = $false
$committed = $false
$backup = $null
$game = $null
try {
    Write-Host "No, I'm not a Human - Door Detector 0.1.3 (top-right panel)" -ForegroundColor Cyan
    Assert-GameClosed
    $game = Resolve-Game $GamePath
    Write-Host ('Game folder: ' + $game)
    if ((File-Hash (Join-Path $game 'GameAssembly.dll')) -ne 'DE2C120D90E757916D74C298F264040DF0CDA1DDAA8C412E438AEEE72EB4DC3F' -or
        (File-Hash (Join-Path $game 'NoImNotAHuman_Data\il2cpp_data\Metadata\global-metadata.dat')) -ne '1773FD058179E577578E08E9B94155B53B029927967F6B189727D709435923C7') {
        throw 'This is a different game build. Nothing installed. This detector targets your collected Steam build 23967901.'
    }
    $data = Join-Path $game 'UserData\DoorDetector'
    $manifest = Join-Path $data 'install.json'
    $mod = Join-Path $game 'Mods\DoorDetector.dll'
    $dllHash = File-Hash (Join-Path $PSScriptRoot 'DoorDetector.dll')
    if ((Test-Path -LiteralPath $manifest) -and (Test-Path -LiteralPath $mod)) {
        if ((File-Hash $mod) -eq $dllHash) { Write-Host 'Door Detector is already installed.' -ForegroundColor Green; exit 0 }
        $previous = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
        $oldHash = File-Hash $mod
        $knownVersions = @{
            '0.1.0' = 'C0963A12727BB1E9A2C4E4A0D393A69769BF71ACD7FADAAD03CB06EBC57DA0E8'
            '0.1.1' = '4A9B8159684462E8BE3C5B1AC4D4529FBD5CC4A1B9473FECD0EE8C34FA7DD646'
            '0.1.2' = 'E1EA91DAF0AEE66F0F9B35C9A6D890C08F033EA1648C4EB4F9D3582877D53710'
        }
        if (-not $knownVersions.ContainsKey([string]$previous.version) -or
            $previous.modHash -ne $oldHash -or $knownVersions[[string]$previous.version] -ne $oldHash) {
            throw 'The installed detector differs from the known 0.1.0/0.1.1/0.1.2 builds. It was left untouched.'
        }
        # Preserve the original install/uninstall provenance. The previous DLL is
        # backed up for rollback only, not made the uninstaller's restore target.
        $oldVersion = [string]$previous.version
        $backup = Join-Path $data ('DoorDetector.v' + $oldVersion + '.rollback.' + [guid]::NewGuid().ToString('N') + '.bak')
        Copy-Item -LiteralPath $mod -Destination $backup
        $manifestTemp = Join-Path $data ('install.' + [guid]::NewGuid().ToString('N') + '.tmp')
        $manifestBackup = Join-Path $data ('install.v' + $oldVersion + '.' + [guid]::NewGuid().ToString('N') + '.bak')
        $modWritten = $true
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'DoorDetector.dll') -Destination $mod -Force
        if ((File-Hash $mod) -ne $dllHash) { throw 'Updated DLL checksum failed.' }
        $previous.version = '0.1.3'
        $previous.modHash = $dllHash
        $previous | Add-Member -NotePropertyName updatedUtc -NotePropertyValue ([DateTime]::UtcNow.ToString('o')) -Force
        $previous | ConvertTo-Json | Set-Content -LiteralPath $manifestTemp -Encoding UTF8
        [IO.File]::Replace($manifestTemp, $manifest, $manifestBackup)
        $committed = $true
        Write-Host 'Updated to 0.1.3. Start the game normally through Steam.' -ForegroundColor Green
        Write-Host 'Look for DOOR DETECTOR at the top right. F8 hides/shows it.'
        Write-Host 'The existing loader, runtime and reservation history were kept.'
        exit 0
    }
    if (Test-Path -LiteralPath $manifest) { throw 'An installation record already exists. Run Uninstall first to restore its previous state.' }
    if (Test-Path -LiteralPath (Join-Path $game 'BepInEx')) { throw 'BepInEx was found. This package needs MelonLoader; remove the other loader before installing.' }
    $loader = Join-Path $game 'MelonLoader'
    $bootstrap = Join-Path $game 'version.dll'
    $package = Join-Path $PSScriptRoot 'Payload\MelonLoader.0.7.3.x64.zip'
    if ((File-Hash $package) -ne '5B2B2F3D1CD42B59EC886C5BDC2663EDAE87A0097A4F4A8F58C0965A99DDA416') { throw 'Loader package checksum failed. Extract a fresh copy of this ZIP.' }
    $stage = Join-Path $env:TEMP ('NINAH-DoorDetector-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stage | Out-Null
    Expand-Archive -LiteralPath $package -DestinationPath $stage
    $expectedBootstrap = File-Hash (Join-Path $stage 'version.dll')
    $newLoader = -not (Test-Path -LiteralPath $loader)
    if (-not $newLoader) {
        $existing = Join-Path $loader 'net6\MelonLoader.dll'
        if (-not (Test-Path -LiteralPath $existing) -or
            (File-Hash $existing) -ne (File-Hash (Join-Path $stage 'MelonLoader\net6\MelonLoader.dll'))) {
            throw 'An existing loader differs from MelonLoader 0.7.3. It was left untouched. Install official MelonLoader 0.7.3, then run this installer again.'
        }
    }
    if ((Test-Path -LiteralPath $bootstrap) -and (File-Hash $bootstrap) -ne $expectedBootstrap) {
        throw 'An existing version.dll belongs to a different loader/version. It was left untouched.'
    }
    # Use a portable runtime in this game only. No machine-wide runtime installation.
    $runtime = Join-Path $loader 'Dependencies\dotnet'
    $runtimeNeeded = -not (Test-Path -LiteralPath (Join-Path $runtime 'shared\Microsoft.NETCore.App\6.0.36\System.Private.CoreLib.dll'))
    if ($runtimeNeeded -and (Test-Path -LiteralPath $runtime)) { throw 'A different portable .NET runtime already exists. It was left untouched.' }
    if ($runtimeNeeded) {
        Write-Host 'Downloading the official Microsoft .NET runtime (about 30 MB)...'
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $download = Join-Path $stage 'dotnet-runtime.zip'
        $ProgressPreference = 'SilentlyContinue'
        Invoke-WebRequest -UseBasicParsing -Uri 'https://builds.dotnet.microsoft.com/dotnet/Runtime/6.0.36/dotnet-runtime-6.0.36-win-x64.zip' -OutFile $download
        if ((File-Hash $download 'SHA512') -ne '935DB5C6CEE19F2C016E67168BFAE7B491044735DE76C673ABB3B125DD325FD5E779D7EFE12BA80178D46689AE70A25E558A3FA846417D44C5F4CA256E7F4BF2') { throw 'Microsoft runtime checksum failed. Nothing installed.' }
        Expand-Archive -LiteralPath $download -DestinationPath (Join-Path $stage 'runtime')
    }
    New-Item -ItemType Directory -Path $data -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $game 'Mods') -Force | Out-Null
    if (Test-Path -LiteralPath $mod) {
        $backup = Join-Path $data ('DoorDetector.previous.' + [guid]::NewGuid().ToString('N') + '.dll')
        Copy-Item -LiteralPath $mod -Destination $backup
    }
    if ($newLoader) {
        $createdLoader = $true
        Copy-Item -LiteralPath (Join-Path $stage 'MelonLoader') -Destination $game -Recurse
    }
    if ($runtimeNeeded) {
        New-Item -ItemType Directory -Path (Split-Path -Parent $runtime) -Force | Out-Null
        $createdRuntime = $true
        Copy-Item -LiteralPath (Join-Path $stage 'runtime') -Destination $runtime -Recurse
    }
    if (-not (Test-Path -LiteralPath $bootstrap)) {
        $createdBootstrap = $true
        Copy-Item -LiteralPath (Join-Path $stage 'version.dll') -Destination $bootstrap
    }
    $modWritten = $true
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'DoorDetector.dll') -Destination $mod -Force
    if ((File-Hash $mod) -ne $dllHash) { throw 'Installed DLL checksum failed.' }
    [ordered]@{
        version = '0.1.3'; installedUtc = [DateTime]::UtcNow.ToString('o')
        modHash = $dllHash; backup = $backup; createdLoader = $createdLoader
        createdBootstrap = $createdBootstrap; bootstrapHash = $expectedBootstrap
    } | ConvertTo-Json | Set-Content -LiteralPath $manifest -Encoding UTF8
    $committed = $true
    Write-Host ''
    Write-Host 'Installed. Start the game normally through Steam.' -ForegroundColor Green
    Write-Host 'First launch may take several minutes while MelonLoader prepares game bindings.'
    Write-Host 'Look for DOOR DETECTOR at the top right. F8 hides/shows it.'
    Write-Host 'Randomized identities are assigned at the door and kept on admission.'
} catch {
    Write-Host ('INSTALLATION FAILED: ' + $_.Exception.Message) -ForegroundColor Red
    if (-not $committed -and $game) {
        if ($modWritten) {
            if ($backup -and (Test-Path -LiteralPath $backup)) { Copy-Item -LiteralPath $backup -Destination $mod -Force }
            elseif (Test-Path -LiteralPath $mod) { Remove-Item -LiteralPath $mod -Force }
        }
        if ($createdBootstrap -and (Test-Path -LiteralPath $bootstrap)) { Remove-Item -LiteralPath $bootstrap -Force }
        if ($createdRuntime -and -not $createdLoader -and (Test-Path -LiteralPath $runtime)) { Remove-Item -LiteralPath $runtime -Recurse -Force }
        if ($createdLoader -and (Test-Path -LiteralPath $loader)) { Remove-Item -LiteralPath $loader -Recurse -Force }
    }
    exit 1
} finally {
    if ($stage -and (Test-Path -LiteralPath $stage)) { Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue }
}
