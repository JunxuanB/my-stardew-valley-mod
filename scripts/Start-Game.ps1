param(
    [string] $GamePath = 'D:\SteamLibrary\steamapps\common\Stardew Valley',
    [switch] $SkipUpdate,
    [switch] $UpdateOnly
)
. "$PSScriptRoot\Common.ps1"
Assert-GameStopped
$exe = Join-Path $GamePath 'StardewModdingAPI.exe'
if (!(Test-Path -LiteralPath $exe)) { throw "SMAPI not found at $exe" }
$installedManifest = Join-Path $GamePath 'Mods\Welcome\manifest.json'
if (!$SkipUpdate) {
    try {
        $headers = @{ 'User-Agent' = 'JunxuanB-Welcome-Updater'; Accept = 'application/vnd.github+json' }
        $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$script:Repository/releases/latest" -Headers $headers -TimeoutSec 20
        $version = $release.tag_name -replace '^v', ''
        $latest = Get-ModVersion $version
        $current = [version] '0.0.0'
        if (Test-Path -LiteralPath $installedManifest) {
            $local = Get-Content -LiteralPath $installedManifest -Raw | ConvertFrom-Json
            if ($local.UniqueID -ne $script:ModId) { throw 'Installed mod identity mismatch.' }
            $current = Get-ModVersion $local.Version
        }
        if ($latest -gt $current) {
            $assetName = "Welcome-$version.zip"
            $asset = @($release.assets | Where-Object name -EQ $assetName)
            $checksum = @($release.assets | Where-Object name -EQ "$assetName.sha256")
            if ($asset.Count -ne 1 -or $checksum.Count -ne 1) { throw 'Release must contain one ZIP and SHA256 file.' }
            $cache = Join-Path $env:TEMP ('welcome-download-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $cache | Out-Null
            $zip = Join-Path $cache $assetName
            Invoke-WebRequest -Uri $asset[0].browser_download_url -OutFile $zip -TimeoutSec 120 -UseBasicParsing
            Invoke-WebRequest -Uri $checksum[0].browser_download_url -OutFile "$zip.sha256" -TimeoutSec 20 -UseBasicParsing
            $expected = ((Get-Content -LiteralPath "$zip.sha256" -Raw).Trim() -split '\s+')[0]
            if ($expected -notmatch '^[a-fA-F0-9]{64}$' -or (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash -ne $expected) {
                throw 'SHA256 verification failed; keeping the installed version.'
            }
            Install-ModPackage -PackagePath $zip -GamePath $GamePath -ExpectedVersion $version
            Write-Host "Updated Welcome: $current -> $latest"
        } else { Write-Host "Welcome $current is up to date." }
    } catch {
        if ($UpdateOnly) { throw }
        Write-Warning "Update unavailable: $($_.Exception.Message) Continuing with the installed version."
    }
}
if (!(Test-Path -LiteralPath $installedManifest)) { throw 'Welcome is not installed. Run scripts\Build.ps1 -Install, or retry the update.' }
if (!$UpdateOnly) { Start-Process -FilePath $exe -WorkingDirectory $GamePath -WindowStyle Hidden }
