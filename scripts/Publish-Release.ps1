param([string] $GamePath = 'D:\SteamLibrary\steamapps\common\Stardew Valley')
. "$PSScriptRoot\Common.ps1"
$gh = Find-DevTool 'gh'
Push-Location $script:ProjectRoot
try {
    & $gh auth status
    if ($LASTEXITCODE -ne 0) { throw 'Run gh auth login first.' }
    $changes = git status --porcelain
    if ($LASTEXITCODE -ne 0 -or $changes) { throw 'Commit all changes before publishing.' }
    $manifest = Get-Content -LiteralPath 'Welcome\manifest.json' -Raw -Encoding UTF8 | ConvertFrom-Json
    $version = $manifest.Version
    $null = Get-ModVersion $version
    $tag = "v$version"
    $commit = git rev-parse HEAD
    $existingTag = git tag --list $tag
    if ($existingTag) {
        $tagCommit = git rev-list -n 1 $tag
        if ($tagCommit -ne $commit) { throw "$tag already points to a different commit. Bump the version." }
    }
    & "$PSScriptRoot\Build.ps1" -GamePath $GamePath
    if (!$existingTag) {
        git tag $tag
        if ($LASTEXITCODE -ne 0) { throw 'Could not create version tag.' }
    }
    git push origin HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Could not push source.' }
    git push origin $tag
    if ($LASTEXITCODE -ne 0) { throw 'Could not push version tag.' }
    $zip = Join-Path $script:ProjectRoot "artifacts\Welcome-$version.zip"
    $notes = Join-Path $script:ProjectRoot 'artifacts\release-notes.md'
    @"
Welcome $version

每次进入存档或加入联机时显示「欢迎！」。每位联机玩家均需安装 SMAPI 和本模组。

首次安装：下载 Welcome-$version.zip，解压其中的 Welcome 文件夹到游戏 Mods 目录。
自动更新：下载本仓库源码 ZIP，解压后双击 Start-Game.cmd（非默认游戏路径请参考 README）。
之后启动器会在启动游戏前检查最新 Release，校验 SHA256 并安装更新。
"@ | Set-Content -LiteralPath $notes -Encoding UTF8
    & $gh release create $tag $zip "$zip.sha256" --repo $script:Repository --verify-tag --title "Welcome $version" --notes-file $notes
    if ($LASTEXITCODE -ne 0) { throw 'Release creation failed. Existing releases are never overwritten; inspect GitHub before retrying.' }
} finally { Pop-Location }
