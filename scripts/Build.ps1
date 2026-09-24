param(
    [string] $GamePath = 'D:\SteamLibrary\steamapps\common\Stardew Valley',
    [switch] $Install
)
. "$PSScriptRoot\Common.ps1"
if (!(Test-Path -LiteralPath (Join-Path $GamePath 'StardewModdingAPI.dll'))) { throw 'Install SMAPI in GamePath first.' }
$manifest = Get-Content -LiteralPath "$script:ProjectRoot\Welcome\manifest.json" -Raw -Encoding UTF8 | ConvertFrom-Json
$version = $manifest.Version
$null = Get-ModVersion $version
$dotnet = Find-DevTool 'dotnet'
& $dotnet build "$script:ProjectRoot\Welcome\Welcome.csproj" --configuration Release "-p:GamePath=$GamePath" "-p:Version=$version" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$stage = Join-Path $script:ProjectRoot ('artifacts\stage-' + [guid]::NewGuid().ToString('N'))
$packageFolder = Join-Path $stage 'Welcome'
New-Item -ItemType Directory -Force -Path $packageFolder | Out-Null
Copy-Item -LiteralPath "$script:ProjectRoot\Welcome\bin\Release\net6.0\Welcome.dll" -Destination $packageFolder
Copy-Item -LiteralPath "$script:ProjectRoot\Welcome\manifest.json" -Destination $packageFolder
$workerFolder = Join-Path $packageFolder 'update'
New-Item -ItemType Directory -Path $workerFolder | Out-Null
Copy-Item -LiteralPath "$PSScriptRoot\Common.ps1","$PSScriptRoot\Apply-AfterExit.ps1" -Destination $workerFolder
$zip = Join-Path $script:ProjectRoot "artifacts\Welcome-$version.zip"
Compress-Archive -LiteralPath $packageFolder -DestinationPath $zip -Force
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$zip.sha256", "$hash  $([IO.Path]::GetFileName($zip))`n")
if ($Install) { Install-ModPackage -PackagePath $zip -GamePath $GamePath -ExpectedVersion $version }
Write-Host "Built $zip"
