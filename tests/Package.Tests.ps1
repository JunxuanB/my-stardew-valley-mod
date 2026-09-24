# Dependency-free integration tests for package validation and configuration preservation.
. "$PSScriptRoot\..\scripts\Common.ps1"
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('welcome-tests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
function Assert($Condition, [string] $Message) { if (!$Condition) { throw $Message } }
function New-Fixture([string] $Version, [string] $Id = 'JunxuanB.Welcome') {
    $folder = Join-Path $testRoot ([guid]::NewGuid().ToString('N'))
    $mod = Join-Path $folder 'Welcome'
    New-Item -ItemType Directory -Path $mod -Force | Out-Null
    # Write BOM-less UTF-8, matching a manifest created by a normal code editor.
    $description = -join ([char[]] @(0x6B22, 0x8FCE, 0xFF01))
    $json = @{ UniqueID = $Id; Version = $Version; EntryDll = 'Welcome.dll'; Description = $description } | ConvertTo-Json
    [IO.File]::WriteAllText((Join-Path $mod 'manifest.json'), $json, [Text.UTF8Encoding]::new($false))
    Set-Content (Join-Path $mod 'Welcome.dll') "fixture-$Version"
    $zip = "$folder.zip"
    Compress-Archive -LiteralPath $mod -DestinationPath $zip
    return $zip
}
function Assert-Rejected([scriptblock] $Action) {
    $rejected = $false
    try { & $Action } catch { $rejected = $true }
    Assert $rejected 'Unsafe package was accepted.'
    $installed = Get-Content (Join-Path $testRoot 'game\Mods\Welcome\manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert ($installed.Version -eq '0.2.0') 'A rejected update changed the installed version.'
}
try {
    $game = Join-Path $testRoot 'game'
    $first = New-Fixture '0.1.0'
    Install-ModPackage $first $game '0.1.0'
    $config = Join-Path $game 'Mods\Welcome\config.json'
    Set-Content $config '{"custom":true}'
    $second = New-Fixture '0.2.0'
    Install-ModPackage $second $game '0.2.0'
    Assert ((Get-Content $config -Raw).Trim() -eq '{"custom":true}') 'Update lost user configuration.'
    Assert-Rejected { Install-ModPackage $second $game '0.3.0' }
    $wrongId = New-Fixture '0.3.0' 'SomeoneElse.Mod'
    Assert-Rejected { Install-ModPackage $wrongId $game '0.3.0' }
    $badZip = Join-Path $testRoot 'traversal.zip'
    $archive = [IO.Compression.ZipFile]::Open($badZip, [IO.Compression.ZipArchiveMode]::Create)
    $null = $archive.CreateEntry('Welcome/../../escape.txt')
    $archive.Dispose()
    Assert-Rejected { Install-ModPackage $badZip $game '0.3.0' }
    Assert (!(Test-Path (Join-Path $game 'Mods\escape.txt'))) 'Archive escaped the install directory.'
    Assert (@(Get-ChildItem (Join-Path $game 'Mods') -Force -Filter '.welcome-*').Count -eq 0) 'Staging folders leaked.'
    Write-Host 'PASS: fresh install, upgrade, config preservation, wrong version, wrong identity, path traversal, cleanup.'
} finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    if ([IO.Path]::GetDirectoryName($resolved).TrimEnd('\') -eq [IO.Path]::GetTempPath().TrimEnd('\') -and
        [IO.Path]::GetFileName($resolved) -match '^welcome-tests-[a-f0-9]{32}$') {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
