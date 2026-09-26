function Assert-ModPackageCompatibility([string] $PackagePath, [string] $ExpectedVersion) {
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($PackagePath))
    try {
        foreach ($entry in $archive.Entries) {
            if ($entry.FullName.Contains('\') -or !$entry.FullName.StartsWith('Welcome/', [StringComparison]::Ordinal)) {
                throw "Noncanonical ZIP entry: $($entry.FullName)"
            }
        }
        # Use the same exact lookup as the already installed 1.0.0 updater.
        $manifest = $archive.GetEntry('Welcome/manifest.json')
        if ($null -eq $manifest) { throw 'Missing update manifest at Welcome/manifest.json.' }
        foreach ($name in @('Welcome/Welcome.dll', 'Welcome/update/Common.ps1', 'Welcome/update/Apply-AfterExit.ps1')) {
            if ($null -eq $archive.GetEntry($name)) { throw "Missing update file: $name" }
        }
        $reader = [IO.StreamReader]::new($manifest.Open(), [Text.Encoding]::UTF8)
        try { $identity = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
        if ($identity.UniqueID -ne 'JunxuanB.Welcome' -or $identity.EntryDll -ne 'Welcome.dll' -or
            ($ExpectedVersion -and $identity.Version -ne $ExpectedVersion)) {
            throw 'Update identity/version mismatch.'
        }
    } finally { $archive.Dispose() }
}

function New-ModPackage([string] $SourceDirectory, [string] $DestinationPath, [string] $ExpectedVersion) {
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    $source = [IO.Path]::GetFullPath($SourceDirectory).TrimEnd('\', '/')
    $destination = [IO.Path]::GetFullPath($DestinationPath)
    if (!(Test-Path -LiteralPath $source -PathType Container)) { throw "Missing package source: $source" }
    $prefix = $source + [IO.Path]::DirectorySeparatorChar
    if ($destination.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The destination ZIP must be outside the source folder.'
    }
    $parent = [IO.Path]::GetDirectoryName($destination)
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
    $temporary = Join-Path $parent ('.welcome-package-' + [guid]::NewGuid().ToString('N') + '.tmp')
    try {
        $archive = [IO.Compression.ZipFile]::Open($temporary, [IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($file in Get-ChildItem -LiteralPath $source -Recurse -File | Sort-Object FullName) {
                $relative = $file.FullName.Substring($prefix.Length).Replace('\', '/')
                $null = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName,
                    ('Welcome/' + $relative), [IO.Compression.CompressionLevel]::Optimal)
            }
        } finally { $archive.Dispose() }
        Assert-ModPackageCompatibility -PackagePath $temporary -ExpectedVersion $ExpectedVersion
        Move-Item -LiteralPath $temporary -Destination $destination -Force
    } finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
    }
}
