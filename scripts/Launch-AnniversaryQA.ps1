param(
    [string] $GamePath = 'D:\SteamLibrary\steamapps\common\Stardew Valley',
    [switch] $PrepareOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\Common.ps1"

$smapi = Join-Path $GamePath 'StardewModdingAPI.exe'
if (!(Test-Path -LiteralPath $smapi)) {
    throw "SMAPI not found: $smapi"
}

$smapiProcesses = @(Get-Process -Name 'StardewModdingAPI' -ErrorAction SilentlyContinue)
$gameProcesses = @(Get-Process -Name 'Stardew Valley' -ErrorAction SilentlyContinue)
$instanceCount = if ($smapiProcesses.Count -gt 0) { $smapiProcesses.Count } else { $gameProcesses.Count }
if ($instanceCount -ge 2) {
    throw 'Two Stardew Valley test instances are already running.'
}

$role = if ($instanceCount -eq 0) { 'host' } else { 'client' }
$localRoot = [IO.Path]::GetFullPath((Join-Path $script:ProjectRoot '.local'))
$hostMod = [IO.Path]::GetFullPath((Join-Path $localRoot 'qa-host\Mods\Welcome'))
$clientMod = [IO.Path]::GetFullPath((Join-Path $localRoot 'qa-client\Mods\Welcome'))
$localPrefix = $localRoot.TrimEnd('\') + '\'
foreach ($path in @($hostMod, $clientMod)) {
    if (!$path.StartsWith($localPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "QA mod path escaped the local workspace: $path"
    }
}

if ($role -eq 'host') {
    $version = (Get-Content -LiteralPath (Join-Path $script:ProjectRoot 'Welcome\manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json).Version
    $package = Join-Path $script:ProjectRoot "artifacts\Welcome-$version.zip"
    if (!(Test-Path -LiteralPath $package)) {
        throw "Built QA package not found: $package"
    }
    Install-ModPackage -PackagePath $package -GamePath $GamePath -ExpectedVersion $version -TargetDirectory $hostMod
    Install-ModPackage -PackagePath $package -GamePath $GamePath -ExpectedVersion $version -TargetDirectory $clientMod
}
elseif (!(Test-Path -LiteralPath (Join-Path $clientMod 'Welcome.dll'))) {
    throw 'The client QA mod is missing. Close the game and run this launcher once to prepare both instances.'
}

$selectedMod = if ($role -eq 'host') { $hostMod } else { $clientMod }
$modsPath = [IO.Path]::GetDirectoryName($selectedMod)
if ($PrepareOnly) {
    Write-Host 'Anniversary QA host and client mod folders are ready.'
    return
}

$label = if ($role -eq 'host') { 'host' } else { 'client' }
Write-Host "Starting Anniversary QA $label..."
if ($role -eq 'host') {
    Write-Host 'Open Co-op -> Host and load an AnniversaryQA test farm. Run this CMD again after entering the farm.'
}
else {
    Write-Host 'Open Co-op -> Join LAN Game, enter 127.0.0.1, then choose the test farmhand.'
}

# This is the interactive game window requested by the user, not a background helper.
Start-Process -FilePath $smapi -ArgumentList '--mods-path', ('"{0}"' -f $modsPath) -WorkingDirectory $GamePath -WindowStyle Normal
