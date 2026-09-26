param(
    [string] $GamePath = 'D:\SteamLibrary\steamapps\common\Stardew Valley',
    [switch] $Install
)
. "$PSScriptRoot\Common.ps1"
. "$PSScriptRoot\New-ModPackage.ps1"
if (!(Test-Path -LiteralPath (Join-Path $GamePath 'StardewModdingAPI.dll'))) { throw 'Install SMAPI in GamePath first.' }
$manifest = Get-Content -LiteralPath "$script:ProjectRoot\Welcome\manifest.json" -Raw -Encoding UTF8 | ConvertFrom-Json
$version = $manifest.Version
$null = Get-ModVersion $version
foreach ($asset in @('festival-layout.json', 'xiaowai.png', 'xiaowai-detail.png', 'xiaowai-portrait.png', 'xiaowai-home-poses.png', 'xiaowai-home-detail.png', 'xiaowai-animation.json', 'collision.png', 'antennae.png', 'nest.png', 'reward-items.png', 'xiaowai-head.png', 'xiaowai-shirt.png', 'xiaowai-shoes.png', 'xiaowai-shoe-colors.png', 'xiaowai-stickers.png', 'reading-glasses.png')) {
    if (!(Test-Path -LiteralPath "$script:ProjectRoot\Welcome\assets\$asset")) { throw "Missing festival asset: $asset" }
}
$festivalLayout = Get-Content -LiteralPath "$script:ProjectRoot\Welcome\assets\festival-layout.json" -Raw -Encoding UTF8 | ConvertFrom-Json
if ($festivalLayout.SurveyTargets.Count -lt 3 -or
    @($festivalLayout.SurveyTargets.Id | Select-Object -Unique).Count -ne $festivalLayout.SurveyTargets.Count) {
    throw 'Festival survey needs at least three uniquely identified targets. Regenerate the layout.'
}
foreach ($target in $festivalLayout.SurveyTargets) {
    if ($target.Area.Count -ne 4 -or !$target.ClueA -or !$target.ClueB) { throw "Incomplete survey target: $($target.Id)" }
    if ($target.Npc -and $target.Npc -notin $festivalLayout.Actors.Name) { throw "Unknown survey NPC: $($target.Npc)" }
}
$dotnet = Find-DevTool 'dotnet'
& $dotnet build "$script:ProjectRoot\Welcome\Welcome.csproj" --configuration Release "-p:GamePath=$GamePath" "-p:Version=$version" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$stage = Join-Path $script:ProjectRoot ('artifacts\stage-' + [guid]::NewGuid().ToString('N'))
$packageFolder = Join-Path $stage 'Welcome'
New-Item -ItemType Directory -Force -Path $packageFolder | Out-Null
Copy-Item -LiteralPath "$script:ProjectRoot\Welcome\bin\Release\net6.0\Welcome.dll" -Destination $packageFolder
Copy-Item -LiteralPath "$script:ProjectRoot\Welcome\manifest.json" -Destination $packageFolder
foreach ($contentFolder in @('assets', 'i18n')) {
    $source = Join-Path "$script:ProjectRoot\Welcome" $contentFolder
    if (Test-Path -LiteralPath $source) {
        Copy-Item -LiteralPath $source -Destination $packageFolder -Recurse
    }
}
$workerFolder = Join-Path $packageFolder 'update'
New-Item -ItemType Directory -Path $workerFolder | Out-Null
Copy-Item -LiteralPath "$PSScriptRoot\Common.ps1","$PSScriptRoot\Apply-AfterExit.ps1" -Destination $workerFolder
$zip = Join-Path $script:ProjectRoot "artifacts\Welcome-$version.zip"
New-ModPackage -SourceDirectory $packageFolder -DestinationPath $zip -ExpectedVersion $version
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$zip.sha256", "$hash  $([IO.Path]::GetFileName($zip))`n")
if ($Install) { Install-ModPackage -PackagePath $zip -GamePath $GamePath -ExpectedVersion $version }
Write-Host "Built $zip"
