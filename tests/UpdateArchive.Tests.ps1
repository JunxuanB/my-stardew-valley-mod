# Archive-level regression checks; no game, installer, network or live release is touched.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\..\scripts\New-ModPackage.ps1"
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archiveTestRoot = Join-Path ([IO.Path]::GetTempPath()) ('welcome-archive-tests-' + [guid]::NewGuid().ToString('N'))
function Assert($Condition, [string] $Message) { if (!$Condition) { throw $Message } }
try {
    $source = Join-Path $archiveTestRoot 'source'
    New-Item -ItemType Directory -Force -Path "$source\assets\nested", "$source\update" | Out-Null
    $identity = @{UniqueID='JunxuanB.Welcome'; EntryDll='Welcome.dll'; Version='1.0.1'} | ConvertTo-Json
    [IO.File]::WriteAllText("$source\manifest.json", $identity, [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllBytes("$source\Welcome.dll", [byte[]]@(1,2,3,4))
    [IO.File]::WriteAllBytes("$source\assets\nested\sample.bin", [byte[]]@(5,6,7,8))
    [IO.File]::WriteAllText("$source\update\Common.ps1", '# fixture')
    [IO.File]::WriteAllText("$source\update\Apply-AfterExit.ps1", '# fixture')

    # Reproduce the original symptom with raw Windows separators, without a reader
    # such as Python ZipInfo silently converting them on Windows.
    $legacyPath = Join-Path $archiveTestRoot 'legacy.zip'
    $legacy = [IO.Compression.ZipFile]::Open($legacyPath, [IO.Compression.ZipArchiveMode]::Create)
    try { $null = $legacy.CreateEntry('Welcome\manifest.json') } finally { $legacy.Dispose() }
    $legacy = [IO.Compression.ZipFile]::OpenRead($legacyPath)
    try { Assert ($null -eq $legacy.GetEntry('Welcome/manifest.json')) 'Legacy failure was not reproduced.' }
    finally { $legacy.Dispose() }

    $zip = Join-Path $archiveTestRoot 'Welcome-1.0.1.zip'
    New-ModPackage -SourceDirectory $source -DestinationPath $zip -ExpectedVersion '1.0.1'
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        Assert (@($archive.Entries | Where-Object { $_.FullName.Contains('\') }).Count -eq 0) 'Backslashes remain in ZIP names.'
        $entry = $archive.GetEntry('Welcome/manifest.json')
        Assert ($null -ne $entry) 'The 1.0.0 updater cannot locate the future manifest.'
        $reader = [IO.StreamReader]::new($entry.Open())
        try { $next = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
        Assert ($next.UniqueID -eq 'JunxuanB.Welcome' -and $next.Version -eq '1.0.1') 'Manifest contents changed.'
        Assert ([version]$next.Version -gt [version]'1.0.0') 'Fixture must exercise a newer version.'
        Assert ($null -ne $archive.GetEntry('Welcome/assets/nested/sample.bin')) 'Nested resources are inaccessible.'
        Assert ($null -ne $archive.GetEntry('Welcome/update/Apply-AfterExit.ps1')) 'Worker is inaccessible.'
    } finally { $archive.Dispose() }
    $expanded = Join-Path $archiveTestRoot 'expanded'
    Expand-Archive -LiteralPath $zip -DestinationPath $expanded
    foreach ($file in Get-ChildItem -LiteralPath $source -Recurse -File) {
        $relative = $file.FullName.Substring($source.Length + 1)
        $unpacked = Join-Path "$expanded\Welcome" $relative
        Assert ((Get-FileHash -LiteralPath $file.FullName).Hash -eq (Get-FileHash -LiteralPath $unpacked).Hash) "Payload changed: $relative"
    }
    Write-Host 'PASS: legacy separator bug reproduced; 1.0.0 exact .NET lookup reads the future package; extraction preserves all files.'
} finally {
    $resolved = [IO.Path]::GetFullPath($archiveTestRoot)
    if ([IO.Path]::GetDirectoryName($resolved).TrimEnd('\') -eq [IO.Path]::GetTempPath().TrimEnd('\') -and
        [IO.Path]::GetFileName($resolved) -match '^welcome-archive-tests-[a-f0-9]{32}$') {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
