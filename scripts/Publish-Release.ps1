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
    $versionNotes = Join-Path $script:ProjectRoot "docs\releases\$version.md"
    if (Test-Path -LiteralPath $versionNotes) {
        Copy-Item -LiteralPath $versionNotes -Destination $notes -Force
    } else {
    @"
Welcome $version

每次进入存档或加入联机时显示「玩家名你来星露谷有什么目的！」，自动使用当前角色的名字。每位联机玩家均需安装 SMAPI 和本模组。
启动阶段检查更新：新版下载校验并准备好安装程序后，终止本次游戏启动，强制玩家稍后重新启动。

首次安装：关闭游戏，下载 Welcome-$version.zip，将完整 Welcome 文件夹（含 update 子目录）解压到游戏 Mods 目录。
继续使用原来的 Steam/SMAPI 启动方式，无需新增启动器或修改启动参数。
Windows 内置自动更新：在 SMAPI 加载模组阶段检查新版，校验 SHA256，终止游戏后自动安装，下次正常启动生效。游玩中不检查、不强制退出。网络或下载失败时继续使用原版模组。
从 0.2.x 升级到本版本的一次迁移仍按旧版逻辑，需要自行退出游戏；已安装 0.3.0 或更高版本时，会在新版准备完成后阻断本次启动。
0.1.x 用户需先覆盖安装一次本版本，之后即可内置自动更新。
"@ | Set-Content -LiteralPath $notes -Encoding UTF8
    }
    & $gh release create $tag $zip "$zip.sha256" --repo $script:Repository --verify-tag --title "Welcome $version" --notes-file $notes
    if ($LASTEXITCODE -ne 0) { throw 'Release creation failed. Existing releases are never overwritten; inspect GitHub before retrying.' }
} finally { Pop-Location }
