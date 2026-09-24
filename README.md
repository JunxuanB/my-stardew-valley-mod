# Welcome — 星露谷物语模组框架

最小 C# / SMAPI 项目：每位玩家每次进入存档（含新建存档）或加入联机后，在可以操作角色时显示 **欢迎！**。
睡觉进入下一天不会重复提示；退出到标题再进入、联机断开重连会重新提示。分屏玩家分别处理。

## 环境

- 星露谷物语 1.6 和 SMAPI 4.x。本机验证环境：游戏 1.6.15 / SMAPI 4.5.2。
- 开发：.NET 8 SDK（构建目标仍为游戏使用的 .NET 6），Git；发布另需 GitHub CLI。
- Windows 自动更新启动器使用自带的 Windows PowerShell 5.1；模组 DLL 不依赖这个启动器。
- 联机时**每位玩家都安装模组**，房主安装不会自动给未安装的客户端弹提示。

## 本地构建与安装

关闭游戏，在项目根目录运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build.ps1 -Install
```

默认游戏目录为 `D:\SteamLibrary\steamapps\common\Stardew Valley`。其他路径通过 `-GamePath "你的游戏目录"` 指定。
构建脚本仅将 `Welcome.dll` 和 `manifest.json` 打包到 `artifacts/Welcome-版本.zip`，不会发布游戏 DLL。
`-Install` 安装到 `Mods/Welcome`。不传该参数则只构建、打包。
工具可从 PATH 查找，也兼容本机 `%LOCALAPPDATA%\StardewModDev` 下的 SDK 和 CLI。

## 游玩与自动更新

双击根目录 **Start-Game.cmd**：

1. 查询 `JunxuanB/my-stardew-valley-mod` 的最新正式 GitHub Release。
2. 有更新时下载 ZIP 与 SHA256，校验版本、模组身份和文件路径，保留 `config.json` 后安装。
3. 启动 SMAPI。离线或更新失败时保留旧版并继续启动；首次安装失败则显示错误。

也可以运行：

```powershell
.\scripts\Start-Game.ps1 -GamePath "D:\SteamLibrary\steamapps\common\Stardew Valley"
.\scripts\Start-Game.ps1 -SkipUpdate  # 本地开发时跳过远端更新
.\scripts\Start-Game.ps1 -UpdateOnly  # 只检查并安装，不启动游戏
```

朋友首次使用：下载本仓库源码 ZIP 并解压，修改启动参数指定游戏目录，运行 `Start-Game.cmd`；启动器会安装最新版本。也可下载 Release ZIP，手动解压到 `Mods`。
更新发生在**此启动器启动游戏之前**。直接从 Steam 或 SMAPI EXE 启动，只会获得 SMAPI 的更新提示，不会自动安装。
启动器自身修改后需要重新下载源码；当前自动安装范围为 Welcome 模组。运行中不替换 DLL，不会更新其他模组或 SMAPI。
当前发布流程仅接受 `x.y.z` 正式版本，不会降级到较旧 Release，也不会安装预发布版本。

## GitHub 登录与发布

仓库：<https://github.com/JunxuanB/my-stardew-valley-mod>

```powershell
gh auth login --hostname github.com --git-protocol https --web
gh auth setup-git
```

浏览器授权即可，使用 HTTPS 无需配置 SSH。首次未创建仓库时：

```powershell
git init -b main
git add .
git commit -m "Initialize Welcome mod"
gh repo create JunxuanB/my-stardew-valley-mod --public --source . --remote origin --push
```

后续发布：修改 `Welcome/manifest.json` 的 `Version`，提交代码，然后执行：

```powershell
git add .
git commit -m "Prepare next release"
.\scripts\Publish-Release.ps1
```

这条命令完成本地构建、推送源码、推送 `v版本号` 标签、创建 GitHub Release 并上传 ZIP 和 SHA256。**普通 git push 只更新源码；玩家更新以正式 Release 为准。**
发布失败可检查远端状态后重试；脚本不会覆盖已存在的 Release 或改写已发布标签。
GitHub Actions 每次推送运行脚本语法与安装器测试。游戏版权文件不入库，所以 C# 构建在装有游戏的本机完成。

## 代码与验证

- `Welcome/ModEntry.cs`：使用 `SaveLoaded` 标记待显示欢迎信息，等加载结束后展示 HUD 提示；`PerScreen` 维护分屏状态。
- `scripts/Build.ps1`：编译、打包、可选本地安装。
- `scripts/Start-Game.ps1`：GitHub Release 自动更新与启动。
- `scripts/Publish-Release.ps1`：发布入口。
- `tests/Package.Tests.ps1`：安装器集成测试。

```powershell
.\tests\Package.Tests.ps1
```

人工验收：单人进入存档显示一次；睡觉不重复；返回标题再进入再次显示；房主和客户端各自安装后加入联机各显示一次；分屏各显示一次。

API 参考：[SMAPI 事件](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Events)、[分屏与多人游戏](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Multiplayer)。
