# Welcome — 星露谷物语模组框架

最小 C# / SMAPI 项目：每位玩家每次进入存档（含新建存档）或加入联机后，在可以操作角色时显示 **欢迎！**。
睡觉进入下一天不会重复；退出到标题再进入、联机断开重连会重新提示。分屏玩家分别处理。

## 安装与游玩

1. 安装星露谷物语 1.6 和 SMAPI 4.x。本机验证环境为游戏 1.6.15 / SMAPI 4.5.2。
2. 从 [GitHub Releases](https://github.com/JunxuanB/my-stardew-valley-mod/releases/latest) 下载 `Welcome-版本.zip`。
3. 关闭游戏，将 ZIP 中的整个 `Welcome` 文件夹解压到游戏 `Mods` 目录，包含 `update` 子目录。
4. **继续使用原来已经配置 SMAPI 的 Steam 入口或 SMAPI EXE 启动。无需新增启动器或修改启动参数。**

联机时每位玩家都安装模组，房主安装不会自动给未安装的客户端弹提示。完全不经过 SMAPI 的纯原版程序无法加载 C# 模组。

## 内置自动更新（Windows，0.2.0 起）

- 启动后在后台检查 GitHub 最新正式 Release；没有新版或网络失败时，每 15 分钟重试。
- 有新版时自动下载并校验 SHA256，游玩不受影响。在存档中显示一条「新版已下载，退出后安装」提示。
- 独立隐藏更新程序等待游戏自然退出，再校验包、保留 `config.json`，替换模组文件。
- 下一次仍按原来的方法启动，加载的就是新版。不需要手动确认、下载或运行脚本，不会主动退出游戏。
- 如果下载时停留在标题页，下载状态记录在 SMAPI 日志中；进入存档后显示提示。
- 只更新本模组，不更新游戏、SMAPI、其他模组或存档。多个游戏实例都退出后才安装，不会降级，也不会安装预发布版本。
- 更新器放在游戏目录之外，因此不会随游戏退出而停止。失败时保留旧版，后续启动重新检查。
- 日志和下载缓存位于 `%LOCALAPPDATA%\JunxuanB.Welcome\updates\任务编号\update.log`。
- macOS / Linux 目前可以加载欢迎模组，但需手动安装更新。

`0.1.x` 没有内置更新能力，需要先手动覆盖安装一次 `0.2.x`；之后均使用上述流程。旧的 `Start-Game.cmd` 可选启动器仍保留以兼容旧版本，但日常游玩不再需要它。

## 本地开发

需要 .NET 8 SDK（构建目标是游戏使用的 .NET 6）、Git；发布另需 GitHub CLI。

关闭游戏，在项目根目录运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build.ps1 -Install
```

默认游戏目录为 `D:\SteamLibrary\steamapps\common\Stardew Valley`。其他路径通过 `-GamePath "你的游戏目录"` 指定。
不传 `-Install` 则只构建和打包。ZIP 位于 `artifacts/`，包含模组 DLL、清单和更新辅助脚本，不包含游戏 DLL。
工具可从 PATH 查找，也兼容本机 `%LOCALAPPDATA%\StardewModDev` 下的 SDK 和 CLI。

## GitHub 发布

仓库：[JunxuanB/my-stardew-valley-mod](https://github.com/JunxuanB/my-stardew-valley-mod)

首次登录：

```powershell
gh auth login --hostname github.com --git-protocol https --web --scopes workflow
gh auth setup-git
```

浏览器授权即可，HTTPS 无需配置 SSH。之后修改 `Welcome/manifest.json` 的 `Version`、提交代码，再执行发布命令：

```powershell
git add .
git commit -m "Prepare next release"
.\scripts\Publish-Release.ps1
```

发布命令完成本地编译、推送源码和 `v版本号` 标签、创建 Release、上传 ZIP 和 SHA256。**普通 git push 只更新源码；玩家自动更新以正式 Release 为准。**
发布失败需检查远端状态再重试；脚本不会覆盖已有 Release 或改写已发布标签。
GitHub Actions 在每次推送运行脚本与安装器测试；C# 构建使用本机游戏引用，游戏版权文件不入库。

## 代码与验证

- `Welcome/ModEntry.cs`：欢迎提示及更新状态通知，`PerScreen` 维护分屏状态。
- `Welcome/AutoUpdater.cs`：后台检查、下载、校验、启动退出后安装程序。
- `scripts/Apply-AfterExit.ps1`：等待游戏进程退出、再次校验、安装并记录结果。
- `scripts/Common.ps1`：包验证、保留配置、替换失败回滚。
- `scripts/Build.ps1` / `scripts/Publish-Release.ps1`：开发与发布入口。

关闭游戏后运行（退出更新器测试会启动隐藏的临时测试进程）：

```powershell
.\tests\Package.Tests.ps1
.\tests\ExitUpdater.Tests.ps1
```

自动测试覆盖首次安装、升级、中文 UTF-8 清单、保留配置、错误版本/身份、路径穿越、等待退出、重命名模组目录、防降级与校验失败。
人工验收：单人进入显示一次；睡觉不重复；重新进入再次显示；房主和客户端各自安装后各显示一次；分屏各显示一次。

API 参考：[SMAPI 事件](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Events)、[分屏与多人游戏](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Multiplayer)。
