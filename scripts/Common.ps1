Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:ProjectRoot = Split-Path $PSScriptRoot -Parent
$script:ModFolder = 'Welcome'
$script:ModId = 'JunxuanB.Welcome'
$script:Repository = 'JunxuanB/my-stardew-valley-mod'

function Find-DevTool([string] $Name) {
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    $relative = if ($Name -eq 'dotnet') { 'dotnet\dotnet.exe' } else { 'gh\bin\gh.exe' }
    $candidate = Join-Path $env:LOCALAPPDATA "StardewModDev\$relative"
    if (Test-Path -LiteralPath $candidate) { return $candidate }
    throw "Missing $Name. Install it and reopen your terminal."
}

function Assert-GameStopped {
    if (Get-Process -Name 'Stardew Valley','StardewModdingAPI' -ErrorAction SilentlyContinue) {
        throw 'Please close Stardew Valley before installing or updating the mod.'
    }
}

function Get-ModVersion([string] $Value) {
    if ($Value -notmatch '^\d+\.\d+\.\d+$') { throw "Expected a stable x.y.z version, got: $Value" }
    return [version] $Value
}

function Install-ModPackage([string] $PackagePath, [string] $GamePath, [string] $ExpectedVersion) {
    Assert-GameStopped
    $modsPath = Join-Path ([IO.Path]::GetFullPath($GamePath)) 'Mods'
    New-Item -ItemType Directory -Path $modsPath -Force | Out-Null
    $destination = Join-Path $modsPath $script:ModFolder
    $staging = Join-Path $modsPath ('.welcome-update-' + [guid]::NewGuid().ToString('N'))
    $backup = Join-Path $modsPath ('.welcome-backup-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $staging | Out-Null
    try {
        # Inspect before extracting; reject paths that could escape the staging folder.
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [IO.Compression.ZipFile]::OpenRead($PackagePath)
        try {
            foreach ($entry in $archive.Entries) {
                $name = $entry.FullName.Replace('\', '/')
                if ($name -notmatch '^Welcome/' -or $name -match '(^|/)\.\.(/|$)|:' -or $name.StartsWith('/')) {
                    throw "Invalid package entry: $name"
                }
            }
        } finally { $archive.Dispose() }
        Expand-Archive -LiteralPath $PackagePath -DestinationPath $staging
        $incoming = Join-Path $staging $script:ModFolder
        $manifest = Get-Content -LiteralPath (Join-Path $incoming 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($manifest.UniqueID -ne $script:ModId -or $manifest.EntryDll -ne 'Welcome.dll' -or
            $manifest.Version -ne $ExpectedVersion -or !(Test-Path -LiteralPath (Join-Path $incoming 'Welcome.dll'))) {
            throw 'Package identity, version or DLL validation failed.'
        }
        if (Test-Path -LiteralPath $destination) {
            $existing = Get-Content -LiteralPath (Join-Path $destination 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($existing.UniqueID -ne $script:ModId) { throw 'The target folder belongs to another mod.' }
            $config = Join-Path $destination 'config.json'
            if (Test-Path -LiteralPath $config) { Copy-Item -LiteralPath $config -Destination $incoming -Force }
            # All move targets are resolved children of this game's Mods folder.
            Move-Item -LiteralPath $destination -Destination $backup
        }
        try { Move-Item -LiteralPath $incoming -Destination $destination }
        catch {
            if (Test-Path -LiteralPath $backup) { Move-Item -LiteralPath $backup -Destination $destination }
            throw
        }
    } finally {
        # Only delete this operation's generated staging/backup paths under Mods.
        foreach ($generated in @($staging, $backup)) {
            $resolved = [IO.Path]::GetFullPath($generated)
            if ([IO.Path]::GetDirectoryName($resolved) -ne $modsPath -or [IO.Path]::GetFileName($resolved) -notmatch '^\.welcome-(update|backup)-[a-f0-9]{32}$') {
                throw "Unexpected cleanup path: $resolved"
            }
            # A failed rollback must leave the backup available for manual recovery.
            if ($generated -eq $backup -and !(Test-Path -LiteralPath $destination)) { continue }
            if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
        }
    }
}
