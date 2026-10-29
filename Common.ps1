#requires -Version 5.1
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
function Get-GameFolder([string]$Candidate) {
    if ([string]::IsNullOrWhiteSpace($Candidate)) { return $null }
    if (Test-Path -LiteralPath $Candidate -PathType Leaf) {
        $Candidate = [IO.Path]::GetDirectoryName((Get-Item -LiteralPath $Candidate).FullName)
    }
    if (Test-Path -LiteralPath (Join-Path $Candidate 'NoImNotAHuman.exe') -PathType Leaf) {
        return (Get-Item -LiteralPath $Candidate).FullName
    }
    return $null
}

function Find-GameFolders {
    $steamRoots = @{}
    $libraryRoots = @{}
    $found = @{}
    foreach ($key in @('HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam', 'HKLM:\SOFTWARE\Valve\Steam')) {
        if (-not (Test-Path -LiteralPath $key)) { continue }
        $props = Get-ItemProperty -LiteralPath $key
        foreach ($name in @('SteamPath', 'InstallPath')) {
            $prop = $props.PSObject.Properties[$name]
            if ($null -ne $prop -and -not [string]::IsNullOrWhiteSpace([string]$prop.Value)) {
                $steamRoots[[string]$prop.Value] = $true
            }
        }
    }
    foreach ($base in @(${env:ProgramFiles(x86)}, $env:ProgramFiles)) {
        if (-not [string]::IsNullOrWhiteSpace($base)) {
            $steamRoots[(Join-Path $base 'Steam')] = $true
        }
    }
    foreach ($root in @($steamRoots.Keys)) {
        $libraryRoots[$root] = $true
        foreach ($relative in @('steamapps\libraryfolders.vdf', 'config\libraryfolders.vdf')) {
            $vdfPath = Join-Path $root $relative
            if (-not (Test-Path -LiteralPath $vdfPath -PathType Leaf)) { continue }
            $vdf = [IO.File]::ReadAllText($vdfPath)
            foreach ($match in [regex]::Matches($vdf, '"path"\s+"([^"]+)"')) {
                $libraryRoots[$match.Groups[1].Value.Replace('\\', '\')] = $true
            }
        }
    }
    foreach ($root in @($libraryRoots.Keys)) {
        $manifest = Join-Path $root 'steamapps\appmanifest_3180070.acf'
        if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) { continue }
        $acf = [IO.File]::ReadAllText($manifest)
        $match = [regex]::Match($acf, '"installdir"\s+"([^"]+)"')
        if (-not $match.Success) { continue }
        $folder = Get-GameFolder (Join-Path (Join-Path $root 'steamapps\common') $match.Groups[1].Value)
        if ($folder) { $found[$folder] = $true }
    }
    foreach ($candidate in @($PSScriptRoot, (Split-Path -Path $PSScriptRoot -Parent))) {
        $folder = Get-GameFolder $candidate
        if ($folder) { $found[$folder] = $true }
    }
    return @($found.Keys | Sort-Object)
}


function Resolve-Game([string]$Hint) {
    if ($Hint) {
        $result = Get-GameFolder $Hint
        if (-not $result) { throw 'That folder does not contain NoImNotAHuman.exe.' }
        return $result
    }
    $folders = @(Find-GameFolders)
    if ($folders.Count -eq 1) { return $folders[0] }
    Add-Type -AssemblyName System.Windows.Forms
    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    $dialog.Title = 'Select NoImNotAHuman.exe in the Steam game folder'
    $dialog.Filter = 'Game executable (NoImNotAHuman.exe)|NoImNotAHuman.exe'
    try {
        if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { throw 'No game folder selected.' }
        $result = Get-GameFolder $dialog.FileName
        if (-not $result) { throw 'Please select NoImNotAHuman.exe.' }
        return $result
    } finally { $dialog.Dispose() }
}
function Assert-GameClosed {
    if (Get-Process -Name 'NoImNotAHuman' -ErrorAction SilentlyContinue) { throw 'Close the game, then run this again.' }
}
function File-Hash([string]$Path, [string]$Algorithm = 'SHA256') {
    return (Get-FileHash -LiteralPath $Path -Algorithm $Algorithm).Hash
}
