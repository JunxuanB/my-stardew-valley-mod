# Welcome — 星露谷物语模组框架

最小 C# / SMAPI 项目：每位玩家每次进入存档（含新建存档）或加入联机后，在可以操作角色时显示 **玩家名你来星露谷有什么目的！**。玩家名使用当前角色的名字，联机和分屏玩家分别显示自己的名字。
睡觉进入下一天不会重复；退出到标题再进入、联机断开重连会重新提示。分屏玩家分别处理。

## 安装与游玩

1. 安装星露谷物语 1.6 和 SMAPI 4.x。本机验证环境为游戏 1.6.15 / SMAPI 4.5.2。
2. 从 [GitHub Releases](https://github.com/JunxuanB/my-stardew-valley-mod/releases/latest) 下载 `Welcome-版本.zip`。
3. 关闭游戏，将 ZIP 中的整个 `Welcome` 文件夹解压到游戏 `Mods` 目录，包含 `update` 子目录。
4. **继续使用原来已经配置 SMAPI 的 Steam 入口或 SMAPI EXE 启动。无需新增启动器或修改启动参数。**

联机时每位玩家都安装模组，房主安装不会自动给未安装的客户端弹提示。完全不经过 SMAPI 的纯原版程序无法加载 C# 模组。

## 启动阶段强制更新（Windows，0.3.0 起）

- 在 SMAPI 加载本模组的 Entry 阶段同步检查 GitHub 最新正式 Release，检查期间暂停启动。网络检查最多等待 15 秒，下载请求最多 90 秒。
- 无新版时正常进入游戏。检查、下载或校验失败时记录警告，继续使用当前模组，避免离线无法游玩。
- 发现新版后下载 ZIP 并校验 SHA256，启动独立隐藏安装程序，等待它确认就绪。
- 准备完成后，在 SMAPI 控制台显示「本次启动已终止，请稍后重启」并停留 3 秒，然后终止整个游戏进程；不会进入存档。
- 独立程序在游戏退出后再次校验、保留配置并安装。等待几秒后，玩家仍从原来的 Steam/SMAPI 入口手动启动，新版即生效。
- 检查只在启动时进行，游玩期间不检查也不强制退出。不是热更新，不会自动重新打开游戏。
- 只有更新程序就绪才终止游戏。多个游戏实例需全部退出后才安装；不降级、不安装预发布版本。
- 只更新本模组，不改动存档、SMAPI、其他模组或 Steam 启动参数。SMAPI 加载阶段可能已创建游戏窗口，本模组保证在自身 Entry 阶段退出、阻止进入标题页和存档，不能控制宿主更早创建的窗口。
- 下载缓存与安装日志：`%LOCALAPPDATA%\JunxuanB.Welcome\updates\任务编号\update.log`。安装失败时保留旧版，下次启动重试。
- macOS / Linux 目前需手动更新。

**迁移说明：** `0.2.x` 下载 `0.3.0` 时仍执行旧逻辑，需要玩家自行退出一次完成安装。安装 `0.3.0` 之后，再有更高版本才会触发启动阻断。`0.1.x` 需要手动覆盖安装。独立启动脚本已删除。

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
- `Welcome/AutoUpdater.cs`：启动阶段检查、下载、校验、等待安装程序就绪。
- `scripts/Apply-AfterExit.ps1`：等待游戏进程退出、再次校验、安装并记录结果。
- `scripts/Common.ps1`：包验证、保留配置、替换失败回滚。
- `scripts/Build.ps1` / `scripts/Publish-Release.ps1`：开发与发布入口。

关闭游戏后运行（退出更新器测试会启动隐藏的临时测试进程）：

```powershell
.\tests\Package.Tests.ps1
.\tests\ExitUpdater.Tests.ps1
.\tests\StartupGate.Tests.ps1
```

自动测试覆盖首次安装、升级、中文 UTF-8 清单、保留配置、错误版本/身份、路径穿越、等待退出、重命名模组目录、防降级与校验失败。
人工验收：单人进入显示一次；睡觉不重复；重新进入再次显示；房主和客户端各自安装后各显示一次；分屏各显示一次。

API 参考：[SMAPI 事件](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Events)、[分屏与多人游戏](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Multiplayer)。
