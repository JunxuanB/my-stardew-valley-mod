using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace Welcome;

/// <summary>Shows one local welcome message for each entry into a world.</summary>
public sealed class ModEntry : Mod
{
    private Anniversary.AnniversaryModule? anniversary;
    // Each split-screen player has their own pending message.
    private readonly PerScreen<bool> pendingWelcome = new();

    public override void Entry(IModHelper helper)
    {
        // Entry runs before the title screen or any save can be opened. Never terminate during gameplay.
        Monitor.Log("正在检查 Welcome 更新，请稍候……", LogLevel.Info);
        var updater = new AutoUpdater(helper.DirectoryPath, ModManifest.Version.ToString(), Monitor);
        // Run async work on the pool to avoid blocking a captured game synchronization context.
        bool restartRequired = Task.Run(updater.RunAsync).GetAwaiter().GetResult();
        if (restartRequired)
        {
            Monitor.Log("新版已下载并校验。本次游戏启动已终止，退出后将自动安装。请等待几秒，再从原来的入口重新启动游戏。", LogLevel.Error);
            Thread.Sleep(3000); // Give the console message time to be seen before the process closes.
            Environment.Exit(0);
            return;
        }

        Monitor.Log($"Welcome {ModManifest.Version} initialized.", LogLevel.Info);
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.GameLoop.ReturnedToTitle += (_, _) => pendingWelcome.Value = false;

        anniversary = new Anniversary.AnniversaryModule(helper, Monitor);
        anniversary.Register();
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        // SaveLoaded also fires for farmhands joining a multiplayer game.
        pendingWelcome.Value = true;
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        // Wait until loading fades and introductory events have finished.
        if (!pendingWelcome.Value || !Context.IsPlayerFree || Game1.fadeToBlack)
            return;

        pendingWelcome.Value = false;
        Game1.addHUDMessage(new HUDMessage($"{Game1.player.Name}你来星露谷有什么目的！", HUDMessage.newQuest_type));
        Monitor.Log($"Welcome shown for player {Game1.player.UniqueMultiplayerID} on screen {Context.ScreenId}.", LogLevel.Info);
    }
}
